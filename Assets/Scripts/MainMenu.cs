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

        WirePanelButtons();
        SettingsMenu.EnsureOn(SettingsPanel);
        StyleMenuChrome();
        SceneFade.PlayLevelIntro();
        GameSettings.ApplyAll();
    }

    void StyleMenuChrome()
    {
        StyleMenuPanel(StartMenuPanel);
        StyleMenuPanel(NewGamePanel);

    }

    static void StyleMenuPanel(GameObject panel)
    {
        if (panel == null)
            return;

        VigilanteUiStyle.StylePanel(panel);
        VigilanteUiStyle.ApplyFontRecursive(panel.transform);
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            VigilanteUiStyle.StyleInvertedButtonPreserveActions(buttons[i]);
    }
    void WirePanelButtons()
    {
        if (NewGamePanel == null)
            return;

        // Scene "Level Select" button has an empty onClick — bind it at runtime.
        Transform levelSelect = FindChildByName(NewGamePanel.transform, "Level Select");
        if (levelSelect != null)
        {
            Button btn = levelSelect.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenLevelSelect);
            }
        }

        // Panel "NewGame" should also begin the campaign at level 1.
        Transform newGame = FindChildByName(NewGamePanel.transform, "NewGame");
        if (newGame != null)
        {
            Button btn = newGame.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(NewGame);
            }
        }
    }

    /// <summary>
    /// Primary Start button: always begin the campaign on Level 1.
    /// </summary>
    public void StartNewGame()
    {
        AudioManager.UIClick();
        GameProgression.StartLevel(1);
    }

    public void OpenLevelSelect()
    {
        AudioManager.UIClick();
        if (NewGamePanel != null) NewGamePanel.SetActive(true);
        if (StartMenuPanel != null) StartMenuPanel.SetActive(false);
        if (SettingsPanel != null) SettingsPanel.SetActive(false);

        LevelSelectUI select = GetComponent<LevelSelectUI>();
        if (select == null)
            select = gameObject.AddComponent<LevelSelectUI>();
        select.EnsureLevelButtons();
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

    /// <summary>
    /// New Game always starts campaign Level 1.
    /// </summary>
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
            Debug.Log("Level 2 locked — finish Level 1 first.");
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

    static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
