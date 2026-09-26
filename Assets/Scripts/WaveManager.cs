using System;
using System.Collections;
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
    [SerializeField] GameObject meleePrefab;
    [SerializeField] GameObject shotgunPrefab;
    [SerializeField] GameObject riflePrefab;
    [SerializeField] GameObject bossPrefab;
    [SerializeField] GameObject pistolPickupPrefab;
    [SerializeField] GameObject shotgunPickupPrefab;
    [SerializeField] GameObject riflePickupPrefab;
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
    readonly HashSet<int> usedSpawnPoints = new HashSet<int>();
    readonly HashSet<int> blockedNearPlayer = new HashSet<int>();
    float timer;
    bool started;
    int enemiesKilled;
    bool paused;
    bool holdNextWave;
    bool preferDistantSpawns;
    bool scriptedSpawn;
    bool hasReservedPose;
    Pose reservedPose;
    int reservedLocation = -1;
    List<SpawnLocation> spawnLocations;
    int[] locationSpawnCounts;
    int[] locationPickPenalty;

    const float LocationClusterRadius = 12f;

    class SpawnLocation
    {
        public Vector3 center;
        public readonly List<int> points = new List<int>();
    }

    public int CurrentWaveIndex => waveIndex;
    public int TotalWaves => waves != null ? waves.Count : 0;
    public bool IsComplete => phase == Phase.Complete;
    public int AliveSpawnedCount => CountAlive();
    public int EnemiesKilled => enemiesKilled;
    public bool IsPaused => paused;
    public bool HasBegun => started;

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
        RegisterLootPrefabs();
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

        // LevelDirector may still be configuring this frame. If it never calls
        // BeginConfigured(), start anyway so a missing director cannot stall waves.
        if (UnityEngine.Object.FindFirstObjectByType<LevelDirector>() != null)
        {
            StartCoroutine(BeginIfStillIdle());
            return;
        }

        TryAutoBegin();
    }

    IEnumerator BeginIfStillIdle()
    {
        yield return null;
        yield return null;
        if (!started)
            TryAutoBegin();
    }

    void TryAutoBegin()
    {
        if (started)
            return;

        EnsureSpawnPoints();
        EnsureFallbackSpawnPoints();

        if (waves == null || waves.Count == 0)
        {
            Debug.LogWarning("WaveManager: no waves defined.");
            CompleteWaves();
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("WaveManager: no spawn points available. Enemies will not spawn.");
            CompleteWaves();
            return;
        }

        started = true;
        paused = false;
        BeginWave(0);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (!paused && !started)
            TryAutoBegin();
    }

    public void ConfigureLevel(List<WaveDefinition> newWaves, List<WaveSpawnPoint> newPoints = null)
    {
        ClearAliveEnemies();

        waves = newWaves ?? new List<WaveDefinition>();
        if (newPoints != null && newPoints.Count > 0)
            spawnPoints = newPoints;

        EnsureSpawnPoints();
        EnsureFallbackSpawnPoints();

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
        if (UsableEnemyPrefab(pistol) != null) enemyPrefab = pistol;
        if (UsableEnemyPrefab(shotgun) != null) shotgunPrefab = shotgun;
        if (UsableEnemyPrefab(rifle) != null) riflePrefab = rifle;
        if (UsableEnemyPrefab(boss) != null) bossPrefab = boss;
        RegisterLootPrefabs();
    }

    public void SetPickupPrefabs(GameObject pistol, GameObject shotgun, GameObject rifle)
    {
        if (pistol != null) pistolPickupPrefab = pistol;
        if (shotgun != null) shotgunPickupPrefab = shotgun;
        if (rifle != null) riflePickupPrefab = rifle;
        RegisterLootPrefabs();
    }

    void RegisterLootPrefabs()
    {
        WeaponPickup.RegisterPrefabs(pistolPickupPrefab, shotgunPickupPrefab, riflePickupPrefab);
    }

    public void BeginConfigured()
    {
        EnsureSpawnPoints();
        EnsureFallbackSpawnPoints();

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
        if (!started || paused || scriptedSpawn || phase == Phase.Complete)
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
        usedSpawnPoints.Clear();
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

        EnsureFallbackSpawnPoints();
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

        DialogueManager.Announcer("Enemies spawning!");

        if (!LevelCombatBootstrap.HasNavMeshAt(spawnPoints[0].position, 8f))
            LevelCombatBootstrap.RebuildPlayableNavMesh();

        if (!TrySpawnNext())
        {
            LevelCombatBootstrap.RebuildPlayableNavMesh();
            EnsureFallbackSpawnPoints();
            TrySpawnNext();
        }

        if (spawnedThisWave >= CurrentWave().enemyCount)
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
            EnsureFallbackSpawnPoints();
            if (!TrySpawnNext())
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

        if (holdNextWave)
            return;

        bool timedOut = CurrentWave().maxWaitBeforeNext > 0f && timer >= CurrentWave().maxWaitBeforeNext;
        if (cleared || timedOut)
            BeginWave(waveIndex + 1);
    }

    public void HoldFollowingWave()
    {
        holdNextWave = true;
    }

    public void ReleaseFollowingWave()
    {
        holdNextWave = false;
    }

    public void UseDistantSpawns()
    {
        preferDistantSpawns = true;
    }

    public void BeginScriptedFollowingWave()
    {
        holdNextWave = false;
        if (waves == null || waveIndex >= waves.Count - 1)
            return;

        scriptedSpawn = true;
        hasReservedPose = false;
        BeginWave(waveIndex + 1);
        phase = Phase.Spawning;
        spawnedThisWave = 0;
        timer = 0f;
    }

    public bool HasScriptedSpawnsRemaining
    {
        get
        {
            if (!scriptedSpawn || waves == null || waveIndex < 0 || waveIndex >= waves.Count || waves[waveIndex] == null)
                return false;
            return spawnedThisWave < waves[waveIndex].enemyCount;
        }
    }

    public bool PreviewScriptedSpawn(out Vector3 position)
    {
        position = default;
        if (!HasScriptedSpawnsRemaining)
            return false;

        if (!hasReservedPose)
        {
            if (!TryPickSpawnPose(out reservedPose))
                return false;
            hasReservedPose = true;
        }

        position = reservedPose.position;
        return true;
    }

    public bool CommitScriptedSpawn(out Vector3 position)
    {
        position = default;
        if (!PreviewScriptedSpawn(out position))
            return false;

        Pose pose = reservedPose;
        hasReservedPose = false;
        if (!SpawnAtPose(pose))
            return false;

        position = pose.position;
        return true;
    }

    public void EndScriptedSpawn()
    {
        scriptedSpawn = false;
        hasReservedPose = false;
        if (phase != Phase.Spawning)
            return;

        if (waves != null && waveIndex >= 0 && waveIndex < waves.Count && waves[waveIndex] != null
            && spawnedThisWave < waves[waveIndex].enemyCount)
        {
            timer = 0f;
            return;
        }

        EnterActive();
    }

    bool TrySpawnNext()
    {
        for (int guard = 0; guard < 6; guard++)
        {
            if (!TryPickSpawnPose(out Pose pose))
                return false;
            if (SpawnAtPose(pose))
                return true;
        }

        return false;
    }

    bool TryPickSpawnPose(out Pose pose)
    {
        pose = default;
        if (spawnPoints == null || spawnPoints.Count == 0)
            return false;

        HashSet<int> blocked = null;
        if (preferDistantSpawns && spawnPoints.Count > 1 && TryGetPlayerPosition(out Vector3 origin))
            blocked = BlockedNearPlayerSpawns(origin);

        int count = spawnPoints.Count;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int n = 0; n < count; n++)
            {
                int index = (nextPointIndex + n) % count;
                if (blocked != null && blocked.Contains(index))
                    continue;
                if (pass == 0 && usedSpawnPoints.Contains(index))
                    continue;

                WaveSpawnPoint point = spawnPoints[index];
                if (point == null)
                    continue;
                if (!TryGetClearSpawnPose(point, out pose))
                    continue;

                usedSpawnPoints.Add(index);
                nextPointIndex = index + 1;
                return true;
            }
        }

        return false;
    }

    int ClosestSpawnIndex(Vector3 origin)
    {
        int closest = -1;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] == null)
                continue;
            float distance = HorizontalDistance(spawnPoints[i].position, origin);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closest = i;
        }

        return closest;
    }

    HashSet<int> BlockedNearPlayerSpawns(Vector3 origin)
    {
        blockedNearPlayer.Clear();
        int closest = ClosestSpawnIndex(origin);
        if (closest >= 0)
            blockedNearPlayer.Add(closest);

        EnsureSpawnLocations();
        if (spawnLocations != null && spawnLocations.Count > 1)
        {
            int loc = ClosestLocation(origin);
            if (loc >= 0 && loc < spawnLocations.Count)
            {
                SpawnLocation location = spawnLocations[loc];
                for (int i = 0; i < location.points.Count; i++)
                    blockedNearPlayer.Add(location.points[i]);
            }
        }

        int usable = 0;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null && !blockedNearPlayer.Contains(i))
                usable++;
        }

        if (usable == 0)
        {
            blockedNearPlayer.Clear();
            if (closest >= 0)
                blockedNearPlayer.Add(closest);
        }

        return blockedNearPlayer;
    }

    void EnsureSpawnLocations()
    {
        if (spawnLocations != null && locationSpawnCounts != null && locationSpawnCounts.Length == spawnLocations.Count)
            return;

        spawnLocations = ClusterSpawnLocations();
        locationSpawnCounts = new int[spawnLocations.Count];
        locationPickPenalty = new int[spawnLocations.Count];
    }

    List<SpawnLocation> ClusterSpawnLocations()
    {
        int count = spawnPoints.Count;
        int[] parent = new int[count];
        for (int i = 0; i < count; i++)
            parent[i] = i;

        for (int i = 0; i < count; i++)
        {
            if (spawnPoints[i] == null)
                continue;
            for (int j = i + 1; j < count; j++)
            {
                if (spawnPoints[j] == null)
                    continue;
                if (HorizontalDistance(spawnPoints[i].position, spawnPoints[j].position) > LocationClusterRadius)
                    continue;
                UnionSpawn(parent, i, j);
            }
        }

        Dictionary<int, SpawnLocation> groups = new Dictionary<int, SpawnLocation>();
        for (int i = 0; i < count; i++)
        {
            if (spawnPoints[i] == null)
                continue;

            int root = FindSpawn(parent, i);
            if (!groups.TryGetValue(root, out SpawnLocation location))
            {
                location = new SpawnLocation();
                groups.Add(root, location);
            }

            location.points.Add(i);
            location.center += spawnPoints[i].position;
        }

        List<SpawnLocation> result = new List<SpawnLocation>();
        foreach (KeyValuePair<int, SpawnLocation> pair in groups)
        {
            SpawnLocation location = pair.Value;
            if (location.points.Count > 0)
                location.center /= location.points.Count;
            result.Add(location);
        }

        return result;
    }

    static int FindSpawn(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    static void UnionSpawn(int[] parent, int a, int b)
    {
        int ra = FindSpawn(parent, a);
        int rb = FindSpawn(parent, b);
        if (ra != rb)
            parent[rb] = ra;
    }

    int ClosestLocation(Vector3 origin)
    {
        int closest = 0;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < spawnLocations.Count; i++)
        {
            float distance = HorizontalDistance(spawnLocations[i].center, origin);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closest = i;
        }

        return closest;
    }

    static bool TryGetPlayerPosition(out Vector3 position)
    {
        position = default;
        PlayerMovement movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (movement != null)
        {
            position = movement.transform.position;
            return true;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        position = player.transform.position;
        return true;
    }

    bool SpawnAtPose(Pose pose)
    {
        GameObject go = SpawnEnemy(pose.position, pose.rotation, CurrentWave());
        if (go == null)
        {
            NoteSpawnLocation(false);
            return false;
        }

        EnemyAI ai = go.GetComponent<EnemyAI>();
        if (ai == null)
        {
            Debug.LogWarning("WaveManager: spawned object has no EnemyAI.", go);
            Destroy(go);
            NoteSpawnLocation(false);
            return false;
        }

        spawnedAlive.Add(ai);
        spawnedThisWave++;
        NoteSpawnLocation(true);
        return true;
    }

    void NoteSpawnLocation(bool placed)
    {
        if (reservedLocation < 0 || locationSpawnCounts == null || reservedLocation >= locationSpawnCounts.Length)
        {
            reservedLocation = -1;
            return;
        }

        if (placed)
            locationSpawnCounts[reservedLocation]++;
        else if (locationPickPenalty != null && reservedLocation < locationPickPenalty.Length)
            locationPickPenalty[reservedLocation]++;

        reservedLocation = -1;
    }

    const float SpawnSeparation = 1.8f;

    bool TryGetClearSpawnPose(WaveSpawnPoint point, out Pose pose)
    {
        pose = default;
        Vector3 face = point.facing;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;
        face.Normalize();
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);

        // Stay on the authored marker. Wide rings walk enemies through walls
        // onto the outdoor navmesh pad.
        float[] radii = { 0f, 1.1f, 2f, 2.8f };
        int sectors = 6;
        for (int r = 0; r < radii.Length; r++)
        {
            int steps = r == 0 ? 1 : sectors;
            for (int s = 0; s < steps; s++)
            {
                float angle = s * (360f / steps) * Mathf.Deg2Rad;
                Vector3 candidate = point.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[r];
                if (!TryResolveClearPosition(candidate, point.position, out Vector3 cleared))
                    continue;
                if (!LooksPlayable(cleared))
                    continue;

                pose = new Pose(cleared, rot);
                return true;
            }
        }

        return false;
    }

    bool TryResolveClearPosition(Vector3 candidate, out Vector3 cleared, EnemyAI ignore = null)
    {
        return TryResolveClearPosition(candidate, candidate, out cleared, ignore);
    }

    bool TryResolveClearPosition(Vector3 candidate, Vector3 anchor, out Vector3 cleared, EnemyAI ignore = null)
    {
        cleared = candidate;
        candidate = LevelCombatBootstrap.SnapToFloor(candidate);
        anchor = LevelCombatBootstrap.SnapToFloor(anchor);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.2f, NavMesh.AllAreas))
            return false;
        if (HorizontalDistance(candidate, hit.position) > 2.2f)
            return false;
        if (HorizontalDistance(anchor, hit.position) > 2.5f)
            return false;

        cleared = hit.position;
        if (IsOccupiedByEnemy(cleared, ignore))
            return false;
        return BodyFits(cleared);
    }

    static bool BodyFits(Vector3 feet)
    {
        Vector3 bottom = feet + Vector3.up * 0.4f;
        Vector3 top = feet + Vector3.up * 1.7f;
        Collider[] hits = Physics.OverlapCapsule(bottom, top, 0.34f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.isTrigger)
                continue;
            if (col.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (col.GetComponentInParent<EnemyAI>() != null)
                continue;
            if (col.GetComponentInParent<WeaponPickup>() != null)
                continue;
            return false;
        }

        return true;
    }

    static bool LooksPlayable(Vector3 position)
    {
        Vector3 origin = LevelCombatBootstrap.SnapToFloor(position) + Vector3.up * 1.35f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor, 8f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        Collider col = floor.collider;
        if (col == null)
            return false;
        if (col.GetComponentInParent<PlayerMovement>() != null)
            return false;
        if (col.GetComponentInParent<EnemyAI>() != null)
            return false;

        string n = col.gameObject.name;
        if (n.Contains("ExpandedFloor") || n.Contains("RuntimeNav"))
            return false;

        int wallHits = 0;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (!Physics.Raycast(origin, dir, out RaycastHit wall, 14f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (wall.collider == null)
                continue;
            if (Mathf.Abs(wall.normal.y) < 0.55f)
                wallHits++;
        }

        return wallHits >= 2;
    }

    bool IsOccupiedByEnemy(Vector3 position, EnemyAI ignore = null)
    {
        for (int i = 0; i < spawnedAlive.Count; i++)
        {
            EnemyAI enemy = spawnedAlive[i];
            if (enemy == null || enemy == ignore || enemy.IsDead)
                continue;
            if (HorizontalDistance(position, enemy.transform.position) < SpawnSeparation)
                return true;
        }

        EnemyAI[] all = UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            EnemyAI enemy = all[i];
            if (enemy == null || enemy == ignore || enemy.IsDead)
                continue;
            if (HorizontalDistance(position, enemy.transform.position) < SpawnSeparation)
                return true;
        }

        return false;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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
            {
                ai.PlaceOnNavMesh();
                Vector3 feet = SpawnFeet(ai.transform.position, nav);
                if (!BodyFits(feet) || IsOccupiedByEnemy(feet, ai))
                {
                    if (TryFindNearbyClear(position, ai, out Vector3 nudged))
                    {
                        feet = nudged;
                        if (nav != null)
                        {
                            nav.enabled = true;
                            nav.Warp(nudged);
                            ai.transform.position = nudged + Vector3.up * nav.baseOffset;
                        }
                        else
                            ai.transform.position = nudged;
                    }
                }

                if (!BodyFits(feet))
                {
                    Destroy(go);
                    return null;
                }

                CombatVfx.PlaySpawnBurst(ai.transform.position);
            }
        }

        return go;
    }

    static Vector3 SpawnFeet(Vector3 placed, NavMeshAgent nav)
    {
        if (nav == null)
            return placed;
        return placed - Vector3.up * nav.baseOffset;
    }

    bool TryFindNearbyClear(Vector3 around, EnemyAI ignore, out Vector3 cleared)
    {
        cleared = around;
        float[] radii = { 1.1f, 2f };
        for (int r = 0; r < radii.Length; r++)
        {
            for (int s = 0; s < 8; s++)
            {
                float angle = s * 45f * Mathf.Deg2Rad;
                Vector3 candidate = around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[r];
                if (TryResolveClearPosition(candidate, around, out cleared, ignore) && LooksPlayable(cleared))
                    return true;
            }
        }

        return false;
    }

    GameObject ResolvePrefab(WaveDefinition wave)
    {
        if (wave != null)
        {
            GameObject overridePrefab = UsableEnemyPrefab(wave.enemyPrefabOverride);
            if (overridePrefab != null)
                return overridePrefab;
        }

        if (wave == null)
            return UsableEnemyPrefab(enemyPrefab);

        switch (wave.archetype)
        {
            case EnemyArchetype.Shotgun:
            {
                GameObject shotgun = UsableEnemyPrefab(shotgunPrefab);
                return shotgun != null ? shotgun : UsableEnemyPrefab(enemyPrefab);
            }
            case EnemyArchetype.Rifle:
            {
                GameObject rifle = UsableEnemyPrefab(riflePrefab);
                return rifle != null ? rifle : UsableEnemyPrefab(enemyPrefab);
            }
            case EnemyArchetype.Boss:
            {
                GameObject boss = UsableEnemyPrefab(bossPrefab);
                return boss != null ? boss : UsableEnemyPrefab(enemyPrefab);
            }
            case EnemyArchetype.Melee:
            {
                if (meleePrefab == null)
                    meleePrefab = Resources.Load<GameObject>("Enemies/BatEnemy");
                return UsableEnemyPrefab(meleePrefab);
            }
            default:
                return UsableEnemyPrefab(enemyPrefab);
        }
    }

    static GameObject UsableEnemyPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;
        if (prefab.GetComponent<EnemyAI>() != null)
            return prefab;
        if (prefab.GetComponentInChildren<EnemyAI>(true) != null)
            return prefab;
        return null;
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

        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].name != "EnemySpawn")
                continue;
            AddSpawnIfUnique(all[i].position, all[i].forward);
        }

        FilterUnplayableSpawnPoints();
    }

    void EnsureFallbackSpawnPoints()
    {
        if (spawnPoints == null)
            spawnPoints = new List<WaveSpawnPoint>();
        if (spawnPoints.Count > 0)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 10f;
            AddSpawnIfUnique(pos, center - pos);
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

    void FilterUnplayableSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Count <= 1)
            return;

        List<WaveSpawnPoint> indoor = new List<WaveSpawnPoint>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            WaveSpawnPoint point = spawnPoints[i];
            if (point == null)
                continue;
            if (LooksPlayable(point.position))
                indoor.Add(point);
        }

        if (indoor.Count > 0)
            spawnPoints = indoor;
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
