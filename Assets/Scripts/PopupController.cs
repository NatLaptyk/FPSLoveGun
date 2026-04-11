using UnityEngine;

public class PopupController : MonoBehaviour
{
    public GameObject popupPanel;
    public GameObject pausePanel;

    void Start()
    {
        Time.timeScale = 0f;
        popupPanel.SetActive(true);
        pausePanel.SetActive(false);
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
    }

    public void OnOKButtonClicked()
    {
        popupPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void PauseGame()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
