using UnityEngine;

// Defines a waypoint path for cars to follow.
// Uses simple linear interpolation between waypoints — no spline math,
// no looping, no overshoot. Just place one waypoint at each road corner.

public class CarPath : MonoBehaviour
{
    [Tooltip("Ordered waypoints the car will follow. One per corner is enough.")]
    public Transform[] waypoints;

    [Tooltip("How much to round off corners (0 = sharp 90 degree turn, 0.3 = gentle curve).")]
    [Range(0f, 0.49f)]
    public float cornerSmoothing = 0.2f;

    private float[] segmentLengths;
    private float totalLength;

    void Awake()
    {
        BuildCache();
    }

    void BuildCache()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        segmentLengths = new float[waypoints.Length - 1];
        totalLength = 0f;
        for (int i = 0; i < segmentLengths.Length; i++)
        {
            segmentLengths[i] = Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            totalLength += segmentLengths[i];
        }
    }

    
    // Returns a world-space position on the path. t ranges from 0 to 1.

    public Vector3 GetPointAtTime(float t)
    {
        if (waypoints == null || waypoints.Length < 2)
            return transform.position;

        if (segmentLengths == null) BuildCache();

        t = Mathf.Clamp01(t);

        float distanceAlong = t * totalLength;
        float accumulated = 0f;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float segLen = segmentLengths[i];
            if (distanceAlong <= accumulated + segLen || i == segmentLengths.Length - 1)
            {
                float localT = (distanceAlong - accumulated) / Mathf.Max(0.001f, segLen);
                localT = Mathf.Clamp01(localT);

                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[i + 1].position;

                if (cornerSmoothing <= 0f)
                    return Vector3.Lerp(a, b, localT);

                // Smooth the corner using a cubic bezier
                Vector3 inDir = (i > 0)
                    ? (b - waypoints[i - 1].position).normalized
                    : (b - a).normalized;
                Vector3 outDir = (i < waypoints.Length - 2)
                    ? (waypoints[i + 2].position - a).normalized
                    : (b - a).normalized;

                float s = cornerSmoothing * segLen;
                return CubicBezier(a, a + inDir * s, b - outDir * s, b, localT);
            }
            accumulated += segLen;
        }

        return waypoints[waypoints.Length - 1].position;
    }

  
    // Returns the forward direction on the path at time t.

    public Vector3 GetDirectionAtTime(float t)
    {
        float delta = 0.005f;
        Vector3 a = GetPointAtTime(Mathf.Max(0f, t - delta));
        Vector3 b = GetPointAtTime(Mathf.Min(1f, t + delta));
        Vector3 dir = b - a;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }
    

    // Total world-space length of the path.
  
    public float GetApproximateLength()
    {
        if (segmentLengths == null) BuildCache();
        return totalLength > 0f ? totalLength : 1f;
    }

    static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0
             + 3f * u * u * t * p1
             + 3f * u * t * t * p2
             + t * t * t * p3;
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        // Rebuild cache in editor
        BuildCache();

        Gizmos.color = Color.yellow;
        int steps = 60 * (waypoints.Length - 1);
        Vector3 prev = GetPointAtTime(0f);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 curr = GetPointAtTime((float)i / steps);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }

        Gizmos.color = new Color(1f, 0.6f, 0f);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.5f);
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
}
