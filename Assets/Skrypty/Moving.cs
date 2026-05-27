using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private Rigidbody rb;
    private float currentStamina;
    private float timeSinceLastRun;
    private float moveX;
    private float moveZ;
    private float xRotation;
    private float yRotation;
    private bool isGrounded;
    public bool isCrouching;

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
    }

    void FixedUpdate()
    {
        Move();
    }

    void Move()
    {
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
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );

        if (Input.GetButtonDown("Jump")  && !isCrouching)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
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
            {
                currentStamina += staminaRegen * Time.deltaTime;
            }
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
        bool moving = moveX != 0 || moveZ != 0;
        bool running = Input.GetKey(KeyCode.LeftShift) && moveZ > 0 && currentStamina > 2f && !isCrouching;
        bool jumping = rb.velocity.y > 0.1f;

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

            bool walking = moving && !running && !jumping;

            animator.SetBool("IsWalking", walking);
            animator.SetBool("IsRunning", running);
            animator.SetBool("IsJumping", jumping);
        }
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
}






public static class SelectedPhotoData
{
    public static Texture2D selectedTexture;
}