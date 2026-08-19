using UnityEngine;

/// <summary>
/// World pickup that unlocks a WeaponSwitcher loadout slot and equips it.
/// Slot indices match keys 1–4: 0 bat, 1 pistol, 2 shotgun, 3 AR.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    [Tooltip("Loadout index to unlock (1 = Pistol, 2 = Shotgun, 3 = AR).")]
    public int weaponIndex = 1;

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

        Transform root = other.transform.root;
        WeaponSwitcher switcher = root.GetComponentInChildren<WeaponSwitcher>();
        if (switcher == null)
            switcher = other.GetComponentInParent<WeaponSwitcher>();

        if (switcher == null)
            return;

        collected = true;
        bool newly = switcher.UnlockWeapon(weaponIndex, equip: true);
        AudioManager.WeaponPickup();
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up, newly ? "GET!" : "AMMO?");
        if (newly)
        {
            string name = weaponIndex == 1 ? "Pistol" : weaponIndex == 2 ? "Shotgun" : "Rifle";
            DialogueManager.PlayerLine($"Acquired {name}.");
            TutorialPrompt.Notify("weapon_pickup");
        }

        Destroy(gameObject);
    }

    public static WeaponPickup SpawnRuntime(Vector3 position, int index)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "WeaponPickup_" + index;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.35f, 0.2f, 0.55f);

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Color color = index == 1 ? new Color(0.85f, 0.75f, 0.2f)
                : index == 2 ? new Color(0.9f, 0.4f, 0.1f)
                : new Color(0.3f, 0.55f, 0.95f);
            Material mat = CelMaterial.Create(color, "WeaponPickup");
            if (mat != null)
                r.sharedMaterial = mat;
        }

        WeaponPickup pickup = go.AddComponent<WeaponPickup>();
        pickup.weaponIndex = index;
        pickup.basePos = position;
        Object.Destroy(go, 90f);
        return pickup;
    }

    static bool IsPlayer(Collider col)
    {
        return col.CompareTag("Player") || col.transform.root.CompareTag("Player");
    }
}
