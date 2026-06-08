using UnityEngine;

// DEV-ONLY skip hotkeys for iterating on later phases without playing through the earlier ones.

public class DebugSkipController : MonoBehaviour
{
    // ── Hotkeys ─────────────────────────────────────────────────────────────────
    [Header("Hotkeys")]
    [SerializeField] private KeyCode skipCafeKey    = KeyCode.Alpha1;
    [SerializeField] private KeyCode skipStadiumKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode skipBothKey    = KeyCode.Alpha3;

    // ── Café skip ───────────────────────────────────────────────────────────────
    [Header("Café Skip")]
    [Tooltip("Café SectionTracker. Each of its sectionPeople is force-converted, which fires the " +
             "tracker's onSectionComplete (BreatherZone.Open, Section2Spawner.UnlockEntrance, etc.).")]
    [SerializeField] private SectionTracker cafeTracker;

    [Tooltip("If ON, after clearing the café also call Section2Spawner.UnlockEntrance() directly, " +
             "BYPASSING any BreatherZone you have wired between them. Handy when you only want to " +
             "test the stadium.")]
    [SerializeField] private bool bypassBreatherOnCafeSkip = false;

    // ── Stadium skip ────────────────────────────────────────────────────────────
    [Header("Stadium Skip")]
    [Tooltip("Section2Spawner — used to stop the infinite wave loop and (optionally) to unlock the " +
             "entrance when 'Bypass Breather On Café Skip' is on.")]
    [SerializeField] private Section2Spawner section2Spawner;

    [Tooltip("Same teleport destination CatVisionEvent uses — the empty GameObject in the street " +
             "where the player should land after the stadium.")]
    [SerializeField] private Transform postStadiumTeleport;

    [Tooltip("What to SetActive(true) after the teleport. Assign the FinalBoss directly to skip " +
             "everything; assign your AmbushGauntlet to skip only the stadium and still play the " +
             "Heartbreak Bridge ambush.")]
    [SerializeField] private GameObject postStadiumActivate;

    [Tooltip("Optional MinimapMarker to Show() after the skip — usually the boss objective marker " +
             "that CatVisionEvent.bossObjectiveMarker points at.")]
    [SerializeField] private MinimapMarker postStadiumObjectiveMarker;

    [Tooltip("Player to teleport. If empty, auto-found by tag 'Player' on the first skip.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("PlayerHealth. If empty, auto-found from the player transform on the first skip.")]
    public PlayerHealth playerHealth;

    [Tooltip("Heal the player to full happiness when skipping the stadium.")]
    [SerializeField] private bool healOnStadiumSkip = true;

    [Tooltip("If ON, live stadium NPCs are Destroyed rather than converted. Faster, no happy-NPC " +
             "wanderers left behind, but the GameManager people-counter won't tick up for them.")]
    [SerializeField] private bool destroyStadiumNpcsOnSkip = false;

    // ── Feedback ────────────────────────────────────────────────────────────────
    [Header("Feedback")]
    [Tooltip("Show a brief HUDManager message when a skip fires.")]
    [SerializeField] private bool showHudMessages = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Start()
    {
        Debug.Log($"[DebugSkip] Hotkeys active — {skipCafeKey}=café, {skipStadiumKey}=stadium, " +
                  $"{skipBothKey}=both. (Compiled out of release builds.)");
    }

    void Update()
    {
        if (Input.GetKeyDown(skipCafeKey))    SkipCafe();
        if (Input.GetKeyDown(skipStadiumKey)) SkipStadium();
        if (Input.GetKeyDown(skipBothKey))    { SkipCafe(); SkipStadium(); }
    }
#endif

    /// <summary>Force-clear the café section. Safe to wire to an Inspector UI button too.</summary>
    public void SkipCafe()
    {
        if (cafeTracker == null)
        {
            Debug.LogWarning("[DebugSkip] SkipCafe — no cafeTracker assigned.");
            return;
        }

        int converted = 0;
        if (cafeTracker.sectionPeople != null)
        {
            foreach (UnhappyPerson p in cafeTracker.sectionPeople)
            {
                if (p == null) continue;
                if (p.currentMood == UnhappyPerson.MoodState.Happy) continue;
                // Large amount bypasses the 'very unhappy' resistance (amount < 3 check)
                // and triggers BecomeHappy, which the SectionTracker picks up on its next poll
                // and fires onSectionComplete (BreatherZone.Open, UnlockEntrance, etc.).
                p.ReceiveLove(999);
                converted++;
            }
        }
        Debug.Log($"[DebugSkip] Café skip — converted {converted} NPCs in '{cafeTracker.sectionName}'.");

        if (bypassBreatherOnCafeSkip)
        {
            if (section2Spawner != null)
            {
                section2Spawner.UnlockEntrance();
                Debug.Log("[DebugSkip] Café skip — bypassed breather, called UnlockEntrance() directly.");
            }
            else
            {
                Debug.LogWarning("[DebugSkip] 'Bypass Breather' is ON but no Section2Spawner is assigned.");
            }
        }

        Hud($"[Skip] Café cleared ({converted} NPCs).");
    }

    /// <summary>Force-end the stadium phase and teleport into the boss arena.</summary>
    public void SkipStadium()
    {
        // 1. Stop the wave loop so no new NPCs spawn after this point.
        if (section2Spawner != null) section2Spawner.StopWaves();

        // 2. Convert (or destroy) every live, unhappy NPC currently in the scene.
        int handled = 0;
        UnhappyPerson[] all = FindObjectsByType<UnhappyPerson>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (UnhappyPerson p in all)
        {
            if (p == null) continue;
            if (p.currentMood == UnhappyPerson.MoodState.Happy) continue;
            if (destroyStadiumNpcsOnSkip) Destroy(p.gameObject);
            else                          p.ReceiveLove(999);
            handled++;
        }

        // 3. Resolve player references lazily (so the Inspector doesn't have to be perfect).
        if (playerHealth == null)
        {
            GameObject pl = GameObject.FindGameObjectWithTag("Player");
            if (pl != null)
            {
                playerHealth = pl.GetComponent<PlayerHealth>();
                if (playerHealth == null) playerHealth = pl.GetComponentInChildren<PlayerHealth>();
            }
        }
        if (playerTransform == null && playerHealth != null)
            playerTransform = playerHealth.transform;

        // 4. Heal the player.
        if (healOnStadiumSkip && playerHealth != null)
            playerHealth.Heal(playerHealth.maxHappiness);

        // 5. Teleport — mirrors CatVisionEvent's CC disable/enable dance so the controller
        //    doesn't fight the new position.
        if (postStadiumTeleport != null && playerTransform != null)
        {
            CharacterController cc = playerTransform.GetComponentInChildren<CharacterController>();
            if (cc == null) cc = playerTransform.GetComponentInParent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = postStadiumTeleport.position;
            playerTransform.rotation = postStadiumTeleport.rotation;

            if (cc != null) cc.enabled = true;

            Rigidbody rb = playerTransform.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            Debug.LogWarning("[DebugSkip] Stadium skip — missing teleport destination or player transform.");
        }

        // 6. Activate the post-stadium target (FinalBoss directly, OR your AmbushGauntlet drop-in).
        if (postStadiumActivate != null)
        {
            postStadiumActivate.SetActive(true);
            Debug.Log($"[DebugSkip] Stadium skip — activated '{postStadiumActivate.name}'.");
        }
        else
        {
            Debug.LogWarning("[DebugSkip] Stadium skip — no 'Post Stadium Activate' target assigned.");
        }

        // 7. Show the boss objective marker just like CatVisionEvent does.
        if (postStadiumObjectiveMarker != null)
            postStadiumObjectiveMarker.Show();

        Debug.Log($"[DebugSkip] Stadium skip — handled {handled} live NPCs.");
        Hud($"[Skip] Stadium cleared ({handled} NPCs).");
    }

    void Hud(string msg)
    {
        if (!showHudMessages) return;
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowMessage(msg, 2f);
    }
}
