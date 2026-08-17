using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Melee : MonoBehaviour
{
    [Header("Attack")]
    public float attackCooldown = 0.5f;
    public string attackTrigger = "Attack";
    public float damage = 25f;
    public float attackRange = 2.2f;
    public float attackRadius = 0.6f;
    public float hitDelay = 0.22f;
    public LayerMask hitMask = ~0;

    [Header("Hit Origin")]
    public Transform attackOrigin;
    public Camera attackCamera;

    [Header("Input")]
    public InputActionReference attackActionReference;

    [Header("Animation")]
    public Animator animator;

    [Header("Optional FX")]
    public AudioClip swingSound;

    float currentCooldown;
    AudioSource audioSource;
    InputAction attackAction;
    bool ownsAttackAction;
    GameObject playerRoot;
    readonly RaycastHit[] sweepHits = new RaycastHit[24];
    readonly Collider[] overlapHits = new Collider[24];
    readonly HashSet<int> damagedIds = new HashSet<int>();
    Coroutine pendingHit;

    void Awake()
    {
        if (attackCooldown < 0f)
            attackCooldown = 0.5f;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (attackCamera == null)
            attackCamera = Camera.main;

        playerRoot = transform.root.gameObject;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && swingSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void OnEnable()
    {
        BindAction();

        if (attackAction != null)
            attackAction.Enable();
    }

    void OnDisable()
    {
        if (attackAction != null && ownsAttackAction)
        {
            attackAction.Disable();
            attackAction.Dispose();
        }

        attackAction = null;
        ownsAttackAction = false;
    }

    void BindAction()
    {
        if (attackActionReference != null && attackActionReference.action != null)
        {
            attackAction = attackActionReference.action;
            ownsAttackAction = false;
        }
        else
        {
            attackAction = new InputAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/rightTrigger");
            ownsAttackAction = true;
        }
    }

    void Update()
    {
        if (currentCooldown > 0f)
            currentCooldown -= Time.deltaTime;

        if (attackAction == null)
            return;

        if (Time.timeScale <= 0f)
            return;

        if (attackAction.WasPressedThisFrame())
            TryAttack();
    }

    void TryAttack()
    {
        if (currentCooldown > 0f)
            return;

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(attackTrigger))
            {
                animator.ResetTrigger(attackTrigger);
                animator.SetTrigger(attackTrigger);
            }
        }

        currentCooldown = Mathf.Max(0.01f, attackCooldown);
        PlaySwingSound();

        if (pendingHit != null)
            StopCoroutine(pendingHit);
        pendingHit = StartCoroutine(DealDamageAfterDelay());
    }

    IEnumerator DealDamageAfterDelay()
    {
        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);

        TryDealDamage();
        pendingHit = null;
    }

    void TryDealDamage()
    {
        Transform originTransform = ResolveAttackOrigin();
        Vector3 direction = originTransform.forward;
        Vector3 origin = originTransform.position - direction * 0.4f;
        float range = Mathf.Max(0.01f, attackRange) + 0.4f;
        float radius = Mathf.Max(0.01f, attackRadius);

        damagedIds.Clear();

        int sweepCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            sweepHits,
            range,
            hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < sweepCount; i++)
            TryApplyHit(sweepHits[i].collider, sweepHits[i].point, sweepHits[i].normal);

        int overlapCount = Physics.OverlapSphereNonAlloc(
            originTransform.position + direction * (attackRange * 0.45f),
            radius + 0.25f,
            overlapHits,
            hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapHits[i];
            if (col == null)
                continue;

            Vector3 point = col.ClosestPoint(originTransform.position);
            Vector3 normal = (originTransform.position - point).sqrMagnitude > 0.001f
                ? (originTransform.position - point).normalized
                : -direction;
            TryApplyHit(col, point, normal);
        }
    }

    void TryApplyHit(Collider other, Vector3 point, Vector3 normal)
    {
        if (other == null)
            return;

        if (playerRoot != null &&
            (other.gameObject == playerRoot || other.transform.IsChildOf(playerRoot.transform)))
            return;

        if (other.CompareTag("Player") || other.CompareTag("Bat") || other.transform.root.CompareTag("Player"))
            return;

        if (other.CompareTag("Breakable") || HasBreakable(other.transform))
        {
            int breakId = other.transform.root.GetInstanceID();
            if (!damagedIds.Add(breakId))
                return;

            Break br = other.GetComponentInParent<Break>();
            if (br != null)
            {
                br.BreakApart(ResolveAttackOrigin().forward * 8f, playerRoot);
                CombatVfx.SpawnOnomatopoeia(point, "CRACK!");
            }
            return;
        }

        bool hitEnemy = other.CompareTag("Enemy") || other.transform.root.CompareTag("Enemy");
        if (!hitEnemy)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        int id = health.GetInstanceID();
        if (!damagedIds.Add(id))
            return;

        health.TakeDamage(damage, point, playerRoot);
        AudioManager.MeleeHit(point);
        CombatVfx.SpawnOnomatopoeia(point, "POW!");
        CombatVfx.SpawnImpact(point, normal);
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

    void PlaySwingSound()
    {
        if (swingSound != null && audioSource != null)
            audioSource.PlayOneShot(swingSound);
        else
            AudioManager.MeleeSwing();
    }

    Transform ResolveAttackOrigin()
    {
        // Prefer the camera so the sweep follows the crosshair, not the bat's idle downward pose.
        if (attackCamera == null)
            attackCamera = Camera.main;

        if (attackCamera != null)
            return attackCamera.transform;

        if (attackOrigin != null)
            return attackOrigin;

        return transform;
    }
}
