using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyWASDMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;   // meters/second (approx)
    [SerializeField] private float turnSpeed = 12f;  // rotates to face movement direction
    [SerializeField] private bool freezeRotation = true;

    private Rigidbody rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (freezeRotation)
            rb.freezeRotation = true;

        // Optional recommended Rigidbody settings:
        // rb.interpolation = RigidbodyInterpolation.Interpolate;
        // rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal"); // A/D
        moveInput.y = Input.GetAxisRaw("Vertical");   // W/S
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void FixedUpdate()
    {
        // World-relative movement (no camera)
        Vector3 desiredMove = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // Move via Rigidbody velocity (physics-friendly)
        Vector3 currentVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(desiredMove.x * moveSpeed, currentVel.y, desiredMove.z * moveSpeed);

        // Rotate to face movement direction
        if (desiredMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredMove, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
        }
    }
}
