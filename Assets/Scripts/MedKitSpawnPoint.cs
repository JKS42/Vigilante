using System.Collections;
using UnityEngine;

/// <summary>
/// Marker that spawns a med kit pickup at this transform, then respawns it
/// after a random delay once collected.
/// Empty objects named MedKitSpawn* are wired automatically by LevelCombatBootstrap.
/// </summary>
public class MedKitSpawnPoint : MonoBehaviour
{
    [SerializeField] GameObject pickupPrefab;

    [Header("Respawn")]
    [SerializeField] float minRespawnSeconds = 12f;
    [SerializeField] float maxRespawnSeconds = 30f;

    MedKitPickup current;

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            current = MedKitPickup.Spawn(transform.position, pickupPrefab);
            yield return new WaitUntil(() => current == null);
            float delay = Random.Range(minRespawnSeconds, maxRespawnSeconds);
            yield return new WaitForSeconds(delay);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.85f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(0.35f, 0.22f, 0.45f));
    }
}
