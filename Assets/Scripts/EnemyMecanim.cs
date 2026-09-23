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

    [SerializeField] float moveThreshold = 0.15f;
    [SerializeField] float dampTime = 0.12f;
    [SerializeField] AnimationClip shootClip;
    [SerializeField] int fireReleaseFrame = 20;

    Animator animator;
    NavMeshAgent agent;
    bool dead;
    float fireBusyUntil;
    float currentSpeed;

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
            if (current.IsName("Shoot"))
                return true;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Shoot"))
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
        if (dead || animator == null || !animator.enabled)
            return;

        // While a shoot is locked in, keep locomotion flags clear so Shoot isn't interrupted.
        if (IsFiring || IsPlayingShoot)
        {
            currentSpeed = 0f;
            animator.SetFloat(SpeedId, 0f);
            animator.SetFloat(ForwardId, 0f);
            animator.SetFloat(StrafeId, 0f);
            animator.SetBool("Moving", false);
            animator.SetBool("Backing", false);
            animator.SetBool("Strafing", false);
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
        fireBusyUntil = 0f;
        animator.SetFloat(DieVariantId, Mathf.Clamp(variant, 0, 5));
        animator.ResetTrigger(DieId);
        animator.SetTrigger(DieId);
        animator.SetBool("Moving", false);
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
