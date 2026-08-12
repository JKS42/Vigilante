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
}

[Serializable]
public class WaveSpawnPoint
{
    public Vector3 position;
    public Vector3 facing = Vector3.forward;
}

/// <summary>
/// Spawns inspector-defined waves using the serialized Spawn Points list.
/// Next wave starts on a full clear or maxWaitBeforeNext, whichever comes first.
/// After the last wave, waits until remaining spawned enemies die.
///
/// Scene wiring:
/// 1. Empty GameObject named WaveManager, add this component.
/// 2. Fill Spawn Points with NavMesh walkable world positions (and facing).
/// 3. Fill Waves (enemyCount, startDelay, maxWaitBeforeNext). Optional: assign Enemy Prefab.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [SerializeField] GameObject enemyPrefab;
    [SerializeField] float spawnStagger = 0.15f;
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

    public int CurrentWaveIndex => waveIndex;
    public int TotalWaves => waves != null ? waves.Count : 0;
    public bool IsComplete => phase == Phase.Complete;
    public int AliveSpawnedCount => CountAlive();
    public int EnemiesKilled => enemiesKilled;
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

    /// <summary>
    /// Seconds left on the current wave timer: startDelay while waiting to spawn,
    /// or maxWaitBeforeNext while a wave is active (not used on the last wave).
    /// </summary>
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

    void Update()
    {
        if (!started || phase == Phase.Complete)
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
        timer = 0f;
        phase = Phase.Delay;
        OnWaveStarted?.Invoke(waveIndex);
    }

    void BeginSpawning()
    {
        timer = 0f;
        spawnedThisWave = 0;
        phase = Phase.Spawning;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("WaveManager: no Spawn Points configured. Skipping remaining waves.");
            CompleteWaves();
            return;
        }

        if (CurrentWave().enemyCount <= 0)
        {
            EnterActive();
            return;
        }

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
            EnterActive();
            return;
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
        while (attempts < spawnPoints.Count)
        {
            WaveSpawnPoint point = spawnPoints[nextPointIndex % spawnPoints.Count];
            nextPointIndex++;
            attempts++;

            if (point == null)
                continue;

            Pose pose = GetSpawnPose(point);
            GameObject go = SpawnEnemy(pose.position, pose.rotation);
            if (go == null)
                continue;

            EnemyAI ai = go.GetComponent<EnemyAI>();
            if (ai == null)
            {
                Debug.LogWarning("WaveManager: spawned object has no EnemyAI.", go);
                continue;
            }

            spawnedAlive.Add(ai);
            spawnedThisWave++;
            return true;
        }

        return false;
    }

    static Pose GetSpawnPose(WaveSpawnPoint point)
    {
        Vector3 pos = point.position;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            pos = hit.position;

        Vector3 face = point.facing;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;

        return new Pose(pos, Quaternion.LookRotation(face.normalized, Vector3.up));
    }

    GameObject SpawnEnemy(Vector3 position, Quaternion rotation)
    {
        if (enemyPrefab != null)
            return Instantiate(enemyPrefab, position, rotation);

        return EnemyFactory.Create(position, rotation);
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
