using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breakable prop / wall. The hit piece launches as a physics object (with a few chips)
/// and can damage enemies (and lightly the player) while it is flying.
/// Optional cracked material swap distinguishes breakable surfaces.
/// </summary>
public class Break : MonoBehaviour
{
    public Rigidbody rb;
    [Header("Shatter")]
    public int debrisCount = 5;
    public float debrisForce = 7f;
    public float debrisDamage = 18f;
    public float debrisLifetime = 0.7f;
    public Material brokenMaterial;
    public bool isWallTile;

    [Header("Fly-off")]
    public float launchSpeed = 12f;
    public float launchUpSpeed = 4.5f;
    public float launchSpin = 10f;
    public float pieceLifetime = 8f;

    bool isBroken;
    bool destroyScheduled;
    static Material s_crackedWallMat;
    static Material s_crackedPropMat;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
            rb.isKinematic = true;

        EnsureBreakableLook();
    }

    void EnsureBreakableLook()
    {
        if (!CompareTag("Breakable"))
            gameObject.tag = "Breakable";

        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || brokenMaterial != null)
        {
            if (brokenMaterial != null && renderer != null)
                renderer.sharedMaterial = brokenMaterial;
            return;
        }

        // Visual cue: cracked / warmer tint so breakables read differently from solid walls.
        bool wall = IsWallPiece();
        Material mat = wall ? GetCrackedWallMaterial() : GetCrackedPropMaterial();
        renderer.sharedMaterial = mat;
    }

    bool IsWallPiece()
    {
        if (isWallTile)
            return true;

        Transform t = transform;
        while (t != null)
        {
            if (t.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }

        return false;
    }

    static Material GetCrackedWallMaterial()
    {
        if (s_crackedWallMat != null)
            return s_crackedWallMat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        s_crackedWallMat = new Material(shader);
        s_crackedWallMat.name = "BreakableWall_Cracked";
        s_crackedWallMat.color = new Color(0.72f, 0.62f, 0.48f);
        if (s_crackedWallMat.HasProperty("_BaseColor"))
            s_crackedWallMat.SetColor("_BaseColor", new Color(0.72f, 0.62f, 0.48f));
        return s_crackedWallMat;
    }

    static Material GetCrackedPropMaterial()
    {
        if (s_crackedPropMat != null)
            return s_crackedPropMat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        s_crackedPropMat = new Material(shader);
        s_crackedPropMat.name = "BreakableProp_Cracked";
        s_crackedPropMat.color = new Color(0.55f, 0.4f, 0.32f);
        if (s_crackedPropMat.HasProperty("_BaseColor"))
            s_crackedPropMat.SetColor("_BaseColor", new Color(0.55f, 0.4f, 0.32f));
        return s_crackedPropMat;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken || collision == null)
            return;

        Collider other = collision.collider;
        if (other == null)
            return;

        if (!other.CompareTag("Bullet") && !other.CompareTag("Bat"))
            return;

        Vector3 impulse = collision.relativeVelocity;
        if (impulse.sqrMagnitude < 1f && collision.contactCount > 0)
            impulse = -collision.GetContact(0).normal * launchSpeed;
        BreakApart(impulse, other.gameObject);
    }

    public void BreakApart()
    {
        BreakApart(Vector3.zero, null);
    }

    public void BreakApart(Vector3 impulse, GameObject instigator)
    {
        BreakApart(impulse, instigator, transform.position);
    }

    public void BreakApart(Vector3 impulse, GameObject instigator, Vector3 hitPoint)
    {
        if (isBroken)
            return;

        isBroken = true;
        bool wall = IsWallPiece();
        Vector3 launchDir = ResolveLaunchDirection(impulse);
        CombatStimulus.EmitBreach(transform.position);
        AudioManager.BreakObject(transform.position);
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 0.5f, "CRACK!");
        CombatVfx.SpawnImpact(hitPoint, Vector3.up);

        LaunchPiece(launchDir, impulse, instigator, hitPoint, wall);
        SpawnDebris(launchDir, instigator, wall);

        if (!destroyScheduled)
        {
            destroyScheduled = true;
            Destroy(gameObject, Mathf.Max(1f, pieceLifetime));
        }
    }

    void LaunchPiece(Vector3 dir, Vector3 impulse, GameObject instigator, Vector3 hitPoint, bool wall)
    {
        transform.SetParent(null, true);

        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        PrepareCollidersForFlight();
        IgnoreNeighborBreakables();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = Mathf.Clamp(rb.mass, 0.35f, 2.5f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        float speed = Mathf.Max(launchSpeed, impulse.magnitude * 0.25f);
        if (wall)
            speed *= 1.15f;

        Vector3 velocity = dir * speed + Vector3.up * launchUpSpeed;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.insideUnitSphere * launchSpin;

        Vector3 torqueAxis = Vector3.Cross(dir, hitPoint - rb.worldCenterOfMass);
        if (torqueAxis.sqrMagnitude > 0.001f)
            rb.AddTorque(torqueAxis.normalized * launchSpin * 0.6f, ForceMode.VelocityChange);

        DebrisHazard hazard = gameObject.GetComponent<DebrisHazard>();
        if (hazard == null)
            hazard = gameObject.AddComponent<DebrisHazard>();
        hazard.damage = debrisDamage;
        hazard.lifetime = Mathf.Min(pieceLifetime, 3f);
        hazard.owner = instigator;
        hazard.sourceBreakable = gameObject;
        hazard.minSpeed = 2f;
    }

    Vector3 ResolveLaunchDirection(Vector3 impulse)
    {
        Vector3 dir = impulse;
        dir.y *= 0.35f;
        if (dir.sqrMagnitude > 0.05f)
            return dir.normalized;

        if (transform.parent != null)
        {
            Vector3 away = transform.position - transform.parent.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.05f)
                return away.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.05f)
            return forward.normalized;

        return Vector3.forward;
    }

    void PrepareCollidersForFlight()
    {
        Collider[] colliders = GetComponents<Collider>();
        bool hasSolid = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            if (col is MeshCollider mesh)
                mesh.convex = true;

            if (col.isTrigger)
            {
                col.enabled = false;
                continue;
            }

            hasSolid = true;
        }

        if (!hasSolid)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
                break;
            }
        }
    }

    void IgnoreNeighborBreakables()
    {
        Collider[] mine = GetComponents<Collider>();
        float radius = 1.35f;
        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
            radius = Mathf.Max(radius, selfCol.bounds.extents.magnitude + 0.35f);

        Collider[] nearby = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider other = nearby[i];
            if (other == null || other.gameObject == gameObject)
                continue;
            if (other.transform.IsChildOf(transform))
                continue;

            bool otherBreakable = other.CompareTag("Breakable") || other.GetComponentInParent<Break>() != null;
            if (!otherBreakable)
                continue;

            for (int c = 0; c < mine.Length; c++)
            {
                if (mine[c] != null && mine[c].enabled && mine[c] != other)
                    Physics.IgnoreCollision(mine[c], other, true);
            }
        }
    }

    void SpawnDebris(Vector3 dir, GameObject instigator, bool wall)
    {
        int count = wall
            ? Mathf.Clamp(debrisCount / 2, 2, 4)
            : Mathf.Clamp(debrisCount, 2, 12);
        Vector3 origin = transform.position;
        Vector3 size = GetComponent<Collider>() != null
            ? GetComponent<Collider>().bounds.size
            : Vector3.one;
        bool useWallMat = wall || isWallTile;

        for (int i = 0; i < count; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "Debris";
            float scale = Random.Range(0.08f, 0.18f) * Mathf.Clamp(size.magnitude * 0.12f, 0.4f, 1.4f);
            chunk.transform.position = origin + Random.insideUnitSphere * 0.25f;
            chunk.transform.rotation = Random.rotation;
            chunk.transform.localScale = Vector3.one * scale;

            Renderer r = chunk.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = useWallMat ? GetCrackedWallMaterial() : GetCrackedPropMaterial();

            Rigidbody body = chunk.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 force = dir * debrisForce + Random.insideUnitSphere * debrisForce * 0.7f + Vector3.up * 3.5f;
            body.AddForce(force, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);

            DebrisHazard hazard = chunk.AddComponent<DebrisHazard>();
            hazard.damage = debrisDamage * Random.Range(0.45f, 0.8f);
            hazard.lifetime = Mathf.Max(debrisLifetime, 1.2f);
            hazard.owner = instigator;
            hazard.sourceBreakable = gameObject;

            Object.Destroy(chunk, Mathf.Max(debrisLifetime, 1.2f) + 0.8f);
        }
    }
}

/// <summary>
/// Flying debris chunk that damages enemies (and lightly damages player) on impact.
/// </summary>
public class DebrisHazard : MonoBehaviour
{
    public float damage = 18f;
    public float lifetime = 0.7f;
    public GameObject owner;
    public GameObject sourceBreakable;
    public float minSpeed = 3.5f;

    readonly HashSet<int> hitIds = new HashSet<int>();
    float age;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age > lifetime)
            enabled = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (age > lifetime || collision == null)
            return;

        if (rb != null && rb.linearVelocity.magnitude < minSpeed)
            return;

        Collider other = collision.collider;
        if (other == null)
            return;

        if (sourceBreakable != null &&
            (other.gameObject == sourceBreakable || other.transform.IsChildOf(sourceBreakable.transform)))
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        int id = health.GetInstanceID();
        if (!hitIds.Add(id))
            return;

        bool isPlayer = health.CompareTag("Player") || health.transform.root.CompareTag("Player");
        float dmg = isPlayer ? damage * 0.35f : damage;
        health.TakeDamage(dmg, collision.GetContact(0).point, owner != null ? owner : gameObject);
        CombatVfx.SpawnOnomatopoeia(collision.GetContact(0).point, "WHAM!");
    }
}
