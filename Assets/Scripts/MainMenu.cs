using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the MainMenu scene panels and buttons.
/// Start → New Game panel; Settings → volume/brightness; Quit exits.
/// Escape closes the open submenu back to the start buttons.
/// </summary>
public class MainMenu : MonoBehaviour
{
    const string VolumePrefsKey = "Settings.Volume";
    const string BrightnessPrefsKey = "Settings.Brightness";

    [Header("Panels")]
    public GameObject startMenuButtons;
    public GameObject newGamePanel;
    public GameObject settingsPanel;
    public GameObject storySynopsisPanel;

    [Header("Start menu buttons")]
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("New game panel buttons")]
    public Button newGameButton;
    public Button levelSelectButton;
    public Button storySynopsisButton;
    public Button newGameQuitButton;

    [Header("Settings")]
    public Button settingsBackButton;
    public Slider volumeSlider;
    public Slider brightnessSlider;
    public Light sceneLight;

    [Header("Scenes")]
    public string gameSceneName = "LevelDemo";

    [Header("Defaults (0–100 slider range)")]
    public float defaultVolume = 80f;
    public float defaultBrightness = 50f;

    float defaultLightIntensity = 1f;

    void Awake()
    {
        if (sceneLight != null)
            defaultLightIntensity = sceneLight.intensity;

        WireButton(startButton, OpenNewGamePanel);
        WireButton(settingsButton, OpenSettings);
        WireButton(quitButton, QuitGame);

        WireButton(newGameButton, StartNewGame);
        WireButton(levelSelectButton, StartNewGame);
        WireButton(storySynopsisButton, OpenStorySynopsis);
        WireButton(newGameQuitButton, QuitGame);

        WireButton(settingsBackButton, ShowStartMenu);

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        LoadSettings();
        ShowStartMenu();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            ShowStartMenu();
            return;
        }

        if (storySynopsisPanel != null && storySynopsisPanel.activeSelf)
        {
            CloseStorySynopsis();
            return;
        }

        if (newGamePanel != null && newGamePanel.activeSelf)
            ShowStartMenu();
    }

    public void ShowStartMenu()
    {
        SetActive(startMenuButtons, true);
        SetActive(newGamePanel, false);
        SetActive(settingsPanel, false);
        SetActive(storySynopsisPanel, false);
    }

    public void OpenNewGamePanel()
    {
        SetActive(startMenuButtons, false);
        SetActive(newGamePanel, true);
        SetActive(settingsPanel, false);
        SetActive(storySynopsisPanel, false);
    }

    public void OpenSettings()
    {
        SetActive(startMenuButtons, false);
        SetActive(newGamePanel, false);
        SetActive(settingsPanel, true);
        SetActive(storySynopsisPanel, false);
    }

    public void OpenStorySynopsis()
    {
        if (storySynopsisPanel == null)
            return;

        SetActive(storySynopsisPanel, true);
    }

    public void CloseStorySynopsis()
    {
        SetActive(storySynopsisPanel, false);
        if (newGamePanel != null)
            SetActive(newGamePanel, true);
    }

    public void StartNewGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LoadSettings()
    {
        float volume = PlayerPrefs.GetFloat(VolumePrefsKey, defaultVolume);
        float brightness = PlayerPrefs.GetFloat(BrightnessPrefsKey, defaultBrightness);

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(volume);
            OnVolumeChanged(volume);
        }
        else
        {
            OnVolumeChanged(volume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(brightness);
            OnBrightnessChanged(brightness);
        }
        else
        {
            OnBrightnessChanged(brightness);
        }
    }

    void OnVolumeChanged(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value / 100f);
        PlayerPrefs.SetFloat(VolumePrefsKey, value);
        PlayerPrefs.Save();
    }

    void OnBrightnessChanged(float value)
    {
        float t = Mathf.Clamp01(value / 100f);

        if (sceneLight != null)
            sceneLight.intensity = defaultLightIntensity * Mathf.Lerp(0.25f, 1.5f, t);

        // Mobile / supported platforms only; harmless no-op elsewhere.
        Screen.brightness = Mathf.Lerp(0.2f, 1f, t);

        PlayerPrefs.SetFloat(BrightnessPrefsKey, value);
        PlayerPrefs.Save();
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
