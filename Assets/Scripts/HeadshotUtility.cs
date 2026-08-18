using UnityEngine;

/// <summary>
/// Locational damage for enemies. Hits on a named Head collider, or the top
/// of the body capsule, deal extra damage.
/// </summary>
public static class HeadshotUtility
{
    public const float Multiplier = 2f;
    public const float HeadHeightNormalized = 0.8f;

    static int lastPopupId;
    static float lastPopupTime;

    public static bool TryApply(Health health, Collider hitCollider, Vector3 hitPoint, ref float damage)
    {
        if (health == null || damage <= 0f)
            return false;

        if (!IsEnemy(health))
            return false;

        if (!IsHeadshot(health, hitCollider, hitPoint))
            return false;

        damage *= Multiplier;
        return true;
    }

    public static bool IsHeadshot(Health health, Collider hitCollider, Vector3 hitPoint)
    {
        if (health == null)
            return false;

        if (hitCollider != null && NameLooksLikeHead(hitCollider.transform, health.transform))
            return true;

        Bounds bounds = GetBodyBounds(health, hitCollider);
        float height = bounds.size.y;
        if (height < 0.05f)
            return false;

        float normalized = (hitPoint.y - bounds.min.y) / height;
        return normalized >= HeadHeightNormalized;
    }

    public static void Announce(Vector3 hitPoint, Health health)
    {
        if (health == null)
            return;

        int id = health.GetInstanceID();
        if (id == lastPopupId && Time.unscaledTime - lastPopupTime < 0.12f)
            return;

        lastPopupId = id;
        lastPopupTime = Time.unscaledTime;
        CombatVfx.SpawnHeadshot(hitPoint);
    }

    static bool IsEnemy(Health health)
    {
        Transform t = health.transform;
        return t.CompareTag("Enemy") || t.root.CompareTag("Enemy");
    }

    static bool NameLooksLikeHead(Transform from, Transform stopAt)
    {
        Transform t = from;
        while (t != null)
        {
            string n = t.name;
            if (!string.IsNullOrEmpty(n))
            {
                if (ContainsToken(n, "Head") || ContainsToken(n, "Helmet"))
                    return true;
            }

            if (t == stopAt)
                break;
            t = t.parent;
        }

        return false;
    }

    static bool ContainsToken(string value, string token)
    {
        return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Bounds GetBodyBounds(Health health, Collider hitCollider)
    {
        Collider body = health.GetComponent<Collider>();
        if (body == null)
            body = health.GetComponentInChildren<Collider>();
        if (body == null)
            body = hitCollider;

        if (body != null)
            return body.bounds;

        Renderer renderer = health.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(health.transform.position, Vector3.one * 2f);
    }
}
