using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    
    public string sceneToLoad = "GameScene";

    public void OnPlayButtonClicked()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    public void OnExitButtonClicked()
    {
        Debug.Log("Quitting game");
        Application.Quit();
    }
}