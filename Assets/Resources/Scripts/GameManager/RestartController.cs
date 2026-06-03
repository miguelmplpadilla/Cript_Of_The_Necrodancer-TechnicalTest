using Resources.Scripts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RestartController : MonoBehaviour
{
    public static RestartController instance;
    
    public Button buttonRestartLevel;
    public Button buttonReturnMainMenu;

    private void OnEnable()
    {
        instance = this;
    }

    private void Start()
    {
        buttonRestartLevel.onClick.AddListener(RestartLevel);
        buttonReturnMainMenu.onClick.AddListener(ReturnMainMenu);
    }

    private void OnDestroy()
    {
        buttonRestartLevel.onClick.RemoveListener(RestartLevel);
        buttonReturnMainMenu.onClick.RemoveListener(ReturnMainMenu);
    }

    public void OpenRestartLevel()
    {
        Time.timeScale = 0;
        transform.localScale = Vector3.one;
    }

    private void RestartLevel()
    {
        PlayerPrefs.DeleteAll();
        StartCoroutine(GameManager.instance.RestartLevel(false));
    }

    private void ReturnMainMenu()
    {
        PlayerPrefs.SetFloat("playerLife", PlayerController.instance.maxLife);
        SceneManager.LoadScene("MainMenu");
    }
}
