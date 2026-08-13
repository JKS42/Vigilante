using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class WaveDefinition
{
    public int enemyCount = 2;
    [Min(0f)] public float startDelay = 3f;
    [Min(0f)] public float maxWaitBeforeNext = 40f;
    [Tooltip("Optional override prefab for this wave. Falls back to WaveManager.enemyPrefab.")]
    public GameObject enemyPrefabOverride;
    public EnemyArchetype archetype = EnemyArchetype.Pistol;
    public bool useArchetypeProfile = true;
}

[Serializable]
public class WaveSpawnPoint
{
    public Vector3 position;
    public Vector3 facing = Vector3.forward;
}

/// <summary>
/// Spawns inspector-defined waves. Supports per-wave prefabs / archetypes for
/// Level 1 pistol thugs, Level 2 mixed shotgun+rifle, Level 3 + boss.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [SerializeField] GameObject enemyPrefab;
    [SerializeField] GameObject shotgunPrefab;
    [SerializeField] GameObject riflePrefab;
    [SerializeField] GameObject bossPrefab;
    [SerializeField] float spawnStagger = 0.35f;
    [SerializeField] List<WaveSpawnPoint> spawnPoints = new List<WaveSpawnPoint>();
    [SerializeField] List<WaveDefinition> waves = new List<WaveDefinition>
    {
        new WaveDefinition { enemyCount = 2, startDelay = 3f, maxWaitBeforeNext = 40f },
        new WaveDefinition { enemyCount = 3, startDelay = 4f, maxWaitBeforeNext = 45f },
        new WaveDefinition { enemyCount = 4, startDelay = 5f, maxWaitBeforeNext = 50f }
    };

    readonly List<EnemyAI> spawnedAlive = new List<EnemyAI>();

    enum Phase
    {
        Delay,
        Spawning,
        Active,
        Complete
    }

    Phase phase;
    int waveIndex;
    int spawnedThisWave;
    int nextPointIndex;
    float timer;
    bool started;
    int enemiesKilled;
    bool paused;

    public int CurrentWaveIndex => waveIndex;
    public int TotalWaves => waves != null ? waves.Count : 0;
    public bool IsComplete => phase == Phase.Complete;
    public int AliveSpawnedCount => CountAlive();
    public int EnemiesKilled => enemiesKilled;
    public bool IsPaused => paused;

    public int TotalEnemyCount
    {
        get
        {
            if (waves == null)
                return 0;

            int total = 0;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i] != null)
                    total += Mathf.Max(0, waves[i].enemyCount);
            }

            return total;
        }
    }

    public float TimeRemaining
    {
        get
        {
            if (!started || phase == Phase.Complete || waves == null || waves.Count == 0)
                return 0f;

            WaveDefinition wave = CurrentWave();
            switch (phase)
            {
                case Phase.Delay:
                    return Mathf.Max(0f, wave.startDelay - timer);
                case Phase.Active:
                    if (waveIndex >= waves.Count - 1 || wave.maxWaitBeforeNext <= 0f)
                        return 0f;
                    return Mathf.Max(0f, wave.maxWaitBeforeNext - timer);
                default:
                    return 0f;
            }
        }
    }

    public GameObject DefaultEnemyPrefab => enemyPrefab;
    public GameObject ShotgunEnemyPrefab => shotgunPrefab;
    public GameObject RifleEnemyPrefab => riflePrefab;
    public GameObject BossEnemyPrefab => bossPrefab;

    public List<WaveSpawnPoint> GetSpawnPoints()
    {
        return spawnPoints ?? new List<WaveSpawnPoint>();
    }

    public event Action OnAllWavesCompleted;
    public event Action<int> OnWaveStarted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        CombatStimulus.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        CombatStimulus.OnEnemyDied -= HandleEnemyDied;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (started)
            return;

        // LevelDirector owns campaign wave setup — wait for BeginConfigured().
        if (paused || UnityEngine.Object.FindFirstObjectByType<LevelDirector>() != null)
        {
            paused = true;
            return;
        }

        if (waves == null || waves.Count == 0)
        {
            Debug.LogWarning("WaveManager: no waves defined.");
            CompleteWaves();
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("WaveManager: no Spawn Points configured in the Inspector. Enemies will not spawn.");
            CompleteWaves();
            return;
        }

        started = true;
        BeginWave(0);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (!paused && !started && waves != null && waves.Count > 0 && spawnPoints != null && spawnPoints.Count > 0)
        {
            started = true;
            BeginWave(0);
        }
    }

    public void ConfigureLevel(List<WaveDefinition> newWaves, List<WaveSpawnPoint> newPoints = null)
    {
        ClearAliveEnemies();

        waves = newWaves ?? new List<WaveDefinition>();
        if (newPoints != null && newPoints.Count > 0)
            spawnPoints = newPoints;

        EnsureSpawnPoints();

        spawnedAlive.Clear();
        enemiesKilled = 0;
        waveIndex = 0;
        nextPointIndex = 0;
        spawnedThisWave = 0;
        timer = 0f;
        phase = Phase.Delay;
        started = false;
    }

    public void SetPrefabs(GameObject pistol, GameObject shotgun, GameObject rifle, GameObject boss)
    {
        if (pistol != null) enemyPrefab = pistol;
        if (shotgun != null) shotgunPrefab = shotgun;
        if (rifle != null) riflePrefab = rifle;
        if (boss != null) bossPrefab = boss;
    }

    public void BeginConfigured()
    {
        EnsureSpawnPoints();

        if (waves == null || waves.Count == 0 || spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("WaveManager: cannot begin — missing waves or spawn points.");
            CompleteWaves();
            return;
        }

        started = true;
        paused = false;
        BeginWave(0);
    }

    void Update()
    {
        if (!started || paused || phase == Phase.Complete)
            return;

        PruneDead();
        timer += Time.deltaTime;

        switch (phase)
        {
            case Phase.Delay:
                if (timer >= CurrentWave().startDelay)
                    BeginSpawning();
                break;

            case Phase.Spawning:
                TickSpawning();
                break;

            case Phase.Active:
                TickActive();
                break;
        }
    }

    void BeginWave(int index)
    {
        waveIndex = index;
        spawnedThisWave = 0;
        nextPointIndex = 0;
        timer = 0f;
        phase = Phase.Delay;
        OnWaveStarted?.Invoke(waveIndex);
        DialogueManager.Announcer($"Wave {waveIndex + 1}");
    }

    void BeginSpawning()
    {
        timer = 0f;
        spawnedThisWave = 0;
        phase = Phase.Spawning;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            CompleteWaves();
            return;
        }

        if (CurrentWave().enemyCount <= 0)
        {
            EnterActive();
            return;
        }

        if (!LevelCombatBootstrap.HasNavMeshAt(spawnPoints[0].position, 8f))
            LevelCombatBootstrap.RebuildPlayableNavMesh();

        if (!TrySpawnNext() || spawnedThisWave >= CurrentWave().enemyCount)
            EnterActive();
    }

    void TickSpawning()
    {
        if (spawnedThisWave >= Mathf.Max(0, CurrentWave().enemyCount))
        {
            EnterActive();
            return;
        }

        if (timer < spawnStagger)
            return;

        timer = 0f;
        if (!TrySpawnNext())
        {
            LevelCombatBootstrap.RebuildPlayableNavMesh();
            if (!TrySpawnNext())
            {
                EnterActive();
                return;
            }
        }

        if (spawnedThisWave >= CurrentWave().enemyCount)
            EnterActive();
    }

    void EnterActive()
    {
        timer = 0f;
        phase = Phase.Active;
        TickActive();
    }

    void TickActive()
    {
        bool lastWave = waveIndex >= waves.Count - 1;
        bool cleared = CountAlive() == 0;

        if (lastWave)
        {
            if (cleared)
                CompleteWaves();
            return;
        }

        bool timedOut = CurrentWave().maxWaitBeforeNext > 0f && timer >= CurrentWave().maxWaitBeforeNext;
        if (cleared || timedOut)
            BeginWave(waveIndex + 1);
    }

    bool TrySpawnNext()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return false;

        int attempts = 0;
        int maxAttempts = Mathf.Max(8, spawnPoints.Count * 4);
        while (attempts < maxAttempts)
        {
            WaveSpawnPoint point = spawnPoints[nextPointIndex % spawnPoints.Count];
            nextPointIndex++;
            attempts++;

            if (point == null)
                continue;

            if (!TryGetClearSpawnPose(point, out Pose pose))
                continue;

            GameObject go = SpawnEnemy(pose.position, pose.rotation, CurrentWave());
            if (go == null)
                continue;

            EnemyAI ai = go.GetComponent<EnemyAI>();
            if (ai == null)
            {
                Debug.LogWarning("WaveManager: spawned object has no EnemyAI.", go);
                Destroy(go);
                continue;
            }

            spawnedAlive.Add(ai);
            spawnedThisWave++;
            return true;
        }

        return false;
    }

    bool TryGetClearSpawnPose(WaveSpawnPoint point, out Pose pose)
    {
        Vector3 face = point.facing;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;
        face.Normalize();
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);

        for (int i = 0; i < 16; i++)
        {
            float angle = (i % 8) * 45f * Mathf.Deg2Rad;
            float radius = i == 0 ? 0f : (i < 8 ? 1.7f : 3.2f);
            Vector3 candidate = point.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                continue;

            if (IsOccupiedByEnemy(hit.position))
                continue;

            pose = new Pose(hit.position, rot);
            return true;
        }

        // Last resort: furthest valid sample from the player so a wave never silently skips.
        Vector3 playerPos = Vector3.zero;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerPos = player.transform.position;

        Vector3 best = point.position;
        float bestDist = -1f;
        bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float angle = (i % 8) * 45f * Mathf.Deg2Rad;
            float radius = i == 0 ? 0f : (i < 8 ? 1.7f : 3.2f);
            Vector3 candidate = point.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                continue;

            float dist = Vector3.Distance(hit.position, playerPos);
            if (dist > bestDist)
            {
                bestDist = dist;
                best = hit.position;
                found = true;
            }
        }

        if (!found && NavMesh.SamplePosition(point.position, out NavMeshHit snap, 8f, NavMesh.AllAreas))
        {
            best = snap.position;
            found = true;
        }

        if (!found)
        {
            pose = new Pose(point.position, rot);
            return false;
        }

        pose = new Pose(best, rot);
        return true;
    }

    bool IsOccupiedByEnemy(Vector3 position)
    {
        const float occupancy = 1.8f;
        for (int i = 0; i < spawnedAlive.Count; i++)
        {
            EnemyAI enemy = spawnedAlive[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (Vector3.Distance(position, enemy.transform.position) < occupancy)
                return true;
        }

        return false;
    }

    GameObject SpawnEnemy(Vector3 position, Quaternion rotation, WaveDefinition wave)
    {
        EnemyArchetype type = wave != null ? wave.archetype : EnemyArchetype.Pistol;
        GameObject prefab = ResolvePrefab(wave);
        GameObject go = null;

        if (prefab != null)
            go = Instantiate(prefab, position, rotation);

        if (go != null && go.GetComponent<EnemyAI>() == null)
        {
            Destroy(go);
            go = null;
        }

        if (go == null)
            go = EnemyFactory.Create(position, rotation, type);

        if (go != null && wave != null && wave.useArchetypeProfile)
            EnemyProfile.ApplyDefaults(go, type);

        if (go != null)
        {
            NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
            if (nav != null)
                nav.enabled = false;

            EnemyAI ai = go.GetComponent<EnemyAI>();
            if (ai != null)
                ai.PlaceOnNavMesh();
        }

        return go;
    }

    GameObject ResolvePrefab(WaveDefinition wave)
    {
        if (wave != null && wave.enemyPrefabOverride != null)
            return wave.enemyPrefabOverride;

        if (wave == null)
            return enemyPrefab;

        switch (wave.archetype)
        {
            case EnemyArchetype.Shotgun:
                return shotgunPrefab != null ? shotgunPrefab : enemyPrefab;
            case EnemyArchetype.Rifle:
                return riflePrefab != null ? riflePrefab : enemyPrefab;
            case EnemyArchetype.Boss:
                return bossPrefab != null ? bossPrefab : enemyPrefab;
            default:
                return enemyPrefab;
        }
    }

    void HandleEnemyDied(EnemyAI enemy)
    {
        if (enemy == null)
            return;

        if (spawnedAlive.Remove(enemy))
            enemiesKilled++;
    }

    void PruneDead()
    {
        for (int i = spawnedAlive.Count - 1; i >= 0; i--)
        {
            EnemyAI enemy = spawnedAlive[i];
            if (enemy == null || enemy.IsDead)
                spawnedAlive.RemoveAt(i);
        }
    }

    int CountAlive()
    {
        int count = 0;
        for (int i = 0; i < spawnedAlive.Count; i++)
        {
            EnemyAI enemy = spawnedAlive[i];
            if (enemy != null && !enemy.IsDead)
                count++;
        }

        return count;
    }

    WaveDefinition CurrentWave()
    {
        return waves[waveIndex];
    }

    void CompleteWaves()
    {
        if (phase == Phase.Complete)
            return;

        phase = Phase.Complete;
        started = false;
        OnAllWavesCompleted?.Invoke();
        Debug.Log("WaveManager: all waves complete.");
    }

    void EnsureSpawnPoints()
    {
        if (spawnPoints == null)
            spawnPoints = new List<WaveSpawnPoint>();

        EnemySpawnPoint[] markers = UnityEngine.Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
                continue;
            AddSpawnIfUnique(markers[i].transform.position, markers[i].transform.forward);
        }
    }

    void AddSpawnIfUnique(Vector3 position, Vector3 facing)
    {
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null && Vector3.Distance(spawnPoints[i].position, position) < 1.5f)
                return;
        }

        Vector3 face = facing;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;

        spawnPoints.Add(new WaveSpawnPoint { position = position, facing = face.normalized });
    }

    void ClearAliveEnemies()
    {
        for (int i = spawnedAlive.Count - 1; i >= 0; i--)
        {
            EnemyAI enemy = spawnedAlive[i];
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        spawnedAlive.Clear();

        EnemyAI[] leftovers = UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] != null)
                Destroy(leftovers[i].gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
            return;

        Gizmos.color = new Color(1f, 0.45f, 0.15f);
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            WaveSpawnPoint point = spawnPoints[i];
            if (point == null)
                continue;

            Vector3 face = point.facing;
            face.y = 0f;
            if (face.sqrMagnitude < 0.001f)
                face = Vector3.forward;
            face.Normalize();

            Gizmos.DrawWireSphere(point.position, 0.4f);
            Gizmos.DrawRay(point.position, face * 1.2f);
        }
    }
}
