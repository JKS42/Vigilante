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
/// Close range uses a melee swipe so shots are not required to land damage.
/// Bosses also throw grenades via BossController.
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [SerializeField] EnemyWeaponKind weaponKind = EnemyWeaponKind.Pistol;
    [SerializeField] float damage = 12f;
    [SerializeField] float meleeDamage = 18f;
    [SerializeField] float fireRate = 2.5f;
    [SerializeField] float attackRange = 18f;
    [SerializeField] float meleeRange = 2.2f;
    [SerializeField] float meleeRadius = 1.15f;
    [SerializeField] float pelletCount = 1f;
    [SerializeField] float spreadDegrees = 0f;
    [Header("Shot error (degrees)")]
    [SerializeField] float shotErrorDegrees = 0f;
    [SerializeField] float shotErrorPerMeter = 0f;
    [SerializeField] float movingShotError = 0f;
    [SerializeField] float airShotError = 0f;
    [SerializeField] float dashShotError = 0f;
    [SerializeField] float maxShotError = 0f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] Transform muzzle;
    [SerializeField] AudioClip shotClip;

    float nextFireTime;
    EnemyAnimator animator;
    bool meleeOnly;
    WeaponSwitcher playerLoadout;
    PlayerMovement playerMove;
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public float AttackRange => meleeOnly ? meleeRange : attackRange;
    public float MeleeRange => meleeRange;
    public bool MeleeOnly => meleeOnly;
    public float Damage => damage;
    public EnemyWeaponKind WeaponKind => weaponKind;
    public float PreferredMinRange { get; private set; } = 2f;

    void Awake()
    {
        animator = null;
        if (muzzle == null)
        {
            GameObject m = new GameObject("Muzzle");
            m.transform.SetParent(transform);
            m.transform.localPosition = new Vector3(0.25f, 1.4f, 0.85f);
            muzzle = m.transform;
        }
    }

    public void ConfigureForArchetype(EnemyArchetype archetype)
    {
        meleeOnly = archetype == EnemyArchetype.Melee;
        switch (archetype)
        {
            case EnemyArchetype.Melee:
                weaponKind = EnemyWeaponKind.Pistol;
                damage = 0f;
                meleeDamage = 18f;
                fireRate = 1.4f;
                attackRange = 2.2f;
                meleeRange = 2.4f;
                pelletCount = 0f;
                spreadDegrees = 0f;
                SetShotError(0f, 0f, 0f, 0f, 0f, 0f);
                PreferredMinRange = 1.2f;
                break;

            case EnemyArchetype.Pistol:
                weaponKind = EnemyWeaponKind.Pistol;
                damage = 6f;
                meleeDamage = 16f;
                fireRate = 2.2f;
                attackRange = 16f;
                pelletCount = 1f;
                spreadDegrees = 0.4f;
                SetShotError(2.8f, 0.18f, 3.5f, 2f, 4f, 9f);
                PreferredMinRange = 3f;
                break;

            case EnemyArchetype.Shotgun:
                weaponKind = EnemyWeaponKind.Shotgun;
                damage = 7f;
                meleeDamage = 22f;
                fireRate = 1.05f;
                attackRange = 9f;
                pelletCount = 7f;
                spreadDegrees = 9f;
                SetShotError(1.2f, 0.08f, 2f, 1.5f, 2.5f, 6f);
                PreferredMinRange = 1.5f;
                break;

            case EnemyArchetype.Rifle:
                weaponKind = EnemyWeaponKind.Rifle;
                damage = 14f;
                meleeDamage = 16f;
                fireRate = 3.4f;
                attackRange = 24f;
                pelletCount = 1f;
                spreadDegrees = 0.4f;
                SetShotError(1f, 0.10f, 2.5f, 1.5f, 3f, 6f);
                PreferredMinRange = 8f;
                break;

            case EnemyArchetype.Boss:
                weaponKind = EnemyWeaponKind.BossGun;
                damage = 16f;
                meleeDamage = 28f;
                fireRate = 3.8f;
                attackRange = 22f;
                pelletCount = 2f;
                spreadDegrees = 1.5f;
                SetShotError(1.6f, 0.12f, 2.5f, 1.5f, 3f, 7f);
                PreferredMinRange = 4f;
                break;
        }
    }

    public bool TryAttack(Transform target)
    {
        if (target == null)
            return false;

        float dist = Vector3.Distance(transform.position, target.position);
        if (meleeOnly || dist <= meleeRange)
            return TryMeleeAt(target);

        return TryFireAt(target);
    }

    public bool TryMeleeAt(Transform target)
    {
        if (target == null || Time.time < nextFireTime)
            return false;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > meleeRange + 0.35f)
            return false;

        nextFireTime = Time.time + 0.7f;
        animator?.PlayFire();

        Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * 0.55f;
        AudioManager.MeleeHit(origin);
        CombatVfx.SpawnOnomatopoeia(origin, "WHACK!");

        Collider[] hits = Physics.OverlapSphere(origin, meleeRadius, hitMask, QueryTriggerInteraction.Collide);
        bool anyHit = false;
        Health damaged = null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || IsOwnCollider(col))
                continue;

            if (col.CompareTag("Breakable") || HasBreakable(col.transform))
            {
                Break br = col.GetComponentInParent<Break>();
                if (br != null)
                    br.BreakApart(transform.forward * 14f, gameObject, col.ClosestPoint(origin));
                continue;
            }

            if (!IsPlayerHit(col))
                continue;

            Health health = col.GetComponentInParent<Health>();
            if (health == null || health.transform.root == transform.root || health == damaged)
                continue;

            health.TakeDamage(meleeDamage, col.ClosestPoint(origin), gameObject);
            CombatVfx.SpawnImpact(col.ClosestPoint(origin), -transform.forward);
            damaged = health;
            anyHit = true;
        }

        return anyHit;
    }

    public bool TryFireAt(Transform target)
    {
        if (target == null || Time.time < nextFireTime)
            return false;

        Vector3 origin = GetMuzzlePosition();
        Vector3 aimPoint = GetAimPoint(target);
        Vector3 baseDir = aimPoint - origin;
        float dist = baseDir.magnitude;
        if (dist > attackRange)
            return false;

        Vector3 aimDir = ApplySpread(baseDir.normalized, EvaluateShotError(dist, GetPlayerMovement(target)));

        nextFireTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);
        animator?.PlayFire();

        CombatStimulus.EmitNoise(origin, weaponKind == EnemyWeaponKind.Shotgun ? 28f : 22f, StimulusType.Gunfire);
        AudioManager.EnemyGunshot(origin, weaponKind);
        CombatVfx.SpawnMuzzleFlash(origin, aimDir);
        DialogueManager.EnemyBark(transform.position, "fire");

        bool anyHit = false;
        int pellets = Mathf.Max(1, Mathf.RoundToInt(pelletCount));
        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplySpread(aimDir, spreadDegrees);
            Debug.DrawRay(origin, dir * dist, Color.red, 0.08f);

            if (!TryGetFirstHit(origin, dir, attackRange, out RaycastHit hit))
                continue;

            if (hit.collider.CompareTag("Breakable") || HasBreakable(hit.collider.transform))
            {
                Break br = hit.collider.GetComponentInParent<Break>();
                if (br != null)
                    br.BreakApart(dir * 14f, gameObject, hit.point);
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
                health.TakeDamage(GetShotDamage(), hit.point, gameObject);
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

        Vector3 origin = GetMuzzlePosition();
        Vector3 aimPoint = GetAimPoint(target);
        Vector3 dir = aimPoint - origin;
        float dist = dir.magnitude;
        if (dist > attackRange)
            return false;

        if (!TryGetFirstHit(origin, dir.normalized, dist + 0.35f, out RaycastHit hit))
            return false;

        return IsPlayerHit(hit.collider);
    }

    public static Vector3 GetAimPoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Collider col = target.GetComponent<Collider>();
        if (col == null)
            col = target.GetComponentInChildren<Collider>();

        if (col != null)
            return col.bounds.center;

        return target.position + Vector3.up * 0.9f;
    }

    Vector3 GetMuzzlePosition()
    {
        return muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
    }

    void SetShotError(float baseError, float perMeter, float moving, float air, float dash, float maxError)
    {
        shotErrorDegrees = baseError;
        shotErrorPerMeter = perMeter;
        movingShotError = moving;
        airShotError = air;
        dashShotError = dash;
        maxShotError = maxError;
    }

    float EvaluateShotError(float distance, PlayerMovement move)
    {
        float error = shotErrorDegrees + shotErrorPerMeter * Mathf.Max(0f, distance);
        if (move != null)
        {
            float sprint = Mathf.Max(0.01f, move.sprintSpeed);
            error += movingShotError * Mathf.InverseLerp(0f, sprint, move.HorizontalSpeed);
            if (!move.IsGrounded)
                error += airShotError;
            if (move.IsDashing)
                error += dashShotError;
        }

        if (maxShotError > 0f)
            error = Mathf.Min(error, maxShotError);
        return Mathf.Max(0f, error);
    }

    PlayerMovement GetPlayerMovement(Transform target)
    {
        if (playerMove != null)
            return playerMove;

        if (target != null)
            playerMove = target.GetComponentInParent<PlayerMovement>();
        if (playerMove == null)
            playerMove = FindFirstObjectByType<PlayerMovement>();
        return playerMove;
    }

    float GetShotDamage()
    {
        if (weaponKind != EnemyWeaponKind.Pistol)
            return damage;

        if (playerLoadout == null)
            playerLoadout = FindFirstObjectByType<WeaponSwitcher>();

        float shotDamage = damage;
        if (playerLoadout != null)
        {
            if (playerLoadout.IsUnlocked(2)) // shotgun
                shotDamage += 1f;
            if (playerLoadout.IsUnlocked(3)) // AR
                shotDamage += 1f;
        }

        return shotDamage;
    }

    bool TryGetFirstHit(Vector3 origin, Vector3 dir, float maxDistance, out RaycastHit hit)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null || IsOwnCollider(col))
                continue;

            if (hitBuffer[i].distance < bestDist)
            {
                bestDist = hitBuffer[i].distance;
                best = i;
            }
        }

        if (best < 0)
        {
            hit = default;
            return false;
        }

        hit = hitBuffer[best];
        return true;
    }

    bool IsOwnCollider(Collider col)
    {
        return col != null && col.transform.root == transform.root;
    }

    static Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        Vector3 dir = forward.normalized;
        if (degrees <= 0.01f)
            return dir;

        Quaternion aim = Quaternion.LookRotation(dir);
        Vector3 right = aim * Vector3.right;
        Vector3 up = aim * Vector3.up;
        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-degrees, degrees), up);
        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-degrees, degrees), right);
        return (yaw * pitch * dir).normalized;
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
