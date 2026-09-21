using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level 1 tutorial prompts. Opening tips play once; the rest appear when
/// criteria fire (kills, pickups, breakables, trigger volumes, waves).
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
        [Tooltip("Empty = part of the opening sequence. Otherwise shown when Notify(eventId) fires.")]
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
    bool visible;
    int openingIndex = -1;
    List<Tip> openingTips = new List<Tip>();
    bool openingDone;
    int killCount;

    void Awake()
    {
        Instance = this;
        EnsureUi();
        if (tips.Count == 0)
            LoadDefaultTips();
        IndexTips();
        SetVisible(false);
    }

    void OnDestroy()
    {
        WaveManager waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            waves.OnWaveStarted -= HandleWaveStarted;
            waves.OnAllWavesCompleted -= HandleAllWavesCleared;
        }

        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        WaveManager waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            waves.OnWaveStarted -= HandleWaveStarted;
            waves.OnAllWavesCompleted -= HandleAllWavesCleared;
        }
    }

    void Start()
    {
        tipEndsAt = Time.unscaledTime + 0.6f;
        openingIndex = -1;
        openingDone = openingTips.Count == 0;
        if (!openingDone)
            ScheduleNextOpening();

        WaveManager waves = WaveManager.Instance != null
            ? WaveManager.Instance
            : FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            waves.OnWaveStarted += HandleWaveStarted;
            waves.OnAllWavesCompleted += HandleAllWavesCleared;
        }
    }

    void HandleWaveStarted(int waveIndex)
    {
        // Skip the very first wave — opening tips cover that beat.
        if (waveIndex >= 1)
            Notify("wave_started");
    }

    void HandleAllWavesCleared()
    {
        Notify("all_waves_cleared");
    }

    void Update()
    {
        if (visible && current != null && Time.unscaledTime >= tipEndsAt)
        {
            SetVisible(false);
            current = null;
            TryShowNextQueued();
            if (!openingDone)
                ScheduleNextOpening();
        }
        else if (!visible && !openingDone && Time.unscaledTime >= tipEndsAt)
        {
            ScheduleNextOpening();
        }
    }

    void IndexTips()
    {
        openingTips.Clear();
        eventTips.Clear();
        for (int i = 0; i < tips.Count; i++)
        {
            Tip tip = tips[i];
            if (tip == null || string.IsNullOrEmpty(tip.message))
                continue;

            if (string.IsNullOrEmpty(tip.showOnEvent))
                openingTips.Add(tip);
            else if (!eventTips.ContainsKey(tip.showOnEvent))
                eventTips.Add(tip.showOnEvent, tip);
        }
    }

    void LoadDefaultTips()
    {
        tips = new List<Tip>
        {
            new Tip
            {
                id = "move",
                message = "WASD to move · Mouse to look · Shift to sprint",
                duration = 5f
            },
            new Tip
            {
                id = "crouch_dash",
                message = "C / Ctrl to crouch · Alt / Q to dash",
                duration = 4.5f
            },
            new Tip
            {
                id = "melee",
                message = "Left click to swing the bat. Close the gap and smash them.",
                duration = 5f
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
                id = "break",
                message = "Cracked walls are breakable — smash or shoot them for new routes.",
                duration = 5.5f,
                showOnEvent = "near_breakable"
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

    void ScheduleNextOpening()
    {
        if (openingDone)
            return;

        // Don't interrupt an event tip with opening fluff.
        if (visible || queue.Count > 0)
        {
            tipEndsAt = Time.unscaledTime + 0.5f;
            return;
        }

        openingIndex++;
        if (openingIndex >= openingTips.Count)
        {
            openingDone = true;
            return;
        }

        ShowTip(openingTips[openingIndex]);
    }

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

        current = tip;
        EnsureUi();
        if (promptText != null)
            promptText.text = tip.message;
        SetVisible(true);
        tipEndsAt = Time.unscaledTime + Mathf.Max(1.5f, tip.duration);
        AudioManager.UIClick();
    }

    void EnqueueOrShow(Tip tip)
    {
        if (tip == null)
            return;
        if (!string.IsNullOrEmpty(tip.id) && shownIds.Contains(tip.id))
            return;

        if (!visible && queue.Count == 0)
        {
            ShowTip(tip);
            return;
        }

        queue.Enqueue(tip);
    }

    /// <summary>
    /// Fire a tutorial criterion. Matching tips with showOnEvent == eventId will appear.
    /// </summary>
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
        // wave_started may fire every wave; tip itself is still once via shownIds.
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

    void SetVisible(bool on)
    {
        visible = on;
        if (panel != null)
            panel.gameObject.SetActive(on);
    }

    void EnsureUi()
    {
        if (promptText != null && panel != null)
            return;

        GameObject canvasGo = GameObject.Find("TutorialCanvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("TutorialCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        Transform panelTf = canvasGo.transform.Find("TutorialPanel");
        GameObject panelGo = panelTf != null ? panelTf.gameObject : null;
        if (panelGo == null)
        {
            panelGo = new GameObject("TutorialPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            panel = panelGo.AddComponent<Image>();
            Texture2D tex = Texture2D.whiteTexture;
            panel.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
            panel.color = new Color(0f, 0f, 0f, 0.72f);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-24f, -96f);
            prt.sizeDelta = new Vector2(480f, 88f);
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
            promptText.fontSize = 22f;
            promptText.alignment = TextAlignmentOptions.MidlineRight;
            promptText.color = Color.white;
            RectTransform trt = promptText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 10f);
            trt.offsetMax = new Vector2(-16f, -10f);
        }
        else
        {
            promptText = textTf.GetComponent<TextMeshProUGUI>();
        }
    }
}
