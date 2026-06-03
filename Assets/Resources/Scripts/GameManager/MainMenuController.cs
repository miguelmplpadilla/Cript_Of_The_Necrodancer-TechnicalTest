using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public Button buttonPlayGame;
    public Button buttonCloseGame;

    private void Start()
    {
        buttonPlayGame.onClick.AddListener(StartGame);
        buttonCloseGame.onClick.AddListener(CloseGame);
        
        Time.timeScale = 1;
        PlayerPrefs.DeleteAll();
    }

    private void OnDestroy()
    {
        buttonPlayGame.onClick.RemoveListener(StartGame);
        buttonCloseGame.onClick.RemoveListener(CloseGame);
    }

    private void StartGame()
    {
        SceneManager.LoadScene("Game");
    }

    private void CloseGame()
    {
        Application.Quit();
    }
}
