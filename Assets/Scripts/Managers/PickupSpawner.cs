using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// Randomly places Ammo, Love Bomb, and Health pickups across the city at game start,
// distributed among named zones.  Each collected pickup respawns independently
// after <see cref="respawnDelay"/> seconds.

public class PickupSpawner : MonoBehaviour
{
    // ── Pickup Prefabs ────────────────────────────────────────────────────────

    [Header("Pickup Prefabs")]
    [Tooltip("Ammo pickup prefab.")]
    public GameObject ammoPrefab;

    [Tooltip("Love Bomb pickup prefab.")]
    public GameObject loveBombPrefab;

    [Tooltip("Health pickup prefab.")]
    public GameObject healthPrefab;

    // ── Counts ────────────────────────────────────────────────────────────────

    [Header("Counts (total across all zones)")]
    public int ammoCount     = 12;
    public int loveBombCount =  6;
    public int healthCount   =  6;

    // ── Zones ─────────────────────────────────────────────────────────────────

    [Header("Spawn Zones")]
    [Tooltip("Each zone is an empty GameObject placed in the city.  Pickups spawn " +
             "at random NavMesh positions within each zone's radius.  Pickups are " +
             "distributed evenly across all zones.")]
    public PickupZoneConfig[] zones;

    // ── Respawn ───────────────────────────────────────────────────────────────

    [Header("Respawn")]
    [Tooltip("Seconds after a pickup is collected before it reappears at the same spot.")]
    public float respawnDelay = 30f;

    // ── Placement Tuning ──────────────────────────────────────────────────────

    [Header("Placement")]
    [Tooltip("Search radius used by NavMesh.SamplePosition around each random candidate. " +
             "Increase if pickups fail to spawn (NavMesh is coarse).")]
    public float navMeshSampleRadius = 4f;

    [Tooltip("How far above the NavMesh surface to position each pickup so it sits " +
             "visibly on the ground rather than clipping through it.")]
    public float heightOffset = 0.15f;

    [Tooltip("Max random attempts per pickup slot before giving up on that slot.")]
    public int maxAttempts = 40;

    [Tooltip("Optional parent Transform to keep spawned pickups tidy in the hierarchy. " +
             "Leave empty to spawn at scene root.")]
    public Transform pickupParent;

    // ── Internals ─────────────────────────────────────────────────────────────

    // Tracks one pickup slot: its fixed world position, prefab, and live instance.
    private class PickupSlot
    {
        public Vector3     position;
        public GameObject  prefab;
        public GameObject  instance;   // null when collected (destroyed)
    }

    private readonly List<PickupSlot> slots = new List<PickupSlot>();

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (zones == null || zones.Length == 0)
        {
            Debug.LogWarning("[PickupSpawner] No zones assigned — add at least one zone in the Inspector.");
            return;
        }

        BuildSlots(ammoPrefab,     ammoCount);
        BuildSlots(loveBombPrefab, loveBombCount);
        BuildSlots(healthPrefab,   healthCount);

        Debug.Log($"[PickupSpawner] Created {slots.Count} pickup slots.");

        // Spawn everything immediately and start per-slot respawn watchers
        foreach (PickupSlot slot in slots)
        {
            slot.instance = Spawn(slot);
            StartCoroutine(WatchAndRespawn(slot));
        }
    }

    // ── Slot building ─────────────────────────────────────────────────────────

    //
    // Generates <paramref name="count"/> pickup slots for the given prefab,
    // spreading them evenly across all configured zones.
  
    void BuildSlots(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        // Distribute count pickups round-robin across zones, so each zone
        // ends up with roughly the same number of each pickup type.
        for (int i = 0; i < count; i++)
        {
            PickupZoneConfig zone = zones[i % zones.Length];
            if (zone.center == null) continue;

            Vector3? pos = SampleNavMeshPoint(zone.center.position, zone.radius);
            if (pos.HasValue)
            {
                slots.Add(new PickupSlot
                {
                    position = pos.Value,
                    prefab   = prefab,
                    instance = null
                });
            }
            else
            {
                Debug.LogWarning($"[PickupSpawner] Could not find a valid NavMesh point " +
                                 $"in zone '{zone.center.name}' (radius {zone.radius} m) " +
                                 $"after {maxAttempts} attempts.  Is the NavMesh baked there?");
            }
        }
    }

    // ── Respawn coroutine ─────────────────────────────────────────────────────

    // Continuously watches a single slot.  The moment its instance is collected
    // (destroyed → null), it waits <see cref="respawnDelay"/> seconds then
    // respawns the pickup at exactly the same position.
    
    IEnumerator WatchAndRespawn(PickupSlot slot)
    {
        while (true)
        {
            // Wait until the pickup has been collected
            yield return new WaitUntil(() => slot.instance == null);

            // Delay before respawning
            yield return new WaitForSeconds(respawnDelay);

            slot.instance = Spawn(slot);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    //Instantiates the slot's prefab at its stored position.
    GameObject Spawn(PickupSlot slot)
    {
        return Instantiate(slot.prefab, slot.position,
                           Quaternion.identity, pickupParent);
    }

  
    // Picks a random horizontal point within <paramref name="radius"/> of
    // <paramref name="center"/> and snaps it to the nearest NavMesh surface.
    // Returns null if no valid point is found within <see cref="maxAttempts"/>.
  
    Vector3? SampleNavMeshPoint(Vector3 center, float radius)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 r2D       = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(r2D.x, 5f, r2D.y);
            // Cast downward 20 m first so the sample works even if the center
            // is placed above rooftops or floating terrain.
            Vector3 groundCandidate = candidate;
            if (Physics.Raycast(candidate + Vector3.up * 10f, Vector3.down, out RaycastHit rayHit, 30f))
                groundCandidate.y = rayHit.point.y;

            if (NavMesh.SamplePosition(groundCandidate, out NavMeshHit navHit,
                                       navMeshSampleRadius, NavMesh.AllAreas))
            {
                return navHit.position + Vector3.up * heightOffset;
            }
        }
        return null;
    }

    // ── Editor gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (zones == null) return;

        foreach (PickupZoneConfig zone in zones)
        {
            if (zone.center == null) continue;

            // Draw the zone radius ring
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.DrawSphere(zone.center.position, zone.radius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);
            DrawWireCircle(zone.center.position, zone.radius);
        }

        // Draw already-computed slot positions
        Gizmos.color = Color.yellow;
        foreach (PickupSlot slot in slots)
            Gizmos.DrawSphere(slot.position, 0.3f);
    }

    static void DrawWireCircle(Vector3 center, float radius, int segments = 32)
    {
        float step = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float rad = i * step * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}


// Defines one pickup spawn zone: a center Transform and a scatter radius.

[System.Serializable]
public class PickupZoneConfig
{
    [Tooltip("Empty GameObject placed somewhere in the city.  Pickups spawn " +
             "at random NavMesh positions within Radius metres of this point.")]
    public Transform center;

    [Tooltip("Max scatter radius around the zone centre (metres).")]
    public float radius = 15f;
}
