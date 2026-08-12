using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject NewGamePanel;
    public GameObject StartMenuPanel;
    public GameObject SettingsPanel;
    void Start()
    {
        NewGamePanel.SetActive(false);
        SettingsPanel.SetActive(false);
        StartMenuPanel.SetActive(true);
    }
    public void StartNewGame()
    {
        NewGamePanel.SetActive(true);
        StartMenuPanel.SetActive(false);
        SettingsPanel.SetActive(false);
    }
    public void OpenSettings()
    {
        SettingsPanel.SetActive(true);
        StartMenuPanel.SetActive(false);
        NewGamePanel.SetActive(false);
    }
    public void BackToMenu()
    {
        SettingsPanel.SetActive(false);
        StartMenuPanel.SetActive(true);
        NewGamePanel.SetActive(false);
    }
    public void NewGame()
    {
        SceneManager.LoadSceneAsync(1);
    }
    public void QuitGame()
    {
        Application.Quit();
    }
    public void MainMenuScene()
    {
        SceneManager.LoadSceneAsync(0);
    }

}
