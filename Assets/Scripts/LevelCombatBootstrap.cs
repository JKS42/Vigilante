using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Runtime LevelDemo wiring for tactical AI + campaign systems.
/// </summary>
public static class LevelCombatBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        BootstrapActiveScene();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex < 1)
            return;
        BootstrapActiveScene();
    }

    static void BootstrapActiveScene()
    {
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return;

        SetupPlayer();
        EnemySquad.EnsureExists();
        EnsureNavMesh();
        EnsureCampaignSystems();
    }

    public static void EnsureCampaignSystems()
    {
        // Only wire campaign systems in the playable combat scene (build index 1+).
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return;

        AudioManager.EnsureExists();
        DialogueManager.EnsureExists();
        PauseMenu.EnsureExists();

        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        LevelDirector director = Object.FindFirstObjectByType<LevelDirector>();
        if (director == null)
        {
            GameObject go = new GameObject("LevelDirector");
            director = go.AddComponent<LevelDirector>();
        }

        // Don't pause a manager that already began — a second bootstrap would freeze waves.
        if (waves != null && !waves.HasBegun)
            waves.SetPaused(true);

        director.Configure();
        GameSettings.ApplyAll();
    }

    public static void SetupPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (movement != null)
                player = movement.gameObject;
        }

        if (player == null)
            return;

        if (!player.CompareTag("Player"))
            player.tag = "Player";

        foreach (Collider col in player.GetComponentsInChildren<Collider>())
        {
            if (col != null && !col.CompareTag("Player"))
                col.gameObject.tag = "Player";
        }

        if (player.GetComponent<Health>() == null)
            player.AddComponent<Health>();

        if (player.GetComponent<WeaponAccuracy>() == null)
            player.AddComponent<WeaponAccuracy>();
    }

    public static void EnsureNavMesh()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex < 1)
            return;
        RebuildPlayableNavMesh();
    }

    public static bool HasNavMeshAt(Vector3 position, float maxDistance = 8f)
    {
        return NavMesh.SamplePosition(position, out _, maxDistance, NavMesh.AllAreas);
    }

    public static void RebuildPlayableNavMesh()
    {
        NavMeshSurface surface = GetOrCreateRuntimeSurface();
        EnsureRuntimeWalkableFloor(surface.transform);

        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();

        if (!HasPlayableNavMesh())
        {
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
        }

        // Collider is only for baking. Leaving it in the scene blocks the player jump.
        RemoveRuntimeFloorCollider(surface.transform);

        EnemyAI[] ais = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < ais.Length; i++)
        {
            if (ais[i] != null)
                ais[i].PlaceOnNavMesh();
        }
    }

    static bool HasPlayableNavMesh()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && HasNavMeshAt(player.transform.position, 8f))
            return true;

        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        if (waves == null)
            return NavMesh.SamplePosition(Vector3.zero, out _, 20f, NavMesh.AllAreas);

        List<WaveSpawnPoint> points = waves.GetSpawnPoints();
        for (int i = 0; i < points.Count; i++)
        {
            if (HasNavMeshAt(points[i].position, 8f))
                return true;
        }

        return false;
    }

    static NavMeshSurface GetOrCreateRuntimeSurface()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        NavMeshSurface runtime = null;
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] == null)
                continue;

            if (surfaces[i].gameObject.name == "RuntimeNavMeshSurface")
            {
                runtime = surfaces[i];
                continue;
            }

            // Scene FloorTiles surface has no baked data and a tiny volume — don't use it.
            surfaces[i].enabled = false;
        }

        if (runtime != null)
            return runtime;

        GameObject go = new GameObject("RuntimeNavMeshSurface");
        runtime = go.AddComponent<NavMeshSurface>();
        runtime.collectObjects = CollectObjects.Children;
        runtime.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        return runtime;
    }

    static void EnsureRuntimeWalkableFloor(Transform parent)
    {
        Transform existing = parent.Find("RuntimeNavFloor");
        GameObject floor = existing != null ? existing.gameObject : new GameObject("RuntimeNavFloor");
        if (existing == null)
            floor.transform.SetParent(parent, false);

        BoxCollider box = floor.GetComponent<BoxCollider>();
        if (box == null)
            box = floor.AddComponent<BoxCollider>();

        Bounds bounds = new Bounds();
        bool any = false;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            EncapsulateFloorPoint(ref bounds, ref any, player.transform.position);

        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            List<WaveSpawnPoint> points = waves.GetSpawnPoints();
            for (int i = 0; i < points.Count; i++)
                EncapsulateFloorPoint(ref bounds, ref any, points[i].position);
        }

        if (!any)
        {
            bounds = new Bounds(Vector3.zero, new Vector3(40f, 0f, 40f));
            any = true;
        }

        const float pad = 18f;
        floor.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        box.size = new Vector3(Mathf.Max(24f, bounds.size.x + pad * 2f), 0.2f, Mathf.Max(24f, bounds.size.z + pad * 2f));
        box.center = Vector3.zero;
    }

    static void RemoveRuntimeFloorCollider(Transform parent)
    {
        if (parent == null)
            return;

        Transform existing = parent.Find("RuntimeNavFloor");
        if (existing == null)
            return;

        BoxCollider box = existing.GetComponent<BoxCollider>();
        if (box == null)
            return;

        box.enabled = false;
        Object.Destroy(box);
    }

    static void EncapsulateFloorPoint(ref Bounds bounds, ref bool any, Vector3 probe)
    {
        Vector3 point = probe;
        point.y = FindFloorY(probe);
        if (!any)
        {
            bounds = new Bounds(point, Vector3.zero);
            any = true;
        }
        else
        {
            bounds.Encapsulate(point);
        }
    }

    static float FindFloorY(Vector3 probe)
    {
        Vector3 origin = probe + Vector3.up * 3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null)
                continue;
            if (col.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (col.GetComponentInParent<EnemyAI>() != null)
                continue;
            if (col.gameObject.name == "RuntimeNavFloor")
                continue;

            float y = hits[i].point.y;
            if (y > probe.y + 2f || y < probe.y - 4f)
                continue;
            if (y > bestY)
                bestY = y;
        }

        if (bestY > float.NegativeInfinity)
            return bestY;

        return probe.y;
    }
}
