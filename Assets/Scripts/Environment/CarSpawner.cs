using UnityEngine;

/// <summary>
/// Continuously spawns cars that drive along a CarPath and self-destruct at the end.
///
/// SETUP:
/// 1. Create an empty GameObject named "CarSpawner"
/// 2. Attach this script
/// 3. Assign your car prefab(s) to "carPrefabs" — if multiple, a random one is picked each time
/// 4. Assign the CarPath the cars should follow
/// 5. Tweak spawn interval and speed range
///
/// Cars are spawned at the first waypoint of the path and drive to the end.
/// </summary>
public class CarSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("One or more car prefabs. A random one is chosen each spawn.")]
    public GameObject[] carPrefabs;

    [Header("Path")]
    public CarPath path;

    [Header("Timing")]
    [Tooltip("Seconds between each car spawn")]
    public float spawnInterval = 4f;

    [Tooltip("Random variation on spawn interval (±)")]
    public float spawnVariance = 1f;

    [Header("Speed")]
    [Tooltip("Min speed for spawned cars")]
    public float minSpeed = 6f;

    [Tooltip("Max speed for spawned cars")]
    public float maxSpeed = 12f;

    [Header("Limits")]
    [Tooltip("Max cars alive at once (0 = unlimited)")]
    public int maxCars = 5;

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
        if (carPrefabs == null || carPrefabs.Length == 0 || path == null) return;

        // Check car limit
        if (maxCars > 0)
        {
            // Count active CarFollowers on this path
            CarFollower[] followers = FindObjectsByType<CarFollower>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var f in followers)
            {
                if (f.path == path) count++;
            }
            if (count >= maxCars) return;
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
    }
}
