using UnityEngine;
using System.Collections;

// Event Manager — controls scripted events in your level.
// A scripted event is triggered by a TriggerZone and orchestrates
// multiple things happening at once (spawn NPCs, play animations, shake camera, etc.)

public class EventManager : MonoBehaviour
{
    [Header("Event Settings")]
    [SerializeField] private string eventName = "Scripted Event";
    [SerializeField] private bool hasBeenTriggered = false;   // Prevents re-triggering
    [SerializeField] private bool canRetrigger = false;        // Set true if event can repeat

    [Header("Objects to Activate")]
    [SerializeField] private GameObject[] objectsToActivate;   // GameObjects to enable when event triggers

    [Header("Objects to Deactivate")]
    [SerializeField] private GameObject[] objectsToDeactivate; // GameObjects to disable (e.g., close a door behind player)

    [Header("NPCs to Spawn/Activate")]
    [SerializeField] private GameObject[] npcsToActivate;      // Pre-placed (disabled) NPCs to enable

    [Header("Camera Effects")]
    [SerializeField] private bool doCameraShake = false;
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip eventSound;             // Sound to play when event triggers
    [SerializeField] private AudioClip eventMusic;             // Music to switch to (optional)

    [Header("UI Message")]
    [SerializeField] private string hintMessage = "";          // Message to display on HUD
    [SerializeField] private float messageDuration = 4f;

    [Header("Timing")]
    [SerializeField] private float delayBeforeEvent = 0f;      // Delay after trigger before things happen
    [SerializeField] private float delayBetweenActions = 0.5f; // Stagger NPC activations for dramatic effect

    private AudioSource audioSource;

    void Start()
    {
        TryGetComponent(out audioSource);
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>
    /// Called by a TriggerZone when the player enters it.
    /// </summary>
    public void TriggerEvent()
    {
        if (hasBeenTriggered && !canRetrigger) return;
        hasBeenTriggered = true;

        StartCoroutine(ExecuteEvent());
    }

    IEnumerator ExecuteEvent()
    {
        // Wait for initial delay
        if (delayBeforeEvent > 0f)
            yield return new WaitForSeconds(delayBeforeEvent);

        // Play event sound
        if (eventSound != null)
            audioSource.PlayOneShot(eventSound);

        // Switch music
        if (eventMusic != null)
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                AudioSource gmAudio = gm.GetComponent<AudioSource>();
                if (gmAudio != null)
                {
                    gmAudio.clip = eventMusic;
                    gmAudio.Play();
                }
            }
        }

        // Activate objects
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null) obj.SetActive(true);
        }

        // Deactivate objects
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null) obj.SetActive(false);
        }

        // Camera shake
        if (doCameraShake)
        {
            StartCoroutine(CameraShake());
        }

        // Activate NPCs with staggered timing for dramatic effect
        foreach (GameObject npc in npcsToActivate)
        {
            if (npc != null)
            {
                npc.SetActive(true);
                yield return new WaitForSeconds(delayBetweenActions);
            }
        }

        // Show hint message
        if (!string.IsNullOrEmpty(hintMessage))
        {
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null) hud.ShowMessage(hintMessage, messageDuration);
        }
    }

    IEnumerator CameraShake()
    {
        if (Camera.main == null) yield break;
        Transform cam = Camera.main.transform;
        Vector3 originalPos = cam.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            cam.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.localPosition = originalPos;
    }

    /// <summary>
    /// Rewind this event (called by checkpoint system if player dies).
    /// </summary>
    public void RewindEvent()
    {
        hasBeenTriggered = false;

        // Deactivate NPCs that were spawned
        foreach (GameObject npc in npcsToActivate)
        {
            if (npc != null) npc.SetActive(false);
        }

        // Reactivate objects that were deactivated
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null) obj.SetActive(true);
        }

        // Deactivate objects that were activated
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null) obj.SetActive(false);
        }
    }
}
