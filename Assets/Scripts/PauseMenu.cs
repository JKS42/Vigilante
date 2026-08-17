using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause overlay for LevelDemo. Lives on an always-active object (PlayerUI / HUD)
/// so Escape still works while the panel is hidden.
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
    GameObject dimmer;

    public bool IsPaused => isPaused;

    public static PauseMenu EnsureExists()
    {
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return null;

        Canvas canvas = FindPlayCanvas();
        PauseMenu host = canvas != null ? canvas.GetComponent<PauseMenu>() : null;

        PauseMenu sceneMenu = FindSceneMenu(host);

        if (host == null && canvas != null)
            host = canvas.gameObject.AddComponent<PauseMenu>();

        if (host == null)
            host = sceneMenu;

        if (host == null)
            return null;

        if (sceneMenu != null && sceneMenu != host)
        {
            host.CopyFrom(sceneMenu);
            Destroy(sceneMenu);
        }

        host.EnsureUi();
        host.WireAll();
        return host;
    }

    void Awake()
    {
        WireAll();
    }

    void Start()
    {
        if (pauseMenuPanel == null)
        {
            Transform named = transform.Find("PauseMenu");
            if (named != null)
                pauseMenuPanel = named.gameObject;
            else if (gameObject.name == "PauseMenu")
                pauseMenuPanel = gameObject;
        }

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

        UIManager ui = UIManager.Instance;
        if (ui != null && ui.IsPlayerDead)
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
        SettingsMenu.EnsureOn(settingsPanel, CloseSettings);
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        if (pauseMenuPanel != null && pauseMenuPanel != settingsPanel)
            pauseMenuPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        AudioManager.UIBack();
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (isPaused && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            pauseMenuPanel.transform.SetAsLastSibling();
        }
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

        if (dimmer != null)
        {
            dimmer.SetActive(paused);
            if (paused)
                dimmer.transform.SetAsLastSibling();
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(paused);
            if (paused)
                pauseMenuPanel.transform.SetAsLastSibling();
        }

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

    void CopyFrom(PauseMenu other)
    {
        if (other == null)
            return;

        pauseMenuPanel = other.pauseMenuPanel != null ? other.pauseMenuPanel : other.gameObject;
        settingsPanel = other.settingsPanel;
        resumeButton = other.resumeButton;
        settingsButton = other.settingsButton;
        restartLevelButton = other.restartLevelButton;
        mainMenuButton = other.mainMenuButton;
        if (!string.IsNullOrEmpty(other.mainMenuSceneName))
            mainMenuSceneName = other.mainMenuSceneName;
        disableWhilePaused = other.disableWhilePaused;
    }

    void EnsureUi()
    {
        Transform hud = FindHud();

        if (pauseMenuPanel == null && hud != null)
        {
            Transform existing = hud.Find("PauseMenu");
            if (existing != null)
                pauseMenuPanel = existing.gameObject;
        }

        if (settingsPanel == null && hud != null)
        {
            for (int i = 0; i < hud.childCount; i++)
            {
                Transform child = hud.GetChild(i);
                if (child.name != "Settings")
                    continue;
                if (child.GetComponent<Button>() != null)
                    continue;
                settingsPanel = child.gameObject;
                break;
            }
        }

        if (pauseMenuPanel == null && hud != null)
            pauseMenuPanel = BuildPausePanel(hud);

        if (settingsPanel == null && hud != null)
            settingsPanel = BuildSettingsPanel(hud);

        ResolveButtons();
        EnsurePausedTitle();
        EnsureDimmer(hud);
        SettingsMenu.EnsureOn(settingsPanel, CloseSettings);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void ResolveButtons()
    {
        if (pauseMenuPanel == null)
            return;

        if (resumeButton == null)
            resumeButton = FindButton(pauseMenuPanel.transform, "Resume");
        if (settingsButton == null)
            settingsButton = FindButton(pauseMenuPanel.transform, "Settings");
        if (restartLevelButton == null)
            restartLevelButton = FindButton(pauseMenuPanel.transform, "Restart Level");
        if (mainMenuButton == null)
            mainMenuButton = FindButton(pauseMenuPanel.transform, "Main Menu");
    }

    void WireAll()
    {
        ResolveButtons();
        WireButton(resumeButton, Resume);
        WireButton(settingsButton, OpenSettings);
        WireButton(restartLevelButton, RestartLevel);
        WireButton(mainMenuButton, LoadMainMenu);
    }

    void EnsurePausedTitle()
    {
        if (pauseMenuPanel == null)
            return;
        if (FindChildByName(pauseMenuPanel.transform, "PausedTitle") != null)
            return;

        CreateLabel(pauseMenuPanel.transform, "PausedTitle", "PAUSED", new Vector2(0f, 210f), new Vector2(360f, 48f), 36f);
    }

    void EnsureDimmer(Transform hud)
    {
        if (dimmer != null || hud == null)
            return;

        Transform existing = hud.Find("PauseDimmer");
        if (existing != null)
        {
            dimmer = existing.gameObject;
            return;
        }

        dimmer = new GameObject("PauseDimmer");
        dimmer.transform.SetParent(hud, false);
        Image image = dimmer.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = true;
        Stretch(image.rectTransform);
        dimmer.SetActive(false);
        dimmer.transform.SetAsFirstSibling();
    }

    static GameObject BuildPausePanel(Transform hud)
    {
        GameObject panel = CreatePanel(hud, "PauseMenu", new Vector2(400f, 500f), new Color(0.08f, 0.08f, 0.1f, 0.92f));
        CreateLabel(panel.transform, "PausedTitle", "PAUSED", new Vector2(0f, 210f), new Vector2(360f, 48f), 36f);
        CreateMenuButton(panel.transform, "Resume", new Vector2(0f, 142f));
        CreateMenuButton(panel.transform, "Settings", new Vector2(0f, 72f));
        CreateMenuButton(panel.transform, "Restart Level", new Vector2(0f, -5f));
        CreateMenuButton(panel.transform, "Main Menu", new Vector2(0f, -85f));
        panel.SetActive(false);
        return panel;
    }

    static GameObject BuildSettingsPanel(Transform hud)
    {
        GameObject panel = CreatePanel(hud, "Settings", new Vector2(600f, 700f), new Color(0.08f, 0.08f, 0.1f, 0.92f));
        panel.SetActive(false);
        return panel;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = color;
        image.raycastTarget = true;
        return go;
    }

    static Button CreateMenuButton(Transform parent, string label, Vector2 position)
    {
        GameObject go = new GameObject(label);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 36f);
        rt.anchoredPosition = position;

        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        CreateLabel(go.transform, "Label", label.ToUpperInvariant(), Vector2.zero, Vector2.zero, 22f, stretch: true);
        return button;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, bool stretch = false)
    {
        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button FindButton(Transform root, string name)
    {
        Transform t = FindChildByName(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    static PauseMenu FindSceneMenu(PauseMenu host)
    {
        PauseMenu[] menus = Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != null && menus[i] != host)
                return menus[i];
        }
        return null;
    }

    static Canvas FindPlayCanvas()
    {
        GameObject playerUi = GameObject.Find("PlayerUI");
        if (playerUi != null)
        {
            Canvas canvas = playerUi.GetComponent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        return Object.FindFirstObjectByType<Canvas>();
    }

    static Transform FindHud()
    {
        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            return hud.transform;

        Canvas canvas = FindPlayCanvas();
        return canvas != null ? canvas.transform : null;
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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite WhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }

    static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
