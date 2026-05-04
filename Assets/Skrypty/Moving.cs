using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Moving : MonoBehaviour
{
    [Header("Ruch")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpForce = 5f;

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

    private Rigidbody rb;
    private bool isGrounded;
    private float currentStamina;
    private float xRotation = 0f;
    private float yRotation = 0f;
    private float timeSinceLastRun = 0f;

    private float moveX;
    private float moveZ;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;

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
        Jump();
        HandleStamina();
        UpdateSpeedUI();
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

        bool isRunning = wantsToRun && canRun;

        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        Vector3 moveDirection = transform.forward * moveZ + transform.right * moveX;

        if (moveDirection.magnitude > 0.1f)
            moveDirection.Normalize();

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.velocity.y;           

        rb.velocity = velocity;
    }

    void HandleStamina()
    {
        bool movingForward = moveZ > 0f;
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && movingForward;

        if (wantsToRun && currentStamina > 0f)
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

    void UpdateStaminaUI()
    {
        if (staminaSlider != null)
            staminaSlider.value = currentStamina;

        if (staminaFill != null)
        {
            float percent = currentStamina / maxStamina;

            if (percent > 0.5f)
                staminaFill.color = Color.green;
            else if (percent > 0.25f)
                staminaFill.color = Color.yellow;
            else
                staminaFill.color = Color.red;
        }
    }

    void UpdateSpeedUI()
    {
        if (speedText == null) return;

        float horizontalSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        speedText.text = "Speed: " + horizontalSpeed.ToString("F1");
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

    void Jump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f)   
            {
                isGrounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}