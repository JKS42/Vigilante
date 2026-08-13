using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject NewGamePanel;
    public GameObject StartMenuPanel;
    public GameObject SettingsPanel;

    void Start()
    {
        AudioManager.EnsureExists();

        if (NewGamePanel != null) NewGamePanel.SetActive(false);
        if (SettingsPanel != null) SettingsPanel.SetActive(false);
        if (StartMenuPanel != null) StartMenuPanel.SetActive(true);

        if (GetComponent<LevelSelectUI>() == null)
            gameObject.AddComponent<LevelSelectUI>();

        SettingsMenu.EnsureOn(SettingsPanel);
        GameSettings.ApplyAll();
    }

    public void StartNewGame()
    {
        AudioManager.UIClick();
        if (NewGamePanel != null) NewGamePanel.SetActive(true);
        if (StartMenuPanel != null) StartMenuPanel.SetActive(false);
        if (SettingsPanel != null) SettingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        AudioManager.UIClick();
        if (SettingsPanel != null)
        {
            SettingsPanel.SetActive(true);
            SettingsMenu.EnsureOn(SettingsPanel);
        }
        if (StartMenuPanel != null) StartMenuPanel.SetActive(false);
        if (NewGamePanel != null) NewGamePanel.SetActive(false);
    }

    public void BackToMenu()
    {
        AudioManager.UIBack();
        if (SettingsPanel != null) SettingsPanel.SetActive(false);
        if (StartMenuPanel != null) StartMenuPanel.SetActive(true);
        if (NewGamePanel != null) NewGamePanel.SetActive(false);
    }

    /// <summary>Starts Level 1 tutorial (bat + pistol enemies).</summary>
    public void NewGame()
    {
        AudioManager.UIClick();
        GameProgression.StartLevel(1);
    }

    public void StartLevel1()
    {
        AudioManager.UIClick();
        GameProgression.StartLevel(1);
    }

    public void StartLevel2()
    {
        AudioManager.UIClick();
        if (GameProgression.UnlockedLevel < 2)
        {
            Debug.Log("Level 2 locked — finish Level 1 first.");
            // Allow free play during development.
        }
        GameProgression.StartLevel(2);
    }

    public void StartLevel3()
    {
        AudioManager.UIClick();
        if (GameProgression.UnlockedLevel < 3)
            Debug.Log("Level 3 locked — finish Level 2 first.");
        GameProgression.StartLevel(3);
    }

    public void QuitGame()
    {
        AudioManager.UIClick();
        Application.Quit();
    }

    public void MainMenuScene()
    {
        AudioManager.UIClick();
        SceneManager.LoadSceneAsync(0);
    }
}
