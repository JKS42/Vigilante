using UnityEngine;

public enum EnemyWeaponKind
{
    Pistol,
    Shotgun,
    Rifle,
    BossGun
}

/// <summary>
/// Enemy hitscan / pellet fire configured per archetype.
/// Bosses also throw grenades via BossController.
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [SerializeField] EnemyWeaponKind weaponKind = EnemyWeaponKind.Pistol;
    [SerializeField] float damage = 12f;
    [SerializeField] float fireRate = 2.5f;
    [SerializeField] float attackRange = 18f;
    [SerializeField] float pelletCount = 1f;
    [SerializeField] float spreadDegrees = 0f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] Transform muzzle;
    [SerializeField] AudioClip shotClip;

    float nextFireTime;
    EnemyAnimator animator;

    public float AttackRange => attackRange;
    public float Damage => damage;
    public EnemyWeaponKind WeaponKind => weaponKind;
    public float PreferredMinRange { get; private set; } = 2f;

    void Awake()
    {
        animator = GetComponent<EnemyAnimator>();
        if (muzzle == null)
        {
            GameObject m = new GameObject("Muzzle");
            m.transform.SetParent(transform);
            m.transform.localPosition = new Vector3(0.25f, 1.4f, 0.55f);
            muzzle = m.transform;
        }
    }

    public void ConfigureForArchetype(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Pistol:
                weaponKind = EnemyWeaponKind.Pistol;
                damage = 10f;
                fireRate = 2.2f;
                attackRange = 16f;
                pelletCount = 1f;
                spreadDegrees = 2.5f;
                PreferredMinRange = 3f;
                break;

            case EnemyArchetype.Shotgun:
                weaponKind = EnemyWeaponKind.Shotgun;
                damage = 7f;
                fireRate = 1.05f;
                attackRange = 9f;
                pelletCount = 7f;
                spreadDegrees = 9f;
                PreferredMinRange = 1.5f;
                break;

            case EnemyArchetype.Rifle:
                weaponKind = EnemyWeaponKind.Rifle;
                damage = 14f;
                fireRate = 3.4f;
                attackRange = 24f;
                pelletCount = 1f;
                spreadDegrees = 1.2f;
                PreferredMinRange = 8f;
                break;

            case EnemyArchetype.Boss:
                weaponKind = EnemyWeaponKind.BossGun;
                damage = 16f;
                fireRate = 3.8f;
                attackRange = 22f;
                pelletCount = 2f;
                spreadDegrees = 3.5f;
                PreferredMinRange = 4f;
                break;
        }
    }

    public bool TryFireAt(Transform target)
    {
        if (target == null || Time.time < nextFireTime)
            return false;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 aimPoint = target.position + Vector3.up * 1.2f;
        Vector3 baseDir = aimPoint - origin;
        float dist = baseDir.magnitude;
        if (dist > attackRange)
            return false;

        nextFireTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);
        animator?.PlayFire();

        CombatStimulus.EmitNoise(origin, weaponKind == EnemyWeaponKind.Shotgun ? 28f : 22f, StimulusType.Gunfire);
        AudioManager.EnemyGunshot(origin, weaponKind);
        CombatVfx.SpawnMuzzleFlash(origin, baseDir.normalized);
        DialogueManager.EnemyBark(transform.position, "fire");

        bool anyHit = false;
        int pellets = Mathf.Max(1, Mathf.RoundToInt(pelletCount));
        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplySpread(baseDir.normalized, spreadDegrees);
            Debug.DrawRay(origin, dir * dist, Color.red, 0.08f);

            if (!Physics.Raycast(origin, dir, out RaycastHit hit, attackRange, hitMask, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.collider.CompareTag("Breakable") || HasBreakable(hit.collider.transform))
            {
                Break br = hit.collider.GetComponentInParent<Break>();
                if (br != null)
                    br.BreakApart(dir * 8f, gameObject);
                CombatVfx.SpawnImpact(hit.point, hit.normal);
                continue;
            }

            if (!IsPlayerHit(hit.collider))
            {
                CombatVfx.SpawnImpact(hit.point, hit.normal);
                continue;
            }

            Health health = hit.collider.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage, hit.point, gameObject);
                CombatVfx.SpawnImpact(hit.point, hit.normal);
                CombatVfx.SpawnOnomatopoeia(hit.point, "BANG!");
                anyHit = true;
            }
        }

        return anyHit;
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

    static Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (degrees <= 0.01f)
            return forward;

        float yaw = Random.Range(-degrees, degrees);
        float pitch = Random.Range(-degrees, degrees);
        Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
        return (rot * forward).normalized;
    }

    static bool IsPlayerHit(Collider col)
    {
        if (col == null)
            return false;
        return col.CompareTag("Player") || col.transform.root.CompareTag("Player");
    }

    static bool HasBreakable(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Breakable"))
                return true;
            t = t.parent;
        }
        return false;
    }
}
