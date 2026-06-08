using UnityEngine;

// First-Person Player Controller
// Handles WASD movement, mouse look, and jumping.

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;
    public float gravity = -15f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("References")]
    public Transform cameraHolder; // Assign the Camera (child of Player)

    private CharacterController characterController;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isGrounded;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // Lock and hide the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Freeze all player input while the pause menu is open
        if (PopupController.IsPaused) return;

        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical rotation (pitch) — applied to the camera
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (yaw) — applied to the player body
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        // Ground check
        isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }

        // WASD input
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        // Clamp to magnitude 1 so diagonal movement isn't ~41 % faster than
        // straight movement (raw vector length would be √2 ≈ 1.41 otherwise).
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Sprint
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        characterController.Move(move * currentSpeed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
}
