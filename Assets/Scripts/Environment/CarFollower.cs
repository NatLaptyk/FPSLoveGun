using UnityEngine;

/// <summary>
/// Drives a car along a CarPath spline and destroys it at the end.
/// Attach this to your car prefab.
///
/// The car smoothly follows the curve, rotating to face the road direction.
/// Speed is in world units per second (not spline-t), so cars move at
/// a consistent real-world speed regardless of path length.
/// </summary>
public class CarFollower : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Speed in units per second")]
    public float speed = 8f;

    [Tooltip("How quickly the car rotates to match the road direction")]
    public float rotationSmoothing = 10f;

    [Header("Path (set by CarSpawner — leave empty on prefab)")]
    public CarPath path;

    // Internal state
    private float   currentT = 0f;
    private float   pathLength;
    private float   tSpeed;   // how much t changes per second
    private Rigidbody rb;

    void Start()
    {
        if (path == null)
        {
            Debug.LogWarning("[CarFollower] No path assigned, destroying.");
            Destroy(gameObject);
            return;
        }

        pathLength = path.GetApproximateLength();
        if (pathLength < 0.01f)
        {
            Debug.LogWarning("[CarFollower] Path length is near-zero, destroying.");
            Destroy(gameObject);
            return;
        }

        // Convert world-speed to t-speed
        tSpeed = speed / pathLength;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Cars follow a fixed path — lock axes that physics shouldn't control.
            // Collision detection still works; cars just won't be knocked off-road.
            rb.constraints = RigidbodyConstraints.FreezePositionY
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Snap to start
        Vector3 startPos = path.GetPointAtTime(0f);
        Vector3 startDir = path.GetDirectionAtTime(0f);
        if (rb != null)
        {
            rb.position = startPos;
            if (startDir.sqrMagnitude > 0.001f)
                rb.rotation = Quaternion.LookRotation(startDir);
        }
        else
        {
            transform.position = startPos;
            if (startDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(startDir);
        }
    }

    void FixedUpdate()
    {
        if (path == null) return;

        currentT += tSpeed * Time.fixedDeltaTime;

        if (currentT >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 pos = path.GetPointAtTime(currentT);
        Vector3 dir = path.GetDirectionAtTime(currentT);

        if (rb != null)
        {
            // MovePosition/MoveRotation go through the physics engine,
            // so colliders on other cars will interact correctly.
            rb.MovePosition(pos);

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot,
                    Time.fixedDeltaTime * rotationSmoothing));
            }
        }
        else
        {
            // Fallback if no Rigidbody — original behaviour
            transform.position = pos;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                    Time.fixedDeltaTime * rotationSmoothing);
            }
        }
    }
}
