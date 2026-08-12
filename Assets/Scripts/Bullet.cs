using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage = 25f;
    public GameObject instigator;

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

        Collider col = spawned.GetComponent<Collider>();
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
        if (other == null)
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
                Destroy(gameObject);
            return;
        }

        if (hitEnemy || hitPlayer)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage, other.ClosestPoint(transform.position), instigator);
        }

        Destroy(gameObject);
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
