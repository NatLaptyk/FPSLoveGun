using UnityEngine;

/// <summary>
/// Player Health (Happiness Meter)
/// The player starts with full happiness. Getting hit by sadness reduces it.
/// If happiness reaches 0, the player becomes too sad and it's game over.
/// Attach to the Player GameObject.
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
        currentHappiness -= amount;
        currentHappiness = Mathf.Max(currentHappiness, 0);

        // Play hurt sound
        if (hurtSound != null)
            audioSource.PlayOneShot(hurtSound);

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateHappiness(currentHappiness, maxHappiness);

        // Check for game over
        if (currentHappiness <= 0)
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
