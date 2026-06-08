using UnityEngine;
using UnityEngine.UI;

// Shows a UI arrow on the edge of the circular minimap pointing toward the
// nearest active Objective marker. Automatically switches to the next objective
// when the current one is hidden (section complete). No manual wiring needed.

public class MinimapDirectionArrow : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    [SerializeField] private MinimapCamera minimapCamera;

    [Header("Minimap UI")]
    [Tooltip("Radius of the circular minimap in pixels. Half the width of your minimap panel.")]
    [SerializeField] private float minimapRadius = 80f;

    [Tooltip("How far inside the edge to place the arrow.")]
    [SerializeField] private float arrowEdgeOffset = 12f;

    [Header("Debug")]
    [Tooltip("Currently tracked objective — set automatically, shown for reference.")]
    [SerializeField] private Transform currentObjective;

    private RectTransform rectTransform;
    private Image arrowImage;
    private float refreshTimer;
    private const float RefreshInterval = 0.5f; // re-scan for objectives every 0.5s

    void Start()
    {
        TryGetComponent(out rectTransform);
        TryGetComponent(out arrowImage);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        FindNearestObjective();
    }

    void LateUpdate()
    {
        // Periodically re-scan in case an objective was hidden or a new one appeared
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= RefreshInterval)
        {
            refreshTimer = 0f;
            FindNearestObjective();
        }

        if (player == null || currentObjective == null || minimapCamera == null)
        {
            if (arrowImage != null) arrowImage.enabled = false;
            return;
        }

        Vector3 toObjective = currentObjective.position - player.position;
        toObjective.y = 0f;

        float worldDistance = toObjective.magnitude;
        float worldViewRadius = minimapCamera.viewSize;

        if (worldDistance < worldViewRadius * 0.85f)
        {
            // Objective is within minimap view — hide edge arrow
            if (arrowImage != null) arrowImage.enabled = false;
            return;
        }

        if (arrowImage != null) arrowImage.enabled = true;

        // World X → UI right, World Z → UI up
        Vector2 uiDir = new Vector2(toObjective.x, toObjective.z).normalized;

        float placementRadius = minimapRadius - arrowEdgeOffset;
        rectTransform.anchoredPosition = uiDir * placementRadius;

        float angle = Mathf.Atan2(uiDir.x, uiDir.y) * Mathf.Rad2Deg;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    /// <summary>
    /// Scans the scene for all active Objective MinimapMarkers and tracks the nearest one.
    /// Called automatically every 0.5s and on Start.
    /// </summary>
    void FindNearestObjective()
    {
        MinimapMarker[] all = FindObjectsByType<MinimapMarker>(FindObjectsSortMode.None);

        MinimapMarker nearest = null;
        float nearestDist = float.MaxValue;

        foreach (MinimapMarker m in all)
        {
            // Only care about active Objective markers
            if (m.markerType != MinimapMarker.MarkerType.Objective) continue;
            if (!m.enabled || !m.gameObject.activeInHierarchy) continue;

            float dist = player != null
                ? Vector3.Distance(m.transform.position, player.position)
                : 0f;

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = m;
            }
        }

        currentObjective = nearest != null ? nearest.transform : null;
    }

    /// <summary>
    /// Manually point to a specific objective. Optional — auto-tracking handles
    /// this automatically, but you can call this from code if you prefer explicit control.
    /// </summary>
    public void SetObjective(Transform target)
    {
        currentObjective = target;
    }
}
