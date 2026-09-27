using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Level 2 story ending and final menu choices.</summary>
public class Level2Ending : MonoBehaviour
{
    static Level2Ending instance;

    readonly string[] endingLines =
    {
        "Two floors cleared.",
        "The rest of the building remains.",
        "And they’re waiting."
    };

    CanvasGroup contentGroup;
    TextMeshProUGUI message;
    TextMeshProUGUI continueHint;
    TextMeshProUGUI totalTimeText;
    GameObject buttonsRoot;
    bool active;
    bool transitioning;
    bool finished;
    int lineIndex;

    public static void Show()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("Level2Ending");
        instance = go.AddComponent<Level2Ending>();
        instance.Begin();
    }

    void Begin()
    {
        BuildUi();
        active = true;
        lineIndex = 0;
        message.text = endingLines[lineIndex];
        contentGroup.alpha = 1f;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    float savedTimeScale = 1f;

    void Update()
    {
        if (active && !transitioning && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            StartCoroutine(Advance());
    }

    IEnumerator Advance()
    {
        transitioning = true;
        yield return FadeContent(1f, 0f, 0.35f);
        lineIndex++;

        if (lineIndex < endingLines.Length)
        {
            message.text = endingLines[lineIndex];
            yield return FadeContent(0f, 1f, 0.35f);
        }
        else
        {
            ShowFinalChoices();
            yield return FadeContent(0f, 1f, 0.45f);
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;
            active = false;
            finished = true;
        }

        transitioning = false;
    }

    void ShowFinalChoices()
    {
        message.text = "Thanks for playing";
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0.1f, 0.58f);
        messageRect.anchorMax = new Vector2(0.9f, 0.7f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        totalTimeText.text = $"Elapsed time: {FormatElapsedTime(GameProgression.TotalElapsedSeconds)}";
        totalTimeText.gameObject.SetActive(true);
        continueHint.gameObject.SetActive(false);
        buttonsRoot.SetActive(true);
    }

    static string FormatElapsedTime(float elapsedSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds / 60) % 60;
        int seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{totalSeconds / 60:00}:{seconds:00}";
    }

    IEnumerator FadeContent(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            contentGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        contentGroup.alpha = to;
    }

    void BuildUi()
    {
        GameObject canvasGo = new GameObject("Level2EndingCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5300;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("Level2EndingEventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject background = new GameObject("BlackBackground", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasGo.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(background.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentGroup = content.GetComponent<CanvasGroup>();
        contentGroup.interactable = false;
        contentGroup.blocksRaycasts = false;

        message = CreateText(content.transform, "EndingLine", string.Empty, 34f);
        message.alignment = TextAlignmentOptions.Center;
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0.1f, 0.42f);
        messageRect.anchorMax = new Vector2(0.9f, 0.58f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        totalTimeText = CreateText(content.transform, "TotalElapsedTime", string.Empty, 20f);
        totalTimeText.alignment = TextAlignmentOptions.Center;
        totalTimeText.color = new Color(1f, 1f, 1f, 0.75f);
        totalTimeText.gameObject.SetActive(false);
        RectTransform totalTimeRect = totalTimeText.rectTransform;
        totalTimeRect.anchorMin = new Vector2(0.1f, 0.48f);
        totalTimeRect.anchorMax = new Vector2(0.9f, 0.55f);
        totalTimeRect.offsetMin = Vector2.zero;
        totalTimeRect.offsetMax = Vector2.zero;

        continueHint = CreateText(content.transform, "ContinueHint", "Space to continue", 18f);
        continueHint.alignment = TextAlignmentOptions.Center;
        continueHint.color = new Color(1f, 1f, 1f, 0.7f);
        RectTransform hintRect = continueHint.rectTransform;
        hintRect.anchorMin = new Vector2(0.2f, 0.12f);
        hintRect.anchorMax = new Vector2(0.8f, 0.2f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        buttonsRoot = new GameObject("EndingButtons", typeof(RectTransform));
        buttonsRoot.transform.SetParent(content.transform, false);
        RectTransform buttonsRect = buttonsRoot.GetComponent<RectTransform>();
        buttonsRect.anchorMin = Vector2.zero;
        buttonsRect.anchorMax = Vector2.one;
        buttonsRect.offsetMin = Vector2.zero;
        buttonsRect.offsetMax = Vector2.zero;
        buttonsRoot.SetActive(false);
        CreateButton(buttonsRoot.transform, "RETURN TO MAIN MENU", 0.39f, ReturnToMenu);
        CreateButton(buttonsRoot.transform, "RESTART GAME", 0.27f, RestartFromLevelOne);
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.raycastTarget = false;
        VigilanteUiStyle.ApplyFont(label);
        return label;
    }

    static void CreateButton(Transform parent, string text, float y, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(text.Replace(' ', '_'), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, y);
        rect.anchorMax = new Vector2(0.5f, y);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(380f, 54f);

        TextMeshProUGUI label = CreateText(go.transform, "Label", text, 19f);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        VigilanteUiStyle.StyleInvertedButtonPreserveActions(button);
    }

    void ReturnToMenu()
    {
        AudioManager.UIClick();
        SceneFade.FadeToBlack(GameProgression.ReturnToMainMenu, 0.8f);
    }

    void RestartFromLevelOne()
    {
        AudioManager.UIClick();
        SceneFade.FadeToBlack(() => GameProgression.StartLevel(1), 0.8f);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (finished || active)
        {
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
