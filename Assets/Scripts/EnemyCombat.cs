using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] float damage = 12f;
    [SerializeField] float fireRate = 2.5f;
    [SerializeField] float attackRange = 18f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] Transform muzzle;

    float nextFireTime;

    public float AttackRange => attackRange;
    public float Damage => damage;

    public bool TryFireAt(Transform target)
    {
        if (target == null || Time.time < nextFireTime)
            return false;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 aimPoint = target.position + Vector3.up * 1.2f;
        Vector3 dir = aimPoint - origin;
        float dist = dir.magnitude;

        if (dist > attackRange)
            return false;

        nextFireTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);

        Debug.DrawRay(origin, dir.normalized * dist, Color.red, 0.1f);

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, attackRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (IsPlayerHit(hit.collider))
            {
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage, hit.point, gameObject);
                    return true;
                }
            }
        }

        return false;
    }

    public bool HasLineOfFire(Transform target)
    {
        if (target == null)
            return false;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 aimPoint = target.position + Vector3.up * 1.2f;
        Vector3 dir = aimPoint - origin;
        float dist = dir.magnitude;
        if (dist > attackRange)
            return false;

        if (!Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
            return false;

        return IsPlayerHit(hit.collider);
    }

    static bool IsPlayerHit(Collider col)
    {
        if (col == null)
            return false;
        return col.CompareTag("Player") || col.transform.root.CompareTag("Player");
    }
}
