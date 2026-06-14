using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Moving : MonoBehaviour
{
    [Header("Ruch")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float crouchSpeedMultiplier = 0.5f;
    public float jumpForce = 5f;
    public Animator animator;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrain = 20f;
    public float staminaRegen = 15f;
    public float staminaRegenDelay = 1.2f;

    [Header("UI")]
    public Slider staminaSlider;
    public Image staminaFill;
    public TextMeshProUGUI speedText;

    [Header("Mysz")]
    public float mouseSensitivity = 200f;
    public Transform cameraTransform;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Double Jump")]
    public bool canDoubleJump = true;
    public float flipForwardForce = 4f;

    [Header("Edge Climb")]
    public float edgeClimbForward = 1.0f;
    public float edgeClimbUp = 1.5f;
    public float edgeClimbDuration = 0.35f;

    [Header("Roll")]
    public float rollSpeed = 7f;
    public float rollDuration = 0.5f;

    [Header("System Punktacji")]
    public Scoring scoringSystem;

    private Rigidbody rb;
    private float currentStamina;
    private float timeSinceLastRun;
    private float moveX;
    private float moveZ;
    private float xRotation;
    private float yRotation;
    private bool isGrounded;
    public bool isCrouching;

    private bool wasGroundedLastFrame = true;
    private float lastDebugTime = 0f;

    private bool usedDoubleJump = false;
    private bool isFlipJump = false;
    private float jumpCooldown = 0f;
    private const float JUMP_COOLDOWN_DURATION = 0.15f;

    private bool isEdgeClimbing = false;

    private bool isRolling = false;
    private float rollTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentStamina = maxStamina;

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
        UpdateStaminaUI();

        if (scoringSystem == null)
            scoringSystem = GetComponent<Scoring>();

        Debug.Log("Moving: Start - wszystko gotowe");
    }

    void Update()
    {
        moveX = Input.GetAxis("Horizontal");
        moveZ = Input.GetAxis("Vertical");

        RotateCamera();
        HandleCrouch();
        Jump();
        HandleStamina();
        UpdateSpeedUI();
        HandleAnimations();
        CheckLanding();

        if (Time.time - lastDebugTime > 1f)
        {
            Debug.Log($"Moving: isGrounded={isGrounded}, usedDoubleJump={usedDoubleJump}");
            lastDebugTime = Time.time;
        }
    }

    void FixedUpdate()
    {
        if (isEdgeClimbing) return;

        bool isClimbing = animator.GetBool("IsClimbing");
        bool isClimbingEdge = animator.GetBool("IsClimbingEdge");

        if (isClimbing || isClimbingEdge)
        {
            rb.useGravity = false;
            if (isClimbing)
                rb.velocity = new Vector3(0, 3.0f, 0);
        }
        else
        {
            rb.useGravity = true;
            Move();
        }
    }

    void Move()
    {
        if (isRolling)
        {
            Vector3 rollVelocity = transform.forward * rollSpeed;
            rollVelocity.y = rb.velocity.y;
            rb.velocity = rollVelocity;
            return;
        }

        bool movingForward = moveZ > 0f;
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && movingForward;
        bool canRun = currentStamina > 2f;
        bool isRunning = wantsToRun && canRun && !isCrouching;

        float currentSpeed = isCrouching ? walkSpeed * crouchSpeedMultiplier :
                             isRunning ? runSpeed : walkSpeed;

        Vector3 moveDirection = transform.forward * moveZ + transform.right * moveX;
        if (moveDirection.magnitude > 1f)
            moveDirection.Normalize();

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.velocity.y;
        rb.velocity = velocity;
    }

    void Jump()
    {
        if (jumpCooldown > 0f)
            jumpCooldown -= Time.deltaTime;

        bool groundCheck = jumpCooldown <= 0f && Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );

        bool wasGroundedBefore = isGrounded;
        isGrounded = groundCheck;

        if (wasGroundedBefore != isGrounded)
            Debug.Log($"Moving: isGrounded zmienił się: {wasGroundedBefore} → {isGrounded}");

        if (isGrounded)
        {
            usedDoubleJump = false;
            isFlipJump = false;
            animator.SetBool("IsFlipJump", false);
        }

        if (Input.GetKeyDown(KeyCode.Space) && !isCrouching && !isEdgeClimbing && !isRolling)
        {
            if (isGrounded)
            {
                Debug.Log("Moving: Skok 1 (zwykły)");
                DoJump(false);
            }
            else if (canDoubleJump && !usedDoubleJump)
            {
                Debug.Log("Moving: Skok 2 (przewrót)!");
                DoJump(true);
            }
            else
            {
                Debug.Log($"Moving: skok zablokowany — isGrounded={isGrounded}, usedDoubleJump={usedDoubleJump}");
            }
        }

        if (isFlipJump && rb.velocity.y < -0.5f)
        {
            isFlipJump = false;
            animator.SetBool("IsFlipJump", false);
        }
    }

    void DoJump(bool flip)
    {
        isFlipJump = flip;
        animator.SetBool("IsFlipJump", flip);

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        if (flip)
        {
            usedDoubleJump = true;
            rb.AddForce(transform.forward * flipForwardForce, ForceMode.Impulse);
        }

        jumpCooldown = JUMP_COOLDOWN_DURATION;
        isGrounded = false;
    }

    void HandleCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl);
    }

    void HandleStamina()
    {
        bool movingForward = moveZ > 0f;
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && movingForward;

        if (wantsToRun && currentStamina > 0f && !isCrouching)
        {
            currentStamina -= staminaDrain * Time.deltaTime;
            timeSinceLastRun = 0f;
        }
        else
        {
            timeSinceLastRun += Time.deltaTime;
            if (timeSinceLastRun >= staminaRegenDelay)
                currentStamina += staminaRegen * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        UpdateStaminaUI();
    }

    void RotateCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        yRotation += mouseX;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    void HandleAnimations()
    {
        if (isEdgeClimbing) return;

        bool moving = moveX != 0 || moveZ != 0;
        bool running = Input.GetKey(KeyCode.LeftShift) && moveZ > 0 && currentStamina > 2f && !isCrouching;
        bool jumping = !isGrounded;

        if (isCrouching)
        {
            animator.SetBool("IsCrouch", true);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsJumping", false);
        }
        else
        {
            animator.SetBool("IsCrouch", false);

            bool walking = moving && !running && isGrounded && !isRolling;
            bool isRunningNow = running && isGrounded && !isRolling;

            animator.SetBool("IsWalking", walking);
            animator.SetBool("IsRunning", isRunningNow);
            animator.SetBool("IsJumping", jumping && !isFlipJump);
        }

        bool wantsToRoll = Input.GetKeyDown(KeyCode.R);

        if (wantsToRoll && !isRolling && isGrounded && !isCrouching && !isEdgeClimbing)
        {
            isRolling = true;
            rollTimer = rollDuration;
            animator.SetBool("IsRoll", true);
        }

        if (isRolling)
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0f)
            {
                isRolling = false;
                animator.SetBool("IsRoll", false);
            }
        }

        bool wantToClimb = Input.GetKey(KeyCode.C);

        if (wantToClimb && CanClimbEdge())
        {
            animator.SetBool("IsClimbingEdge", true);
            animator.SetBool("IsClimbing", false);
            StartEdgeClimb();
        }
        else if (wantToClimb && IsNearWall())
        {
            animator.SetBool("IsClimbingEdge", false);
            animator.SetBool("IsClimbing", true);
        }
        else
        {
            animator.SetBool("IsClimbingEdge", false);
            animator.SetBool("IsClimbing", false);
        }
    }

    void StartEdgeClimb()
    {
        if (!isEdgeClimbing)
            StartCoroutine(EdgeClimbCoroutine());
    }

    IEnumerator EdgeClimbCoroutine()
    {
        isEdgeClimbing = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;

        Vector3 startPos = transform.position;

        RaycastHit wallHit;
        float targetY = startPos.y + edgeClimbUp;
        float forwardDistance = edgeClimbForward;

        if (Physics.Raycast(startPos + Vector3.up * 1.6f, transform.forward, out wallHit, 1f, groundMask))
        {
            forwardDistance = wallHit.distance + 0.2f; 

            Vector3 topCheck = wallHit.point + Vector3.up * 0.1f;
            if (Physics.Raycast(topCheck, Vector3.down, out RaycastHit topHit, 2f, groundMask))
            {
                targetY = topHit.point.y;
                Debug.Log($"Wysokość dachu: {targetY}");
            }
        }

        Vector3 targetPos = new Vector3(
            startPos.x + transform.forward.x * forwardDistance,
            targetY,
            startPos.z + transform.forward.z * forwardDistance
        );

        Debug.Log($"Wspinaczka: {startPos.y:F2} -> {targetPos.y:F2} (różnica: {targetPos.y - startPos.y:F2})");

        if (targetPos.y - startPos.y < 0.5f)
        {
            Debug.LogWarning("Zbyt niska krawędź, przerywam wspinaczkę");
            rb.isKinematic = false;
            rb.useGravity = true;
            isEdgeClimbing = false;
            yield break;
        }

        float elapsed = 0f;
        Vector3 currentPos = startPos;

        while (elapsed < edgeClimbDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / edgeClimbDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.velocity = Vector3.zero;

        animator.SetBool("IsClimbingEdge", false);
        animator.SetBool("IsClimbing", false);

        isEdgeClimbing = false;

        rb.velocity = new Vector3(rb.velocity.x, 2f, rb.velocity.z);
    }

    void UpdateSpeedUI()
    {
        if (speedText == null) return;
        float speed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        speedText.text = "Speed: " + speed.ToString("F1");
    }

    void UpdateStaminaUI()
    {
        if (staminaSlider != null)
            staminaSlider.value = currentStamina;

        if (staminaFill != null)
        {
            float percent = currentStamina / maxStamina;
            staminaFill.color = percent > 0.5f ? Color.green :
                                percent > 0.25f ? Color.yellow : Color.red;
        }
    }

    void CheckLanding()
    {
        if (!wasGroundedLastFrame && isGrounded)
        {
            Debug.Log("Moving: WYKRYTO LĄDOWANIE!");
            if (scoringSystem != null)
                scoringSystem.OnPlayerLanded(transform.position);
            else
                Debug.LogError("Moving: scoringSystem jest NULL!");
        }
        wasGroundedLastFrame = isGrounded;
    }

    bool IsNearWall()
    {
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 1.5f, Color.red, 0.5f);

        bool hit = Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            transform.forward,
            out RaycastHit hitInfo,
            1.5f
        );

        if (hit)
            Debug.Log("Raycast trafił w: " + hitInfo.collider.name +
                      " na warstwie: " + LayerMask.LayerToName(hitInfo.collider.gameObject.layer));

        return Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            transform.forward,
            1.5f,
            groundMask
        );
    }

    bool CanClimbEdge()
    {
        float wallHeight = 1.8f;
        float overheadHeight = 2.2f;

        bool hitWall = Physics.Raycast(
            transform.position + Vector3.up * wallHeight,
            transform.forward,
            0.6f,
            groundMask
        );

        bool hitAir = !Physics.Raycast(
            transform.position + Vector3.up * overheadHeight,
            transform.forward,
            0.6f,
            groundMask
        );

        return hitWall && hitAir;
    }

    public bool IsGrounded() => isGrounded;
}

public static class SelectedPhotoData
{
    public static Texture2D selectedTexture;
}