using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scripted event: when the player is surrounded in wave 3 and their happiness
/// drops to 10%, they see a vision of their cat — gaining a burst of joy that
/// radiates outward as a shockwave, instantly converting every NPC it touches.
///
/// SETUP:
/// 1. Attach this script to any persistent GameObject (e.g. GameManager or a
///    dedicated "CatVisionEvent" empty object).
/// 2. Build the Cat Vision Canvas (see instructions at bottom of this file).
/// 3. Assign all Inspector references.
/// 4. Assign the cat sprite to the CatImage component.
/// </summary>
public class CatVisionEvent : MonoBehaviour
{
    // ── Trigger ────────────────────────────────────────────────────────────────
    [Header("Trigger")]
    [Tooltip("Which wave the event can fire on.")]
    public int triggerOnWave = 3;

    [Tooltip("Health fraction (0–1) at which the event fires. 0.1 = 10 %.")]
    [Range(0.01f, 0.5f)]
    public float healthThreshold = 0.1f;

    // ── Scene References ───────────────────────────────────────────────────────
    [Header("Scene References")]
    public Section2Spawner section2Spawner;
    public PlayerHealth playerHealth;       // The player's happiness/health component

    // ── UI ─────────────────────────────────────────────────────────────────────
    [Header("Cat Vision UI")]
    [Tooltip("Root CanvasGroup of the cat vision overlay panel.")]
    public CanvasGroup catVisionGroup;

    [Tooltip("The cat image inside the panel.")]
    public Image catImage;

    [Tooltip("Optional caption text (e.g. 'You are not alone…').")]
    public TMPro.TextMeshProUGUI captionText;

    [Tooltip("A full-screen dark Image used for the closing-in vignette.")]
    public CanvasGroup vignetteGroup;

    [Tooltip("A full-screen white Image for the flash just before the shockwave.")]
    public Image flashImage;

    // ── Shockwave ──────────────────────────────────────────────────────────────
    [Header("Shockwave")]
    [Tooltip("Maximum radius the happiness shockwave reaches.")]
    public float shockwaveMaxRadius = 25f;

    [Tooltip("Seconds for the shockwave to expand to max radius.")]
    public float shockwaveDuration = 2.5f;

    [Tooltip("Love applied to each NPC the shockwave touches. 999 = instant convert.")]
    public int shockwaveLovePower = 999;

    [Tooltip("Width of the visual shockwave ring line.")]
    public float ringWidth = 0.4f;

    [Tooltip("Color of the expanding ring.")]
    public Color ringColor = new Color(1f, 0.9f, 0.3f, 1f);

    // ── Timing ─────────────────────────────────────────────────────────────────
    [Header("Timing")]
    public float vignetteFadeIn    = 0.8f;   // seconds to darken the screen
    public float catFadeIn         = 0.6f;   // seconds for cat image to appear
    public float catHoldDuration   = 2.8f;   // seconds the vision is held
    public float flashDuration     = 0.35f;  // white flash before shockwave
    public float catFadeOut        = 0.4f;   // seconds for cat image to disappear

    // ── Audio ──────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("Soft sound when the vision begins (heartbeat, purring, etc.)")]
    public AudioClip visionStartSound;

    [Tooltip("Impact sound for the shockwave burst.")]
    public AudioClip shockwaveSound;

    private AudioSource audioSource;

    // ── State ──────────────────────────────────────────────────────────────────
    private bool hasTriggered = false;

    // ──────────────────────────────────────────────────────────────────────────
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Hide all UI elements at start
        if (catVisionGroup  != null) { catVisionGroup.alpha  = 0f; catVisionGroup.gameObject.SetActive(false); }
        if (vignetteGroup   != null) { vignetteGroup.alpha   = 0f; vignetteGroup.gameObject.SetActive(false); }
        if (flashImage      != null) { SetAlpha(flashImage, 0f);   flashImage.gameObject.SetActive(false); }
        if (captionText     != null) SetAlpha(captionText, 0f);
    }

    void Update()
    {
        if (hasTriggered) return;
        if (section2Spawner == null || playerHealth == null) return;

        // Wait for wave 3
        if (section2Spawner.currentWave < triggerOnWave) return;

        // Check health threshold
        float healthPct = playerHealth.currentHappiness / (float)playerHealth.maxHappiness;
        if (healthPct <= healthThreshold)
        {
            hasTriggered = true;
            StartCoroutine(CatVisionSequence());
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    IEnumerator CatVisionSequence()
    {
        Debug.Log("[CatVision] Event triggered — player needs their cat.");

        // ── 1. Slow time & play vision sound ──────────────────────────────────
        Time.timeScale = 0.15f;
        PlaySound(visionStartSound);

        // ── 2. Vignette closes in ─────────────────────────────────────────────
        if (vignetteGroup != null)
        {
            vignetteGroup.gameObject.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(vignetteGroup, 0f, 0.75f, vignetteFadeIn));
        }

        // ── 3. Cat image fades in ─────────────────────────────────────────────
        if (catVisionGroup != null)
        {
            catVisionGroup.gameObject.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(catVisionGroup, 0f, 1f, catFadeIn));
        }

        // ── 4. Caption appears ────────────────────────────────────────────────
        if (captionText != null)
            yield return StartCoroutine(FadeGraphic(captionText, 0f, 1f, 0.4f));

        // ── 5. Hold the vision ────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(catHoldDuration);

        // ── 6. Caption fades out, cat image fades out ─────────────────────────
        if (captionText != null)
            yield return StartCoroutine(FadeGraphic(captionText, 1f, 0f, 0.25f));

        if (catVisionGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(catVisionGroup, 1f, 0f, catFadeOut));

        // ── 7. Restore time & heal the player to full ─────────────────────────
        Time.timeScale = 1f;
        playerHealth.Heal(playerHealth.maxHappiness);

        // ── 8. White flash ────────────────────────────────────────────────────
        if (flashImage != null)
        {
            flashImage.gameObject.SetActive(true);
            yield return StartCoroutine(FadeGraphic(flashImage, 0f, 1f, flashDuration * 0.3f));
            yield return StartCoroutine(FadeGraphic(flashImage, 1f, 0f, flashDuration * 0.7f));
            flashImage.gameObject.SetActive(false);
        }

        // Hide vignette
        if (vignetteGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(vignetteGroup, 0.75f, 0f, 0.3f));
            vignetteGroup.gameObject.SetActive(false);
        }
        if (catVisionGroup != null)
            catVisionGroup.gameObject.SetActive(false);

        // ── 9. Shockwave bursts outward ───────────────────────────────────────
        PlaySound(shockwaveSound);
        yield return StartCoroutine(ExpandShockwave());

        Debug.Log("[CatVision] Event complete.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    IEnumerator ExpandShockwave()
    {
        // Build a procedural LineRenderer ring
        GameObject ringObj = new GameObject("HappinessShockwaveRing");
        LineRenderer ring = ringObj.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.startWidth = ringWidth;
        ring.endWidth = ringWidth * 0.2f;

        Material ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.color = ringColor;
        ring.material = ringMat;
        ring.startColor = ringColor;
        ring.endColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);

        Vector3 origin = playerHealth.transform.position;
        float ringY = origin.y + 0.1f;  // just above ground

        HashSet<UnhappyPerson> alreadyConverted = new HashSet<UnhappyPerson>();

        float elapsed = 0f;
        while (elapsed < shockwaveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shockwaveDuration;
            float radius = Mathf.Lerp(0f, shockwaveMaxRadius, t);

            // Update ring positions
            UpdateRingPositions(ring, origin, ringY, radius);

            // Fade ring out as it expands
            Color c = Color.Lerp(ringColor, new Color(ringColor.r, ringColor.g, ringColor.b, 0f), t);
            ring.startColor = c;
            ring.endColor = new Color(c.r, c.g, c.b, 0f);

            // Hit any NPC the wave has reached that hasn't been converted yet
            Collider[] hits = Physics.OverlapSphere(origin, radius);
            foreach (var col in hits)
            {
                UnhappyPerson npc = col.GetComponentInParent<UnhappyPerson>();
                if (npc != null && !alreadyConverted.Contains(npc)
                    && npc.currentMood == UnhappyPerson.MoodState.Unhappy)
                {
                    alreadyConverted.Add(npc);
                    npc.ReceiveLove(shockwaveLovePower);
                }
            }

            yield return null;
        }

        Destroy(ringObj);
    }

    void UpdateRingPositions(LineRenderer lr, Vector3 center, float y, float radius)
    {
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                y,
                center.z + Mathf.Sin(angle) * radius));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // Uses unscaled time so the fade works even while Time.timeScale is 0.15
    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    IEnumerator FadeGraphic(Graphic graphic, float from, float to, float duration)
    {
        float elapsed = 0f;
        SetAlpha(graphic, from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(graphic, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetAlpha(graphic, to);
    }

    void SetAlpha(Graphic graphic, float alpha)
    {
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}

/*
════════════════════════════════════════════════════════
  CANVAS SETUP INSTRUCTIONS
════════════════════════════════════════════════════════

Create a new Canvas (Screen Space — Overlay, Sort Order 99):

  ┌─ CatVisionCanvas
  │   ├─ Vignette          (Image, black, full-screen, alpha 0)
  │   │                      → assign to "Vignette Group" (CanvasGroup)
  │   │
  │   ├─ CatVisionPanel    (CanvasGroup, alpha 0)
  │   │   ├─ Background    (Image, soft dark colour, full-screen, alpha ~0.6)
  │   │   ├─ CatImage      (Image, your cat sprite, centred, ~400×400 px)
  │   │   └─ Caption       (TextMeshPro, "You are not alone…", centred below cat)
  │   │
  │   └─ Flash             (Image, white, full-screen, alpha 0)

Assign:
  • CatVisionGroup  →  CatVisionPanel's CanvasGroup
  • CatImage        →  CatImage's Image component
  • CaptionText     →  Caption's TextMeshProUGUI
  • VignetteGroup   →  Vignette's CanvasGroup
  • FlashImage      →  Flash's Image component

════════════════════════════════════════════════════════
*/
