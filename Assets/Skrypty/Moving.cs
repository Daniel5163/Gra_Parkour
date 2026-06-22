using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class Moving : MonoBehaviour
{
    [Header("Ruch")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float crouchSpeedMultiplier = 0.5f;
    public float jumpForce = 5f;
    public Animator animator;
    public float rotationSpeed = 1080f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrain = 20f;
    public float staminaRegen = 15f;
    public float staminaRegenDelay = 1.2f;

    [Header("UI")]
    public Slider staminaSlider;
    public Image staminaFill;
    public TextMeshProUGUI speedText;

    [Header("Energia Sztuczek")]
    public float maxTrickEnergy = 100f;
    public float trickEnergyRegen = 8f;
    public float trickEnergyRegenDelay = 2f;
    public float rollEnergyCost = 25f;
    public float backflipEnergyCost = 40f;
    public Slider trickEnergySlider;
    public Image trickEnergyFill;

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
    public float rollCooldown = 1.5f;

    [Header("System Punktacji")]
    public Scoring scoringSystem;
    public event System.Action<string> OnTrickPerformed;

    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    private float currentStamina;
    private float timeSinceLastRun;
    private float currentTrickEnergy;
    private float timeSinceLastTrick = 999f;

    private float moveX;
    private float moveZ;

    private float xRotation;
    private float yRotation;

    private bool isGrounded;
    public bool isCrouching;
    private float lastDebugTime = 0f;

    private bool usedDoubleJump = false;
    private bool isFlipJump = false;
    private float jumpCooldown = 0f;
    private const float JUMP_COOLDOWN_DURATION = 0.15f;

    private bool isEdgeClimbing = false;
    private bool isRolling = false;
    private float rollTimer = 0f;
    private bool rollTrickFired = false;
    private float rollCooldownTimer = 0f;

    private Quaternion targetRotation;
    private float currentTargetAngle = 0f;
    private bool isRotating = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (capsuleCollider != null)
        {
            originalColliderHeight = capsuleCollider.height;
            originalColliderCenter = capsuleCollider.center;
        }

        rb.freezeRotation = true;
       

        currentStamina = maxStamina;
        currentTrickEnergy = maxTrickEnergy;

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
        if (trickEnergySlider != null)
        {
            trickEnergySlider.maxValue = maxTrickEnergy;
            trickEnergySlider.value = currentTrickEnergy;
        }

        UpdateStaminaUI();
        UpdateTrickEnergyUI();

        if (scoringSystem == null)
            scoringSystem = GetComponent<Scoring>();

        OnTrickPerformed += (trick) => scoringSystem?.RegisterTrick(trick);

        targetRotation = transform.rotation;
        currentTargetAngle = transform.eulerAngles.y;

        Debug.Log("Moving: Start - wszystko gotowe");
    }

    void Update()
    {
        Debug.Log($"moveX={moveX}, moveZ={moveZ}, velocity={rb.velocity}");
        KeyCode forwardKey = KeyRebindSystem.GetKey("Chód w przód");
        KeyCode backwardKey = KeyRebindSystem.GetKey("Chód w tył");
        KeyCode leftKey = KeyRebindSystem.GetKey("Chód w lewo");
        KeyCode rightKey = KeyRebindSystem.GetKey("Chód w prawo");
        KeyCode runKey = KeyRebindSystem.GetKey("Bieg");
        KeyCode jumpKey = KeyRebindSystem.GetKey("Skok");
        KeyCode crouchKey = KeyRebindSystem.GetKey("Kucnięcie");
        KeyCode rollKey = KeyRebindSystem.GetKey("Roll");
        KeyCode climbKey = KeyRebindSystem.GetKey("Wspinaczka");

        bool wantsForward = Input.GetKey(forwardKey);
        bool wantsBackward = Input.GetKey(backwardKey);
        bool wantsLeft = Input.GetKey(leftKey);
        bool wantsRight = Input.GetKey(rightKey);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = Vector3.zero;
        if (wantsForward) moveDirection += forward;
        if (wantsBackward) moveDirection -= forward;
        if (wantsRight) moveDirection += right;
        if (wantsLeft) moveDirection -= right;

        if (moveDirection.magnitude > 1f)
            moveDirection.Normalize();

        moveX = wantsRight ? 1 : (wantsLeft ? -1 : 0);
        moveZ = wantsForward ? 1 : (wantsBackward ? -1 : 0);

        if (rollCooldownTimer > 0f)
            rollCooldownTimer -= Time.deltaTime;

        timeSinceLastTrick += Time.deltaTime;
        if (isGrounded && timeSinceLastTrick >= trickEnergyRegenDelay)
        {
            currentTrickEnergy += trickEnergyRegen * Time.deltaTime;
            currentTrickEnergy = Mathf.Clamp(currentTrickEnergy, 0f, maxTrickEnergy);
            UpdateTrickEnergyUI();
        }

        RotateCamera();
        HandleCrouch(crouchKey);
        Jump(jumpKey);
        HandleStamina(runKey);
        UpdateSpeedUI();
        HandleAnimations(runKey, rollKey, climbKey);

        if (Time.time - lastDebugTime > 1f)
        {
            Debug.Log($"Moving: isGrounded={isGrounded}, usedDoubleJump={usedDoubleJump}, isRolling={isRolling}, trickEnergy={currentTrickEnergy:F1}");
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
            RotateCharacter();
        }
    }

    void RotateCharacter()
    {
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
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

        bool moving = moveX != 0 || moveZ != 0;
        bool wantsToRun = Input.GetKey(KeyRebindSystem.GetKey("Bieg")) && moving;
        bool canRun = currentStamina > 2f;
        bool isRunning = wantsToRun && canRun && !isCrouching;

        float currentSpeed = isCrouching ? walkSpeed * crouchSpeedMultiplier :
                             isRunning ? runSpeed : walkSpeed;

        if (moving)
        {
            Vector3 forward = Camera.main.transform.forward;
            Vector3 right = Camera.main.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = forward * moveZ + right * moveX;
            if (moveDirection.magnitude > 1f)
                moveDirection.Normalize();

            Vector3 velocity = moveDirection * currentSpeed;
            velocity.y = rb.velocity.y;
            rb.velocity = velocity;
        }
        else
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }
    }

    void Jump(KeyCode jumpKey)
    {
        if (jumpCooldown > 0f)
            jumpCooldown -= Time.deltaTime;

        bool groundCheck = jumpCooldown <= 0f && Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );

        isGrounded = groundCheck;

        if (isGrounded)
        {
            usedDoubleJump = false;
            isFlipJump = false;
            animator.SetBool("IsFlipJump", false);
        }

        if (Input.GetKeyDown(jumpKey) && !isCrouching && !isEdgeClimbing && !isRolling)
        {
            if (isGrounded)
            {
                DoJump(false);
            }
            else if (canDoubleJump && !usedDoubleJump)
            {
                if (currentTrickEnergy >= backflipEnergyCost)
                {
                    DoJump(true);
                }
                else
                {
                    Debug.Log($"Moving: brak energii na backflip ({currentTrickEnergy:F1}/{backflipEnergyCost})");
                }
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
            OnTrickPerformed?.Invoke("backflip");
            UseTrickEnergy(backflipEnergyCost);
        }

        jumpCooldown = JUMP_COOLDOWN_DURATION;
        isGrounded = false;
    }

    void HandleCrouch(KeyCode crouchKey)
    {
        isCrouching = Input.GetKey(crouchKey);
    }

    void HandleStamina(KeyCode runKey)
    {
        bool movingForward = moveZ > 0f;
        bool wantsToRun = Input.GetKey(runKey) && movingForward;

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

    void UseTrickEnergy(float amount)
    {
        currentTrickEnergy -= amount;
        currentTrickEnergy = Mathf.Clamp(currentTrickEnergy, 0f, maxTrickEnergy);
        timeSinceLastTrick = 0f;
        UpdateTrickEnergyUI();
    }

    void RotateCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        yRotation += mouseX;
    }

    void HandleAnimations(KeyCode runKey, KeyCode rollKey, KeyCode climbKey)
    {
        if (isEdgeClimbing) return;

        bool moving = moveX != 0 || moveZ != 0;
        bool movingForward = moveZ > 0f;
        bool wantsToRun = Input.GetKey(runKey) && movingForward;
        bool canRun = currentStamina > 2f;
        bool running = wantsToRun && canRun && !isCrouching;
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

        bool wantsToRoll = Input.GetKeyDown(rollKey);
        if (wantsToRoll && !isRolling && rollCooldownTimer <= 0f && isGrounded && !isCrouching && !isEdgeClimbing)
        {
            if (currentTrickEnergy >= rollEnergyCost)
            {
                StartRoll();
            }
            else
            {
                Debug.Log($"Moving: brak energii na roll ({currentTrickEnergy:F1}/{rollEnergyCost})");
            }
        }

        if (isRolling)
        {
            if (!rollTrickFired)
            {
                rollTrickFired = true;
                OnTrickPerformed?.Invoke("roll");
            }
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0f)
                EndRoll();
        }

        bool wantToClimb = Input.GetKey(climbKey);
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

    void StartRoll()
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollTrickFired = false;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (capsuleCollider != null)
        {
            capsuleCollider.height = originalColliderHeight * 0.5f;
            capsuleCollider.center = new Vector3(
                originalColliderCenter.x,
                originalColliderCenter.y * 0.5f,
                originalColliderCenter.z
            );
        }

        UseTrickEnergy(rollEnergyCost);
        animator.SetBool("IsRoll", true);
        Debug.Log("Roll: start");
    }

    void EndRoll()
    {
        isRolling = false;
        rollTrickFired = false;
        rollCooldownTimer = rollCooldown;

        if (capsuleCollider != null)
        {
            capsuleCollider.height = originalColliderHeight;
            capsuleCollider.center = originalColliderCenter;
        }

        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        animator.SetBool("IsRoll", false);
        Debug.Log("Roll: koniec");
    }

    void StartEdgeClimb()
    {
        if (!isEdgeClimbing)
        {
            OnTrickPerformed?.Invoke("edge_climb");
            StartCoroutine(EdgeClimbCoroutine());
        }
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
            }
        }

        Vector3 targetPos = new Vector3(
            startPos.x + transform.forward.x * forwardDistance,
            targetY,
            startPos.z + transform.forward.z * forwardDistance
        );

        if (targetPos.y - startPos.y < 0.5f)
        {
            Debug.LogWarning("Zbyt niska krawędź, przerywam wspinaczkę");
            rb.isKinematic = false;
            rb.useGravity = true;
            isEdgeClimbing = false;
            yield break;
        }

        float elapsed = 0f;
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

    void UpdateTrickEnergyUI()
    {
        if (trickEnergySlider != null)
            trickEnergySlider.value = currentTrickEnergy;
        if (trickEnergyFill != null)
        {
            float percent = currentTrickEnergy / maxTrickEnergy;
            trickEnergyFill.color = percent > 0.5f ? new Color(0.2f, 0.6f, 1f) :
                                    percent > 0.25f ? new Color(0.8f, 0.5f, 0.1f) :
                                    new Color(0.5f, 0.5f, 0.5f);
        }
    }

    bool IsNearWall()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, 1.5f, groundMask);
    }

    bool CanClimbEdge()
    {
        float wallHeight = 1.8f;
        float overheadHeight = 2.2f;
        bool hitWall = Physics.Raycast(transform.position + Vector3.up * wallHeight, transform.forward, 0.6f, groundMask);
        bool hitAir = !Physics.Raycast(transform.position + Vector3.up * overheadHeight, transform.forward, 0.6f, groundMask);
        return hitWall && hitAir;
    }

    public bool IsGrounded() => isGrounded;
}

public static class SelectedPhotoData
{
    public static Texture2D selectedTexture;
}