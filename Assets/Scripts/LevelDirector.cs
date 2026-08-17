using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configures LevelDemo for the selected campaign level:
/// L1 tutorial (pistols, bat start, prompts),
/// L2 larger mixed shotgun/rifle (player starts with pistol),
/// L3 Uncharted-style boss arena finale.
/// </summary>
public class LevelDirector : MonoBehaviour
{
    public static LevelDirector Instance { get; private set; }

    [Header("Optional prefab overrides")]
    public GameObject pistolEnemyPrefab;
    public GameObject shotgunEnemyPrefab;
    public GameObject rifleEnemyPrefab;
    public GameObject bossEnemyPrefab;

    WaveManager waves;
    bool configured;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Configure();
    }

    public void Configure()
    {
        if (configured)
            return;
        configured = true;

        AudioManager.EnsureExists();
        DialogueManager.EnsureExists();

        waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();

        if (waves == null)
        {
            GameObject go = new GameObject("WaveManager");
            waves = go.AddComponent<WaveManager>();
        }

        // Pause default Start() flow — we rebuild waves then begin.
        waves.SetPaused(true);

        int level = GameProgression.SelectedLevel;

        if (pistolEnemyPrefab == null)
            pistolEnemyPrefab = waves.DefaultEnemyPrefab;
        if (shotgunEnemyPrefab == null)
            shotgunEnemyPrefab = waves.ShotgunEnemyPrefab;
        if (rifleEnemyPrefab == null)
            rifleEnemyPrefab = waves.RifleEnemyPrefab;
        if (bossEnemyPrefab == null)
            bossEnemyPrefab = waves.BossEnemyPrefab;

        waves.SetPrefabs(pistolEnemyPrefab, shotgunEnemyPrefab, rifleEnemyPrefab, bossEnemyPrefab);
        ConfigurePlayerLoadout(level);

        switch (level)
        {
            case 1:
                SetupLevel1();
                break;
            case 2:
                SetupLevel2();
                break;
            default:
                SetupLevel3();
                break;
        }

        LevelCombatBootstrap.RebuildPlayableNavMesh();
        waves.BeginConfigured();
        waves.OnAllWavesCompleted -= HandleLevelComplete;
        waves.OnAllWavesCompleted += HandleLevelComplete;
    }

    void ConfigurePlayerLoadout(int level)
    {
        WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (switcher == null)
            return;

        if (level == 2)
        {
            // Slot 1 is the pistol. Set starting index so WeaponSwitcher.Start
            // cannot overwrite this if it runs after LevelDirector.
            switcher.startingWeaponIndex = 1;
            switcher.UnlockWeapon(1, equip: true);
            return;
        }

        // Level 1 tutorial: bat only. Level 3 still starts melee; guns come from drops.
        switcher.SelectWeapon(0, force: true);
    }

    void SetupLevel1()
    {
        TutorialPrompt.EnsureForLevel1();
        DialogueManager.PlayerLine("Bat only. Take their pistols when they fall.");
        DialogueManager.Announcer("LEVEL 1 — TUTORIAL");

        List<WaveDefinition> defs = new List<WaveDefinition>
        {
            new WaveDefinition { enemyCount = 3, startDelay = 2f, maxWaitBeforeNext = 90f, archetype = EnemyArchetype.Melee },
            new WaveDefinition { enemyCount = 5, startDelay = 3f, maxWaitBeforeNext = 100f, archetype = EnemyArchetype.Pistol },
            new WaveDefinition { enemyCount = 6, startDelay = 4f, maxWaitBeforeNext = 0f, archetype = EnemyArchetype.Pistol },
        };

        waves.ConfigureLevel(defs, ExpandExistingSpawnPoints());
        AudioManager.SetCombatMusicIntensity(0.9f);
    }

    void SetupLevel2()
    {
        DialogueManager.Announcer("LEVEL 2 — CROSSFIRE");
        DialogueManager.PlayerLine("Pistol's loaded. Shotgunners rush. Riflemen hold the angles.");

        ExpandArena(1.35f);
        EnsureExtraCover(8);

        List<WaveDefinition> defs = new List<WaveDefinition>
        {
            new WaveDefinition { enemyCount = 4, startDelay = 2f, maxWaitBeforeNext = 80f, archetype = EnemyArchetype.Rifle },
            new WaveDefinition { enemyCount = 4, startDelay = 3f, maxWaitBeforeNext = 80f, archetype = EnemyArchetype.Shotgun },
            new WaveDefinition { enemyCount = 3, startDelay = 3f, maxWaitBeforeNext = 70f, archetype = EnemyArchetype.Rifle },
            new WaveDefinition { enemyCount = 3, startDelay = 3f, maxWaitBeforeNext = 70f, archetype = EnemyArchetype.Shotgun },
            new WaveDefinition { enemyCount = 3, startDelay = 4f, maxWaitBeforeNext = 50f, archetype = EnemyArchetype.Rifle },
            new WaveDefinition { enemyCount = 3, startDelay = 2f, maxWaitBeforeNext = 0f, archetype = EnemyArchetype.Shotgun },
        };

        waves.ConfigureLevel(defs, BuildExpandedSpawnPoints());
        AudioManager.SetCombatMusicIntensity(1.1f);
    }

    void SetupLevel3()
    {
        DialogueManager.Announcer("LEVEL 3 — BOSS BATTLE");
        BossArenaBuilder.BuildAroundPlayer();
        EnsureExtraCover(12);

        List<WaveDefinition> defs = new List<WaveDefinition>
        {
            new WaveDefinition { enemyCount = 3, startDelay = 2f, maxWaitBeforeNext = 60f, archetype = EnemyArchetype.Rifle },
            new WaveDefinition { enemyCount = 3, startDelay = 3f, maxWaitBeforeNext = 60f, archetype = EnemyArchetype.Shotgun },
            new WaveDefinition { enemyCount = 4, startDelay = 3f, maxWaitBeforeNext = 55f, archetype = EnemyArchetype.Rifle },
            new WaveDefinition { enemyCount = 1, startDelay = 4f, maxWaitBeforeNext = 0f, archetype = EnemyArchetype.Boss },
            new WaveDefinition { enemyCount = 2, startDelay = 6f, maxWaitBeforeNext = 40f, archetype = EnemyArchetype.Shotgun },
            new WaveDefinition { enemyCount = 2, startDelay = 2f, maxWaitBeforeNext = 0f, archetype = EnemyArchetype.Rifle },
        };

        waves.ConfigureLevel(defs, BuildExpandedSpawnPoints());
        AudioManager.SetCombatMusicIntensity(1.2f);
        DialogueManager.BossLine("Welcome to the end of the line.");
    }

    List<WaveSpawnPoint> ExpandExistingSpawnPoints()
    {
        List<WaveSpawnPoint> points = new List<WaveSpawnPoint>();
        if (waves == null)
            return points;

        List<WaveSpawnPoint> source = waves.GetSpawnPoints();
        MergeSceneSpawnMarkers(source);
        for (int i = 0; i < source.Count; i++)
        {
            WaveSpawnPoint point = source[i];
            if (point == null)
                continue;

            TryAddUniqueSpawn(points, point.position, point.facing, 2.5f);

            Vector3 right = Vector3.Cross(Vector3.up, point.facing.sqrMagnitude > 0.01f ? point.facing : Vector3.forward).normalized;
            TryAddUniqueSpawn(points, point.position + right * 2.2f, point.facing, 2.5f);
            TryAddUniqueSpawn(points, point.position - right * 2.2f, point.facing, 2.5f);
        }

        if (points.Count == 0)
            AddRingAroundPlayer(points, source.Count > 0 ? source[0].position : Vector3.zero);

        return points.Count > 0 ? points : source;
    }

    List<WaveSpawnPoint> BuildExpandedSpawnPoints()
    {
        List<WaveSpawnPoint> points = new List<WaveSpawnPoint>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        float[] radii = { 10f, 14f, 18f, 12f, 16f, 20f, 11f, 15f };
        for (int i = 0; i < radii.Length; i++)
        {
            float angle = i * (360f / radii.Length) * Mathf.Deg2Rad;
            Vector3 intended = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[i];
            Vector3 pos = intended;
            if (NavMesh.SamplePosition(intended, out NavMeshHit hit, 8f, NavMesh.AllAreas)
                && Vector3.Distance(intended, hit.position) <= 6f)
                pos = hit.position;

            TryAddUniqueSpawn(points, pos, center - pos, 2.5f);
        }

        if (points.Count < 4)
        {
            List<WaveSpawnPoint> fallback = ExpandExistingSpawnPoints();
            for (int i = 0; i < fallback.Count; i++)
                TryAddUniqueSpawn(points, fallback[i].position, fallback[i].facing, 2.5f);
        }

        if (points.Count < 4)
            AddRingAroundPlayer(points, center);

        return points;
    }

    static void AddRingAroundPlayer(List<WaveSpawnPoint> points, Vector3 center)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            center = player.transform.position;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 10f;
            TryAddUniqueSpawn(points, pos, center - pos, 2.5f);
        }
    }

    static void MergeSceneSpawnMarkers(List<WaveSpawnPoint> points)
    {
        EnemySpawnPoint[] markers = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
                continue;
            TryAddUniqueSpawn(points, markers[i].transform.position, markers[i].transform.forward, 2.5f);
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].name != "EnemySpawn")
                continue;
            TryAddUniqueSpawn(points, all[i].position, all[i].forward, 2.5f);
        }
    }

    static void TryAddUniqueSpawn(List<WaveSpawnPoint> points, Vector3 position, Vector3 facing, float minSeparation)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            position = hit.position;

        for (int i = 0; i < points.Count; i++)
        {
            if (Vector3.Distance(points[i].position, position) < minSeparation)
                return;
        }

        Vector3 face = facing;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;

        points.Add(new WaveSpawnPoint
        {
            position = position,
            facing = face.normalized
        });
    }

    void ExpandArena(float scale)
    {
        // Soft "bigger level" read: add outer walkable pads + cover ring.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (16f * scale);
            CreateFloorPad(pos, 6f);
        }

        RebuildNavMesh();
    }

    static void CreateFloorPad(Vector3 position, float size)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "ExpandedFloor";
        pad.transform.position = new Vector3(position.x, -0.05f, position.z);
        pad.transform.localScale = new Vector3(size, 0.1f, size);
        Renderer r = pad.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = new Color(0.25f, 0.25f, 0.28f);
                r.sharedMaterial = mat;
            }
        }
    }

    void EnsureExtraCover(int count)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(7f, 15f);
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "CoverCrate";
            crate.tag = "Breakable";
            crate.transform.position = pos + Vector3.up * 0.6f;
            crate.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            Rigidbody rb = crate.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Break br = crate.AddComponent<Break>();
            br.isWallTile = false;

            EnemyFactory.CreateCoverPoint(pos + Vector3.forward * 0.8f, center - pos);
        }

        RebuildNavMesh();
    }

    static void RebuildNavMesh()
    {
        LevelCombatBootstrap.RebuildPlayableNavMesh();
    }

    void HandleLevelComplete()
    {
        DialogueManager.Announcer("AREA CLEARED");
        DialogueManager.PlayerLine(GameProgression.SelectedLevel >= 3
            ? "It's over."
            : "Moving to the next district.");

        Invoke(nameof(Advance), 3.5f);
    }

    void Advance()
    {
        GameProgression.AdvanceOrReturnToMenu();
    }
}
