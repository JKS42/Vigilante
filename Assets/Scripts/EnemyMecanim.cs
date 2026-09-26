using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives a Mecanim Animator on pistol enemies.
/// Fire plays Shooting.fbx; ranged attacks require standing still.
/// Hitscan releases on a configured frame of the shoot clip.
/// </summary>
public class EnemyMecanim : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int ForwardId = Animator.StringToHash("Forward");
    static readonly int StrafeId = Animator.StringToHash("Strafe");
    static readonly int FireId = Animator.StringToHash("Fire");
    static readonly int DieId = Animator.StringToHash("Die");
    static readonly int DieVariantId = Animator.StringToHash("DieVariant");

    const float DefaultSampleRate = 30f;
    const float MeleeAttackSpeed = 1.5f;

    [SerializeField] float moveThreshold = 0.15f;
    [SerializeField] float dampTime = 0.12f;
    [SerializeField] AnimationClip shootClip;
    [SerializeField] int fireReleaseFrame = 20;

    Animator animator;
    NavMeshAgent agent;
    bool dead;
    float fireBusyUntil;
    float currentSpeed;
    float baseAnimatorSpeed = 1f;

    public bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;
    public bool IsDead => dead;
    public bool IsMoving => currentSpeed > moveThreshold;
    public bool IsFiring => Time.time < fireBusyUntil;
    public bool BlocksCombat => dead || IsFiring;
    public bool CanFireRanged => !dead && !IsFiring && !IsMoving;

    public bool IsPlayingShoot
    {
        get
        {
            if (animator == null || dead)
                return false;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName("Shoot") || current.IsName("Strike"))
                return true;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Shoot") || next.IsName("Strike"))
                    return true;
            }

            return false;
        }
    }

    public float FireReleaseDelay
    {
        get
        {
            float rate = shootClip != null && shootClip.frameRate > 1f
                ? shootClip.frameRate
                : DefaultSampleRate;
            return Mathf.Clamp(fireReleaseFrame, 0, 500) / rate;
        }
    }

    public float MeleeCycleDuration => ShootCycleDuration / MeleeAttackSpeed;

    public float ShootCycleDuration
    {
        get
        {
            if (shootClip != null && shootClip.length > 0.05f)
                return shootClip.length;
            return 70f / DefaultSampleRate;
        }
    }

    public void SetShootClip(AnimationClip clip, int releaseFrame = 20)
    {
        shootClip = clip;
        fireReleaseFrame = releaseFrame;
    }

    public void CancelFireLock()
    {
        fireBusyUntil = 0f;
        if (animator != null)
            animator.ResetTrigger(FireId);
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            baseAnimatorSpeed = animator.speed;
        }
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();

        if (shootClip == null)
            TryResolveShootClip();
    }

    void TryResolveShootClip()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
            return;

        AnimationClip best = null;
        float bestScore = float.MaxValue;
        float targetLen = 70f / DefaultSampleRate;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip c = clips[i];
            if (c == null || c.length < 0.4f || c.length > 4f)
                continue;
            float score = Mathf.Abs(c.length - targetLen);
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best != null)
            shootClip = best;
    }

    void Update()
    {
        if (animator == null)
            return;
        animator.speed = dead ? baseAnimatorSpeed : (IsPlayingMeleeStrike() ? baseAnimatorSpeed * MeleeAttackSpeed : baseAnimatorSpeed);
        if (dead || !animator.enabled)
            return;

        // While a swing or shot is locked in, keep locomotion flags clear so it isn't interrupted.
        if (IsFiring || IsPlayingShoot)
        {
            currentSpeed = 0f;
            animator.SetFloat(SpeedId, 0f);
            animator.SetFloat(ForwardId, 0f);
            animator.SetFloat(StrafeId, 0f);
            animator.SetBool("Moving", false);
            animator.SetBool("Backing", false);
            animator.SetBool("Strafing", false);
            SetUpperIdle(false);
            return;
        }

        Vector3 velocity = agent != null && agent.enabled ? agent.velocity : Vector3.zero;
        velocity.y = 0f;
        currentSpeed = velocity.magnitude;

        float forward = 0f;
        float strafe = 0f;
        if (currentSpeed > 0.01f)
        {
            Vector3 local = transform.InverseTransformDirection(velocity.normalized);
            forward = local.z;
            strafe = local.x;
        }

        animator.SetFloat(SpeedId, currentSpeed, dampTime, Time.deltaTime);
        animator.SetFloat(ForwardId, forward, dampTime, Time.deltaTime);
        animator.SetFloat(StrafeId, strafe, dampTime, Time.deltaTime);

        animator.SetBool("Moving", currentSpeed > moveThreshold);
        animator.SetBool("Backing", currentSpeed > moveThreshold && forward < -0.35f);
        animator.SetBool("Strafing", currentSpeed > moveThreshold && Mathf.Abs(strafe) > Mathf.Abs(forward) + 0.1f);
        // Legs keep the run. Torso stays on the idle pose while moving either direction.
        SetUpperIdle(currentSpeed > moveThreshold);
    }

    bool IsPlayingMeleeStrike()
    {
        if (animator == null)
            return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName("Strike"))
            return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Strike");
    }
    void SetUpperIdle(bool on)
    {
        if (animator == null)
            return;
        int layer = animator.GetLayerIndex("UpperIdle");
        if (layer < 0)
            return;
        animator.SetLayerWeight(layer, on ? 1f : 0f);
    }

    public float MeleeImpactDelay => MeleeCycleDuration * 0.38f;

    public bool PlayMelee()
    {
        if (dead || animator == null || IsFiring)
            return false;

        currentSpeed = 0f;
        animator.SetBool("Moving", false);
        animator.SetBool("Backing", false);
        animator.SetBool("Strafing", false);
        SetUpperIdle(false);
        animator.ResetTrigger(FireId);
        animator.CrossFadeInFixedTime("Strike", 0.06f, 0, 0f);
        fireBusyUntil = Time.time + MeleeCycleDuration;
        return true;
    }

    public bool PlayFire()
    {
        if (!CanFireRanged || animator == null)
            return false;

        // Clear loco flags so Idle→Shoot (or a direct CrossFade) can take over.
        currentSpeed = 0f;
        animator.SetBool("Moving", false);
        animator.SetBool("Backing", false);
        animator.SetBool("Strafing", false);

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool inIdleOrShoot = info.IsName("Idle") || info.IsName("Shoot");
        if (!inIdleOrShoot)
        {
            // Still in Run/Strafe — Fire trigger only exists on Idle. Force Shoot.
            animator.ResetTrigger(FireId);
            animator.CrossFadeInFixedTime("Shoot", 0.05f, 0, 0f);
        }
        else
        {
            animator.ResetTrigger(FireId);
            animator.SetTrigger(FireId);
        }

        fireBusyUntil = Time.time + ShootCycleDuration;
        return true;
    }

    public void PlayDeath(int variant = 0)
    {
        if (animator == null)
            return;

        dead = true;
        if (animator != null)
            animator.speed = baseAnimatorSpeed;
        fireBusyUntil = 0f;
        SetUpperIdle(false);
        animator.SetFloat(DieVariantId, Mathf.Clamp(variant, 0, 5));
        animator.ResetTrigger(DieId);
        animator.SetTrigger(DieId);
        animator.SetBool("Moving", false);
    }

    public bool DeathAnimationFinished
    {
        get
        {
            if (!dead || animator == null)
                return false;
            if (animator.IsInTransition(0))
                return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName("Die") && info.normalizedTime >= 0.98f;
        }
    }

    public void ReleaseCorpseWhenDeathEnds()
    {
        StartCoroutine(ReleaseCorpse());
    }

    IEnumerator ReleaseCorpse()
    {
        float elapsed = 0f;
        yield return null;
        while (elapsed < 12f && !DeathAnimationFinished)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (this != null)
            Destroy(gameObject, 0.75f);
    }

    public static int ResolveDeathVariant(Transform enemy, Vector3 hitPoint, Vector3 hitDir, bool headshot)
    {
        Vector3 local = enemy.InverseTransformDirection(
            hitDir.sqrMagnitude > 0.01f ? -hitDir : (enemy.position - hitPoint));
        local.y = 0f;
        if (local.sqrMagnitude < 0.001f)
            return headshot ? 4 : 0;
        local.Normalize();

        if (headshot)
            return local.z > 0f ? 4 : 5;

        if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
            return 3;
        return local.z > 0f ? 1 : 2;
    }
}
