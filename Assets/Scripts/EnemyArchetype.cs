using UnityEngine;

public enum EnemyArchetype
{
    Pistol,
    Shotgun,
    Rifle,
    Boss,
    Melee
}

/// <summary>
/// Behaviour + combat profile per enemy type. Attach beside EnemyAI / EnemyCombat
/// (or let factory / WaveManager apply it). Shotgun rushes, rifle holds range,
/// pistol is balanced tutorial foe, boss is aggressive with grenades.
/// </summary>
public class EnemyProfile : MonoBehaviour
{
    public EnemyArchetype archetype = EnemyArchetype.Pistol;

    [Header("Behaviour weights (0–1)")]
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float coverPreference = 0.55f;
    [Range(0f, 1f)] public float flankTendency = 0.4f;
    [Range(0f, 1f)] public float holdDistanceBias = 0.4f;

    [Header("Movement overrides")]
    public float moveSpeed = 3.5f;
    public float preferredEngageDistance = 10f;

    [Header("Loot")]
    public int weaponDropIndex = 1;
    public float weaponDropChance = 1f;

    [Header("Presentation")]
    public Color tint = new Color(0.75f, 0.2f, 0.2f);
    public string displayName = "Gunman";

    public static EnemyProfile ApplyDefaults(GameObject go, EnemyArchetype type)
    {
        EnemyProfile profile = go.GetComponent<EnemyProfile>();
        if (profile == null)
            profile = go.AddComponent<EnemyProfile>();

        profile.archetype = type;
        switch (type)
        {
            case EnemyArchetype.Pistol:
                profile.aggression = 0.45f;
                profile.coverPreference = 0.55f;
                profile.flankTendency = 0.35f;
                profile.holdDistanceBias = 0.35f;
                profile.moveSpeed = 3.4f;
                profile.preferredEngageDistance = 9f;
                profile.weaponDropIndex = 1;
                profile.weaponDropChance = 1f;
                profile.tint = new Color(0.78f, 0.22f, 0.18f);
                profile.displayName = "Pistol Thug";
                break;

            case EnemyArchetype.Shotgun:
                profile.aggression = 0.9f;
                profile.coverPreference = 0.25f;
                profile.flankTendency = 0.55f;
                profile.holdDistanceBias = 0.1f;
                profile.moveSpeed = 4.6f;
                profile.preferredEngageDistance = 4.5f;
                profile.weaponDropIndex = 2;
                profile.weaponDropChance = 0.85f;
                profile.tint = new Color(0.85f, 0.45f, 0.1f);
                profile.displayName = "Shotgun Bruiser";
                break;

            case EnemyArchetype.Rifle:
                profile.aggression = 0.35f;
                profile.coverPreference = 0.85f;
                profile.flankTendency = 0.25f;
                profile.holdDistanceBias = 0.85f;
                profile.moveSpeed = 3.1f;
                profile.preferredEngageDistance = 16f;
                profile.weaponDropIndex = 3;
                profile.weaponDropChance = 0.75f;
                profile.tint = new Color(0.2f, 0.35f, 0.75f);
                profile.displayName = "Rifle Marksman";
                break;

            case EnemyArchetype.Boss:
                profile.aggression = 0.8f;
                profile.coverPreference = 0.4f;
                profile.flankTendency = 0.5f;
                profile.holdDistanceBias = 0.45f;
                profile.moveSpeed = 4.2f;
                profile.preferredEngageDistance = 11f;
                profile.weaponDropIndex = 3;
                profile.weaponDropChance = 0f;
                profile.tint = new Color(0.15f, 0.05f, 0.2f);
                profile.displayName = "Boss";
                break;

            case EnemyArchetype.Melee:
                profile.aggression = 0.95f;
                profile.coverPreference = 0.1f;
                profile.flankTendency = 0.35f;
                profile.holdDistanceBias = 0.05f;
                profile.moveSpeed = 4.8f;
                profile.preferredEngageDistance = 2f;
                profile.weaponDropIndex = 1;
                profile.weaponDropChance = 0f;
                profile.tint = new Color(0.72f, 0.18f, 0.16f);
                profile.displayName = "Bat Thug";
                break;
        }

        profile.ApplyToComponents();
        return profile;
    }

    public void ApplyToComponents()
    {
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.speed = moveSpeed;

        EnemyCombat combat = GetComponent<EnemyCombat>();
        if (combat != null)
            combat.ConfigureForArchetype(archetype);

        Health health = GetComponent<Health>();
        if (health != null && archetype == EnemyArchetype.Boss)
            health.SetMaxHealth(420f, refill: true);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].sharedMaterial == null)
                continue;

            Material mat = renderers[i].material;
            CelMaterial.Convert(mat);
            CelMaterial.ApplyColor(mat, tint);
        }

        EnemyAnimator leftover = GetComponent<EnemyAnimator>();
        if (leftover != null)
        {
            leftover.enabled = false;
            Destroy(leftover);
        }

        if (archetype == EnemyArchetype.Boss)
        {
            BossController boss = GetComponent<BossController>();
            if (boss == null)
                gameObject.AddComponent<BossController>();
        }

        ApplyWeaponVisuals(gameObject, archetype);
    }

    static void ApplyWeaponVisuals(GameObject go, EnemyArchetype type)
    {
        Transform[] transforms = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t == go.transform)
                continue;

            string n = t.name;
            bool isGun = ContainsIgnoreCase(n, "Rifle")
                || ContainsIgnoreCase(n, "Shotgun")
                || ContainsIgnoreCase(n, "Pistol")
                || ContainsIgnoreCase(n, "Gun")
                || ContainsIgnoreCase(n, "AR");
            bool isBat = ContainsIgnoreCase(n, "Bat") && !ContainsIgnoreCase(n, "BatEnemy");

            if (type == EnemyArchetype.Melee)
            {
                if (isGun)
                    t.gameObject.SetActive(false);
                else if (isBat)
                    t.gameObject.SetActive(true);
            }
        }
    }

    static bool ContainsIgnoreCase(string value, string token)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
