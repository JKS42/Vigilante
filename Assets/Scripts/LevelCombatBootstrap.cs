using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Runtime LevelDemo wiring for tactical AI + campaign systems.
/// </summary>
public static class LevelCombatBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        SetupPlayer();
        EnemySquad.EnsureExists();
        EnsureNavMesh();
        EnsureCampaignSystems();
    }

    public static void EnsureCampaignSystems()
    {
        // Only wire campaign systems in the playable combat scene (build index 1+).
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex < 1)
            return;

        AudioManager.EnsureExists();
        DialogueManager.EnsureExists();

        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        if (waves != null)
            waves.SetPaused(true);

        if (Object.FindFirstObjectByType<LevelDirector>() == null)
        {
            GameObject go = new GameObject("LevelDirector");
            LevelDirector director = go.AddComponent<LevelDirector>();

            // Hook existing enemy prefabs from WaveManager if present.
            if (waves != null)
            {
                // Prefabs remain assignable on LevelDirector in the inspector when placed manually.
            }
        }
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
    }

    public static void EnsureNavMesh()
    {
        Vector3 probe = Vector3.zero;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            probe = player.transform.position;

        if (NavMesh.SamplePosition(probe, out _, 5f, NavMesh.AllAreas))
            return;

        NavMeshSurface existing = Object.FindFirstObjectByType<NavMeshSurface>();
        if (existing == null)
        {
            GameObject go = new GameObject("RuntimeNavMeshSurface");
            existing = go.AddComponent<NavMeshSurface>();
            existing.collectObjects = CollectObjects.All;
            existing.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }

        existing.BuildNavMesh();
    }
}
