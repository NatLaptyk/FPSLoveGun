using UnityEngine;

/// <summary>
/// Player Health (Happiness Meter)
/// The player starts with full happiness. Getting hit by sadness reduces it.
/// If happiness reaches 0, the player becomes too sad and it's game over.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Happiness")]
    public int maxHappiness = 100;
    public int currentHappiness = 100;

    [Header("Visuals")]
    public Color normalScreenColor = Color.clear;
    public Color sadScreenColor = new Color(0f, 0f, 0.3f, 0.3f); // Blue overlay when sad

    [Header("Audio")]
    public AudioClip hurtSound;
    public AudioClip healSound;

    private AudioSource audioSource;

    /// <summary>
    /// While true, sadness damage is absorbed — health cannot drop to 0 and
    /// GameOver cannot fire. Set by CatVisionEvent during the scripted sequence.
    /// </summary>
    [HideInInspector] public bool invincible = false;

    /// <summary>
    /// Fires synchronously the first time health drops to ≤ lowHealthThreshold (25 %).
    /// Used to show the "Happiness level dangerously low!" warning.
    /// </summary>
    public System.Action onLowHealth;
    [HideInInspector] public float lowHealthThreshold = 0.25f;
    private bool lowHealthFired = false;

    /// <summary>
    /// Fires synchronously inside TakeSadness the first time health crosses
    /// below the near-death threshold (10 %). CatVisionEvent subscribes to this
    /// so it triggers in the same frame as the lethal hit, before GameOver runs.
    /// </summary>
    public System.Action onNearDeath;
    private bool nearDeathFired = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        currentHappiness = maxHappiness;
    }

    /// <summary>
    /// Called when hit by a sadness projectile.
    /// </summary>
    public void TakeSadness(int amount)
    {
        if (invincible) return;

        bool wasAboveWarning  = currentHappiness > maxHappiness * lowHealthThreshold;
        bool wasAboveNearDeath = currentHappiness > maxHappiness * 0.1f;

        currentHappiness -= amount;
        currentHappiness = Mathf.Max(currentHappiness, 0);

        // Play hurt sound
        if (hurtSound != null)
            audioSource.PlayOneShot(hurtSound);

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateHappiness(currentHappiness, maxHappiness);

        // Fire low-health warning the first time health drops to ≤ 25 %
        if (wasAboveWarning && currentHappiness <= maxHappiness * lowHealthThreshold && !lowHealthFired)
        {
            lowHealthFired = true;
            onLowHealth?.Invoke();
        }

        // Fire the near-death callback the first time health drops to ≤ 10 %.
        // This lets CatVisionEvent intercept synchronously — before GameOver runs.
        if (wasAboveNearDeath && currentHappiness <= maxHappiness * 0.1f && !nearDeathFired)
        {
            nearDeathFired = true;
            onNearDeath?.Invoke();
        }

        // Check for game over — skipped if CatVisionEvent set invincible above
        if (currentHappiness <= 0 && !invincible)
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.GameOver();
        }
    }

    /// <summary>
    /// Restore happiness (from pickups or friendly NPCs).
    /// </summary>
    public void Heal(int amount)
    {
        currentHappiness += amount;
        currentHappiness = Mathf.Min(currentHappiness, maxHappiness);

        if (healSound != null)
            audioSource.PlayOneShot(healSound);

        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateHappiness(currentHappiness, maxHappiness);
    }
}
