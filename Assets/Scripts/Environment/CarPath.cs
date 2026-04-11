using UnityEngine;

/// <summary>
/// Defines a smooth Catmull-Rom spline path for cars to follow.
///
/// SETUP:
/// 1. Create an empty GameObject named "CarPath"
/// 2. Add this script
/// 3. Create child empty GameObjects as waypoints along the road
/// 4. Drag them into the "waypoints" array (order matters!)
/// 5. The yellow spline appears in Scene view when selected
///
/// Cars interpolate smoothly between waypoints — no sharp corners.
/// </summary>
public class CarPath : MonoBehaviour
{
    [Tooltip("Ordered waypoints the car will follow. Place these along the road.")]
    public Transform[] waypoints;

    [Tooltip("If true, the path loops back to the first waypoint")]
    public bool loop = false;

    /// <summary>
    /// Returns a world-space position on the spline.
    /// t ranges from 0 (start) to 1 (end).
    /// </summary>
    public Vector3 GetPointAtTime(float t)
    {
        if (waypoints == null || waypoints.Length < 2)
            return transform.position;

        // Map t to a segment index + local t
        float totalSegments = loop ? waypoints.Length : waypoints.Length - 1;
        float scaledT = Mathf.Clamp01(t) * totalSegments;
        int segment = Mathf.FloorToInt(scaledT);
        float localT = scaledT - segment;

        // Clamp segment for non-looping paths
        if (!loop && segment >= waypoints.Length - 1)
        {
            segment = waypoints.Length - 2;
            localT = 1f;
        }

        // Get four control points for Catmull-Rom
        Vector3 p0 = GetWaypointPosition(segment - 1);
        Vector3 p1 = GetWaypointPosition(segment);
        Vector3 p2 = GetWaypointPosition(segment + 1);
        Vector3 p3 = GetWaypointPosition(segment + 2);

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    /// <summary>
    /// Returns the forward direction on the spline at time t.
    /// </summary>
    public Vector3 GetDirectionAtTime(float t)
    {
        float delta = 0.001f;
        Vector3 a = GetPointAtTime(t - delta);
        Vector3 b = GetPointAtTime(t + delta);
        return (b - a).normalized;
    }

    /// <summary>
    /// Approximate total length of the spline (for speed normalization).
    /// </summary>
    public float GetApproximateLength(int samples = 50)
    {
        float length = 0f;
        Vector3 prev = GetPointAtTime(0f);
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 curr = GetPointAtTime(t);
            length += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return length;
    }

    Vector3 GetWaypointPosition(int index)
    {
        if (loop)
        {
            index = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
        }
        else
        {
            index = Mathf.Clamp(index, 0, waypoints.Length - 1);
        }
        return waypoints[index].position;
    }

    /// <summary>
    /// Catmull-Rom spline interpolation between p1 and p2.
    /// </summary>
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // Draw the spline in Scene view
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        Vector3 prev = GetPointAtTime(0f);

        for (int i = 1; i <= 100; i++)
        {
            float t = (float)i / 100f;
            Vector3 curr = GetPointAtTime(t);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }

        // Draw waypoint spheres
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        foreach (var wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawWireSphere(wp.position, 0.4f);
        }
    }
}
