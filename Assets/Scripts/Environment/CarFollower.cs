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
    private float currentT = 0f;
    private float pathLength;
    private float tSpeed;   // how much t changes per second

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

        // Snap to start
        transform.position = path.GetPointAtTime(0f);
        Vector3 dir = path.GetDirectionAtTime(0f);
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    void Update()
    {
        if (path == null) return;

        currentT += tSpeed * Time.deltaTime;

        if (currentT >= 1f)
        {
            // Reached the end — destroy
            Destroy(gameObject);
            return;
        }

        // Move along spline
        Vector3 pos = path.GetPointAtTime(currentT);
        transform.position = pos;

        // Rotate to face road direction
        Vector3 dir = path.GetDirectionAtTime(currentT);
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                Time.deltaTime * rotationSmoothing);
        }
    }
}
