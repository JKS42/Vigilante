using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Runtime LevelDemo wiring for tactical AI.
/// - Tags player as Player and adds Health if missing
/// - Ensures EnemySquad exists
/// - Builds a NavMesh at runtime if none is baked (NavMeshSurface)
///
/// Waves are not created here. Place WaveManager in the scene and fill Spawn Points + Waves.
///
/// Manual editor steps still recommended for shipping:
/// 1. Add NavMeshSurface to level root and Bake (Window/AI Navigation)
/// 2. Place CoverPoint empties near walls/corners (optional; dynamic cover is the fallback)
/// 3. Add an empty with WaveManager; fill Spawn Points (NavMesh walkable) and Waves
/// 4. Menu: Vigilante/Create Tactical Enemy At Scene View (prefab authoring only)
/// 5. Menu: Vigilante/Save Selected Enemy As Prefab
/// </summary>
public static class LevelCombatBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        SetupPlayer();
        EnemySquad.EnsureExists();
        EnsureNavMesh();
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

        // Ensure body colliders resolve as Player for hitscan/projectiles.
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
