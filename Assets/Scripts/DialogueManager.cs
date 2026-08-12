using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Subtitle-style dialogue for player, enemies, boss, and announcer lines.
/// Uses optional clips when assigned; otherwise shows text only.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI subtitleText;
    public float defaultDuration = 2.4f;

    [Header("Optional voice clips")]
    public AudioClip[] playerLines;
    public AudioClip[] enemyLines;
    public AudioClip[] bossLines;

    float hideAt;
    readonly Queue<(string text, float duration)> queue = new Queue<(string, float)>();
    bool showing;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUi();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (showing && Time.unscaledTime >= hideAt)
        {
            showing = false;
            if (subtitleText != null)
                subtitleText.text = string.Empty;

            if (queue.Count > 0)
            {
                var next = queue.Dequeue();
                ShowInternal(next.text, next.duration);
            }
        }
    }

    void EnsureUi()
    {
        if (subtitleText != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("DialogueCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject textGo = new GameObject("DialogueSubtitle");
        textGo.transform.SetParent(canvas.transform, false);
        subtitleText = textGo.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = 28f;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = Color.white;
        subtitleText.text = string.Empty;

        RectTransform rt = subtitleText.rectTransform;
        rt.anchorMin = new Vector2(0.1f, 0.08f);
        rt.anchorMax = new Vector2(0.9f, 0.18f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void ShowInternal(string text, float duration)
    {
        EnsureUi();
        showing = true;
        hideAt = Time.unscaledTime + duration;
        if (subtitleText != null)
            subtitleText.text = text;
    }

    void Enqueue(string text, float duration)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!showing)
            ShowInternal(text, duration);
        else
            queue.Enqueue((text, duration));
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        GameObject go = new GameObject("DialogueManager");
        go.AddComponent<DialogueManager>();
    }

    public static void PlayerLine(string text)
    {
        EnsureExists();
        Instance.Enqueue($"<color=#7FDBFF>You:</color> {text}", Instance.defaultDuration);
        Instance.PlayRandom(Instance.playerLines);
    }

    public static void BossLine(string text)
    {
        EnsureExists();
        Instance.Enqueue($"<color=#FF6B6B>Boss:</color> {text}", Instance.defaultDuration + 0.6f);
        Instance.PlayRandom(Instance.bossLines);
        AudioManager.BossVoice();
    }

    public static void Announcer(string text)
    {
        EnsureExists();
        Instance.Enqueue($"<color=#FFE66D>{text}</color>", 1.6f);
    }

    public static void EnemyBark(Vector3 position, string context)
    {
        EnsureExists();
        string line = PickBark(context);
        // Soft bark: occasional subtitle, always spatial beep.
        if (Random.value < 0.35f)
            Instance.Enqueue($"<color=#FF9F43>Enemy:</color> {line}", 1.4f);
        AudioManager.EnemyVoice(position);
        Instance.PlayRandom(Instance.enemyLines);
    }

    static string PickBark(string context)
    {
        switch (context)
        {
            case "fire": return Random.value < 0.5f ? "Open fire!" : "Drop him!";
            case "hurt": return Random.value < 0.5f ? "I'm hit!" : "Argh!";
            case "flank": return "Flank him!";
            case "breach": return "They're breaking through!";
            case "grenade": return "Grenade!";
            case "death": return "Ugh...";
            default: return "Over there!";
        }
    }

    void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        AudioManager.Play(clip, 0.9f);
    }
}
