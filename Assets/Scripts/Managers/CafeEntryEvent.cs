using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Scripted opening event: a sad NPC walks to the café, the door opens,
/// the NPC enters, the door closes, then a Watcher bursts out and attacks.
///
/// SETUP:
/// 1. Add a BoxCollider (Is Trigger = true) near the café. Attach this script.
/// 2. Place an empty GameObject at the NPC's start position → NPC Spawn Point.
/// 3. Place an empty GameObject just outside the café door → Cafe Entrance Target.
/// 4. Place an empty GameObject just inside the café → Cafe Interior Target.
/// 5. Assign the café door GameObject (pivot MUST be at the hinge edge, not the centre).
///    If your door pivots at its centre, wrap it in an empty parent at the hinge.
/// 6. Place an empty GameObject above / in front of the café exit → Watcher Spawn Point.
/// 7. Assign your WatcherAI prefab.
/// 8. Assign Player Movement Script and Player Camera (same fields as CatVisionEvent).
/// </summary>
[RequireComponent(typeof(Collider))]
public class CafeEntryEvent : MonoBehaviour
{
    // ── NPC ───────────────────────────────────────────────────────────────────
    [Header("NPC")]
    [Tooltip("Unhappy NPC prefab to spawn for the walk-in.")]
    public GameObject npcPrefab;

    [Tooltip("Where the NPC appears at the start of the sequence.")]
    public Transform npcSpawnPoint;

    [Tooltip("Position just outside the café door where the NPC pauses.")]
    public Transform cafeEntranceTarget;

    [Tooltip("Position just inside the café where the NPC walks to after the door opens.")]
    public Transform cafeInteriorTarget;

    // ── Door ──────────────────────────────────────────────────────────────────
    [Header("Door")]
    [Tooltip("The door GameObject — must have an Animator component on it.")]
    public GameObject door;

    [Tooltip("Exact name of the Animator trigger that plays the open animation.")]
    public string doorOpenTrigger = "Open";

    [Tooltip("Exact name of the Animator trigger that plays the close animation.")]
    public string doorCloseTrigger = "Close";

    [Tooltip("How long to wait after firing the Open trigger before the NPC walks in. " +
             "Match this to the length of your open animation clip.")]
    public float doorOpenDuration = 0.6f;

    [Tooltip("How long to wait after firing the Close trigger before continuing. " +
             "Match this to the length of your close animation clip.")]
    public float doorCloseDuration = 0.6f;

    [Tooltip("How long the NPC waits at the entrance before the door opens.")]
    public float pauseAtDoor = 0.8f;

    [Tooltip("How long the NPC stands inside before the door closes.")]
    public float pauseInsideBeforeClose = 0.6f;

    private Animator doorAnimator;

    // ── Watcher ───────────────────────────────────────────────────────────────
    [Header("Watcher")]
    [Tooltip("WatcherAI prefab that bursts out after the door closes.")]
    public GameObject watcherPrefab;

    [Tooltip("Spawn point for the Watcher — place it just outside the café exit.")]
    public Transform watcherSpawnPoint;

    [Tooltip("Seconds between the door fully closing and the Watcher appearing.")]
    public float delayBeforeWatcher = 0.4f;

    // ── Camera ────────────────────────────────────────────────────────────────
    [Header("Camera")]
    [Tooltip("Your FPS movement / look script — disabled during the cutscene.")]
    public MonoBehaviour playerMovementScript;

    [Tooltip("The Transform that handles vertical camera look (Main Camera or pivot).")]
    public Transform playerCamera;

    [Tooltip("Seconds the camera takes to pan from the player's view to the NPC.")]
    public float cameraPanDuration = 1f;

    // ── Events ────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fires after the Watcher spawns and player control is restored.")]
    public UnityEngine.Events.UnityEvent onSequenceComplete;

    // ── Privates ──────────────────────────────────────────────────────────────
    private bool hasTriggered = false;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (door != null)
        {
            doorAnimator = door.GetComponent<Animator>();
            if (doorAnimator == null)
                doorAnimator = door.GetComponentInChildren<Animator>();

            if (doorAnimator == null)
                Debug.LogWarning("[CafeEntry] Door has no Animator — checked root and all children.");
            else
                Debug.Log($"[CafeEntry] Door Animator found on '{doorAnimator.gameObject.name}'.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        hasTriggered = true;
        Debug.Log("[CafeEntry] Player entered trigger — starting café sequence.");
        StartCoroutine(CafeSequence(other.transform.root));
    }

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator CafeSequence(Transform player)
    {
        // ── 1. Lock player input ──────────────────────────────────────────────
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        // ── 2. Spawn NPC ──────────────────────────────────────────────────────
        if (npcPrefab == null || npcSpawnPoint == null)
        {
            Debug.LogWarning("[CafeEntry] NPC prefab or spawn point not assigned!");
            yield break;
        }

        GameObject npcObj = Instantiate(npcPrefab, npcSpawnPoint.position, npcSpawnPoint.rotation);
        NavMeshAgent npcAgent = npcObj.GetComponent<NavMeshAgent>();
        UnhappyPerson npc    = npcObj.GetComponent<UnhappyPerson>();

        // Disable throwing so the NPC just walks
        if (npc != null)
        {
            npc.detectionRange = 0f;
            npc.attackRange    = 0f;
        }

        // ── 3. Pan camera to look at the NPC ─────────────────────────────────
        if (playerCamera != null && cameraPanDuration > 0f)
        {
            Quaternion startRot = playerCamera.rotation;
            float elapsed = 0f;

            while (elapsed < cameraPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraPanDuration);

                // Rotate toward the NPC's position
                Vector3 toNPC = (npcObj.transform.position - playerCamera.position).normalized;
                if (toNPC != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toNPC);
                    playerCamera.rotation = Quaternion.Slerp(startRot, targetRot, t);
                }

                yield return null;
            }
        }

        // ── 4. NPC walks to café entrance ─────────────────────────────────────
        if (npcAgent != null && cafeEntranceTarget != null)
        {
            npcAgent.SetDestination(cafeEntranceTarget.position);

            // Track NPC with camera while walking
            yield return StartCoroutine(WalkToPoint(
                npcAgent, cafeEntranceTarget.position, trackTarget: npcObj.transform));
        }

        // ── 5. NPC pauses at the door ─────────────────────────────────────────
        yield return new WaitForSeconds(pauseAtDoor);

        // ── 6. Door swings open ───────────────────────────────────────────────
        if (doorAnimator != null)
        {
            Debug.Log($"[CafeEntry] Playing door open state '{doorOpenTrigger}'.");
            doorAnimator.Play(doorOpenTrigger);
            yield return new WaitForSeconds(doorOpenDuration);
        }
        else
        {
            Debug.LogWarning("[CafeEntry] Cannot open door — Animator is null.");
        }

        // ── 7. NPC walks inside (direct transform — no NavMesh inside café) ───
        if (cafeInteriorTarget != null)
        {
            // Disable the agent so it doesn't fight the manual movement
            if (npcAgent != null) npcAgent.enabled = false;

            yield return StartCoroutine(WalkToPointDirect(
                npcObj.transform, cafeInteriorTarget.position, trackTarget: npcObj.transform));
        }

        // ── 8. NPC is inside — stop them, hide them ───────────────────────────
        if (npcAgent != null && npcAgent.enabled && npcAgent.isOnNavMesh)
            npcAgent.isStopped = true;
        npcObj.SetActive(false);   // disappears inside — no fadeout needed since door will close

        yield return new WaitForSeconds(pauseInsideBeforeClose);

        // ── 9. Door swings closed ─────────────────────────────────────────────
        if (doorAnimator != null)
        {
            Debug.Log($"[CafeEntry] Playing door close state '{doorCloseTrigger}'.");
            doorAnimator.Play(doorCloseTrigger);
            yield return new WaitForSeconds(doorCloseDuration);
        }
        else
        {
            Debug.LogWarning("[CafeEntry] Cannot close door — Animator is null.");
        }

        // ── 10. Brief pause, then Watcher bursts out ──────────────────────────
        yield return new WaitForSeconds(delayBeforeWatcher);

        if (watcherPrefab != null && watcherSpawnPoint != null)
        {
            GameObject watcherObj = Instantiate(
                watcherPrefab, watcherSpawnPoint.position, watcherSpawnPoint.rotation);

            // Point the Watcher straight at the player so it aggros immediately
            WatcherAI watcher = watcherObj.GetComponent<WatcherAI>();
            if (watcher != null)
                watcher.player = player;

            Debug.Log("[CafeEntry] Watcher spawned — attacking player.");
        }
        else
        {
            Debug.LogWarning("[CafeEntry] Watcher prefab or spawn point not assigned!");
        }

        // ── 11. Restore player control ────────────────────────────────────────
        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        onSequenceComplete?.Invoke();
        Debug.Log("[CafeEntry] Sequence complete.");
    }

    // ── Waits until the NavMeshAgent reaches its destination (with timeout) ──
    IEnumerator WalkToPoint(NavMeshAgent agent, Vector3 destination,
                            Transform trackTarget = null, float timeout = 30f)
    {
        yield return null; // let the agent start calculating the path

        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
                yield break;

            // Keep camera tracking the NPC during the walk
            if (playerCamera != null && trackTarget != null)
            {
                Vector3 toTarget = (trackTarget.position - playerCamera.position).normalized;
                if (toTarget != Vector3.zero)
                    playerCamera.rotation = Quaternion.Slerp(
                        playerCamera.rotation,
                        Quaternion.LookRotation(toTarget),
                        5f * Time.deltaTime);
            }

            yield return null;
        }

        Debug.LogWarning("[CafeEntry] WalkToPoint timed out — continuing sequence.");
    }

    // ── Moves a transform directly toward a position (no NavMesh required) ───
    IEnumerator WalkToPointDirect(Transform mover, Vector3 destination,
                                  Transform trackTarget = null, float speed = 2.5f,
                                  float timeout = 15f)
    {
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // Face the direction of travel
            Vector3 dir = (destination - mover.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                mover.rotation = Quaternion.Slerp(mover.rotation,
                    Quaternion.LookRotation(dir), 8f * Time.deltaTime);

            mover.position = Vector3.MoveTowards(mover.position, destination,
                                                 speed * Time.deltaTime);

            // Track with camera
            if (playerCamera != null && trackTarget != null)
            {
                Vector3 toTarget = (trackTarget.position - playerCamera.position).normalized;
                if (toTarget != Vector3.zero)
                    playerCamera.rotation = Quaternion.Slerp(
                        playerCamera.rotation,
                        Quaternion.LookRotation(toTarget),
                        5f * Time.deltaTime);
            }

            if (Vector3.Distance(mover.position, destination) < 0.15f)
                yield break;

            yield return null;
        }

        Debug.LogWarning("[CafeEntry] WalkToPointDirect timed out — continuing sequence.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (npcSpawnPoint != null)
            Gizmos.DrawWireSphere(npcSpawnPoint.position, 0.4f);

        if (cafeEntranceTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cafeEntranceTarget.position, 0.4f);
        }

        if (cafeInteriorTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cafeInteriorTarget.position, 0.4f);
        }

        if (watcherSpawnPoint != null)
        {
            Gizmos.color = new Color(0.75f, 0.1f, 1f);
            Gizmos.DrawWireSphere(watcherSpawnPoint.position, 0.5f);
        }
    }
}
