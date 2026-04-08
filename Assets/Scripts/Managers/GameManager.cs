using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game Manager — tracks overall game progress.
/// Counts how many people have been made happy, handles win/lose conditions.
/// Create an empty GameObject called "GameManager" and attach this script.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Level Goal")]
    public int totalUnhappyPeople = 0;   // Auto-counted at start
    public int peopleMadeHappy = 0;

    [Header("Game State")]
    public bool isGameOver = false;
    public bool isGameWon = false;

    [Header("UI Panels")]
    public GameObject winPanel;          // Assign a UI panel that shows "You Win!"
    public GameObject losePanel;         // Assign a UI panel that shows "Game Over"
    public GameObject pausePanel;        // Assign a UI panel for pause menu (optional)

    [Header("Audio")]
    public AudioClip winMusic;
    public AudioClip loseMusic;
    public AudioClip backgroundMusic;

    private AudioSource audioSource;
    private bool isPaused = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Count ALL unhappy people in the scene, including disabled ones
        // (NPCs start disabled and are activated later by EventManager).
        UnhappyPerson[] allPeople = FindObjectsByType<UnhappyPerson>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        totalUnhappyPeople = allPeople.Length;
        Debug.Log($"[GameManager] Found {totalUnhappyPeople} unhappy people (including disabled)");

        // Hide UI panels
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Play background music
        if (backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Update HUD with initial count
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdatePeopleCount(peopleMadeHappy, totalUnhappyPeople);

        // Lock and hide the cursor at game start
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Pause with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    [Header("Win Mode")]
    [Tooltip("If true, GameManager triggers WinGame when ALL unhappy people are happy. " +
             "Turn this OFF if you're using SectionTrackers — let the final section call WinGame() instead.")]
    public bool useGlobalWinCondition = false;

    /// <summary>
    /// Called by UnhappyPerson when they become happy.
    /// </summary>
    public void PersonMadeHappy()
    {
        if (isGameOver || isGameWon) return;

        peopleMadeHappy++;

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdatePeopleCount(peopleMadeHappy, totalUnhappyPeople);

        // Global win — only fires if you haven't switched to per-section tracking
        if (useGlobalWinCondition && peopleMadeHappy >= totalUnhappyPeople)
        {
            WinGame();
        }
    }

    /// <summary>
    /// Public so SectionTrackers (or any other system) can trigger the win screen
    /// when their own goal is met. Wire this to your final section's onSectionComplete.
    /// </summary>
    public void TriggerWin()
    {
        WinGame();
    }

    void WinGame()
    {
        isGameWon = true;

        // Show win UI
        if (winPanel != null) winPanel.SetActive(true);

        // Play win music
        if (winMusic != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(winMusic);
        }

        // Unlock cursor so player can click UI buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Slow down time (optional cinematic feel)
        Time.timeScale = 0.5f;
        Invoke(nameof(ResetTimeScale), 2f);
    }

    public void GameOver()
    {
        if (isGameOver || isGameWon) return;

        isGameOver = true;

        // Show lose UI
        if (losePanel != null) losePanel.SetActive(true);

        // Play lose music
        if (loseMusic != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(loseMusic);
        }

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel != null) pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;

        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;
    }

    void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    // ---- UI Button Methods (assign these to your UI buttons) ----

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
