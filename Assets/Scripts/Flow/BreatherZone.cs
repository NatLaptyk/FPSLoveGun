using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// CAFÉ → STADIUM interstitial: a calm "catch your breath and restock" beat.
///
/// Right now the café→stadium handoff is abrupt — the café SectionTracker completes and
/// <c>Section2Spawner.UnlockEntrance()</c> fires immediately, so the player walks straight
/// from one fight into the next. This zone inserts a safe corridor (think "Lovers' Lane" or
/// a quiet park) between them: guaranteed pickups, an optional couple of easy NPCs to convert
/// (great for teaching the Love Bomb on a "very unhappy" one before the stadium), a calming
/// music swap, and an optional top-up of the player's happiness/health.
///
/// Crucially it RE-GATES the stadium: the stadium now unlocks when the player reaches the
/// breather EXIT, not the instant the café clears. That gives the breather a purpose.
///
/// FLOW (two public entry points, both wired in the Inspector):
///   café SectionTracker.onSectionComplete ──► Open()      (light up the breather)
///   exit SequenceTrigger.onTriggered      ──► Exit()       (player leaves → unlock stadium)
///
/// REUSE NOTE: the actual *content* (pickups, NPCs, hint, music) can also be driven by an
/// EventManager if you prefer — this script just gives you a single tidy place to gate the
/// stadium behind the exit and to top the player up. Mix and match freely.
/// </summary>
public class BreatherZone : MonoBehaviour
{
    [Header("Identity")]
    public string zoneName = "Breather — Lovers' Lane";

    // ── What appears when the breather opens ────────────────────────────────────
    [Header("Restock Pickups")]
    [Tooltip("Pre-placed (disabled) pickup GameObjects to switch on when the breather opens. " +
             "Place a generous spread of ammo / health / love-bomb pickups here.")]
    public GameObject[] pickupsToActivate;

    [Header("Optional Bonus NPCs")]
    [Tooltip("Pre-placed (disabled) UnhappyPerson GameObjects to switch on for optional " +
             "practice. They are ALREADY counted by GameManager (it includes disabled NPCs " +
             "at Start), so do NOT register them again. Tip: make one 'very unhappy' so the " +
             "player learns the Love Bomb before the stadium.")]
    public GameObject[] bonusNpcsToActivate;
    [Tooltip("Seconds between switching on each bonus NPC, for a little staggered drama.")]
    public float npcActivateStagger = 0.4f;

    // ── Player top-up ───────────────────────────────────────────────────────────
    [Header("Player Top-Up")]
    [Tooltip("PlayerHealth to top up on entry. Leave empty to auto-find the 'Player' tag.")]
    public PlayerHealth playerHealth;
    [Tooltip("Restore the player to full happiness when the breather opens.")]
    public bool healToFull = true;
    [Tooltip("If 'Heal To Full' is off, heal by this fixed amount instead (0 = no heal).")]
    public int healAmount = 0;

    // ── Music ───────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("Café/combat music to fade out as the breather opens. Optional.")]
    public MusicController musicToFadeOut;
    [Tooltip("Calm breather music to start as the breather opens. Optional. " +
             "It is enabled (SetActive) so its OnEnable starts playback, matching your other scripts.")]
    public MusicController calmMusicToStart;
    [Tooltip("Calm music to fade out again when the player leaves toward the stadium. Optional.")]
    public MusicController calmMusicToStopOnExit;

    // ── Exit gating ─────────────────────────────────────────────────────────────
    [Header("Exit Gate")]
    [Tooltip("The breather's EXIT trigger. It starts DISARMED and is armed here when the " +
             "breather opens, so the player can't unlock the stadium before the breather exists. " +
             "Wire that trigger's On Triggered → this script's Exit().")]
    public SequenceTrigger exitTrigger;

    // ── Messages ────────────────────────────────────────────────────────────────
    [Header("HUD Messages")]
    [TextArea] public string entryMessage = "Catch your breath. Stock up — the stadium crowd won't be so gentle.";
    public float entryMessageDuration = 4f;
    [TextArea] public string exitMessage = "Here we go. Into the stadium.";
    public float exitMessageDuration = 3f;

    // ── Events ──────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fired when the breather opens (after the café is cleared).")]
    public UnityEvent onBreatherOpened;
    [Tooltip("Fired when the player leaves the breather. WIRE THIS to " +
             "Section2Spawner.UnlockEntrance() so the stadium opens only now.")]
    public UnityEvent onBreatherCleared;

    public bool IsOpen { get; private set; }
    public bool IsCleared { get; private set; }

    /// <summary>
    /// Light up the breather. Wire to the café SectionTracker's onSectionComplete.
    /// </summary>
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Debug.Log($"[BreatherZone] '{zoneName}' opened.");

        // Music: fade the fight track, bring in the calm one
        if (musicToFadeOut != null) musicToFadeOut.FadeOut();
        if (calmMusicToStart != null) calmMusicToStart.gameObject.SetActive(true);

        // Restock pickups
        if (pickupsToActivate != null)
            foreach (var p in pickupsToActivate)
                if (p != null) p.SetActive(true);

        // Top the player up
        if (playerHealth == null)
        {
            GameObject pl = GameObject.FindGameObjectWithTag("Player");
            if (pl != null) playerHealth = pl.GetComponent<PlayerHealth>();
        }
        if (playerHealth != null)
        {
            if (healToFull) playerHealth.Heal(playerHealth.maxHappiness);
            else if (healAmount > 0) playerHealth.Heal(healAmount);
        }

        // Optional bonus NPCs (staggered)
        if (bonusNpcsToActivate != null && bonusNpcsToActivate.Length > 0)
            StartCoroutine(ActivateBonusNpcs());

        // Entry hint
        ShowMessage(entryMessage, entryMessageDuration);

        // Arm the exit so leaving now unlocks the stadium
        if (exitTrigger != null) exitTrigger.Arm();
        else Debug.LogWarning("[BreatherZone] No exit trigger assigned — the stadium gate won't be armed.");

        onBreatherOpened?.Invoke();
    }

    IEnumerator ActivateBonusNpcs()
    {
        foreach (var npc in bonusNpcsToActivate)
        {
            if (npc != null) npc.SetActive(true);
            if (npcActivateStagger > 0f)
                yield return new WaitForSeconds(npcActivateStagger);
        }
    }

    /// <summary>
    /// Player has reached the far end of the breather. Wire to the exit SequenceTrigger's
    /// onTriggered. Shows the exit message and fires onBreatherCleared (→ unlock the stadium).
    /// </summary>
    public void Exit()
    {
        if (IsCleared) return;
        IsCleared = true;
        Debug.Log($"[BreatherZone] '{zoneName}' cleared — handing off to the stadium.");

        if (calmMusicToStopOnExit != null) calmMusicToStopOnExit.FadeOut();

        ShowMessage(exitMessage, exitMessageDuration);

        onBreatherCleared?.Invoke();
    }

    void ShowMessage(string msg, float duration)
    {
        if (string.IsNullOrEmpty(msg)) return;
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowMessage(msg, duration);
    }
}
