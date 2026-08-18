using UnityEngine;

public class Bullet : MonoBehaviour
{
    const float MinSweepRadius = 0.08f;

    public float damage = 25f;
    public GameObject instigator;

    bool consumed;
    Vector3 lastPosition;
    Collider bulletCollider;

    public static Bullet Spawn(
        GameObject prefab,
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        GameObject instigator,
        float scale = 1f,
        float lifetime = 5f)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Bullet.Spawn: prefab is not assigned.");
            return null;
        }

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(dir);

        // Nudge forward so the projectile clears the gun mesh.
        Vector3 spawnPos = position + dir * 0.15f;

        GameObject spawned = Object.Instantiate(prefab, spawnPos, rotation);
        if (scale != 1f)
            spawned.transform.localScale = Vector3.one * scale;

        Bullet bullet = spawned.GetComponent<Bullet>();
        if (bullet == null)
            bullet = spawned.AddComponent<Bullet>();
        bullet.damage = damage;
        bullet.instigator = instigator;
        bullet.lastPosition = spawnPos;
        bullet.consumed = false;

        Collider col = spawned.GetComponent<Collider>();
        bullet.bulletCollider = col;
        if (col != null)
        {
            col.isTrigger = true;
            if (instigator != null)
            {
                Collider[] ownerCols = instigator.GetComponentsInChildren<Collider>();
                for (int i = 0; i < ownerCols.Length; i++)
                {
                    if (ownerCols[i] != null && ownerCols[i] != col)
                        Physics.IgnoreCollision(col, ownerCols[i], true);
                }
            }
        }

        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb == null)
            rb = spawned.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = dir * speed;

        Object.Destroy(spawned, lifetime);
        return bullet;
    }

    void Awake()
    {
        if (bulletCollider == null)
            bulletCollider = GetComponent<Collider>();
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (consumed)
            return;

        Vector3 current = transform.position;
        Vector3 delta = current - lastPosition;
        float distance = delta.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 direction = delta / distance;
            float radius = GetSweepRadius();
            if (Physics.SphereCast(
                    lastPosition,
                    radius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                HandleHit(hit.collider);
                if (consumed)
                    return;
            }
        }

        lastPosition = current;
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    void HandleHit(Collider other)
    {
        if (consumed || other == null)
            return;

        if (other == bulletCollider)
            return;

        // Ignore other projectiles so shotgun pellets don't wipe each other out.
        if (other.GetComponent<Bullet>() != null || other.CompareTag("Bullet"))
            return;

        if (instigator != null)
        {
            if (other.transform.IsChildOf(instigator.transform) || other.gameObject == instigator)
                return;
        }

        Transform root = other.transform.root;
        bool hitBreakable = other.CompareTag("Breakable") || HasTagInParents(other.transform, "Breakable");
        bool hitEnemy = other.CompareTag("Enemy") || root.CompareTag("Enemy");
        bool hitPlayer = other.CompareTag("Player") || root.CompareTag("Player");

        if (!hitBreakable && !hitEnemy && !hitPlayer)
        {
            if (!other.isTrigger)
                Consume();
            return;
        }

        if (hitEnemy || hitPlayer)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage, other.ClosestPoint(transform.position), instigator);
        }

        // Sweep can destroy the bullet before trigger overlap notifies Break.
        if (hitBreakable)
        {
            Break breakable = other.GetComponentInParent<Break>();
            if (breakable != null)
            {
                Vector3 hitDir = transform.forward;
                Rigidbody body = GetComponent<Rigidbody>();
                if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
                    hitDir = body.linearVelocity.normalized;
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                breakable.BreakApart(hitDir * 14f, instigator, hitPoint);
            }
        }

        Consume();
    }

    void Consume()
    {
        if (consumed)
            return;

        consumed = true;
        Destroy(gameObject);
    }

    float GetSweepRadius()
    {
        if (bulletCollider == null)
            bulletCollider = GetComponent<Collider>();

        if (bulletCollider == null)
            return MinSweepRadius;

        float radius = MinSweepRadius;
        Vector3 lossy = bulletCollider.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

        if (bulletCollider is SphereCollider sphere)
            radius = Mathf.Max(MinSweepRadius, sphere.radius * maxScale);
        else if (bulletCollider is CapsuleCollider capsule)
            radius = Mathf.Max(MinSweepRadius, capsule.radius * maxScale);
        else if (bulletCollider is BoxCollider box)
        {
            Vector3 half = Vector3.Scale(box.size * 0.5f, new Vector3(
                Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            radius = Mathf.Max(MinSweepRadius, Mathf.Min(half.x, half.y, half.z));
        }
        else
        {
            Vector3 extents = bulletCollider.bounds.extents;
            radius = Mathf.Max(MinSweepRadius, Mathf.Min(extents.x, extents.y, extents.z));
        }

        return radius;
    }

    static bool HasTagInParents(Transform t, string tag)
    {
        while (t != null)
        {
            if (t.CompareTag(tag))
                return true;
            t = t.parent;
        }
        return false;
    }
}
