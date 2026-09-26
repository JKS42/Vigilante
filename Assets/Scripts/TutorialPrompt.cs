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
    float openControlsAt;
    float savedTimeScale = 1f;
    bool savedCursorVisible;
    CursorLockMode savedCursorLock;

    public static bool BlocksGameplay => Instance != null && (Instance.controlsOpen || Instance.markerLesson);

    void Awake()
    {
        Instance = this;
        if (tips.Count == 0)
            LoadDefaultTips();
        IndexTips();
        EnsureTipUi();
        SetTipVisible(false);
        SetControlsVisible(false);
    }

    void OnDestroy()
    {
        UnbindWaves();
        UnbindWeaponSwitcher();
        if (controlsOpen)
            EndControlsPause(restoreTime: true);
        EndMarkerLesson();
        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        UnbindWaves();
        UnbindWeaponSwitcher();
        if (controlsOpen)
            EndControlsPause(restoreTime: true);
        EndMarkerLesson();
    }

    void Start()
    {
        BindWaves();
        BindWeaponSwitcher();
        // Unscaled, so a tip that pauses gameplay cannot swallow the controls screen.
        openControlsAt = Time.unscaledTime + 0.35f;
    }

    void Update()
    {
        if (!controlsDone && !controlsOpen && openControlsAt > 0f && Time.unscaledTime >= openControlsAt)
            OpenControlsModal();

        if (controlsOpen)
        {
            if (WasDismissPressed())
                CloseControlsModal();
            return;
        }

        if (markerLesson && WasContinuePressed())
        {
            DismissPausingTip();
            return;
        }

        if (tipVisible && !pinned && !markerLesson && current != null && current.id != liveShowingId && Time.unscaledTime >= tipEndsAt)
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
                message = "Attack cracked walls to open up new pathways.",
                duration = 5.5f,
                showOnEvent = "near_breakable"
            },
            new Tip
            {
                id = "first_kill",
                message = "Defeat all enemies to progress.",
                duration = 5f,
                showOnEvent = "first_kill"
            },
            new Tip
            {
                id = "wall_broken",
                message = "Broken walls can be used for greater navigation.",
                duration = 4.5f,
                showOnEvent = "wall_broken"
            },
            new Tip
            {
                id = "loot",
                message = "Pistol dropped. Walk over it to pick it up.",
                duration = 6f,
                showOnEvent = "weapon_drop"
            },
            new Tip
            {
                id = "weapon_pickup",
                message = "Armed. Use the numeric keys to switch weapons.",
                duration = 6f,
                showOnEvent = "weapon_pickup"
            },
            new Tip
            {
                id = "damage",
                message = "Red markers show incoming damage. Keep an eye on your health and avoid taking damage.",
                duration = 5f,
                showOnEvent = "player_hurt"
            },
            new Tip
            {
                id = "health_pickup",
                message = "Health kits restore HP. Walk over one when you are hurt.",
                duration = 6f,
                showOnEvent = "health_pickup"
            },
            new Tip
            {
                id = "wave",
                message = "Make use of your radar to help find enemies",
                duration = 5f,
                showOnEvent = "wave_started"
            },
            new Tip
            {
                id = "clear",
                message = "All enemies defeated. Level complete",
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

        RebuildControlsUi();
        BeginControlsPause();
        SetControlsVisible(true);
        Record("Controls");
        AudioManager.TutorialPopup();
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

        // Continue on mouse / pad *release* so the Continue button can show press feedback first.
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasReleasedThisFrame || pad.startButton.wasReleasedThisFrame))
            return true;

        return false;
    }

    // ── Event popups (do not pause) ───────────────────────────────────────

    void TryShowNextQueued()
    {
        while (queue.Count > 0)
        {
            Tip next = queue.Dequeue();
            if (next == null)
                continue;
            if (next.id == "empty_mag" || next.id == "out_of_ammo")
            {
                DisplayLive(next);
                return;
            }
            if (shownIds.Contains(next.id))
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

        // Controls come first. A wall trigger at the start used to pause on scaled
        // time and the controls screen never opened, so every later tip stayed queued.
        if (!controlsDone)
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
        Record(tip.message);
        bool pausing = IsPausingTip(tip);
        tipEndsAt = pausing ? float.PositiveInfinity : Time.unscaledTime + Mathf.Max(1.5f, tip.duration);
        AudioManager.TutorialPopup();
        if (pausing)
            BeginMarkerLesson(tip);
    }

    static bool WasContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    void DismissPausingTip()
    {
        if (!markerLesson)
            return;

        AudioManager.UIClick();
        SetTipVisible(false);
        current = null;
        bool keepPaused = queue.Count > 0 && IsPausingTip(PeekQueue());
        EndMarkerLesson(restoreTime: !keepPaused);
        TryShowNextQueued();
    }

    Tip PeekQueue()
    {
        return queue.Count > 0 ? queue.Peek() : null;
    }

    static bool IsPausingTip(Tip tip)
    {
        if (tip == null)
            return false;
        return tip.id == "damage" || tip.showOnEvent == "player_hurt"
            || tip.id == "health_pickup" || tip.showOnEvent == "health_pickup"
            || tip.id == "break" || tip.showOnEvent == "near_breakable"
            || tip.id == "wall_broken" || tip.showOnEvent == "wall_broken"
            || tip.id == "wave" || tip.showOnEvent == "wave_started";
    }

    void BeginMarkerLesson(Tip tip)
    {
        if (markerLesson)
            return;

        markerLesson = true;
        if (Time.timeScale > 0f)
        {
            markerLessonPaused = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        EnsureMarkerDim();
        if (markerDim != null)
            markerDim.SetActive(true);
        EnsureContinueHint();
        if (continueHint != null)
            continueHint.SetActive(true);

        try
        {
            if (IsMarkerTip(tip))
            {
                DamageIndicatorUI markers = DamageIndicatorUI.EnsureExists();
                if (markers != null)
                    markers.BeginTutorialHighlight();
                if (UIManager.Instance != null)
                    UIManager.Instance.BeginHealthTutorialHighlight();
                return;
            }

            if (IsHealthPickupTip(tip))
            {
                MedKitPickup kit = FindNearestMedKit();
                if (kit != null)
                {
                    BeginCameraFocus(kit.transform.position);
                    HidePlayerVisuals();
                    TutorialWorldHighlight.ShowObject(kit.gameObject);
                }
                return;
            }

            if (tip.id == "wall_broken" || tip.showOnEvent == "wall_broken")
            {
                TutorialWorldHighlight.ShowPiece(focusBreak);
                focusBreak = null;
                return;
            }

            if (tip.id == "wave" || tip.showOnEvent == "wave_started")
            {
                MinimapUI radar = MinimapUI.EnsureExists();
                if (radar != null)
                    radar.BeginTutorialHighlight();
            }
        }
        catch (Exception)
        {
            TutorialWorldHighlight.ClearHighlight();
        }
    }

    static bool IsMarkerTip(Tip tip)
    {
        return tip != null && (tip.id == "damage" || tip.showOnEvent == "player_hurt");
    }

    static bool IsHealthPickupTip(Tip tip)
    {
        return tip != null && (tip.id == "health_pickup" || tip.showOnEvent == "health_pickup");
    }

    void EndMarkerLesson()
    {
        EndMarkerLesson(restoreTime: true);
    }

    void EndMarkerLesson(bool restoreTime)
    {
        if (!markerLesson)
            return;

        markerLesson = false;
        if (markerDim != null)
            markerDim.SetActive(false);

        DamageIndicatorUI markers = UnityEngine.Object.FindFirstObjectByType<DamageIndicatorUI>();
        if (markers != null)
            markers.EndTutorialHighlight();

        if (UIManager.Instance != null)
            UIManager.Instance.EndHealthTutorialHighlight();

        MinimapUI radar = UnityEngine.Object.FindFirstObjectByType<MinimapUI>();
        if (radar != null)
            radar.EndTutorialHighlight();

        EndCameraFocus();
        RestorePlayerVisuals();
        TutorialWorldHighlight.ClearHighlight();
        if (continueHint != null)
            continueHint.SetActive(false);

        if (!markerLessonPaused)
            return;

        if (!restoreTime)
            return;

        markerLessonPaused = false;
        if (!controlsOpen)
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
    }

    void BeginCameraFocus(Vector3 focus)
    {
        EndCameraFocus();

        cameraRig = PistolIntroCinematic.ResolveCameraRig();
        if (cameraRig == null)
            return;

        cameraParent = cameraRig.parent;
        cameraLocalPos = cameraRig.localPosition;
        cameraLocalRot = cameraRig.localRotation;
        Vector3 preferFrom = cameraRig.position;
        cameraRig.SetParent(null, true);
        cameraHeld = true;

        PistolIntroCinematic.Frame(focus, preferFrom, 2.4f, 1.15f, 0.2f, out Vector3 shotPos, out Quaternion shotRot);
        cameraRig.SetPositionAndRotation(shotPos, shotRot);
    }

    readonly List<Renderer> hiddenPlayerRenderers = new List<Renderer>();
    readonly List<bool> playerRendererWasEnabled = new List<bool>();

    void HidePlayerVisuals()
    {
        RestorePlayerVisuals();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        AddPlayerRenderers(player.GetComponentsInChildren<Renderer>(true));
        WeaponSwitcher switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.weapons != null)
        {
            foreach (GameObject weapon in switcher.weapons)
                if (weapon != null)
                    AddPlayerRenderers(weapon.GetComponentsInChildren<Renderer>(true));
        }

        for (int i = 0; i < hiddenPlayerRenderers.Count; i++)
        {
            Renderer renderer = hiddenPlayerRenderers[i];
            playerRendererWasEnabled.Add(renderer != null && renderer.enabled);
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    void AddPlayerRenderers(Renderer[] renderers)
    {
        if (renderers == null)
            return;
        foreach (Renderer renderer in renderers)
            if (renderer != null && !hiddenPlayerRenderers.Contains(renderer))
                hiddenPlayerRenderers.Add(renderer);
    }

    void RestorePlayerVisuals()
    {
        for (int i = 0; i < hiddenPlayerRenderers.Count; i++)
            if (hiddenPlayerRenderers[i] != null)
                hiddenPlayerRenderers[i].enabled = i < playerRendererWasEnabled.Count && playerRendererWasEnabled[i];
        hiddenPlayerRenderers.Clear();
        playerRendererWasEnabled.Clear();
    }
    void EndCameraFocus()
    {
        if (!cameraHeld)
            return;

        cameraHeld = false;
        if (cameraRig != null)
        {
            cameraRig.SetParent(cameraParent, false);
            cameraRig.localPosition = cameraLocalPos;
            cameraRig.localRotation = cameraLocalRot;
        }

        cameraRig = null;
        cameraParent = null;
    }

    static MedKitPickup FindNearestMedKit()
    {
        MedKitPickup[] kits = UnityEngine.Object.FindObjectsByType<MedKitPickup>(FindObjectsSortMode.None);
        if (kits == null || kits.Length == 0)
            return null;

        Vector3 from = Vector3.zero;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            from = player.transform.position;
        else if (Camera.main != null)
            from = Camera.main.transform.position;

        MedKitPickup best = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < kits.Length; i++)
        {
            MedKitPickup kit = kits[i];
            if (kit == null)
                continue;
            float dist = (kit.transform.position - from).sqrMagnitude;
            if (dist >= bestDist)
                continue;
            bestDist = dist;
            best = kit;
        }

        return best;
    }

    void EnsureMarkerDim()
    {
        if (markerDim != null)
            return;

        markerDim = new GameObject("MarkerLessonDim");
        Canvas canvas = markerDim.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = markerDim.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image dim = markerDim.AddComponent<Image>();
        dim.sprite = WhiteSprite();
        dim.color = new Color(0f, 0f, 0f, 0.62f);
        dim.raycastTarget = false;
        RectTransform rt = dim.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        markerDim.SetActive(false);
    }

    void EnsureContinueHint()
    {
        if (continueHint != null)
            return;

        GameObject canvasGo = EnsureCanvas();
        continueHint = new GameObject("ContinueHint");
        continueHint.transform.SetParent(canvasGo.transform, false);
        Image panelImage = continueHint.AddComponent<Image>();
        VigilanteUiStyle.StylePanel(panelImage);
        RectTransform rect = panelImage.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(280f, 56f);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(continueHint.transform, false);
        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.text = "Space to continue";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        VigilanteUiStyle.ApplyFont(label);
        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);
        continueHint.SetActive(false);
    }

    void UnbindWeaponSwitcher()
    {
        WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (switcher == null)
            return;
        switcher.WeaponChanged -= HandleWeaponChanged;
    }

    void BindWeaponSwitcher()
    {
        WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (switcher == null)
            return;
        switcher.WeaponChanged -= HandleWeaponChanged;
        switcher.WeaponChanged += HandleWeaponChanged;
    }

    void HandleWeaponChanged(int index, GameObject weapon)
    {
        if (lastWeaponIndex >= 0 && index != lastWeaponIndex)
        {
            HideLive("empty_mag");
            HideLive("out_of_ammo");
        }
        lastWeaponIndex = index;
    }

    void Record(string message)
    {
        if (string.IsNullOrEmpty(message) || appeared.Contains(message))
            return;
        appeared.Add(message);
    }

    public static string[] GetLog()
    {
        if (Instance == null || Instance.appeared.Count == 0)
            return System.Array.Empty<string>();
        return Instance.appeared.ToArray();
    }

    public static void NotifyWallBroken(Break piece)
    {
        focusBreak = piece;
        Notify("wall_broken");
    }

    public static void ShowLive(string id, string message)
    {
        if (Instance == null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(message))
            return;
        Instance.HandleShowLive(id, message);
    }

    public static void HideLive(string id)
    {
        if (Instance == null || string.IsNullOrEmpty(id))
            return;
        Instance.HandleHideLive(id);
    }

    void HandleShowLive(string id, string message)
    {
        if (liveShowingId == id || QueueContains(id))
            return;

        Tip tip = new Tip { id = id, message = message, duration = 999f };
        if (controlsOpen || markerLesson || tipVisible || queue.Count > 0)
        {
            queue.Enqueue(tip);
            return;
        }

        DisplayLive(tip);
    }

    void HandleHideLive(string id)
    {
        RemoveQueued(id);
        if (liveShowingId != id)
            return;

        liveShowingId = null;
        current = null;
        SetTipVisible(false);
        TryShowNextQueued();
    }

    void DisplayLive(Tip tip)
    {
        current = tip;
        liveShowingId = tip.id;
        EnsureTipUi();
        if (promptText != null)
            promptText.text = tip.message;
        SetTipVisible(true);
        tipEndsAt = float.PositiveInfinity;
        Record(tip.message);
        AudioManager.TutorialPopup();
    }

    bool QueueContains(string id)
    {
        int count = queue.Count;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Tip tip = queue.Dequeue();
            if (tip != null && tip.id == id)
                found = true;
            queue.Enqueue(tip);
        }
        return found;
    }

    void RemoveQueued(string id)
    {
        int count = queue.Count;
        for (int i = 0; i < count; i++)
        {
            Tip tip = queue.Dequeue();
            if (tip != null && tip.id == id)
                continue;
            queue.Enqueue(tip);
        }
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

        if (tip.id == "empty_mag" || tip.id == "out_of_ammo")
        {
            DisplayLive(tip);
            return;
        }

        ShowTip(tip);
    }

    bool pinned;
    bool markerLesson;
    bool markerLessonPaused;
    GameObject markerDim;
    readonly List<string> appeared = new List<string>();
    string liveShowingId;
    int lastWeaponIndex = -1;
    static Break focusBreak;
    GameObject continueHint;
    bool cameraHeld;
    Transform cameraRig;
    Transform cameraParent;
    Vector3 cameraLocalPos;
    Quaternion cameraLocalRot;

    public static void ShowPinned(string message)
    {
        if (Instance == null || string.IsNullOrEmpty(message))
            return;

        Instance.EnsureTipUi();
        Instance.EndMarkerLesson();
        Instance.liveShowingId = null;
        Instance.Record(message);
        Instance.current = null;
        Instance.pinned = true;
        if (Instance.promptText != null)
            Instance.promptText.text = message;
        Instance.SetTipVisible(true);
        Instance.tipEndsAt = float.PositiveInfinity;
    }

    public static void HidePinned()
    {
        if (Instance == null || !Instance.pinned)
            return;

        Instance.pinned = false;
        Instance.current = null;
        Instance.SetTipVisible(false);
        Instance.TryShowNextQueued();
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

        if (eventId == "player_hurt" && eventTips.TryGetValue("health_pickup", out Tip health))
            EnqueueOrShow(health);
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
        {
            VigilanteUiStyle.StylePanel(panel);
            VigilanteUiStyle.ApplyFont(promptText);
            return;
        }

        GameObject canvasGo = EnsureCanvas();

        Transform panelTf = canvasGo.transform.Find("TutorialPanel");
        GameObject panelGo = panelTf != null ? panelTf.gameObject : null;
        if (panelGo == null)
        {
            panelGo = new GameObject("TutorialPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            panel = panelGo.AddComponent<Image>();
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.12f);
            prt.anchorMax = new Vector2(0.5f, 0.12f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(640f, 110f);
        }
        else
        {
            panel = panelGo.GetComponent<Image>();
        }

        VigilanteUiStyle.StylePanel(panel);

        Transform textTf = panelGo.transform.Find("TutorialText");
        if (textTf == null)
        {
            GameObject textGo = new GameObject("TutorialText");
            textGo.transform.SetParent(panelGo.transform, false);
            promptText = textGo.AddComponent<TextMeshProUGUI>();
            promptText.fontSize = 24f;
            promptText.alignment = TextAlignmentOptions.Center;
            RectTransform trt = promptText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(28f, 18f);
            trt.offsetMax = new Vector2(-28f, -18f);
        }
        else
        {
            promptText = textTf.GetComponent<TextMeshProUGUI>();
        }

        VigilanteUiStyle.ApplyFont(promptText);
    }

    void RebuildControlsUi()
    {
        GameObject canvasGo = EnsureCanvas();

        if (controlsRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(controlsRoot);
            controlsRoot = null;
        }

        Transform existing = canvasGo.transform.Find("ControlsModal");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        // Also clear any stray modal left under other canvases.
        GameObject stray = GameObject.Find("ControlsModal");
        if (stray != null)
            UnityEngine.Object.DestroyImmediate(stray);

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
        RectTransform boxRt = boxImg.rectTransform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 420f);
        VigilanteUiStyle.StylePanel(boxImg);

        TextMeshProUGUI title = CreateTmp(box.transform, "Title", "CONTROLS", 34f, FontStyles.Bold);
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -28f);
        titleRt.sizeDelta = new Vector2(-48f, 44f);
        title.alignment = TextAlignmentOptions.Center;

        string[][] rows =
        {
            new[] { "WASD", "Move" },
            new[] { "Mouse", "Look" },
            new[] { "Shift", "Sprint" },
            new[] { "C / Ctrl", "Crouch" },
            new[] { "Alt / Q", "Dash" },
            new[] { "Left Click", "Swing bat / Fire" },
            new[] { "1–4 / Scroll", "Switch weapons" },
            new[] { "Esc", "Pause" },
        };
        BuildControlBindings(box.transform, rows);

        GameObject btnGo = new GameObject("Continue");
        btnGo.transform.SetParent(box.transform, false);
        Image btnImg = btnGo.AddComponent<Image>();
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        RectTransform btnRt = btnImg.rectTransform;
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, 50f);
        btnRt.sizeDelta = new Vector2(200f, 44f);

        TextMeshProUGUI btnLabel = CreateTmp(btnGo.transform, "Label", "CONTINUE", 20f, FontStyles.Bold);
        RectTransform btnLabelRt = btnLabel.rectTransform;
        btnLabelRt.anchorMin = Vector2.zero;
        btnLabelRt.anchorMax = Vector2.one;
        btnLabelRt.offsetMin = Vector2.zero;
        btnLabelRt.offsetMax = Vector2.zero;
        btnLabel.alignment = TextAlignmentOptions.Center;

        VigilanteUiStyle.StyleInvertedButton(btn, CloseControlsModal);

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

    static void BuildControlBindings(Transform box, string[][] rows)
    {
        GameObject list = new GameObject("Bindings", typeof(RectTransform));
        list.transform.SetParent(box, false);
        RectTransform listRt = list.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0f);
        listRt.anchorMax = new Vector2(1f, 1f);
        listRt.offsetMin = new Vector2(40f, 88f);
        listRt.offsetMax = new Vector2(-40f, -84f);

        const float rowHeight = 32f;
        float totalHeight = rows.Length * rowHeight;
        float startY = totalHeight * 0.5f - rowHeight * 0.5f;

        for (int i = 0; i < rows.Length; i++)
        {
            float y = startY - i * rowHeight;
            CreateBindingRow(list.transform, rows[i][0], rows[i][1], y, rowHeight);
        }
    }

    static void CreateBindingRow(Transform parent, string input, string action, float y, float height)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0.5f);
        rowRt.anchorMax = new Vector2(1f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0f, y);
        rowRt.sizeDelta = new Vector2(0f, height);

        TextMeshProUGUI inputTmp = CreateTmp(row.transform, "Input", input, 20f, FontStyles.Normal);
        RectTransform inputRt = inputTmp.rectTransform;
        inputRt.anchorMin = new Vector2(0f, 0f);
        inputRt.anchorMax = new Vector2(0.42f, 1f);
        inputRt.offsetMin = Vector2.zero;
        inputRt.offsetMax = Vector2.zero;
        inputTmp.alignment = TextAlignmentOptions.MidlineLeft;

        TextMeshProUGUI dashTmp = CreateTmp(row.transform, "Dash", "-", 20f, FontStyles.Normal);
        RectTransform dashRt = dashTmp.rectTransform;
        dashRt.anchorMin = new Vector2(0.42f, 0f);
        dashRt.anchorMax = new Vector2(0.58f, 1f);
        dashRt.offsetMin = Vector2.zero;
        dashRt.offsetMax = Vector2.zero;
        dashTmp.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI actionTmp = CreateTmp(row.transform, "Action", action, 20f, FontStyles.Normal);
        RectTransform actionRt = actionTmp.rectTransform;
        actionRt.anchorMin = new Vector2(0.58f, 0f);
        actionRt.anchorMax = new Vector2(1f, 1f);
        actionRt.offsetMin = Vector2.zero;
        actionRt.offsetMax = Vector2.zero;
        actionTmp.alignment = TextAlignmentOptions.MidlineRight;
    }

    static GameObject EnsureCanvas()
    {
        GameObject canvasGo = GameObject.Find("TutorialCanvas");
        if (canvasGo != null)
        {
            Canvas existing = canvasGo.GetComponent<Canvas>();
            if (existing != null)
                existing.sortingOrder = 5200;
            return canvasGo;
        }

        canvasGo = new GameObject("TutorialCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
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
        tmp.raycastTarget = false;
        VigilanteUiStyle.ApplyFont(tmp);
        return tmp;
    }

    static Sprite WhiteSprite()
    {
        return VigilanteUiStyle.WhiteSprite();
    }
}
