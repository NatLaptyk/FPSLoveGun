using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a hint/message overlay to the player without pausing the game.
/// Can be triggered two ways:
///   A) As a trigger zone — attach to a GameObject with a Trigger Collider;
///      the message shows when the player walks through.
///   B) Via UnityEvent — call Show() from any Inspector event
///      (e.g. SectionTracker.onSectionComplete, CatVisionEvent.onSequenceComplete).
///
/// CANVAS SETUP:
///   Create a Screen Space – Overlay Canvas (Sort Order 50) as a child of nothing.
///   Inside it:
///     HintPanel       (Image — dark semi-transparent background, anchored centre)
///       └─ MessageText   (TextMeshProUGUI — main message)
///       └─ DismissText   (TextMeshProUGUI — "Press [Enter] to continue", smaller)
///   Add a CanvasGroup to HintPanel and assign it to "hintCanvasGroup" below.
///   Keep HintPanel INACTIVE by default — this script enables it.
/// </summary>
public class HintMessage : MonoBehaviour
{
    // ── UI ─────────────────────────────────────────────────────────────────────
    [Header("UI References")]
    [Tooltip("CanvasGroup on the hint panel. Keep the panel INACTIVE in the scene.")]
    public CanvasGroup hintCanvasGroup;

    [Tooltip("TextMeshPro component for the main message body.")]
    public TextMeshProUGUI messageText;

    [Tooltip("Optional smaller text that says 'Press Enter to dismiss'. " +
             "Hidden automatically if displayDuration > 0.")]
    public TextMeshProUGUI dismissPromptText;

    // ── Content ────────────────────────────────────────────────────────────────
    [Header("Content")]
    [Tooltip("The message shown to the player. Supports rich text tags.")]
    [TextArea(3, 8)]
    public string message = "Explore the city!\nStock up on <color=#00AAFF>ammo</color>, " +
                            "<color=#FF3399>love bombs</color>, and " +
                            "<color=#88FF00>health</color> before entering the stadium.";

    // ── Behaviour ──────────────────────────────────────────────────────────────
    [Header("Behaviour")]
    [Tooltip("Seconds before the message auto-dismisses. " +
             "Set to 0 to require the player to press Enter / Space.")]
    public float displayDuration = 6f;

    [Tooltip("Seconds for the panel to fade in and out.")]
    public float fadeDuration = 0.4f;

    [Tooltip("If true, this message can only be shown once per session.")]
    public bool showOnce = true;

    // ── Events ─────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fires after the message is dismissed.")]
    public UnityEngine.Events.UnityEvent onDismissed;

    // ── Privates ───────────────────────────────────────────────────────────────
    private bool isShowing  = false;
    private bool hasShown   = false;
    private Coroutine activeRoutine;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        // Make sure the panel starts hidden
        if (hintCanvasGroup != null)
        {
            hintCanvasGroup.alpha          = 0f;
            hintCanvasGroup.gameObject.SetActive(false);
        }
    }

    // ── Trigger zone ──────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;
        Show();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the configured message. Safe to call from UnityEvents and code.
    /// </summary>
    public void Show()
    {
        if (isShowing)               return;
        if (showOnce && hasShown)    return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine(message));
    }

    /// <summary>
    /// Show a custom message string at runtime (overrides the Inspector field).
    /// </summary>
    public void ShowWithText(string customMessage)
    {
        if (isShowing)             return;
        if (showOnce && hasShown)  return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine(customMessage));
    }

    /// <summary>
    /// Dismiss the message early (e.g. from a UI button).
    /// </summary>
    public void Dismiss()
    {
        if (!isShowing) return;
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeOut());
    }

    // ── Coroutines ─────────────────────────────────────────────────────────────

    IEnumerator ShowRoutine(string text)
    {
        isShowing = true;
        hasShown  = true;

        // Set text
        if (messageText != null) messageText.text = text;

        // Show dismiss prompt only when player must press a key
        if (dismissPromptText != null)
            dismissPromptText.gameObject.SetActive(displayDuration <= 0f);

        // Activate and fade in
        hintCanvasGroup.gameObject.SetActive(true);
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        // Hold — auto-dismiss or wait for key
        if (displayDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < displayDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space))
                    break;
                yield return null;
            }
        }
        else
        {
            // Wait indefinitely for a key press
            while (true)
            {
                if (Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space))
                    break;
                yield return null;
            }
        }

        yield return StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return StartCoroutine(Fade(hintCanvasGroup.alpha, 0f, fadeDuration));
        hintCanvasGroup.gameObject.SetActive(false);
        isShowing = false;
        onDismissed?.Invoke();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        hintCanvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            hintCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        hintCanvasGroup.alpha = to;
    }
}
