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
    [Tooltip("Seconds to wait after a wave finishes spawning before the next wave begins. " +
             "Waves do NOT wait to be cleared — they overlap intentionally. " +
             "Waves repeat forever until StopWaves() is called by CatVisionEvent.")]
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

    [Header("Entry Blockers")]
    [Tooltip("GameObjects that physically block the stadium entrance at game start. " +
             "Call UnlockEntrance() (via the café SectionTracker's onSectionComplete event) " +
             "to remove them once the café section is cleared.")]
    public GameObject[] entryBlockers;

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
    private bool wavesActive  = false;   // set false by StopWaves() to end the infinite loop
    private float crowdRadius;
    private UnhappyPerson[] spawnedNPCs;          // current wave — used for crowd radius shrink
    private System.Collections.Generic.List<UnhappyPerson> allSpawnedNPCs
        = new System.Collections.Generic.List<UnhappyPerson>(); // all waves — for SectionTracker
    private Coroutine shrinkCoroutine;

    /// <summary>Current active wave (1-based). 0 = not started. Read by CatVisionEvent.</summary>
    [HideInInspector] public int currentWave = 0;

    /// <summary>
    /// Called by CatVisionEvent when the health threshold is crossed.
    /// Stops the infinite wave loop after the current wave finishes spawning.
    /// </summary>
    public void StopWaves()
    {
        wavesActive = false;
        Debug.Log("[Section2Spawner] StopWaves() called — no further waves will start.");
    }

    /// <summary>
    /// Wire this to the café SectionTracker's onSectionComplete event.
    /// Removes the entry blockers so the player can enter the stadium.
    /// </summary>
    public void UnlockEntrance()
    {
        if (entryBlockers == null) return;
        foreach (var blocker in entryBlockers)
            if (blocker != null) blocker.SetActive(false);
        Debug.Log("[Section2Spawner] Café cleared — stadium entrance unlocked.");
    }

    void Start()
    {
        // Activate entry blockers at game start so the player can't skip to the stadium
        if (entryBlockers != null)
            foreach (var blocker in entryBlockers)
                if (blocker != null) blocker.SetActive(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        hasTriggered = true;
        wavesActive  = true;
        if (stadiumMusic != null) stadiumMusic.gameObject.SetActive(true);
        Debug.Log("[Section2Spawner] Player entered stadium — starting infinite wave sequence.");

        // Lock the exit so the player can't leave until the cat vision fires
        SetBlockers(true);

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

        // Waves loop forever — CatVisionEvent calls StopWaves() when health
        // drops to the threshold, which sets wavesActive = false and ends the loop
        // after the current wave finishes spawning.
        while (wavesActive)
        {
            currentWave++;
            Debug.Log($"[Section2Spawner] Starting wave {currentWave} (infinite loop).");

            // Register this wave's NPCs with GameManager before spawning so the
            // HUD counter is always accurate.
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.RegisterAdditionalPeople(seatSpawnPoints.Length);

            yield return StartCoroutine(SpawnWave(currentWave));

            if (!wavesActive) break;

            Debug.Log($"[Section2Spawner] Wave {currentWave} spawned — next wave in {timeBetweenWaves}s.");
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        Debug.Log("[Section2Spawner] Wave loop ended (StopWaves called).");

        // Hand ALL spawned NPCs (every wave) to SectionTracker
        if (sectionTracker != null && allSpawnedNPCs.Count > 0)
        {
            sectionTracker.sectionPeople = allSpawnedNPCs.ToArray();
            Debug.Log($"[Section2Spawner] Registered {allSpawnedNPCs.Count} total NPCs with SectionTracker.");
        }

        // Unlock exit and fire event
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

        for (int i = 0; i < seatSpawnPoints.Length && wavesActive; i++)
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
