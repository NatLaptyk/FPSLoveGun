using UnityEngine;

public class SectionCompletePopup : MonoBehaviour
{
    public GameObject sectionCompletePanel;

    void Start()
    {
        sectionCompletePanel.SetActive(false);
    }

    void Update()
    {
        // Close section complete popup on Enter
        if (sectionCompletePanel.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OnSectionCompleteOKClicked();
        }
    }

    public void OnSectionComplete()
    {
        sectionCompletePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnSectionCompleteOKClicked()
    {
        sectionCompletePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
