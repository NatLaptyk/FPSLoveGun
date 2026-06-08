using UnityEngine;

// Continuously spawns cars that drive along a CarPath and self-destruct at the end.

public class CarSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("One or more car prefabs. A random one is chosen each spawn.")]
    [SerializeField] private GameObject[] carPrefabs;

    [Header("Path")]
    [SerializeField] private CarPath path;

    [Header("Timing")]
    [Tooltip("Seconds between each car spawn")]
    [SerializeField] private float spawnInterval = 4f;

    [Tooltip("Random variation on spawn interval (±)")]
    [SerializeField] private float spawnVariance = 1f;

    [Header("Speed")]
    [Tooltip("Min speed for spawned cars")]
    [SerializeField] private float minSpeed = 6f;

    [Tooltip("Max speed for spawned cars")]
    [SerializeField] private float maxSpeed = 12f;

    [Header("Limits")]
    [Tooltip("Max cars alive at once (0 = unlimited)")]
    [SerializeField] private int maxCars = 5;

    private float nextSpawnTime;
    private int activeCars = 0;

    void Start()
    {
        ScheduleNext();
    }

    void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnCar();
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        float variance = Random.Range(-spawnVariance, spawnVariance);
        nextSpawnTime = Time.time + Mathf.Max(0.5f, spawnInterval + variance);
    }

    void SpawnCar()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
        {
            Debug.LogWarning("[CarSpawner] No car prefabs assigned!");
            return;
        }
        if (path == null)
        {
            Debug.LogWarning("[CarSpawner] No CarPath assigned!");
            return;
        }
        if (path.waypoints == null || path.waypoints.Length < 2)
        {
            Debug.LogWarning("[CarSpawner] CarPath needs at least 2 waypoints!");
            return;
        }

        // Check car limit
        if (maxCars > 0)
        {
            CarFollower[] followers = FindObjectsByType<CarFollower>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var f in followers)
            {
                if (f.path == path) count++;
            }
            if (count >= maxCars)
            {
                Debug.Log($"[CarSpawner] Max cars ({maxCars}) reached, skipping spawn.");
                return;
            }
        }

        // Pick a random prefab
        GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];

        // Spawn at path start
        Vector3 startPos = path.GetPointAtTime(0f);
        Vector3 startDir = path.GetDirectionAtTime(0f);
        Quaternion startRot = startDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(startDir)
            : Quaternion.identity;

        GameObject car = Instantiate(prefab, startPos, startRot);

        // Assign path and random speed
        CarFollower follower = car.GetComponent<CarFollower>();
        if (follower == null)
            follower = car.AddComponent<CarFollower>();

        follower.path = path;
        follower.speed = Random.Range(minSpeed, maxSpeed);
        Debug.Log($"[CarSpawner] Spawned {prefab.name} at {startPos}, speed={follower.speed:F1}");
    }
}
