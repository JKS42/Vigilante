using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    [Tooltip("Direction the cover faces away from the wall (peek direction).")]
    public Vector3 coverNormal = Vector3.forward;

    public EnemyAI OccupiedBy { get; private set; }
    public bool IsOccupied => OccupiedBy != null;

    void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawRay(transform.position, GetWorldNormal() * 1.2f);
    }

    public Vector3 GetWorldNormal()
    {
        return transform.TransformDirection(coverNormal).normalized;
    }

    public bool TryOccupy(EnemyAI enemy)
    {
        if (IsOccupied && OccupiedBy != enemy)
            return false;

        OccupiedBy = enemy;
        return true;
    }

    public void Release(EnemyAI enemy)
    {
        if (OccupiedBy == enemy)
            OccupiedBy = null;
    }

    /// <summary>
    /// Useful when the point sits roughly between the threat and the occupant,
    /// or when the cover normal faces the threat.
    /// </summary>
    public bool IsUsefulAgainst(Vector3 threatPos, Vector3 occupantPos)
    {
        Vector3 toThreat = (threatPos - transform.position).normalized;
        float facing = Vector3.Dot(GetWorldNormal(), toThreat);
        if (facing < 0.15f)
            return false;

        // Prefer cover that actually blocks a ray from threat toward the point.
        Vector3 origin = threatPos + Vector3.up * 1.4f;
        Vector3 target = transform.position + Vector3.up * 1.0f;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist < 0.1f)
            return false;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist))
        {
            // Hit something before the cover point → solid cover.
            if (hit.distance < dist - 0.35f)
                return true;
        }

        // Soft fallback: cover is between occupant approach and threat.
        Vector3 occupantToCover = (transform.position - occupantPos).normalized;
        return Vector3.Dot(occupantToCover, toThreat) > -0.2f;
    }
}
