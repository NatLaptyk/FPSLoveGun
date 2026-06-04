using UnityEngine;

/// <summary>
/// DEV-ONLY skip hotkeys for iterating on later phases without playing through the earlier ones.
///
/// Default bindings (rebindable in the Inspector):
///   1 — Skip the café phase (force-converts the café SectionTracker's people).
///   2 — Skip the stadium phase (stops waves, converts any live NPCs, teleports the player into
///       the boss-arena spawn, and activates whatever you have wired as the post-stadium target —
///       the FinalBoss directly, OR your AmbushGauntlet drop-in if you went with Option A).
///   3 — Skip both in sequence.
///
/// Heads-up on number keys: if you later wire 1/2/3 to weapon switching, rebind these in the
/// Inspector — that's a single field change, no code edit needed.
///
/// The hotkey handling itself is guarded by #if UNITY_EDITOR || DEVELOPMENT_BUILD so the keys
/// silently disappear in a release/Master build. The public SkipCafe() / SkipStadium() methods
/// always exist — wire them to debug UI buttons if you prefer mouse clicks to hotkeys.
///
/// SETUP:
///   1. Create an empty GameObject named "DebugSkip" (placing it on your GameManager works too).
///   2. Add this component.
///   3. Drag the café SectionTracker, your Section2Spawner, the player's teleport destination
///      (the same Transform CatVisionEvent.teleportDestination uses), and the post-stadium target
///      (FinalBoss OR your AmbushGauntlet GameObject) into the matching fields.
///   4. Play, press F1 / F2 / F3.
///
/// What it INTENTIONALLY does not do:
///   - It does not play the cat-vision cinematic. The point is to skip; the cinematic only matters
///     when you're actually playing the encounter.
///   - It does not bypass your AmbushGauntlet by default. If your AmbushGauntlet is the target
///     activated after the stadium, skipping the stadium will start the ambush (which is usually
///     what you want). Set Post Stadium Activate = FinalBoss directly if you want to skip the
///     ambush too, OR call AmbushGauntlet.ForceComplete() from a separate debug button.
/// </summary>
public class DebugSkipController : MonoBehaviour
{
    // ── Hotkeys ─────────────────────────────────────────────────────────────────
    [Header("Hotkeys")]
    public KeyCode skipCafeKey    = KeyCode.Alpha1;
    public KeyCode skipStadiumKey = KeyCode.Alpha2;
    public KeyCode skipBothKey    = KeyCode.Alpha3;

    // ── Café skip ───────────────────────────────────────────────────────────────
    [Header("Café Skip")]
    [Tooltip("Café SectionTracker. Each of its sectionPeople is force-converted, which fires the " +
             "tracker's onSectionComplete (BreatherZone.Open, Section2Spawner.UnlockEntrance, etc.).")]
    public SectionTracker cafeTracker;

    [Tooltip("If ON, after clearing the café also call Section2Spawner.UnlockEntrance() directly, " +
             "BYPASSING any BreatherZone you have wired between them. Handy when you only want to " +
             "test the stadium.")]
    public bool bypassBreatherOnCafeSkip = false;

    // ── Stadium skip ────────────────────────────────────────────────────────────
    [Header("Stadium Skip")]
    [Tooltip("Section2Spawner — used to stop the infinite wave loop and (optionally) to unlock the " +
             "entrance when 'Bypass Breather On Café Skip' is on.")]
    public Section2Spawner section2Spawner;

    [Tooltip("Same teleport destination CatVisionEvent uses — the empty GameObject in the street " +
             "where the player should land after the stadium.")]
    public Transform postStadiumTeleport;

    [Tooltip("What to SetActive(true) after the teleport. Assign the FinalBoss directly to skip " +
             "everything; assign your AmbushGauntlet to skip only the stadium and still play the " +
             "Heartbreak Bridge ambush.")]
    public GameObject postStadiumActivate;

    [Tooltip("Optional MinimapMarker to Show() after the skip — usually the boss objective marker " +
             "that CatVisionEvent.bossObjectiveMarker points at.")]
    public MinimapMarker postStadiumObjectiveMarker;

    [Tooltip("Player to teleport. If empty, auto-found by tag 'Player' on the first skip.")]
    public Transform playerTransform;

    [Tooltip("PlayerHealth. If empty, auto-found from the player transform on the first skip.")]
    public PlayerHealth playerHealth;

    [Tooltip("Heal the player to full happiness when skipping the stadium.")]
    public bool healOnStadiumSkip = true;

    [Tooltip("If ON, live stadium NPCs are Destroyed rather than converted. Faster, no happy-NPC " +
             "wanderers left behind, but the GameManager people-counter won't tick up for them.")]
    public bool destroyStadiumNpcsOnSkip = false;

    // ── Feedback ────────────────────────────────────────────────────────────────
    [Header("Feedback")]
    [Tooltip("Show a brief HUDManager message when a skip fires.")]
    public bool showHudMessages = true;

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
