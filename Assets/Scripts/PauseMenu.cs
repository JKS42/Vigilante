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
    public Button tutorialLogButton;

    GameObject tutorialLogPanel;
    TextMeshProUGUI tutorialLogText;

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

        // Controls tutorial owns Escape until dismissed.
        if (TutorialPrompt.BlocksGameplay || PistolIntroCinematic.IsRunning)
            return;

        UIManager ui = UIManager.Instance;
        if (ui != null && ui.IsPlayerDead)
            return;

        if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (isPaused && tutorialLogPanel != null && tutorialLogPanel.activeSelf)
        {
            CloseTutorialLog();
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
        if (dimmer != null)
        {
            dimmer.SetActive(true);
            dimmer.transform.SetAsFirstSibling();
            ConfigureDimmer(dimmer);
        }
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
            if (tutorialLogPanel != null)
                tutorialLogPanel.SetActive(false);
        }

        isPaused = paused;

        if (dimmer != null)
        {
            dimmer.SetActive(paused);
            if (paused)
            {
                // Dim behind the pause / settings panels.
                dimmer.transform.SetAsFirstSibling();
                ConfigureDimmer(dimmer);
            }
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
        EnsureTutorialLog(hud);
        StylePauseChrome();

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
        if (tutorialLogButton == null)
            tutorialLogButton = FindButton(pauseMenuPanel.transform, "Tutorial Log");
    }

    void WireAll()
    {
        ResolveButtons();
        StyleMenuButton(resumeButton, Resume);
        StyleMenuButton(settingsButton, OpenSettings);
        StyleMenuButton(restartLevelButton, RestartLevel);
        StyleMenuButton(mainMenuButton, LoadMainMenu);
        StyleMenuButton(tutorialLogButton, OpenTutorialLog);
        if (tutorialLogPanel != null)
            StyleMenuButton(FindButton(tutorialLogPanel.transform, "Back"), CloseTutorialLog);
    }

    void StylePauseChrome()
    {
        VigilanteUiStyle.StylePanel(pauseMenuPanel);
        VigilanteUiStyle.StylePanel(tutorialLogPanel);
        StyleTitle(pauseMenuPanel, "PausedTitle");
        StyleTitle(tutorialLogPanel, "Title");
        if (tutorialLogText != null)
            VigilanteUiStyle.ApplyFont(tutorialLogText);
    }

    static void StyleTitle(GameObject panel, string childName)
    {
        if (panel == null)
            return;
        Transform title = FindChildByName(panel.transform, childName);
        if (title == null)
            return;
        TextMeshProUGUI label = title.GetComponent<TextMeshProUGUI>();
        if (label == null)
            return;
        VigilanteUiStyle.ApplyFont(label);
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
    }

    const float MenuButtonWidth = 180f;
    const float MenuButtonHeight = 36f;

    static void StyleMenuButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(MenuButtonWidth, MenuButtonHeight);

        VigilanteUiStyle.StyleInvertedButton(button, () => action.Invoke());
    }

    void EnsurePausedTitle()
    {
        if (pauseMenuPanel == null)
            return;
        if (FindChildByName(pauseMenuPanel.transform, "PausedTitle") != null)
            return;

        CreateLabel(pauseMenuPanel.transform, "PausedTitle", "PAUSED", new Vector2(0f, 210f), new Vector2(360f, 48f), 36f);
    }

    void EnsureTutorialLog(Transform hud)
    {
        if (pauseMenuPanel == null)
            return;

        if (tutorialLogButton == null && FindButton(pauseMenuPanel.transform, "Tutorial Log") == null)
        {
            Button sample = FindButton(pauseMenuPanel.transform, "Settings");
            if (sample == null)
                sample = FindButton(pauseMenuPanel.transform, "Resume");

            float step = ButtonStep(sample);
            ShiftButton("Restart Level", -step);
            ShiftButton("Main Menu", -step);
            Vector2 position = new Vector2(0f, 2f);
            RectTransform settings = sample != null ? sample.GetComponent<RectTransform>() : null;
            if (settings != null)
                position = settings.anchoredPosition + new Vector2(0f, -step);
            tutorialLogButton = CreateMenuButton(pauseMenuPanel.transform, "Tutorial Log", position);
            MatchButton(tutorialLogButton, sample);
            RectTransform panelRect = pauseMenuPanel.GetComponent<RectTransform>();
            if (panelRect != null)
                panelRect.sizeDelta += new Vector2(0f, step);
        }

        if (tutorialLogPanel == null && hud != null)
        {
            tutorialLogPanel = CreatePanel(hud, "TutorialLog", new Vector2(640f, 560f), new Color(0.08f, 0.08f, 0.1f, 0.94f));
            CreateLabel(tutorialLogPanel.transform, "Title", "TUTORIAL LOG", new Vector2(0f, 230f), new Vector2(560f, 48f), 32f);

            GameObject body = new GameObject("Entries");
            body.layer = 5;
            body.transform.SetParent(tutorialLogPanel.transform, false);
            RectTransform bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(36f, 96f);
            bodyRect.offsetMax = new Vector2(-36f, -72f);
            tutorialLogText = body.AddComponent<TextMeshProUGUI>();
            tutorialLogText.fontSize = 20f;
            tutorialLogText.alignment = TextAlignmentOptions.TopLeft;
            tutorialLogText.color = Color.white;
            tutorialLogText.raycastTarget = false;

            Button back = CreateMenuButton(tutorialLogPanel.transform, "Back", new Vector2(0f, -230f));
            StyleMenuButton(back, CloseTutorialLog);
            tutorialLogPanel.SetActive(false);
        }
    }

    static float ButtonStep(Button sample)
    {
        if (sample == null || sample.transform.parent == null)
            return 70f;

        RectTransform sampleRect = sample.GetComponent<RectTransform>();
        if (sampleRect == null)
            return 70f;

        float nearest = 0f;
        for (int i = 0; i < sample.transform.parent.childCount; i++)
        {
            Transform child = sample.transform.parent.GetChild(i);
            if (child == sample.transform)
                continue;
            if (child.GetComponent<Button>() == null)
                continue;
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect == null)
                continue;
            float gap = Mathf.Abs(rect.anchoredPosition.y - sampleRect.anchoredPosition.y);
            if (gap < 1f)
                continue;
            if (nearest < 1f || gap < nearest)
                nearest = gap;
        }

        return nearest > 1f ? nearest : 70f;
    }

    static void MatchButton(Button created, Button sample)
    {
        if (created == null || sample == null)
            return;

        RectTransform createdRect = created.GetComponent<RectTransform>();
        RectTransform sampleRect = sample.GetComponent<RectTransform>();
        if (createdRect != null && sampleRect != null)
        {
            createdRect.sizeDelta = sampleRect.sizeDelta;
            createdRect.localScale = sampleRect.localScale;
        }

        TextMeshProUGUI sampleLabel = sample.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI createdLabel = created.GetComponentInChildren<TextMeshProUGUI>();
        if (sampleLabel == null || createdLabel == null)
            return;

        createdLabel.fontSize = sampleLabel.fontSize;
        createdLabel.enableAutoSizing = false;
    }

    void ShiftButton(string name, float deltaY)
    {
        Button button = FindButton(pauseMenuPanel.transform, name);
        if (button == null)
            return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition += new Vector2(0f, deltaY);
    }

    public void OpenTutorialLog()
    {
        if (tutorialLogPanel == null)
            return;

        AudioManager.UIClick();
        string[] entries = TutorialPrompt.GetLog();
        if (tutorialLogText != null)
        {
            if (entries == null || entries.Length == 0)
                tutorialLogText.text = "No tutorials yet.";
            else
                tutorialLogText.text = string.Join("\n\n", entries);
        }

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        tutorialLogPanel.SetActive(true);
        tutorialLogPanel.transform.SetAsLastSibling();
    }

    public void CloseTutorialLog()
    {
        AudioManager.UIBack();
        if (tutorialLogPanel != null)
            tutorialLogPanel.SetActive(false);
        if (isPaused && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            pauseMenuPanel.transform.SetAsLastSibling();
        }
    }

    void EnsureDimmer(Transform hud)
    {
        if (hud == null)
            return;

        if (dimmer == null)
        {
            Transform existing = hud.Find("PauseDimmer");
            if (existing != null)
                dimmer = existing.gameObject;
        }

        if (dimmer == null)
        {
            dimmer = new GameObject("PauseDimmer");
            dimmer.transform.SetParent(hud, false);
            dimmer.AddComponent<Image>();
        }

        ConfigureDimmer(dimmer);
        dimmer.SetActive(false);
        dimmer.transform.SetAsFirstSibling();
    }

    static void ConfigureDimmer(GameObject dimmerGo)
    {
        if (dimmerGo == null)
            return;

        Image image = dimmerGo.GetComponent<Image>();
        if (image == null)
            image = dimmerGo.AddComponent<Image>();

        image.sprite = WhiteSprite();
        image.color = new Color(0f, 0f, 0f, 0.62f);
        image.raycastTarget = true;
        Stretch(image.rectTransform);
    }

    static GameObject BuildPausePanel(Transform hud)
    {
        GameObject panel = CreatePanel(hud, "PauseMenu", new Vector2(400f, 500f), new Color(0.08f, 0.08f, 0.1f, 0.92f));
        CreateLabel(panel.transform, "PausedTitle", "PAUSED", new Vector2(0f, 210f), new Vector2(360f, 48f), 36f);
        CreateMenuButton(panel.transform, "Resume", new Vector2(0f, 168f));
        CreateMenuButton(panel.transform, "Settings", new Vector2(0f, 98f));
        CreateMenuButton(panel.transform, "Tutorial Log", new Vector2(0f, 28f));
        CreateMenuButton(panel.transform, "Restart Level", new Vector2(0f, -42f));
        CreateMenuButton(panel.transform, "Main Menu", new Vector2(0f, -112f));
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
        rt.sizeDelta = new Vector2(MenuButtonWidth, MenuButtonHeight);
        rt.anchoredPosition = position;

        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        CreateLabel(go.transform, "Label", label, Vector2.zero, Vector2.zero, 22f, stretch: true);
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
}
