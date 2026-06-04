using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// STADIUM → FINAL BOSS interstitial: the "Heartbreak Bridge" mini-boss ambush.
///
/// Today the stadium finale (CatVisionEvent) teleports the player to the street and
/// immediately <c>SetActive(true)</c>s the FinalBoss, so the boss just pops into existence.
/// This controller inserts a short, tense fight on the approach: a mini-boss plus a couple
/// of escorts ambush the player, gates seal the route, and only when the ambush is cleared
/// does the real boss activate.
///
/// ──────────────────────────────────────────────────────────────────────────────────
/// TWO WAYS TO WIRE IT (pick one):
///
///  OPTION A — drop into the existing CatVision flow (recommended, ZERO code edits):
///    • Put this component on a GameObject that starts DISABLED in the scene.
///    • In CatVisionEvent, set "Boss To Activate" to THIS GameObject (instead of the boss).
///    • Leave "Begin On Enable" = true. CatVision teleports the player, switches this on,
///      OnEnable → Begin() runs the ambush.
///    • Wire "On Gauntlet Cleared" → FinalBoss.SetActive(true) (+ the boss objective marker).
///
///  OPTION B — physical walk-in (e.g. if the stadium ends via StadiumEventController):
///    • Leave this GameObject enabled, set "Begin On Enable" = false.
///    • Place a SequenceTrigger on the bridge; wire its onTriggered → this.Begin().
///    • Wire "On Gauntlet Cleared" → activate the boss as above.
/// ──────────────────────────────────────────────────────────────────────────────────
///
/// COMPLETION: the gauntlet is cleared when every assigned ambusher (and the mini-boss) has
/// been converted to Happy. The mini-boss is just an UnhappyPerson with a high
/// "unhappinessLevel" and "isVeryUnhappy" ticked, so it soaks several Love Bombs. If instead
/// you use a WatcherAI / custom boss that does NOT report an UnhappyPerson mood, leave the
/// mini-boss slot empty and call ForceComplete() from that boss's own defeat event.
/// </summary>
public class AmbushGauntlet : MonoBehaviour
{
    [Header("Identity")]
    public string gauntletName = "Heartbreak Bridge";

    [Header("Start")]
    [Tooltip("OPTION A: run Begin() automatically when this GameObject is enabled. Set true " +
             "if CatVisionEvent's 'Boss To Activate' points at this (disabled) object.")]
    public bool beginOnEnable = true;

    // ── Ambushers ───────────────────────────────────────────────────────────────
    [Header("Ambushers")]
    [Tooltip("Pre-placed (disabled) UnhappyPerson GameObjects switched on when the ambush " +
             "begins. They are already counted by GameManager (which includes disabled NPCs " +
             "at Start), so they are NOT re-registered here.")]
    public GameObject[] ambushersToActivate;
    [Tooltip("Seconds between switching on each ambusher, for a staggered 'they're closing in' feel.")]
    public float activateStagger = 0.5f;

    [Header("Mini-Boss")]
    [Tooltip("Optional tougher NPC. Make it an UnhappyPerson with a high Unhappiness Level and " +
             "'Is Very Unhappy' ticked so it needs several Love Bombs. Leave empty for a pure " +
             "wave ambush, or if you use a custom boss (then call ForceComplete() from its defeat).")]
    public GameObject miniBoss;

    // ── Gates / blockers ────────────────────────────────────────────────────────
    [Header("Route Gates")]
    [Tooltip("Blocker GameObjects (e.g. invisible walls / gate meshes) ENABLED when the ambush " +
             "begins, so the player must fight rather than run past. DISABLED again on clear.")]
    public GameObject[] routeBlockers;

    // ── Music ───────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("Calm / approach music to fade out as the ambush starts. Optional.")]
    public MusicController musicToFadeOut;
    [Tooltip("Tense ambush music to start as the ambush begins (enabled so OnEnable plays it). Optional.")]
    public MusicController ambushMusicToStart;

    // ── Messages ────────────────────────────────────────────────────────────────
    [Header("HUD Messages")]
    [TextArea] public string beginMessage = "Heartbreak Bridge — they won't let you pass! Survive the ambush!";
    public float beginMessageDuration = 4f;
    [TextArea] public string clearMessage = "The bridge is clear... but something bigger is coming.";
    public float clearMessageDuration = 4f;

    // ── Events ──────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fired the moment the ambush starts.")]
    public UnityEvent onGauntletBegan;
    [Tooltip("Fired when every ambusher (and mini-boss) is converted. WIRE THIS to activate " +
             "the FinalBoss (FinalBoss.SetActive(true)) and show its objective marker.")]
    public UnityEvent onGauntletCleared;

    public bool HasBegun { get; private set; }
    public bool IsCleared { get; private set; }

    // All UnhappyPersons we poll for conversion (ambushers + mini-boss).
    private readonly List<UnhappyPerson> tracked = new List<UnhappyPerson>();

    void OnEnable()
    {
        if (beginOnEnable) Begin();
    }

    /// <summary>
    /// Start the ambush: seal the route, switch on the ambushers + mini-boss, swap music,
    /// and show the banner. Safe to call once (guards against repeats).
    /// </summary>
    public void Begin()
    {
        if (HasBegun) return;
        HasBegun = true;
        Debug.Log($"[AmbushGauntlet] '{gauntletName}' begins.");

        // Music
        if (musicToFadeOut != null) musicToFadeOut.FadeOut();
        if (ambushMusicToStart != null) ambushMusicToStart.gameObject.SetActive(true);

        // Seal the route so the player has to fight
        SetBlockers(true);

        // Build the conversion-tracking list and switch enemies on (staggered)
        tracked.Clear();
        StartCoroutine(ActivateAmbushers());

        ShowMessage(beginMessage, beginMessageDuration);
        onGauntletBegan?.Invoke();
    }

    IEnumerator ActivateAmbushers()
    {
        if (ambushersToActivate != null)
        {
            foreach (var go in ambushersToActivate)
            {
                if (go == null) continue;
                go.SetActive(true);
                RegisterTracked(go);
                if (activateStagger > 0f)
                    yield return new WaitForSeconds(activateStagger);
            }
        }

        if (miniBoss != null)
        {
            miniBoss.SetActive(true);
            RegisterTracked(miniBoss);
        }

        if (tracked.Count == 0)
            Debug.LogWarning("[AmbushGauntlet] No UnhappyPerson ambushers were tracked. " +
                             "The gauntlet will only clear via ForceComplete() — wire that to " +
                             "your custom boss's defeat event.");
    }

    void RegisterTracked(GameObject go)
    {
        UnhappyPerson up = go.GetComponent<UnhappyPerson>();
        if (up == null) up = go.GetComponentInChildren<UnhappyPerson>(true);
        if (up != null) tracked.Add(up);
    }

    void Update()
    {
        if (!HasBegun || IsCleared) return;
        if (tracked.Count == 0) return;   // nothing pollable yet (or custom-boss mode)

        for (int i = 0; i < tracked.Count; i++)
        {
            var p = tracked[i];
            // A null entry means the NPC was destroyed — treat as resolved rather than blocking.
            if (p != null && p.currentMood != UnhappyPerson.MoodState.Happy)
                return; // at least one still unhappy → not done
        }

        Complete();
    }

    /// <summary>
    /// Manual completion hook. Wire this to a custom mini-boss's defeat/converted event if you
    /// are NOT using an UnhappyPerson mini-boss. Harmless to call more than once.
    /// </summary>
    public void ForceComplete()
    {
        Complete();
    }

    void Complete()
    {
        if (IsCleared) return;
        IsCleared = true;
        Debug.Log($"[AmbushGauntlet] '{gauntletName}' cleared — activating the final boss.");

        // Open the route to the boss arena
        SetBlockers(false);

        if (ambushMusicToStart != null) ambushMusicToStart.FadeOut();

        ShowMessage(clearMessage, clearMessageDuration);

        onGauntletCleared?.Invoke();
    }

    void SetBlockers(bool active)
    {
        if (routeBlockers == null) return;
        foreach (var b in routeBlockers)
            if (b != null) b.SetActive(active);
    }

    void ShowMessage(string msg, float duration)
    {
        if (string.IsNullOrEmpty(msg)) return;
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowMessage(msg, duration);
    }
}
