using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float groundDrag = 5f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public float jumpCooldown = 0.5f;
    public float airMultiplier = 0.5f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    bool readyToJump;

    [Header("Ground Check")]
    public float playerHeight = 2f;
    public LayerMask whatIsGround;
    bool grounded;
    public Transform orientation;

    [Header("Input")]
    // Assign an InputAction (Value - Vector2) in the Inspector (InputActionReference)
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference reloadAction;
    public InputActionReference sprintAction;

    Vector2 moveInput;
    Rigidbody rb;

    // Cache the rigidbody for movement and jumping.
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Enable input actions when this component activates.
    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
        if (reloadAction != null) reloadAction.action.Enable();
        if (sprintAction != null) sprintAction.action.Enable();

    }

    // Disable input actions when this component deactivates.
    void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
        if (reloadAction != null) reloadAction.action.Disable();
        if (sprintAction != null) sprintAction.action.Disable();

    }

    // Prevent rigidbody rotation at startup.
    void Start()
    {
        if (rb != null) rb.freezeRotation = true;
        readyToJump = true;
    }

    // Read input, check ground, and update movement state.
    void Update()
    {
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame() && readyToJump && grounded){
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
            
        }
        if (sprintAction != null && sprintAction.action.IsPressed())
        {
            moveSpeed = 7f; // Sprint speed
        }
        else
        {
            moveSpeed = 5f; // Normal speed
        }
        
        if (rb == null) return;

        rb.linearDamping = grounded ? groundDrag : 0f;
        SpeedControl();
        
    }

    // Apply movement forces every physics step.
    void FixedUpdate()
    {
        MovePlayer();
        ApplyExtraGravity();
        
    }

    // Move the player using camera-relative input.
    void MovePlayer()
    {
        if (rb == null || orientation == null) return;

        Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

        if(grounded){
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        //The player is in the Air
        else if(!grounded){
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }
        
    }

    // Launch the player upward from the ground.
    void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // Apply stronger gravity for tighter jump arcs.
    void ApplyExtraGravity()
    {
        if (rb == null || grounded) return;

        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y > 0f && (jumpAction == null || !jumpAction.action.IsPressed()))
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // Reallow jumping after the cooldown ends.
    private void ResetJump()
    {
        readyToJump = true;
    }

    // Clamp horizontal velocity to the movement speed.
    private void SpeedControl(){
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if(flatVel.magnitude > moveSpeed){
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }
}
