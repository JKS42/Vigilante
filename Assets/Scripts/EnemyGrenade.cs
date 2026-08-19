using UnityEngine;

/// <summary>
/// Thrown explosive used by the boss. Damages Health in radius and can shatter breakables.
/// </summary>
public class EnemyGrenade : MonoBehaviour
{
    public float fuse = 1.6f;
    public float damage = 35f;
    public float radius = 4.5f;
    public float blastForce = 12f;
    public GameObject instigator;

    float age;
    bool detonated;
    Rigidbody rb;

    public static EnemyGrenade Throw(Vector3 origin, Vector3 velocity, GameObject owner)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyGrenade";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * 0.28f;

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;

        Rigidbody body = go.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = velocity;

        EnemyGrenade grenade = go.AddComponent<EnemyGrenade>();
        grenade.instigator = owner;
        grenade.rb = body;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = CelMaterial.Create(new Color(0.15f, 0.45f, 0.15f), "Grenade");
            if (mat != null)
                r.sharedMaterial = mat;
        }

        Object.Destroy(go, 8f);
        return grenade;
    }

    void Update()
    {
        if (detonated)
            return;

        age += Time.deltaTime;
        if (age >= fuse)
            Detonate();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (detonated)
            return;

        if (age > 0.15f)
            Detonate();
    }

    void Detonate()
    {
        if (detonated)
            return;

        detonated = true;
        Vector3 pos = transform.position;
        CombatStimulus.EmitNoise(pos, 30f, StimulusType.Impact);
        CombatVfx.SpawnExplosion(pos, radius);
        CombatVfx.SpawnOnomatopoeia(pos + Vector3.up, "BOOM!");
        AudioManager.Explosion(pos);
        DialogueManager.EnemyBark(pos, "grenade");

        Collider[] hits = Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null)
                continue;

            float dist = Vector3.Distance(pos, c.ClosestPoint(pos));
            float falloff = 1f - Mathf.Clamp01(dist / radius);

            Break br = c.GetComponentInParent<Break>();
            if (br != null)
            {
                Vector3 push = (c.transform.position - pos).normalized * blastForce * falloff;
                br.BreakApart(push, instigator);
            }

            Health health = c.GetComponentInParent<Health>();
            if (health != null && !IsSelf(health))
                health.TakeDamage(damage * falloff, c.ClosestPoint(pos), instigator);

            Rigidbody body = c.attachedRigidbody;
            if (body != null && !body.isKinematic)
                body.AddExplosionForce(blastForce * 40f, pos, radius, 1.5f, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }

    bool IsSelf(Health health)
    {
        if (instigator == null || health == null)
            return false;
        return health.transform == instigator.transform || health.transform.IsChildOf(instigator.transform);
    }
}
