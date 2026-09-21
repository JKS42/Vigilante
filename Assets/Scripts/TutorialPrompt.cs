using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Level 1 tutorial. Starts with a controls modal that pauses gameplay until
/// dismissed, then shows event popups (near breakable walls, kills, loot, …).
/// </summary>
public class TutorialPrompt : MonoBehaviour
{
    public static TutorialPrompt Instance { get; private set; }

    [Serializable]
    public class Tip
    {
        public string id;
        [TextArea] public string message;
        public float duration = 5f;
        [Tooltip("Empty = unused for opening (controls modal is separate). Shown when Notify(eventId) fires.")]
        public string showOnEvent;
    }

    public List<Tip> tips = new List<Tip>();
    public TextMeshProUGUI promptText;
    public Image panel;

    readonly HashSet<string> shownIds = new HashSet<string>();
    readonly HashSet<string> firedEvents = new HashSet<string>();
    readonly Queue<Tip> queue = new Queue<Tip>();
    readonly Dictionary<string, Tip> eventTips = new Dictionary<string, Tip>(StringComparer.Ordinal);

    Tip current;
    float tipEndsAt;
    bool tipVisible;
    int killCount;

    // Controls modal (pausing)
    GameObject controlsRoot;
    bool controlsOpen;
    bool controlsDone;
    float savedTimeScale = 1f;
    bool savedCursorVisible;
    CursorLockMode savedCursorLock;

    public static bool BlocksGameplay => Instance != null && Instance.controlsOpen;

    void Awake()
    {
        Instance = this;
        EnsureTipUi();
        EnsureControlsUi();
        if (tips.Count == 0)
            LoadDefaultTips();
        IndexTips();
        SetTipVisible(false);
        SetControlsVisible(false);
    }

    void OnDestroy()
    {
        UnbindWaves();
        if (controlsOpen)
            EndControlsPause(restoreTime: true);
        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        UnbindWaves();
        if (controlsOpen)
            EndControlsPause(restoreTime: true);
    }

    void Start()
    {
        BindWaves();
        // Brief delay so fade / level announcer can start, then freeze on controls.
        Invoke(nameof(OpenControlsModal), 0.35f);
    }

    void Update()
    {
        if (controlsOpen)
        {
            if (WasDismissPressed())
                CloseControlsModal();
            return;
        }

        if (tipVisible && current != null && Time.unscaledTime >= tipEndsAt)
        {
            SetTipVisible(false);
            current = null;
            TryShowNextQueued();
        }
    }

    void BindWaves()
    {
        WaveManager waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
        if (waves == null)
            return;
        waves.OnWaveStarted -= HandleWaveStarted;
        waves.OnAllWavesCompleted -= HandleAllWavesCleared;
        waves.OnWaveStarted += HandleWaveStarted;
        waves.OnAllWavesCompleted += HandleAllWavesCleared;
    }

    void UnbindWaves()
    {
        WaveManager waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
        if (waves == null)
            return;
        waves.OnWaveStarted -= HandleWaveStarted;
        waves.OnAllWavesCompleted -= HandleAllWavesCleared;
    }

    void HandleWaveStarted(int waveIndex)
    {
        if (waveIndex >= 1)
            Notify("wave_started");
    }

    void HandleAllWavesCleared()
    {
        Notify("all_waves_cleared");
    }

    void IndexTips()
    {
        eventTips.Clear();
        for (int i = 0; i < tips.Count; i++)
        {
            Tip tip = tips[i];
            if (tip == null || string.IsNullOrEmpty(tip.message) || string.IsNullOrEmpty(tip.showOnEvent))
                continue;
            if (!eventTips.ContainsKey(tip.showOnEvent))
                eventTips.Add(tip.showOnEvent, tip);
        }
    }

    void LoadDefaultTips()
    {
        tips = new List<Tip>
        {
            new Tip
            {
                id = "break",
                message = "Cracked wall — left click to smash it open and make a new path.",
                duration = 5.5f,
                showOnEvent = "near_breakable"
            },
            new Tip
            {
                id = "first_kill",
                message = "Nice hit. Keep clearing hostiles — they drop guns when they fall.",
                duration = 5f,
                showOnEvent = "first_kill"
            },
            new Tip
            {
                id = "wall_broken",
                message = "Route opened. Push through and stay mobile.",
                duration = 4.5f,
                showOnEvent = "wall_broken"
            },
            new Tip
            {
                id = "loot",
                message = "Pistol dropped — walk over it to pick it up.",
                duration = 6f,
                showOnEvent = "weapon_drop"
            },
            new Tip
            {
                id = "weapon_pickup",
                message = "Armed. 1–4 / scroll to switch weapons. Left click to fire.",
                duration = 6f,
                showOnEvent = "weapon_pickup"
            },
            new Tip
            {
                id = "damage",
                message = "Red markers show where shots came from. Strafe and use cover.",
                duration = 5f,
                showOnEvent = "player_hurt"
            },
            new Tip
            {
                id = "wave",
                message = "New wave inbound. Use space, break walls, and don't get pinched.",
                duration = 5f,
                showOnEvent = "wave_started"
            },
            new Tip
            {
                id = "clear",
                message = "Clear every wave to finish the tutorial.",
                duration = 5f,
                showOnEvent = "all_waves_cleared"
            },
        };
    }

    // ── Controls modal (pauses) ───────────────────────────────────────────

    void OpenControlsModal()
    {
        if (controlsDone || controlsOpen)
            return;

        EnsureControlsUi();
        BeginControlsPause();
        SetControlsVisible(true);
        AudioManager.UIClick();
    }

    void CloseControlsModal()
    {
        if (!controlsOpen)
            return;

        controlsDone = true;
        SetControlsVisible(false);
        EndControlsPause(restoreTime: true);
        AudioManager.UIClick();
        TryShowNextQueued();
    }

    void BeginControlsPause()
    {
        controlsOpen = true;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void EndControlsPause(bool restoreTime)
    {
        controlsOpen = false;
        if (restoreTime)
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;

        // Don't steal cursor if the pause menu took over.
        PauseMenu pause = FindFirstObjectByType<PauseMenu>();
        if (pause != null && pause.IsPaused)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static bool WasDismissPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            return true;

        return false;
    }

    // ── Event popups (do not pause) ───────────────────────────────────────

    void TryShowNextQueued()
    {
        while (queue.Count > 0)
        {
            Tip next = queue.Dequeue();
            if (next == null || shownIds.Contains(next.id))
                continue;
            ShowTip(next);
            return;
        }
    }

    void ShowTip(Tip tip)
    {
        if (tip == null || string.IsNullOrEmpty(tip.message))
            return;
        if (!string.IsNullOrEmpty(tip.id) && !shownIds.Add(tip.id))
            return;

        // Don't stack over the controls modal.
        if (controlsOpen)
        {
            queue.Enqueue(tip);
            shownIds.Remove(tip.id);
            return;
        }

        current = tip;
        EnsureTipUi();
        if (promptText != null)
            promptText.text = tip.message;
        SetTipVisible(true);
        tipEndsAt = Time.unscaledTime + Mathf.Max(1.5f, tip.duration);
        AudioManager.UIClick();
    }

    void EnqueueOrShow(Tip tip)
    {
        if (tip == null)
            return;
        if (!string.IsNullOrEmpty(tip.id) && shownIds.Contains(tip.id))
            return;

        if (controlsOpen || tipVisible || queue.Count > 0)
        {
            queue.Enqueue(tip);
            return;
        }

        ShowTip(tip);
    }

    public static void Notify(string eventId)
    {
        if (Instance == null || string.IsNullOrEmpty(eventId))
            return;

        Instance.HandleNotify(eventId);
    }

    void HandleNotify(string eventId)
    {
        if (eventId == "enemy_killed")
        {
            killCount++;
            if (killCount == 1)
                HandleNotify("first_kill");
            return;
        }

        bool firstFire = firedEvents.Add(eventId);
        if (!firstFire && eventId != "wave_started")
            return;

        if (eventTips.TryGetValue(eventId, out Tip tip))
            EnqueueOrShow(tip);
    }

    public static void EnsureForLevel1()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("TutorialPrompt");
        go.AddComponent<TutorialPrompt>();
    }

    void SetTipVisible(bool on)
    {
        tipVisible = on;
        if (panel != null)
            panel.gameObject.SetActive(on);
    }

    void SetControlsVisible(bool on)
    {
        if (controlsRoot != null)
            controlsRoot.SetActive(on);
    }

    void EnsureTipUi()
    {
        if (promptText != null && panel != null)
            return;

        GameObject canvasGo = EnsureCanvas();

        Transform panelTf = canvasGo.transform.Find("TutorialPanel");
        GameObject panelGo = panelTf != null ? panelTf.gameObject : null;
        if (panelGo == null)
        {
            panelGo = new GameObject("TutorialPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            panel = panelGo.AddComponent<Image>();
            panel.sprite = WhiteSprite();
            panel.color = new Color(0.05f, 0.05f, 0.07f, 0.88f);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.12f);
            prt.anchorMax = new Vector2(0.5f, 0.12f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(640f, 96f);
        }
        else
        {
            panel = panelGo.GetComponent<Image>();
        }

        Transform textTf = panelGo.transform.Find("TutorialText");
        if (textTf == null)
        {
            GameObject textGo = new GameObject("TutorialText");
            textGo.transform.SetParent(panelGo.transform, false);
            promptText = textGo.AddComponent<TextMeshProUGUI>();
            promptText.fontSize = 24f;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;
            RectTransform trt = promptText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f, 12f);
            trt.offsetMax = new Vector2(-20f, -12f);
        }
        else
        {
            promptText = textTf.GetComponent<TextMeshProUGUI>();
        }
    }

    void EnsureControlsUi()
    {
        if (controlsRoot != null)
            return;

        GameObject canvasGo = EnsureCanvas();
        Transform existing = canvasGo.transform.Find("ControlsModal");
        if (existing != null)
        {
            controlsRoot = existing.gameObject;
            return;
        }

        controlsRoot = new GameObject("ControlsModal");
        controlsRoot.transform.SetParent(canvasGo.transform, false);

        Image dim = controlsRoot.AddComponent<Image>();
        dim.sprite = WhiteSprite();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform dimRt = dim.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        GameObject box = new GameObject("Box");
        box.transform.SetParent(controlsRoot.transform, false);
        Image boxImg = box.AddComponent<Image>();
        boxImg.sprite = WhiteSprite();
        boxImg.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
        RectTransform boxRt = boxImg.rectTransform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 420f);

        TextMeshProUGUI title = CreateTmp(box.transform, "Title", "CONTROLS", 34f, FontStyles.Bold);
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -28f);
        titleRt.sizeDelta = new Vector2(-48f, 44f);
        title.alignment = TextAlignmentOptions.Center;

        const string body =
            "WASD — Move\n" +
            "Mouse — Look\n" +
            "Shift — Sprint\n" +
            "C / Ctrl — Crouch\n" +
            "Alt / Q — Dash\n" +
            "Left Click — Swing bat / Fire\n" +
            "1–4 / Scroll — Switch weapons\n" +
            "Esc — Pause";

        TextMeshProUGUI bodyTmp = CreateTmp(box.transform, "Body", body, 22f, FontStyles.Normal);
        RectTransform bodyRt = bodyTmp.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(40f, 88f);
        bodyRt.offsetMax = new Vector2(-40f, -84f);
        bodyTmp.alignment = TextAlignmentOptions.Left;
        bodyTmp.lineSpacing = 8f;

        GameObject btnGo = new GameObject("Continue");
        btnGo.transform.SetParent(box.transform, false);
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = WhiteSprite();
        btnImg.color = new Color(0.85f, 0.2f, 0.18f, 1f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(CloseControlsModal);
        RectTransform btnRt = btnImg.rectTransform;
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 28f);
        btnRt.sizeDelta = new Vector2(200f, 44f);

        TextMeshProUGUI btnLabel = CreateTmp(btnGo.transform, "Label", "CONTINUE", 20f, FontStyles.Bold);
        RectTransform btnLabelRt = btnLabel.rectTransform;
        btnLabelRt.anchorMin = Vector2.zero;
        btnLabelRt.anchorMax = Vector2.one;
        btnLabelRt.offsetMin = Vector2.zero;
        btnLabelRt.offsetMax = Vector2.zero;
        btnLabel.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI hint = CreateTmp(box.transform, "Hint", "Space / Click to continue", 16f, FontStyles.Italic);
        hint.color = new Color(1f, 1f, 1f, 0.55f);
        RectTransform hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 8f);
        hintRt.sizeDelta = new Vector2(-40f, 22f);
        hint.alignment = TextAlignmentOptions.Center;
    }

    static GameObject EnsureCanvas()
    {
        GameObject canvasGo = GameObject.Find("TutorialCanvas");
        if (canvasGo != null)
            return canvasGo;

        canvasGo = new GameObject("TutorialCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvasGo;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float size, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Sprite WhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }
}
