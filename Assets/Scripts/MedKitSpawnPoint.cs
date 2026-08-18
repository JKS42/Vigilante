using UnityEngine;

/// <summary>
/// Marker that spawns a med kit pickup at this transform on Start.
/// Empty objects named MedKitSpawn* are wired automatically by LevelCombatBootstrap.
/// </summary>
public class MedKitSpawnPoint : MonoBehaviour
{
    [SerializeField] GameObject pickupPrefab;

    bool spawned;

    void Start()
    {
        SpawnKit();
    }

    public void SpawnKit()
    {
        if (spawned)
            return;

        spawned = true;
        MedKitPickup.Spawn(transform.position, pickupPrefab);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.85f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(0.35f, 0.22f, 0.45f));
    }
}
