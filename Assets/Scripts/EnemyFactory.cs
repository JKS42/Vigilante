using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds a fully wired tactical enemy at runtime or from editor menus.
/// </summary>
public static class EnemyFactory
{
    public static GameObject Create(Vector3 position, Quaternion rotation)
    {
        return Create(position, rotation, EnemyArchetype.Pistol);
    }

    public static GameObject Create(Vector3 position, Quaternion rotation, EnemyArchetype archetype)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = archetype + "Enemy";
        enemy.tag = "Enemy";
        enemy.transform.position = position;
        enemy.transform.rotation = rotation;

        Rigidbody rb = enemy.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        enemy.SetActive(false);
        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.height = 2f;
        agent.radius = 0.4f;
        agent.speed = 3.5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 1.2f;
        enemy.SetActive(true);

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            enemy.transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
        }

        enemy.AddComponent<Health>();
        enemy.AddComponent<EnemyCombat>();
        EnemyProfile.ApplyDefaults(enemy, archetype);
        enemy.AddComponent<EnemyWeaponDrop>();
        enemy.AddComponent<EnemyAI>();

        return enemy;
    }

    public static GameObject CreateNearPlayer(float distance = 8f)
    {
        Vector3 pos = Vector3.forward * distance;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            pos = player.transform.position + player.transform.forward * distance;

        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            pos = hit.position;
        else
            pos.y += 1f;

        return Create(pos, Quaternion.identity);
    }

    public static CoverPoint CreateCoverPoint(Vector3 position, Vector3 faceDirection)
    {
        GameObject go = new GameObject("CoverPoint");
        go.transform.position = position;
        if (faceDirection.sqrMagnitude > 0.001f)
            go.transform.rotation = Quaternion.LookRotation(faceDirection.normalized);
        return go.AddComponent<CoverPoint>();
    }

    public static EnemySpawnPoint CreateSpawnPoint(Vector3 position, Vector3 faceDirection)
    {
        GameObject go = new GameObject("EnemySpawnPoint");
        go.transform.position = position;
        if (faceDirection.sqrMagnitude > 0.001f)
            go.transform.rotation = Quaternion.LookRotation(faceDirection.normalized);
        return go.AddComponent<EnemySpawnPoint>();
    }
}
