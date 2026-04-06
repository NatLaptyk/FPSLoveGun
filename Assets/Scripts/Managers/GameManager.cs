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

        // Count all unhappy people in the scene
        UnhappyPerson[] allPeople = FindObjectsByType<UnhappyPerson>(FindObjectsSortMode.None);
        totalUnhappyPeople = allPeople.Length;

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
    }

    void Update()
    {
        // Pause with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

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

        // Check win condition
        if (peopleMadeHappy >= totalUnhappyPeople)
        {
            WinGame();
        }
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
