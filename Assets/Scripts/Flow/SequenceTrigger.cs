using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// A lightweight, reusable "player walked in here" trigger that fires a UnityEvent.
///
/// Why this exists alongside the older <see cref="TriggerZone"/>:
/// TriggerZone is locked to a fixed set of behaviours (Event / Pickup / Checkpoint /
/// AreaMessage) and can only ever call <c>EventManager.TriggerEvent()</c>. SequenceTrigger
/// is the generic building block — its <see cref="onTriggered"/> UnityEvent can be wired in
/// the Inspector to ANY public method (e.g. BreatherZone.Exit, AmbushGauntlet.Begin,
/// Section2Spawner.UnlockEntrance, a door, music, …). It is used by both interstitial
/// activities (the café→stadium breather and the stadium→boss ambush) but is generic
/// enough for anything.
///
/// KEY FEATURE — "armed" gating:
/// A trigger can be left disarmed so that simply walking through it does nothing until
/// some earlier step arms it. Example: the breather's EXIT trigger should only unlock the
/// stadium AFTER the breather has actually opened, not if the player somehow reaches it
/// first. Call <see cref="Arm"/> from the earlier step, or set <see cref="startArmed"/>.
///
/// SETUP:
/// 1. Create an empty GameObject where you want the trigger (e.g. "Trigger_BreatherExit").
/// 2. Add a Box Collider and tick "Is Trigger". Scale it to cover the doorway/area.
/// 3. Attach this script.
/// 4. Wire "On Triggered" in the Inspector to whatever should happen.
/// 5. (Optional) Untick "Start Armed" and call Arm() from an earlier event so the trigger
///    is inert until the sequence reaches it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SequenceTrigger : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Purely for readable Console logs.")]
    public string triggerName = "SequenceTrigger";

    [Header("Activation")]
    [Tooltip("If false, the trigger ignores the player until Arm() is called by an earlier " +
             "step in the sequence. Use this to stop the player short-circuiting a flow.")]
    public bool startArmed = true;

    [Tooltip("If true, the trigger fires only once and then disables its collider. " +
             "If false it can fire every time the player enters (respecting Re-Arm Delay).")]
    public bool triggerOnce = true;

    [Tooltip("When Trigger Once is OFF, the minimum seconds between repeat fires.")]
    public float reArmDelay = 0.5f;

    [Header("Timing")]
    [Tooltip("Seconds to wait after the player enters before On Triggered fires.")]
    public float delayBeforeFiring = 0f;

    [Header("Optional HUD Message")]
    [Tooltip("If set, this is shown via HUDManager.ShowMessage when the trigger fires. " +
             "Leave blank to show nothing.")]
    [TextArea] public string hudMessage = "";
    public float hudMessageDuration = 3f;

    [Header("Optional Audio")]
    [Tooltip("One-shot sound played at the trigger's position when it fires.")]
    public AudioClip triggerSound;

    [Header("Event")]
    [Tooltip("Fired when the (armed) player enters. Wire this to any public method.")]
    public UnityEvent onTriggered;

    // ── State ───────────────────────────────────────────────────────────────────
    public bool IsArmed { get; private set; }
    private bool hasFired = false;
    private float nextAllowedFireTime = 0f;

    void Awake()
    {
        IsArmed = startArmed;

        // A trigger collider only generates OnTriggerEnter if it IS a trigger.
        // Auto-correct a common setup mistake instead of silently doing nothing.
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[SequenceTrigger] '{triggerName}' collider was not marked " +
                             "'Is Trigger' — auto-enabled so the zone can detect the player.");
        }
    }

    /// <summary>Arm the trigger so the next player entry fires it. Wire to an earlier step.</summary>
    public void Arm()
    {
        IsArmed = true;
        Debug.Log($"[SequenceTrigger] '{triggerName}' armed.");
    }

    /// <summary>Disarm the trigger so player entries are ignored again.</summary>
    public void Disarm()
    {
        IsArmed = false;
        Debug.Log($"[SequenceTrigger] '{triggerName}' disarmed.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsArmed) return;
        if (triggerOnce && hasFired) return;
        if (Time.time < nextAllowedFireTime) return;

        // Accept either a directly-tagged collider or a child collider whose root is the Player
        // (mirrors how Section2Spawner detects the player).
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        hasFired = true;
        nextAllowedFireTime = Time.time + reArmDelay;

        if (delayBeforeFiring > 0f)
            StartCoroutine(FireAfterDelay());
        else
            Fire();
    }

    IEnumerator FireAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeFiring);
        Fire();
    }

    void Fire()
    {
        Debug.Log($"[SequenceTrigger] '{triggerName}' fired.");

        if (triggerSound != null)
            AudioSource.PlayClipAtPoint(triggerSound, transform.position);

        if (!string.IsNullOrEmpty(hudMessage))
        {
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null) hud.ShowMessage(hudMessage, hudMessageDuration);
        }

        onTriggered?.Invoke();

        // For one-shot triggers, switch the collider off so it can never re-fire and so
        // it stops costing physics checks for the rest of the session.
        if (triggerOnce)
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    // Visualise the trigger volume in the Scene view (green = armed at start, grey = disarmed)
    void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        Gizmos.color = startArmed
            ? new Color(0.2f, 0.9f, 0.4f, 0.25f)
            : new Color(0.6f, 0.6f, 0.6f, 0.2f);

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
}
