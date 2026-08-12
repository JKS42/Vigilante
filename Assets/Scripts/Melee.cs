using UnityEngine;
using UnityEngine.InputSystem;

public class Melee : MonoBehaviour
{
    [Header("Attack")]
    public float attackCooldown = 0.5f;
    public string attackTrigger = "Attack";
    public float damage = 25f;
    public float attackRange = 2f;
    public float attackRadius = 0.4f;
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

        if (attackAction == null || animator == null)
            return;

        if (attackAction.WasPressedThisFrame())
            TryAttack();
    }

    void TryAttack()
    {
        if (currentCooldown > 0f)
            return;

        animator.SetTrigger(attackTrigger);
        currentCooldown = Mathf.Max(0.01f, attackCooldown);
        PlaySwingSound();
        TryDealDamage();
    }

    void TryDealDamage()
    {
        Transform originTransform = ResolveAttackOrigin();
        Vector3 origin = originTransform.position;
        Vector3 direction = originTransform.forward;

        if (!Physics.SphereCast(
                origin,
                Mathf.Max(0.01f, attackRadius),
                direction,
                out RaycastHit hit,
                Mathf.Max(0.01f, attackRange),
                hitMask,
                QueryTriggerInteraction.Ignore))
            return;

        Collider other = hit.collider;
        if (other == null)
            return;

        if (playerRoot != null &&
            (other.gameObject == playerRoot || other.transform.IsChildOf(playerRoot.transform)))
            return;

        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
            return;

        bool hitEnemy = other.CompareTag("Enemy") || other.transform.root.CompareTag("Enemy");
        if (!hitEnemy)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage, hit.point, playerRoot);
            AudioManager.MeleeHit(hit.point);
            CombatVfx.SpawnOnomatopoeia(hit.point, "POW!");
            CombatVfx.SpawnImpact(hit.point, hit.normal);
        }
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
        if (attackOrigin != null)
            return attackOrigin;

        if (attackCamera == null)
            attackCamera = Camera.main;

        if (attackCamera != null)
            return attackCamera.transform;

        return transform;
    }
}
