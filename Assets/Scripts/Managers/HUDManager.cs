using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// HUD Manager — handles all on-screen UI elements.
// Shows ammo count, love bomb count, happiness bar, people-made-happy counter,
// hint messages, and crosshair.

public class HUDManager : MonoBehaviour
{
    [Header("Ammo Display")]
    public Text ammoText;                // Shows "24 / 30"
    public Text reloadingText;           // Shows "Reloading..." (hidden by default)

    [Header("Love Bomb Display")]
    public Text bombText;                // Shows love bomb count

    [Header("Happiness Bar")]
    public Slider happinessSlider;       // A UI Slider representing player happiness
    public Image happinessFill;          // The fill image (to change color)

    [Header("People Counter")]
    public Text peopleCounterText;       // Shows "3 / 10 people made happy"

    [Header("Message Display")]
    public Text messageText;             // For hint messages and area names
    public float messageFadeSpeed = 2f;

    [Header("Crosshair")]
    public Image crosshairImage;         // Center-screen crosshair

    [Header("Damage Indicator")]
    public Image damageFlash;            // Full-screen red/blue flash when hit

    private Coroutine messageCoroutine;
    private Coroutine damageCoroutine;

    void Start()
    {
        // Initialize
        if (reloadingText != null) reloadingText.gameObject.SetActive(false);
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (damageFlash != null)
        {
            Color c = damageFlash.color;
            c.a = 0f;
            damageFlash.color = c;
        }
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
            ammoText.text = " " + current + " / " + max;
    }

    public void UpdateBombs(int current, int max)
    {
        if (bombText != null)
            bombText.text = " " + current;
    }

    public void ShowReloading(bool show)
    {
        if (reloadingText != null)
            reloadingText.gameObject.SetActive(show);
    }

    public void UpdateHappiness(int current, int max)
    {
        if (happinessSlider != null)
        {
            happinessSlider.maxValue = max;
            happinessSlider.value = current;
        }

        // Change color based on happiness level
        if (happinessFill != null)
        {
            float ratio = (float)current / max;
            if (ratio > 0.5f)
                happinessFill.color = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
            else
                happinessFill.color = Color.Lerp(Color.red, Color.yellow, ratio * 2f);
        }

        // Flash damage indicator when hit
        if (damageFlash != null)
        {
            if (damageCoroutine != null) StopCoroutine(damageCoroutine);
            damageCoroutine = StartCoroutine(FlashDamage());
        }
    }

    public void UpdatePeopleCount(int happy, int total)
    {
        if (peopleCounterText != null)
            peopleCounterText.text = "Make everyone happy " + happy + " / " + total;
    }

    /// <summary>
    /// One-parameter overload so UnityEvents can call this from the Inspector.
    /// Uses a default 3-second display duration.
    /// </summary>
    public void ShowMessage(string message)
    {
        ShowMessage(message, 3f);
    }

    public void ShowMessage(string message, float duration)
    {
        if (messageText == null) return;

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        messageCoroutine = StartCoroutine(DisplayMessage(message, duration));
    }

    IEnumerator DisplayMessage(string message, float duration)
    {
        messageText.gameObject.SetActive(true);
        messageText.text = message;

        // Fade in
        Color c = messageText.color;
        c.a = 0f;
        messageText.color = c;

        while (c.a < 1f)
        {
            c.a += Time.deltaTime * messageFadeSpeed;
            messageText.color = c;
            yield return null;
        }

        // Wait
        yield return new WaitForSeconds(duration);

        // Fade out
        while (c.a > 0f)
        {
            c.a -= Time.deltaTime * messageFadeSpeed;
            messageText.color = c;
            yield return null;
        }

        messageText.gameObject.SetActive(false);
    }

    IEnumerator FlashDamage()
    {
        Color c = damageFlash.color;
        c.a = 0.4f;
        damageFlash.color = c;

        while (c.a > 0f)
        {
            c.a -= Time.deltaTime * 2f;
            damageFlash.color = c;
            yield return null;
        }
    }
}
