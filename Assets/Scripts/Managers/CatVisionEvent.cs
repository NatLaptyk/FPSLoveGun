using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scripted event: when the player is surrounded in wave 3 and happiness drops
/// to 10%, they see a vision of their cat. A shockwave radiates outward converting
/// every NPC while the player is lifted into the air to watch from above. The
/// screen then fades to black and the player is teleported to the street for the
/// final boss fight.
///
/// SETUP:
/// 1. Attach to any persistent GameObject (e.g. "CatVisionEvent" empty).
/// 2. Build the Cat Vision Canvas (see instructions at bottom of this file).
/// 3. Assign all Inspector references.
/// 4. Teleport Destination — empty GameObject in the street where the boss fight
///    happens. Player arrives at its position facing its forward direction.
/// 5. Boss To Activate — the FinalBoss GameObject, DISABLED in the scene.
/// 6. Player Movement Script — drag your FPS movement script here so input is
///    blocked while the player is being lifted.
/// 7. Player Camera — drag the Transform that handles vertical look (usually
///    the child camera pivot, or the Main Camera itself).
/// </summary>
public class CatVisionEvent : MonoBehaviour
{
    // ── Trigger ────────────────────────────────────────────────────────────────
    [Header("Trigger")]
    [Tooltip("Which wave the event fires on.")]
    public int triggerOnWave = 3;

    [Tooltip("Health fraction (0–1) at which the event fires. 0.1 = 10 %.")]
    [Range(0.01f, 0.5f)]
    public float healthThreshold = 0.1f;

    // ── Scene References ───────────────────────────────────────────────────────
    [Header("Scene References")]
    public Section2Spawner  section2Spawner;
    public PlayerHealth     playerHealth;
    [Tooltip("The stadium MusicController. It will be faded out as soon as the cat vision starts.")]
    public MusicController  stadiumMusic;

    // ── Player Lift ────────────────────────────────────────────────────────────
    [Header("Player Lift")]
    [Tooltip("Your FPS movement/look script — disabled during lift so input is ignored.")]
    public MonoBehaviour playerMovementScript;

    [Tooltip("The Transform whose X rotation controls vertical camera look. " +
             "Usually the Main Camera or a child camera pivot.")]
    public Transform playerCamera;

    [Tooltip("How many world units the player rises during the shockwave.")]
    public float liftHeight = 14f;

    [Tooltip("Degrees the camera tilts downward at the peak of the lift. " +
             "60–75 gives a nice bird's-eye view without full 90°.")]
    [Range(30f, 90f)]
    public float cameraDownAngle = 65f;

    // ── Teleport & Boss ────────────────────────────────────────────────────────
    [Header("Teleport & Boss")]
    [Tooltip("Empty GameObject in the street. Player arrives at its position " +
             "facing its forward direction.")]
    public Transform teleportDestination;

    [Tooltip("FinalBoss GameObject — DISABLED in the scene. Activated just " +
             "before the player arrives.")]
    public GameObject bossToActivate;

    [Tooltip("MinimapMarker (Objective type) placed at the boss spawn location. " +
             "Keep it on a separate always-active GameObject — not on the boss itself, " +
             "since the boss starts disabled. Call Hide() on it when the boss is defeated.")]
    public MinimapMarker bossObjectiveMarker;

    // ── UI ─────────────────────────────────────────────────────────────────────
    [Header("Cat Vision UI")]
    public CanvasGroup catVisionGroup;
    public Image       catImage;
    public TMPro.TextMeshProUGUI captionText;

    [Tooltip("Full-screen BLACK image — used for vignette AND teleport blackout.")]
    public CanvasGroup vignetteGroup;

    [Tooltip("Full-screen WHITE image for the flash before the shockwave.")]
    public Image flashImage;

    // ── Shockwave ──────────────────────────────────────────────────────────────
    [Header("Shockwave")]
    public float shockwaveMaxRadius = 25f;
    public float shockwaveDuration  = 3f;
    [Tooltip("Love given to each NPC the wave touches. 999 = instant convert.")]
    public int   shockwaveLovePower = 999;
    public float ringWidth          = 0.4f;
    public Color ringColor          = new Color(1f, 0.9f, 0.3f, 1f);

    // ── Timing ─────────────────────────────────────────────────────────────────
    [Header("Timing")]
    public float vignetteFadeIn  = 0.8f;
    public float catFadeIn       = 0.6f;
    public float catHoldDuration = 2.8f;
    public float flashDuration   = 0.35f;
    public float catFadeOut      = 0.4f;
    public float teleportFadeOut = 0.6f;
    public float teleportFadeIn  = 0.8f;

    // ── Low-health Warning ─────────────────────────────────────────────────────
    [Header("Low Health Warning")]
    [Tooltip("CanvasGroup containing the 'Happiness level dangerously low!' text. " +
             "Flashes on screen when health first drops to 25%.")]
    public CanvasGroup warningGroup;

    [Tooltip("How many times the warning flashes.")]
    public int warningFlashCount = 3;

    // ── Audio ──────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioClip visionStartSound;
    public AudioClip shockwaveSound;
    public AudioClip warningSound;   // optional sting/alarm for the warning

    private AudioSource audioSource;
    private bool        hasTriggered = false;

    // ──────────────────────────────────────────────────────────────────────────
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (catVisionGroup != null) { catVisionGroup.alpha = 0f; catVisionGroup.gameObject.SetActive(false); }
        if (vignetteGroup  != null) { vignetteGroup.alpha  = 0f; vignetteGroup.gameObject.SetActive(false); }
        if (flashImage     != null) { SetAlpha(flashImage, 0f);  flashImage.gameObject.SetActive(false); }
        if (captionText    != null) SetAlpha(captionText, 0f);
        if (warningGroup   != null) { warningGroup.alpha   = 0f; warningGroup.gameObject.SetActive(false); }

        if (playerHealth != null)
        {
            playerHealth.onLowHealth += ShowLowHealthWarning;
            playerHealth.onNearDeath += TriggerCatVision;
        }
        else
            Debug.LogError("[CatVision] PlayerHealth reference not assigned!");
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onLowHealth -= ShowLowHealthWarning;
            playerHealth.onNearDeath -= TriggerCatVision;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    void ShowLowHealthWarning()
    {
        if (warningGroup == null) return;
        PlaySound(warningSound);
        StartCoroutine(FlashWarning());
    }

    IEnumerator FlashWarning()
    {
        warningGroup.gameObject.SetActive(true);

        for (int i = 0; i < warningFlashCount; i++)
        {
            // Fade in
            yield return StartCoroutine(FadeCanvasGroup(warningGroup, 0f, 1f, 0.25f));
            yield return new WaitForSecondsRealtime(0.5f);
            // Fade out
            yield return StartCoroutine(FadeCanvasGroup(warningGroup, 1f, 0f, 0.25f));
            yield return new WaitForSecondsRealtime(0.2f);
        }

        warningGroup.gameObject.SetActive(false);
    }

    void TriggerCatVision()
    {
        if (hasTriggered) return;

        hasTriggered = true;
        playerHealth.invincible = true;
        if (playerHealth.currentHappiness <= 0)
            playerHealth.currentHappiness = 1;

        Debug.Log("[CatVision] Triggered — starting vision sequence.");
        StartCoroutine(CatVisionSequence());
    }

    // ──────────────────────────────────────────────────────────────────────────
    IEnumerator CatVisionSequence()
    {
        // ── 1. Slow time + fade out stadium music ─────────────────────────────
        Time.timeScale = 0.15f;
        if (stadiumMusic != null) stadiumMusic.FadeOut();
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

        // ── 4. Caption ────────────────────────────────────────────────────────
        if (captionText != null)
            yield return StartCoroutine(FadeGraphic(captionText, 0f, 1f, 0.4f));

        // ── 5. Hold ───────────────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(catHoldDuration);

        // ── 6. Caption + cat fade out ─────────────────────────────────────────
        if (captionText != null)
            yield return StartCoroutine(FadeGraphic(captionText, 1f, 0f, 0.25f));

        if (catVisionGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(catVisionGroup, 1f, 0f, catFadeOut));
            catVisionGroup.gameObject.SetActive(false);
        }

        // ── 7. Restore time + heal ────────────────────────────────────────────
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

        // Partially clear vignette so the player can see the shockwave
        if (vignetteGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(vignetteGroup, 0.75f, 0.0f, 0.3f));

        // ── 9. Shockwave + player lift + NPC conversion ───────────────────────
        PlaySound(shockwaveSound);
        yield return StartCoroutine(ShockwaveAndLift());

        // ── 10. Fade to full black for teleport ───────────────────────────────
        if (vignetteGroup != null)
        {
            vignetteGroup.gameObject.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(vignetteGroup, 0f, 1f, teleportFadeOut));
        }

        // ── 11. Teleport player + activate boss ───────────────────────────────
        if (teleportDestination != null)
            TeleportPlayer(teleportDestination.position, teleportDestination.rotation);
        else
            Debug.LogWarning("[CatVision] No teleport destination assigned!");

        if (bossToActivate != null)
            bossToActivate.SetActive(true);
        else
            Debug.LogWarning("[CatVision] No boss assigned to activate!");

        // Show the boss location as the new minimap objective.
        // MinimapDirectionArrow will auto-switch to it within 0.5s.
        if (bossObjectiveMarker != null)
            bossObjectiveMarker.Show();
        else
            Debug.LogWarning("[CatVision] No boss objective marker assigned!");

        // ── 12. Restore player controls (camera rotation, movement) ───────────
        RestorePlayerControls();

        yield return new WaitForSecondsRealtime(0.2f);

        // ── 13. Fade from black ───────────────────────────────────────────────
        if (vignetteGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(vignetteGroup, 1f, 0f, teleportFadeIn));
            vignetteGroup.gameObject.SetActive(false);
        }

        // ── 14. Re-enable normal damage ───────────────────────────────────────
        playerHealth.invincible = false;

        Debug.Log("[CatVision] Complete — player is in the street, boss is active.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Simultaneously expands the shockwave ring (converting NPCs as it goes)
    /// and lifts the player upward so they can watch the conversion from above.
    /// </summary>
    IEnumerator ShockwaveAndLift()
    {
        // ── Disable player input so they float freely ─────────────────────────
        CharacterController cc = playerHealth.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;
        if (playerMovementScript != null) playerMovementScript.enabled = false;

        // Cache original camera angle so we can restore it after teleport
        Quaternion originalCamRotation = playerCamera != null
            ? playerCamera.localRotation
            : Quaternion.identity;

        // Target camera rotation: tilt downward by cameraDownAngle degrees
        Quaternion downRotation = Quaternion.Euler(cameraDownAngle, 0f, 0f);

        // ── Build procedural shockwave ring ───────────────────────────────────
        GameObject ringObj = new GameObject("HappinessShockwaveRing");
        LineRenderer ring = ringObj.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop          = true;
        ring.positionCount = 64;
        ring.startWidth    = ringWidth;
        ring.endWidth      = ringWidth * 0.2f;

        Material ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.color    = ringColor;
        ring.material    = ringMat;
        ring.startColor  = ringColor;
        ring.endColor    = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);

        // Ring expands from where the PLAYER started (before they rise)
        Vector3 shockwaveOrigin = playerHealth.transform.position;
        float   ringY           = shockwaveOrigin.y + 0.1f;

        Vector3 liftStart = playerHealth.transform.position;
        Vector3 liftEnd   = liftStart + Vector3.up * liftHeight;

        HashSet<UnhappyPerson> converted = new HashSet<UnhappyPerson>();

        float elapsed = 0f;
        while (elapsed < shockwaveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shockwaveDuration);

            // ── Lift player smoothly upward ───────────────────────────────────
            // Use SmoothStep for a natural ease-out feel at the top
            float liftT = Mathf.SmoothStep(0f, 1f, t);
            playerHealth.transform.position = Vector3.Lerp(liftStart, liftEnd, liftT);

            // ── Tilt camera to look down as player rises ──────────────────────
            if (playerCamera != null)
                playerCamera.localRotation = Quaternion.Slerp(
                    originalCamRotation, downRotation, liftT);

            // ── Expand ring from original ground position ─────────────────────
            float radius = Mathf.Lerp(0f, shockwaveMaxRadius, t);
            UpdateRingPositions(ring, shockwaveOrigin, ringY, radius);

            Color c = Color.Lerp(ringColor,
                new Color(ringColor.r, ringColor.g, ringColor.b, 0f), t);
            ring.startColor = c;
            ring.endColor   = new Color(c.r, c.g, c.b, 0f);

            // ── Convert NPCs as the ring sweeps over them ─────────────────────
            Collider[] hits = Physics.OverlapSphere(shockwaveOrigin, radius);
            foreach (var col in hits)
            {
                UnhappyPerson npc = col.GetComponentInParent<UnhappyPerson>();
                if (npc != null && !converted.Contains(npc)
                    && npc.currentMood == UnhappyPerson.MoodState.Unhappy)
                {
                    converted.Add(npc);
                    npc.ReceiveLove(shockwaveLovePower);
                }
            }

            yield return null;
        }

        Destroy(ringObj);

        // Hold at the top for a moment so the player can appreciate the view
        yield return new WaitForSeconds(0.8f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        CharacterController cc = playerHealth.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerHealth.transform.position = position;
        playerHealth.transform.rotation = rotation;

        if (cc != null) cc.enabled = true;

        Rigidbody rb = playerHealth.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[CatVision] Player teleported to {position}.");
    }

    void RestorePlayerControls()
    {
        // Re-enable movement script
        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        // Reset camera to level (horizontal) so the player doesn't arrive
        // staring at the ground
        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.identity;
    }

    // ──────────────────────────────────────────────────────────────────────────
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

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed    += Time.unscaledDeltaTime;
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
        c.a     = alpha;
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
  CANVAS SETUP
════════════════════════════════════════════════════════

Canvas (Screen Space – Overlay, Sort Order 99):

  ┌─ CatVisionCanvas
  │   ├─ Vignette          (Image, solid BLACK, full-screen, alpha 0)
  │   │                      + CanvasGroup → "Vignette Group"
  │   │                      Used for both the vision darkening AND the
  │   │                      black teleport transition. Must be solid black.
  │   │
  │   ├─ CatVisionPanel    (CanvasGroup, alpha 0)
  │   │   ├─ Background    (Image, dark colour, full-screen, alpha ~0.6)
  │   │   ├─ CatImage      (Image, cat sprite, centred, ~400×400 px)
  │   │   └─ Caption       (TextMeshPro – "You are not alone…")
  │   │
  │   └─ Flash             (Image, solid WHITE, full-screen, alpha 0)

════════════════════════════════════════════════════════
  INSPECTOR CHECKLIST
════════════════════════════════════════════════════════

  Section2Spawner       → your Section2Spawner component
  Player Health         → PlayerHealth on the Player GameObject
  Player Movement Script→ your FPS movement/look script (e.g. FirstPersonController)
  Player Camera         → the Transform that handles vertical look
                          (drag the Main Camera, or its camera-pivot parent)
  Teleport Destination  → empty GameObject placed in the street
  Boss To Activate      → FinalBoss GameObject (must be DISABLED in the scene)
  Cat Vision Group      → CatVisionPanel CanvasGroup
  Vignette Group        → Vignette CanvasGroup
  Flash Image           → Flash Image component

════════════════════════════════════════════════════════
*/
