using UnityEngine;
using UnityEngine.InputSystem;

public class Melee : MonoBehaviour
{
    [Header("Attack")]
    public float attackCooldown = 0.5f;
    public string attackTrigger = "Attack";

    [Header("Input")]
    public InputActionReference attackActionReference;

    [Header("Animation")]
    public Animator animator;

    float currentCooldown;
    InputAction attackAction;
    bool ownsAttackAction;

    void Awake()
    {
        if (attackCooldown < 0f)
            attackCooldown = 0.5f;

        if (animator == null)
            animator = GetComponent<Animator>();
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
    }
}
