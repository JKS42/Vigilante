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
            new Tip { id = "cover", message = "Enemies take cover and flank — use debris and corners.", duration = 5f },
            new Tip { id = "clear", message = "Clear every wave to finish the tutorial level.", duration = 5f },
        };
    }

    void EnsureUi()
    {
        if (promptText != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("TutorialCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject panelGo = new GameObject("TutorialPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        panel = panelGo.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.15f, 0.78f);
        prt.anchorMax = new Vector2(0.85f, 0.92f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("TutorialText");
        textGo.transform.SetParent(panelGo.transform, false);
        promptText = textGo.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 26f;
        promptText.alignment = TextAlignmentOptions.Center;
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
