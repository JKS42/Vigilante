using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.6f;
    public float moveSpeed = 5f;
    public float groundDrag = 5f;

    [Header("Dash")]
    public float dashForce = 18f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 1.1f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public float jumpCooldown = 0.5f;
    public float airMultiplier = 0.5f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    bool readyToJump;

    [Header("Ground Check")]
    public float playerHeight = 2f;
    public float crouchHeight = 1.2f;
    public LayerMask whatIsGround;
    bool grounded;
    public Transform orientation;

    [Header("Camera")]
    public Transform cameraHolder;
    public float crouchCameraHeight = 0.5f;
    public float crouchCameraLerpSpeed = 14f;

    [Header("Input")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference reloadAction;
    public InputActionReference sprintAction;
    public InputActionReference crouchAction;
    public InputActionReference dashAction;

    Vector2 moveInput;
    Rigidbody rb;
    CapsuleCollider bodyCapsule;
    float standingHeight;
    float standingCenterY;
    float standingCameraHeight;
    bool crouching;
    bool dashing;
    float dashTimer;
    float dashCooldownTimer;
    Vector3 dashDirection;
    InputAction ownedCrouch;
    InputAction ownedDash;
    bool ownsCrouch;
    bool ownsDash;
    readonly Collider[] overlapHits = new Collider[16];

    public bool IsCrouching => crouching;
    public bool IsSprinting { get; private set; }
    public bool IsDashing => dashing;
    public bool IsGrounded => grounded;

    public float HorizontalSpeed
    {
        get
        {
            if (rb == null)
                return 0f;
            Vector3 flat = rb.linearVelocity;
            flat.y = 0f;
            return flat.magnitude;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCapsule = GetComponent<CapsuleCollider>();
        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();

        if (bodyCapsule != null)
        {
            standingHeight = bodyCapsule.height;
            standingCenterY = bodyCapsule.center.y;
        }

        if (cameraHolder == null)
        {
            Transform found = transform.Find("CamHolder");
            if (found != null)
                cameraHolder = found;
        }

        if (cameraHolder != null)
            standingCameraHeight = cameraHolder.localPosition.y;
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
        if (reloadAction != null) reloadAction.action.Enable();
        if (sprintAction != null) sprintAction.action.Enable();

        BindExtraActions();
        if (ResolveCrouch() != null) ResolveCrouch().Enable();
        if (ResolveDash() != null) ResolveDash().Enable();
    }

    void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
        if (reloadAction != null) reloadAction.action.Disable();
        if (sprintAction != null) sprintAction.action.Disable();

        InputAction crouch = ResolveCrouch();
        InputAction dash = ResolveDash();
        if (crouch != null && ownsCrouch)
        {
            crouch.Disable();
            crouch.Dispose();
            ownedCrouch = null;
            ownsCrouch = false;
        }
        else if (crouch != null)
            crouch.Disable();

        if (dash != null && ownsDash)
        {
            dash.Disable();
            dash.Dispose();
            ownedDash = null;
            ownsDash = false;
        }
        else if (dash != null)
            dash.Disable();
    }

    void BindExtraActions()
    {
        if (crouchAction != null && crouchAction.action != null)
        {
            ownedCrouch = null;
            ownsCrouch = false;
        }
        else if (ownedCrouch == null)
        {
            ownedCrouch = new InputAction("Crouch", InputActionType.Button);
            ownedCrouch.AddBinding("<Keyboard>/c");
            ownedCrouch.AddBinding("<Keyboard>/leftCtrl");
            ownedCrouch.AddBinding("<Gamepad>/buttonEast");
            ownsCrouch = true;
        }

        if (dashAction != null && dashAction.action != null)
        {
            ownedDash = null;
            ownsDash = false;
        }
        else if (ownedDash == null)
        {
            ownedDash = new InputAction("Dash", InputActionType.Button);
            ownedDash.AddBinding("<Keyboard>/leftAlt");
            ownedDash.AddBinding("<Keyboard>/q");
            ownedDash.AddBinding("<Gamepad>/buttonNorth");
            ownsDash = true;
        }
    }

    InputAction ResolveCrouch()
    {
        if (crouchAction != null && crouchAction.action != null)
            return crouchAction.action;
        return ownedCrouch;
    }

    InputAction ResolveDash()
    {
        if (dashAction != null && dashAction.action != null)
            return dashAction.action;
        return ownedDash;
    }

    void Start()
    {
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        if (bodyCapsule != null)
        {
            if (bodyCapsule.radius > 0.35f)
                bodyCapsule.radius = 0.3f;

            PhysicsMaterial slip = new PhysicsMaterial("PlayerSlip")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            bodyCapsule.sharedMaterial = slip;
        }

        readyToJump = true;
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        grounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            GroundProbeLength(),
            whatIsGround,
            QueryTriggerInteraction.Ignore);

        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();

        InputAction crouch = ResolveCrouch();
        InputAction dash = ResolveDash();

        bool wantsCrouch = crouch != null && crouch.IsPressed();
        SetCrouching(wantsCrouch && grounded && !dashing);

        IsSprinting = !crouching && !dashing && sprintAction != null && sprintAction.action.IsPressed() && moveInput.sqrMagnitude > 0.01f;

        if (crouching)
            moveSpeed = crouchSpeed;
        else if (IsSprinting)
            moveSpeed = sprintSpeed;
        else
            moveSpeed = walkSpeed;

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame() && readyToJump && grounded && !crouching)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        dashCooldownTimer -= Time.deltaTime;
        if (dash != null && dash.WasPressedThisFrame())
            TryDash();

        if (dashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                dashing = false;
        }

        if (rb == null) return;

        rb.linearDamping = grounded ? groundDrag : 0f;
        SpeedControl();
    }

    void LateUpdate()
    {
        if (cameraHolder == null || Time.timeScale <= 0f)
            return;

        float targetY = crouching ? crouchCameraHeight : standingCameraHeight;
        Vector3 localPos = cameraHolder.localPosition;
        float t = 1f - Mathf.Exp(-crouchCameraLerpSpeed * Time.deltaTime);
        localPos.y = Mathf.Lerp(localPos.y, targetY, t);
        cameraHolder.localPosition = localPos;
    }

    void FixedUpdate()
    {
        if (Time.timeScale <= 0f)
            return;

        if (dashing)
        {
            Vector3 vel = dashDirection * dashForce;
            vel.y = rb.linearVelocity.y;
            rb.linearVelocity = vel;
            return;
        }

        MovePlayer();
        ApplyExtraGravity();
        UnstickFromOverlaps();
    }

    void TryDash()
    {
        if (!grounded || dashing || dashCooldownTimer > 0f || crouching)
            return;

        Vector3 dir = orientation != null
            ? orientation.forward * moveInput.y + orientation.right * moveInput.x
            : transform.forward;

        if (dir.sqrMagnitude < 0.01f)
            dir = orientation != null ? orientation.forward : transform.forward;

        dashDirection = dir.normalized;
        dashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up, "WHOOSH!");
        AudioManager.Dash();
        CombatStimulus.EmitNoise(transform.position, 8f, StimulusType.Footstep);
    }

    void SetCrouching(bool value)
    {
        if (crouching == value)
            return;

        if (!value && !CanStandUp())
            return;

        crouching = value;
        ApplyCapsuleStance();
    }

    void ApplyCapsuleStance()
    {
        if (bodyCapsule == null)
            return;

        if (crouching)
        {
            float standH = standingHeight > 0.1f ? standingHeight : playerHeight;
            bodyCapsule.height = crouchHeight;
            float centerY = standingCenterY - (standH - crouchHeight) * 0.5f;
            bodyCapsule.center = new Vector3(bodyCapsule.center.x, centerY, bodyCapsule.center.z);
        }
        else
        {
            bodyCapsule.height = standingHeight > 0.1f ? standingHeight : playerHeight;
            bodyCapsule.center = new Vector3(bodyCapsule.center.x, standingCenterY, bodyCapsule.center.z);
        }
    }

    float GroundProbeLength()
    {
        if (bodyCapsule == null)
            return playerHeight * 0.5f + 0.2f;

        Vector3 localBottom = bodyCapsule.center + Vector3.down * (bodyCapsule.height * 0.5f);
        Vector3 worldBottom = bodyCapsule.transform.TransformPoint(localBottom);
        float toFeet = Mathf.Abs(transform.position.y - worldBottom.y);
        return toFeet + 0.2f;
    }

    bool CanStandUp()
    {
        if (bodyCapsule == null)
            return true;

        float standH = standingHeight > 0.1f ? standingHeight : playerHeight;
        Vector3 localCenter = new Vector3(bodyCapsule.center.x, standingCenterY, bodyCapsule.center.z);
        Transform capT = bodyCapsule.transform;
        Vector3 worldCenter = capT.TransformPoint(localCenter);

        float halfHeight = capT.TransformVector(Vector3.up * (standH * 0.5f)).magnitude;
        float worldRadius = capT.TransformVector(Vector3.right * bodyCapsule.radius).magnitude;
        float shaft = Mathf.Max(0f, halfHeight - worldRadius);
        Vector3 p1 = worldCenter + capT.up * shaft;
        Vector3 p2 = worldCenter - capT.up * shaft;
        float checkRadius = worldRadius * 0.92f;

        int count = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            checkRadius,
            overlapHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || hit == bodyCapsule)
                continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            return false;
        }

        return true;
    }

    void MovePlayer()
    {
        if (rb == null || orientation == null) return;

        Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

        if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        else
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
    }

    void UnstickFromOverlaps()
    {
        if (rb == null || bodyCapsule == null || dashing)
            return;

        Vector3 flatVel = rb.linearVelocity;
        flatVel.y = 0f;
        if (flatVel.magnitude > moveSpeed * 0.85f)
            return;

        Transform capT = bodyCapsule.transform;
        Vector3 worldCenter = capT.TransformPoint(bodyCapsule.center);
        float halfHeight = capT.TransformVector(Vector3.up * (bodyCapsule.height * 0.5f)).magnitude;
        float worldRadius = capT.TransformVector(Vector3.right * bodyCapsule.radius).magnitude;
        float shaft = Mathf.Max(0f, halfHeight - worldRadius);
        Vector3 p1 = worldCenter + capT.up * shaft;
        Vector3 p2 = worldCenter - capT.up * shaft;

        int count = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            worldRadius,
            overlapHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || hit == bodyCapsule || hit.isTrigger)
                continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (Physics.ComputePenetration(
                bodyCapsule,
                capT.position,
                capT.rotation,
                hit,
                hit.transform.position,
                hit.transform.rotation,
                out Vector3 dir,
                out float dist))
            {
                push += dir * dist;
            }
        }

        push.y = 0f;
        if (push.sqrMagnitude > 0.0001f)
            rb.MovePosition(rb.position + push);
    }

    void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void ApplyExtraGravity()
    {
        if (rb == null || grounded) return;

        if (rb.linearVelocity.y < 0f)
            rb.AddForce(Vector3.up * Physics.gravity.y * (fallMultiplier - 1f), ForceMode.Acceleration);
        else if (rb.linearVelocity.y > 0f && (jumpAction == null || !jumpAction.action.IsPressed()))
            rb.AddForce(Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
    }

    void ResetJump()
    {
        readyToJump = true;
    }

    void SpeedControl()
    {
        if (dashing)
            return;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }
}
