using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PopupController : MonoBehaviour
{
    public GameObject popupPanel;
    public GameObject pausePanel;

    [Tooltip("Drag the Menu button RectTransform here.")]
    public RectTransform menuButton;
    [Tooltip("Drag the Exit button RectTransform here.")]
    public RectTransform exitButton;

    [Tooltip("Exact name of your Main Menu scene as it appears in Build Settings.")]
    public string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Read by PlayerController (and any other script) to suppress input while paused.
    /// </summary>
    public static bool IsPaused { get; private set; } = false;

    void Awake()
    {
        // ── Diagnose and self-heal the canvas setup ───────────────────────────
        // GraphicRaycaster is required for ANY button on this canvas to be
        // clickable. If it's missing, add it automatically and log the fix.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.LogWarning("[PopupController] GraphicRaycaster was MISSING from '" +
                                 canvas.name + "' — added it automatically. " +
                                 "This was why buttons were not clickable.");
            }
            else
            {
                Debug.Log("[PopupController] GraphicRaycaster OK on '" + canvas.name + "'.");
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning("[PopupController] Canvas '" + canvas.name +
                                 "' render mode is '" + canvas.renderMode +
                                 "' — changing to ScreenSpaceOverlay so UI is always on top.");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            else
            {
                Debug.Log("[PopupController] Canvas render mode OK (ScreenSpaceOverlay).");
            }
        }
        else
        {
            Debug.LogError("[PopupController] No Canvas found in scene! UI will not work.");
        }

        // Make sure an EventSystem exists
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            Debug.LogError("[PopupController] No EventSystem in scene! Add one via GameObject → UI → EventSystem.");
        else
            Debug.Log("[PopupController] EventSystem OK.");
    }

    void Start()
    {
        IsPaused = false;
        Time.timeScale = 0f;
        popupPanel.SetActive(true);
        pausePanel.SetActive(false);

        // Show cursor for the intro popup
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void Update()
    {
        // Close intro popup on Enter
        if (popupPanel.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OnOKButtonClicked();
        }

        // Toggle pause panel on ESC (only when intro popup is closed)
        if (!popupPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            if (pausePanel.activeSelf)
                ResumeGame();
            else
                PauseGame();
        }

        // Close pause panel on Enter
        if (pausePanel.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            ResumeGame();
        }

        // ── Direct mouse-position button detection ────────────────────────────
        // Bypasses the EventSystem / GraphicRaycaster pipeline entirely so the
        // buttons work regardless of any Canvas Group, raycast blocker, or other
        // UI element that might be intercepting clicks.
        if (pausePanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Input.mousePosition;

            if (menuButton != null &&
                RectTransformUtility.RectangleContainsScreenPoint(menuButton, mousePos))
            {
                Debug.Log("[PopupController] Menu button clicked (direct detection).");
                GoToMainMenu();
            }
            else if (exitButton != null &&
                     RectTransformUtility.RectangleContainsScreenPoint(exitButton, mousePos))
            {
                Debug.Log("[PopupController] Exit button clicked (direct detection).");
                QuitGame();
            }
        }
    }

    public void OnOKButtonClicked()
    {
        popupPanel.SetActive(false);
        Time.timeScale = 1f;

        // Lock cursor for FPS gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    public void PauseGame()
    {
        IsPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;

        // Unlock cursor so the player can click the buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;

        // Re-lock cursor for FPS gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    /// <summary>Wire to the Menu button's OnClick.</summary>
    public void GoToMainMenu()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Wire to the Exit button's OnClick.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
