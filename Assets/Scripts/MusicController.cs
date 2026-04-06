using UnityEngine;

/// <summary>
/// Plays a music track starting at a specific timestamp.
/// Designed to plug into the existing EventManager system:
///
/// SETUP:
/// 1. Create an empty GameObject named e.g. "Section1_Music"
/// 2. Add an AudioSource — drag your song into AudioClip, uncheck Play On Awake
/// 3. Add this script — set Start Time Seconds to where "Heart over mind..." begins
/// 4. DISABLE the GameObject in the Inspector
/// 5. In your Section 1 EventManager, add this GameObject to "Objects To Activate"
///
/// When the player walks through the cafe-door TriggerZone, EventManager
/// will enable this object and the music will start automatically from the mark.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicController : MonoBehaviour
{
    [Header("Playback")]
    [Tooltip("Time in seconds where the music should start. " +
             "Find this by playing the track in Audacity and noting the timestamp.")]
    public float startTimeSeconds = 0f;

    [Range(0f, 1f)]
    public float playVolume = 0.7f;

    [Tooltip("Optional delay before music begins after activation")]
    public float startDelay = 0f;

    [Header("Fade Out")]
    [Tooltip("Auto-fade out after N seconds of playback (0 = no auto-fade)")]
    public float autoFadeAfter = 0f;
    public float fadeDuration = 2f;

    private AudioSource audioSource;
    private bool isFading = false;
    private float fadeSpeed;
    private float playStartTime;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Called automatically when EventManager enables this GameObject.
    /// </summary>
    void OnEnable()
    {
        if (startDelay > 0f)
            Invoke(nameof(StartPlayback), startDelay);
        else
            StartPlayback();
    }

    void StartPlayback()
    {
        audioSource.time = startTimeSeconds;
        audioSource.volume = playVolume;
        audioSource.Play();
        playStartTime = Time.time;
        Debug.Log($"[Music] Playing '{audioSource.clip?.name}' from {startTimeSeconds}s");
    }

    void Update()
    {
        if (autoFadeAfter > 0f && !isFading && Time.time - playStartTime >= autoFadeAfter)
            FadeOut(fadeDuration);

        if (isFading)
        {
            audioSource.volume -= fadeSpeed * Time.deltaTime;
            if (audioSource.volume <= 0f)
            {
                audioSource.Stop();
                isFading = false;
            }
        }
    }

    /// <summary>
    /// Call externally to fade out early (e.g. on section complete).
    /// </summary>
    public void FadeOut(float duration)
    {
        if (!audioSource.isPlaying) return;
        isFading = true;
        fadeSpeed = audioSource.volume / Mathf.Max(0.01f, duration);
    }
}
