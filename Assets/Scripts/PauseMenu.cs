using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause overlay for LevelDemo. Keep this on an always-active object (e.g. HUD / PlayerUI);
/// assign the PauseMenu panel so Escape still works while the panel is hidden.
/// Buttons: Resume, Settings, Restart Level, Main Menu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pauseMenuPanel;
    public GameObject settingsPanel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button settingsButton;
    public Button restartLevelButton;
    public Button mainMenuButton;

    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";

    [Header("While paused (optional)")]
    public Behaviour[] disableWhilePaused;

    bool isPaused;
    float previousTimeScale = 1f;

    public bool IsPaused => isPaused;

    void Awake()
    {
        WireButton(resumeButton, Resume);
        WireButton(settingsButton, OpenSettings);
        WireButton(restartLevelButton, RestartLevel);
        WireButton(mainMenuButton, LoadMainMenu);
    }

    void Start()
    {
        if (pauseMenuPanel == null)
            pauseMenuPanel = gameObject;

        ApplyPausedState(false, playSound: false);
    }

    void OnDestroy()
    {
        if (isPaused)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        SetPaused(!isPaused);
    }

    public void Resume()
    {
        AudioManager.UIClick();
        SetPaused(false);
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        AudioManager.UIClick();
        settingsPanel.SetActive(true);
        if (pauseMenuPanel != null && pauseMenuPanel != settingsPanel)
            pauseMenuPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        AudioManager.UIBack();
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (isPaused && pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
    }

    public void RestartLevel()
    {
        AudioManager.UIClick();
        GameProgression.RestartCurrentLevel();
    }

    public void LoadMainMenu()
    {
        AudioManager.UIClick();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void SetPaused(bool paused)
    {
        if (paused == isPaused)
            return;

        ApplyPausedState(paused, playSound: paused);
    }

    void ApplyPausedState(bool paused, bool playSound)
    {
        if (playSound && paused)
            AudioManager.UIClick();

        if (paused)
        {
            if (!isPaused)
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        isPaused = paused;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(paused);

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;

        if (disableWhilePaused == null)
            return;

        for (int i = 0; i < disableWhilePaused.Length; i++)
        {
            if (disableWhilePaused[i] != null)
                disableWhilePaused[i].enabled = !paused;
        }
    }

    static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
