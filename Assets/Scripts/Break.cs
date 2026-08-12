using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breakable prop / wall. Shatters into debris chunks that can damage enemies (and lightly the player).
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
        bool wall = isWallTile || name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0;
        Material mat = wall ? GetCrackedWallMaterial() : GetCrackedPropMaterial();
        renderer.sharedMaterial = mat;
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

    void OnTriggerEnter(Collider other)
    {
        if (isBroken)
            return;

        if (other.CompareTag("Bullet"))
            BreakApart();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken)
            return;

        if (collision.collider.CompareTag("Bullet"))
            BreakApart();
    }

    public void BreakApart()
    {
        BreakApart(Vector3.up * 2f + Random.insideUnitSphere, null);
    }

    public void BreakApart(Vector3 impulse, GameObject instigator)
    {
        if (isBroken)
            return;

        isBroken = true;
        CombatStimulus.EmitBreach(transform.position);
        AudioManager.BreakObject(transform.position);
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 0.5f, "CRACK!");
        CombatVfx.SpawnImpact(transform.position, Vector3.up);

        SpawnDebris(impulse, instigator);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(impulse + Vector3.up * 2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
        }

        if (!destroyScheduled)
        {
            destroyScheduled = true;
            Destroy(gameObject, 5f);
        }
    }

    void SpawnDebris(Vector3 impulse, GameObject instigator)
    {
        int count = Mathf.Clamp(debrisCount, 2, 12);
        Vector3 origin = transform.position;
        Vector3 size = GetComponent<Collider>() != null
            ? GetComponent<Collider>().bounds.size
            : Vector3.one;

        for (int i = 0; i < count; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "Debris";
            chunk.tag = "Breakable";
            float scale = Random.Range(0.12f, 0.28f) * Mathf.Clamp(size.magnitude * 0.15f, 0.5f, 2f);
            chunk.transform.position = origin + Random.insideUnitSphere * 0.35f;
            chunk.transform.rotation = Random.rotation;
            chunk.transform.localScale = Vector3.one * scale;

            Renderer r = chunk.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = isWallTile ? GetCrackedWallMaterial() : GetCrackedPropMaterial();

            Rigidbody body = chunk.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            Vector3 force = impulse.normalized * debrisForce + Random.insideUnitSphere * debrisForce * 0.6f + Vector3.up * 3f;
            body.AddForce(force, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 6f, ForceMode.Impulse);

            DebrisHazard hazard = chunk.AddComponent<DebrisHazard>();
            hazard.damage = debrisDamage * Random.Range(0.6f, 1f);
            hazard.lifetime = debrisLifetime;
            hazard.owner = instigator;
            hazard.sourceBreakable = gameObject;

            Object.Destroy(chunk, debrisLifetime + 0.5f);
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
