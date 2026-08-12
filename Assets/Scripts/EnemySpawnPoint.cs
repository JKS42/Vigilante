using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Marker used by WaveManager. Enemies only spawn at these points.
/// </summary>
public class EnemySpawnPoint : MonoBehaviour
{
    [Tooltip("Relative spawn chance for later variety. Unused by WaveManager today.")]
    [SerializeField] [Min(0f)] float weight = 1f;

    public float Weight => weight;

    public Pose GetSpawnPose()
    {
        Vector3 pos = transform.position;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            pos = hit.position;

        return new Pose(pos, transform.rotation);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
    }
}
