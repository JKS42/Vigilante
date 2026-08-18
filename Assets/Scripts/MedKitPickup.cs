using UnityEngine;

/// <summary>
/// World pickup that restores player Health on contact.
/// Left on the ground when the player is already at full HP.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MedKitPickup : MonoBehaviour
{
    public float healAmount = 15f;

    [Header("Motion")]
    public float spinSpeed = 90f;
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2.5f;

    bool collected;
    Vector3 basePos;
    float bobPhase;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Start()
    {
        basePos = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = basePos + Vector3.up * (Mathf.Sin((Time.time + bobPhase) * bobSpeed) * bobAmplitude);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || other == null)
            return;

        if (!IsPlayer(other))
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            health = other.transform.root.GetComponentInChildren<Health>();

        if (health == null || health.IsDead)
            return;

        if (health.CurrentHealth >= health.MaxHealth)
            return;

        collected = true;
        health.Heal(healAmount);
        AudioManager.WeaponPickup();
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up, "HEAL!");
        Destroy(gameObject);
    }

    public static MedKitPickup Spawn(Vector3 position, GameObject prefab = null)
    {
        if (prefab != null)
        {
            GameObject go = Object.Instantiate(prefab, position, Quaternion.identity);
            MedKitPickup pickup = go.GetComponent<MedKitPickup>();
            if (pickup == null)
                pickup = go.GetComponentInChildren<MedKitPickup>();
            if (pickup == null)
                pickup = go.AddComponent<MedKitPickup>();
            return pickup;
        }

        return SpawnRuntime(position);
    }

    public static MedKitPickup SpawnRuntime(Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "MedKitPickup";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.35f, 0.22f, 0.45f);

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = new Color(0.85f, 0.2f, 0.22f);
                r.sharedMaterial = mat;
            }
        }

        MedKitPickup pickup = go.AddComponent<MedKitPickup>();
        pickup.healAmount = 15f;
        pickup.basePos = position;
        return pickup;
    }

    static bool IsPlayer(Collider col)
    {
        return col.CompareTag("Player") || col.transform.root.CompareTag("Player");
    }
}
