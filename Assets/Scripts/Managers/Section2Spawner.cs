using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Spawns stadium NPCs in waves when the player enters the stadium trigger zone.
/// Waves fire on a timer — the player does NOT need to clear a wave before the
/// next one starts. The intent is to overwhelm the player until health drops to
/// the critical threshold and the cat vision event triggers automatically.
///
/// SETUP:
/// 1. Create an empty GameObject named "Section2Spawner"
/// 2. Add a BoxCollider (Is Trigger = true) sized to cover the stadium entrance
/// 3. Attach this script
/// 4. Create seat spawn points (empty GameObjects) in the stadium seating area
///    and assign them to "seatSpawnPoints" — reused for every wave
/// 5. Create one empty GameObject on the football field named "FieldCenter"
///    and assign it to "fieldTarget"
/// 6. Assign your UnhappyPerson prefabs
/// 7. Optionally assign a SectionTracker — it receives ALL spawned NPCs
/// </summary>
public class Section2Spawner : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("One or more NPC prefabs. A random one is picked for each seat.")]
    public GameObject[] npcPrefabs;

    [Tooltip("Seat positions in the stadium where NPCs spawn. Reused each wave.")]
    public Transform[] seatSpawnPoints;

    [Tooltip("A point on the football field all NPCs walk toward after spawning.")]
    public Transform fieldTarget;

    [Tooltip("Seconds between each NPC spawn within a wave.")]
    public float spawnInterval = 0.5f;

    [Header("Waves")]
    [Tooltip("How many waves to send before the section is complete.")]
    public int totalWaves = 3;

    [Tooltip("Seconds to wait after a wave finishes spawning before the next wave begins. " +
             "Waves do NOT wait to be cleared — they overlap intentionally.")]
    public float timeBetweenWaves = 3f;

    [Header("Crowd Ring")]
    [Tooltip("Starting radius of the crowd ring — wide and distant.")]
    public float crowdRadiusStart = 6f;
    [Tooltip("Final radius the crowd closes in to.")]
    public float crowdRadiusEnd = 2f;
    [Tooltip("Seconds it takes to shrink from start radius to end radius.")]
    public float crowdShrinkDuration = 12f;

    [Header("Section Tracking")]
    [Tooltip("Optional — receives all NPCs after the final wave for section-complete tracking.")]
    public SectionTracker sectionTracker;

    [Header("Exit Blockers")]
    [Tooltip("GameObjects to activate when the player enters (e.g. an invisible wall or gate mesh " +
             "blocking the stadium entrance). They are deactivated once all waves are cleared.")]
    public GameObject[] exitBlockers;

    [Header("Music")]
    [Tooltip("MusicController for the stadium section. Played the moment the player enters.")]
    public MusicController stadiumMusic;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onAllWavesComplete;

    private bool hasTriggered = false;
    private float crowdRadius;
    private UnhappyPerson[] spawnedNPCs;          // current wave — used for crowd radius shrink
    private System.Collections.Generic.List<UnhappyPerson> allSpawnedNPCs
        = new System.Collections.Generic.List<UnhappyPerson>(); // all waves — for SectionTracker
    private Coroutine shrinkCoroutine;

    /// <summary>Current active wave (1-based). 0 = not started. Read by CatVisionEvent.</summary>
    [HideInInspector] public int currentWave = 0;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        hasTriggered = true;
        if (stadiumMusic != null) stadiumMusic.gameObject.SetActive(true);
        Debug.Log("[Section2Spawner] Player entered stadium — starting wave sequence.");

        // Lock the exit so the player can't leave until all waves are cleared
        SetBlockers(true);

        // Register ALL wave NPCs with GameManager immediately so the total is
        // correct from the start. If we registered per-wave, the win condition
        // could fire after wave 1 before waves 2 and 3 are counted.
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null) gm.RegisterAdditionalPeople(seatSpawnPoints.Length * totalWaves);

        StartCoroutine(WaveLoop());
    }

    IEnumerator WaveLoop()
    {
        if (seatSpawnPoints == null || seatSpawnPoints.Length == 0)
        {
            Debug.LogWarning("[Section2Spawner] No seat spawn points assigned!");
            yield break;
        }
        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            Debug.LogWarning("[Section2Spawner] No NPC prefabs assigned!");
            yield break;
        }

        for (int wave = 1; wave <= totalWaves; wave++)
        {
            currentWave = wave;
            Debug.Log($"[Section2Spawner] Starting wave {wave}/{totalWaves}.");

            yield return StartCoroutine(SpawnWave(wave));

            // No clear-wait — next wave fires after a fixed delay regardless
            // of how many NPCs are still unhappy. This is intentional: the player
            // should feel overwhelmed until the cat vision triggers.
            if (wave < totalWaves)
            {
                Debug.Log($"[Section2Spawner] Wave {wave} spawned — next wave in {timeBetweenWaves}s.");
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        Debug.Log("[Section2Spawner] All waves spawned.");

        // Hand ALL spawned NPCs (every wave) to SectionTracker
        if (sectionTracker != null && allSpawnedNPCs.Count > 0)
        {
            sectionTracker.sectionPeople = allSpawnedNPCs.ToArray();
            Debug.Log($"[Section2Spawner] Registered {allSpawnedNPCs.Count} total NPCs with SectionTracker.");
        }

        // Unlock exit and fire event once all waves are out
        SetBlockers(false);
        onAllWavesComplete?.Invoke();
    }

    IEnumerator SpawnWave(int waveNumber)
    {
        // Stop any radius shrink from the previous wave before starting fresh
        if (shrinkCoroutine != null)
            StopCoroutine(shrinkCoroutine);

        crowdRadius = crowdRadiusStart;
        spawnedNPCs = new UnhappyPerson[seatSpawnPoints.Length];

        for (int i = 0; i < seatSpawnPoints.Length; i++)
        {
            Transform seat = seatSpawnPoints[i];
            if (seat == null) continue;

            GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
            GameObject obj = Instantiate(prefab, seat.position, seat.rotation);

            // NavMeshAgent.OnEnable snaps to NavMesh during Instantiate — disable it
            // immediately and force the position back to the elevated seat.
            NavMeshAgent agentComp = obj.GetComponent<NavMeshAgent>();
            if (agentComp != null) agentComp.enabled = false;
            obj.transform.position = seat.position;

            UnhappyPerson npc = obj.GetComponent<UnhappyPerson>();
            if (npc != null)
            {
                npc.fieldTarget     = fieldTarget;
                npc.crowdSlotIndex  = i;
                npc.totalCrowdSlots = seatSpawnPoints.Length;
                npc.crowdRadius     = crowdRadius;
                npc.ActivateStadiumBehaviour();
                spawnedNPCs[i] = npc;
                allSpawnedNPCs.Add(npc);  // track across all waves
            }

            Debug.Log($"[Section2Spawner] Wave {waveNumber} — spawned NPC {i + 1}/{seatSpawnPoints.Length} at {seat.name}");
            yield return new WaitForSeconds(spawnInterval);
        }

        Debug.Log($"[Section2Spawner] Wave {waveNumber} fully spawned.");
        shrinkCoroutine = StartCoroutine(ShrinkCrowdRadius());
    }

    IEnumerator ShrinkCrowdRadius()
    {
        float elapsed = 0f;
        while (elapsed < crowdShrinkDuration)
        {
            elapsed += Time.deltaTime;
            crowdRadius = Mathf.Lerp(crowdRadiusStart, crowdRadiusEnd,
                                     elapsed / crowdShrinkDuration);

            if (spawnedNPCs != null)
            {
                foreach (var npc in spawnedNPCs)
                {
                    if (npc != null)
                        npc.crowdRadius = crowdRadius;
                }
            }

            yield return null;
        }

        crowdRadius = crowdRadiusEnd;
    }

    void SetBlockers(bool active)
    {
        if (exitBlockers == null) return;
        foreach (var blocker in exitBlockers)
            if (blocker != null) blocker.SetActive(active);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        if (seatSpawnPoints != null)
        {
            foreach (var seat in seatSpawnPoints)
            {
                if (seat == null) continue;
                Gizmos.DrawWireSphere(seat.position, 0.4f);
            }
        }

        if (fieldTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(fieldTarget.position, 1f);
            Gizmos.DrawLine(transform.position, fieldTarget.position);
        }
    }
}
