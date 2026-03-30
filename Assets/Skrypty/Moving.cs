using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Moving : MonoBehaviour
{
    [Header("Ruch")]
    public float speed = 5f;
    public float jumpForce = 5f;

    [Header("Mysz")]
    public float mouseSensitivity = 200f;
    public Transform cameraTransform;

    private Rigidbody rb;
    private bool isGrounded;

    float xRotation = 0f;
    float yRotation = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Move();
        RotateCamera();
        Jump();
    }

    void Move()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.forward * moveZ + transform.right * moveX;
        Vector3 velocity = move * speed;
        velocity.y = rb.velocity.y;
        rb.velocity = velocity;
    }

    void RotateCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        yRotation += mouseX;
        cameraTransform.parent.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    void Jump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f)
            {
                isGrounded = true;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}