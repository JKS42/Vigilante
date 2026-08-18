using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tutorial overlay prompts for Level 1. Shows sequenced tips and reacts to events.
/// </summary>
public class TutorialPrompt : MonoBehaviour
{
    public static TutorialPrompt Instance { get; private set; }

    [Serializable]
    public class Tip
    {
        public string id;
        public string message;
        public float duration = 4f;
        public bool waitForEvent;
        public string eventId;
    }

    public List<Tip> tips = new List<Tip>();
    public TextMeshProUGUI promptText;
    public Image panel;

    int tipIndex = -1;
    float tipEndsAt;
    bool waitingForEvent;
    string waitingEvent;
    readonly HashSet<string> firedEvents = new HashSet<string>();

    void Awake()
    {
        Instance = this;
        EnsureUi();
        if (tips.Count == 0)
            LoadDefaultTips();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        AdvanceTip();
    }

    void Update()
    {
        if (waitingForEvent)
            return;

        if (tipIndex >= 0 && Time.unscaledTime >= tipEndsAt)
            AdvanceTip();
    }

    void LoadDefaultTips()
    {
        tips = new List<Tip>
        {
            new Tip { id = "move", message = "WASD to move · Mouse to look · Shift to sprint", duration = 5f },
            new Tip { id = "crouch_dash", message = "C / Ctrl to crouch · Alt / Q to dash", duration = 5f },
            new Tip { id = "melee", message = "You start with the bat. Left click to melee.", duration = 5f },
            new Tip { id = "break", message = "Shoot or smash breakable walls (cracked texture) for new routes.", duration = 5f },
            new Tip { id = "loot", message = "Kill pistol enemies and walk over their dropped gun to arm up.", duration = 6f, waitForEvent = true, eventId = "weapon_pickup" },
            new Tip { id = "cover", message = "Enemies take cover and flank — crouch (C / Ctrl) behind debris and furniture.", duration = 5f },
            new Tip { id = "clear", message = "Clear every wave to finish the tutorial level.", duration = 5f },
        };
    }

    void EnsureUi()
    {
        if (promptText != null)
            return;

        GameObject canvasGo = new GameObject("TutorialCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("TutorialPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        panel = panelGo.AddComponent<Image>();
        Texture2D tex = Texture2D.whiteTexture;
        panel.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        panel.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = new Vector2(1f, 1f);
        prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-16f, -78f);
        prt.sizeDelta = new Vector2(420f, 72f);

        GameObject textGo = new GameObject("TutorialText");
        textGo.transform.SetParent(panelGo.transform, false);
        promptText = textGo.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 20f;
        promptText.alignment = TextAlignmentOptions.MidlineRight;
        promptText.color = Color.white;
        RectTransform trt = promptText.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 8f);
        trt.offsetMax = new Vector2(-12f, -8f);
    }

    void AdvanceTip()
    {
        tipIndex++;
        if (tipIndex >= tips.Count)
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            return;
        }

        Tip tip = tips[tipIndex];
        EnsureUi();
        if (panel != null)
            panel.gameObject.SetActive(true);
        if (promptText != null)
            promptText.text = tip.message;

        if (tip.waitForEvent && !string.IsNullOrEmpty(tip.eventId) && !firedEvents.Contains(tip.eventId))
        {
            waitingForEvent = true;
            waitingEvent = tip.eventId;
            tipEndsAt = float.PositiveInfinity;
        }
        else
        {
            waitingForEvent = false;
            waitingEvent = null;
            tipEndsAt = Time.unscaledTime + tip.duration;
        }
    }

    public static void Notify(string eventId)
    {
        if (Instance == null || string.IsNullOrEmpty(eventId))
            return;

        Instance.firedEvents.Add(eventId);
        if (Instance.waitingForEvent && Instance.waitingEvent == eventId)
        {
            Instance.waitingForEvent = false;
            Instance.tipEndsAt = Time.unscaledTime + 1.5f;
        }
    }

    public static void EnsureForLevel1()
    {
        if (Instance != null)
            return;
        GameObject go = new GameObject("TutorialPrompt");
        go.AddComponent<TutorialPrompt>();
    }
}
