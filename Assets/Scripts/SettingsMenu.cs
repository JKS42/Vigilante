using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds Volume, Brightness, and Mouse Sensitivity sliders to GameSettings.
/// Builds the in-game settings panel when the scene panel is empty.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Slider volumeSlider;
    public Slider brightnessSlider;
    public Slider sensitivitySlider;
    public Button backButton;

    bool suppress;
    UnityEngine.Events.UnityAction backAction;

    public static SettingsMenu EnsureOn(GameObject panel, UnityEngine.Events.UnityAction onBack = null)
    {
        if (panel == null)
            return null;

        SettingsMenu menu = panel.GetComponent<SettingsMenu>();
        if (menu == null)
            menu = panel.AddComponent<SettingsMenu>();

        menu.BuildIfNeeded();
        menu.FindSliders();
        menu.WireBack(onBack);
        menu.ApplyStandardLayout();
        if (panel.activeInHierarchy)
            menu.RefreshFromPrefs();
        return menu;
    }

    void ApplyStandardLayout()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600f, 700f);
        }
        VigilanteUiStyle.StyleSettingsPanel(gameObject);

        TextMeshProUGUI title = null;
        TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && (labels[i].name == "SettingsTitle" || labels[i].text.Trim().Equals("SETTINGS", System.StringComparison.OrdinalIgnoreCase)))
            {
                title = labels[i];
                break;
            }
        }
        if (title == null)
            title = CreateLabel(transform, "SettingsTitle", "SETTINGS", new Vector2(0f, 260f), new Vector2(420f, 56f), 42f);
        title.gameObject.name = "SettingsTitle";
        title.text = "SETTINGS";
        title.fontSize = 42f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 260f);
        titleRect.sizeDelta = new Vector2(420f, 56f);
        VigilanteUiStyle.ApplyFont(title);

        NormalizeSlider(volumeSlider, "VOLUME", new Vector2(0f, 80f));
        NormalizeSlider(brightnessSlider, "BRIGHTNESS", new Vector2(0f, -40f));
        NormalizeSlider(sensitivitySlider, "SENSITIVITY", new Vector2(0f, -160f));

        if (backButton != null)
        {
            RectTransform backRect = backButton.transform as RectTransform;
            if (backRect != null)
            {
                backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0.5f);
                backRect.anchoredPosition = new Vector2(0f, -280f);
                backRect.sizeDelta = new Vector2(200f, 40f);
                backRect.localScale = Vector3.one;
            }
            VigilanteUiStyle.StyleSettingsButton(backButton);
        }
    }

    static void NormalizeSlider(Slider slider, string labelText, Vector2 position)
    {
        if (slider == null)
            return;

        RectTransform root = slider.transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            root.sizeDelta = new Vector2(460f, 64f);
            root.localScale = Vector3.one;
        }

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;

        TextMeshProUGUI[] labels = slider.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels.Length > 0)
        {
            TextMeshProUGUI label = labels[0];
            label.text = labelText;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 22f);
            labelRect.sizeDelta = new Vector2(460f, 28f);
            VigilanteUiStyle.ApplyFont(label);
        }

        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = VigilanteUiStyle.WhiteSprite();
                fill.color = new Color(0.92f, 0.86f, 0.55f, 1f);
            }
            RectTransform fillArea = slider.fillRect.parent as RectTransform;
            if (fillArea != null)
            {
                if (fillArea.parent == slider.transform)
                {
                    fillArea.anchorMin = fillArea.anchorMax = fillArea.pivot = new Vector2(0.5f, 0.5f);
                    fillArea.anchoredPosition = new Vector2(0f, -12f);
                    fillArea.sizeDelta = new Vector2(404f, 11f);
                    slider.fillRect.anchorMin = Vector2.zero;
                    slider.fillRect.anchorMax = Vector2.one;
                    slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
                }
                else
                {
                    fillArea.anchorMin = new Vector2(0f, 0.25f);
                    fillArea.anchorMax = new Vector2(1f, 0.75f);
                    fillArea.offsetMin = new Vector2(8f, 0f);
                    fillArea.offsetMax = new Vector2(-8f, 0f);
                }
            }
        }

        if (slider.handleRect != null)
        {
            RectTransform handleArea = slider.handleRect.parent as RectTransform;
            if (handleArea != null)
            {
                if (handleArea.parent == slider.transform)
                {
                    handleArea.anchorMin = handleArea.anchorMax = handleArea.pivot = new Vector2(0.5f, 0.5f);
                    handleArea.anchoredPosition = new Vector2(0f, -12f);
                    handleArea.sizeDelta = new Vector2(400f, 22f);
                }
                else
                {
                    handleArea.anchorMin = Vector2.zero;
                    handleArea.anchorMax = Vector2.one;
                    handleArea.offsetMin = new Vector2(10f, 0f);
                    handleArea.offsetMax = new Vector2(-10f, 0f);
                }
            }
            slider.handleRect.anchorMin = slider.handleRect.anchorMax = slider.handleRect.pivot = new Vector2(0.5f, 0.5f);
            slider.handleRect.sizeDelta = new Vector2(22f, 22f);
        }

        Transform bgTransform = FindChildByName(slider.transform, "Background");
        Image background = bgTransform != null ? bgTransform.GetComponent<Image>() : null;
        if (background != null)
        {
            background.sprite = VigilanteUiStyle.WhiteSprite();
            background.color = new Color(0.22f, 0.22f, 0.24f, 1f);
            RectTransform backgroundRect = background.rectTransform;
            if (backgroundRect.parent == slider.transform)
            {
                backgroundRect.anchorMin = backgroundRect.anchorMax = backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                backgroundRect.anchoredPosition = new Vector2(0f, -12f);
                backgroundRect.sizeDelta = new Vector2(420f, 11f);
            }
            else
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.25f);
                backgroundRect.anchorMax = new Vector2(1f, 0.75f);
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
            }
        }
        Image handle = slider.targetGraphic as Image;
        if (handle != null)
        {
            handle.sprite = VigilanteUiStyle.WhiteSprite();
            handle.color = Color.white;
        }
    }
    void Awake()
    {
        FindSliders();
    }

    void OnEnable()
    {
        FindSliders();
        Wire(volumeSlider, OnVolumeChanged);
        Wire(brightnessSlider, OnBrightnessChanged);
        Wire(sensitivitySlider, OnSensitivityChanged);
        RefreshFromPrefs();
    }

    void OnDisable()
    {
        Unwire(volumeSlider, OnVolumeChanged);
        Unwire(brightnessSlider, OnBrightnessChanged);
        Unwire(sensitivitySlider, OnSensitivityChanged);
    }

    void BuildIfNeeded()
    {
        if (GetComponentInChildren<Slider>(true) != null)
        {
            EnsureSensitivitySlider();
            return;
        }

        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.94f);

        CreateLabel(transform, "SettingsTitle", "SETTINGS", new Vector2(0f, 260f), new Vector2(420f, 56f), 42f);

        volumeSlider = CreateLabeledSlider(transform, "Volume", "VOLUME", new Vector2(0f, 80f));
        brightnessSlider = CreateLabeledSlider(transform, "Brightness", "BRIGHTNESS", new Vector2(0f, -40f));
        sensitivitySlider = CreateLabeledSlider(transform, "Sensitivity", "SENSITIVITY", new Vector2(0f, -160f));
        backButton = CreateBackButton(transform, new Vector2(0f, -280f));
    }

    void EnsureSensitivitySlider()
    {
        if (FindChildByName(transform, "Sensitivity") != null)
            return;

        Transform volume = FindChildByName(transform, "Volume");
        if (volume == null)
            return;

        GameObject clone = Instantiate(volume.gameObject, volume.parent);
        clone.name = "Sensitivity";
        clone.transform.SetSiblingIndex(volume.GetSiblingIndex() + 2);

        RectTransform cloneRt = clone.GetComponent<RectTransform>();
        RectTransform volumeRt = volume.GetComponent<RectTransform>();
        Transform brightness = FindChildByName(transform, "Brightness");
        RectTransform brightnessRt = brightness != null ? brightness.GetComponent<RectTransform>() : null;

        Vector2 spacing = brightnessRt != null
            ? brightnessRt.anchoredPosition - volumeRt.anchoredPosition
            : new Vector2(0f, -136f);
        Vector2 origin = brightnessRt != null ? brightnessRt.anchoredPosition : volumeRt.anchoredPosition;
        cloneRt.anchoredPosition = origin + spacing;

        RelabelClonedSlider(clone.transform, "SENSITIVITY");

        sensitivitySlider = clone.GetComponent<Slider>();
        if (sensitivitySlider == null)
            sensitivitySlider = clone.GetComponentInChildren<Slider>(true);
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveAllListeners();

        Transform back = FindChildByName(transform, "Back");
        if (back != null)
        {
            RectTransform backRt = back.GetComponent<RectTransform>();
            float desiredY = Mathf.Min(backRt.anchoredPosition.y, cloneRt.anchoredPosition.y - 86f);
            backRt.anchoredPosition = new Vector2(backRt.anchoredPosition.x, desiredY);
        }
    }

    static void RelabelClonedSlider(Transform root, string label)
    {
        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels == null || labels.Length == 0)
            return;

        TextMeshProUGUI tmp = labels[0];
        tmp.text = label;
        tmp.gameObject.name = "SensitivityLabel";

        RectTransform rt = tmp.rectTransform;
        const float minWidth = 280f;
        if (rt.sizeDelta.x < minWidth)
        {
            float left = rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f;
            rt.sizeDelta = new Vector2(minWidth, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(left + minWidth * 0.5f, rt.anchoredPosition.y);
        }
    }

    void FindSliders()
    {
        if (volumeSlider == null)
        {
            Transform t = FindChildByName(transform, "Volume");
            if (t != null)
            {
                volumeSlider = t.GetComponent<Slider>();
                if (volumeSlider == null)
                    volumeSlider = t.GetComponentInChildren<Slider>(true);
            }
        }

        if (brightnessSlider == null)
        {
            Transform t = FindChildByName(transform, "Brightness");
            if (t != null)
            {
                brightnessSlider = t.GetComponent<Slider>();
                if (brightnessSlider == null)
                    brightnessSlider = t.GetComponentInChildren<Slider>(true);
            }
        }

        if (sensitivitySlider == null)
        {
            Transform t = FindChildByName(transform, "Sensitivity");
            if (t != null)
            {
                sensitivitySlider = t.GetComponent<Slider>();
                if (sensitivitySlider == null)
                    sensitivitySlider = t.GetComponentInChildren<Slider>(true);
            }
        }

        if (backButton == null)
        {
            Transform t = FindChildByName(transform, "Back");
            if (t != null)
                backButton = t.GetComponent<Button>();
        }
    }

    void WireBack(UnityEngine.Events.UnityAction onBack)
    {
        if (backButton == null || onBack == null)
            return;

        if (backAction != null)
            backButton.onClick.RemoveListener(backAction);

        backAction = onBack;
        backButton.onClick.RemoveListener(backAction);
        backButton.onClick.AddListener(backAction);
    }

    void RefreshFromPrefs()
    {
        suppress = true;
        SetSlider(volumeSlider, GameSettings.Volume);
        SetSlider(brightnessSlider, GameSettings.Brightness);
        SetSlider(sensitivitySlider, GameSettings.MouseSensitivity);
        suppress = false;
        GameSettings.ApplyAll();
    }

    void OnVolumeChanged(float value)
    {
        if (suppress || volumeSlider == null)
            return;
        GameSettings.Volume = SliderToUnit(volumeSlider, value);
    }

    void OnBrightnessChanged(float value)
    {
        if (suppress || brightnessSlider == null)
            return;
        GameSettings.Brightness = SliderToUnit(brightnessSlider, value);
    }

    void OnSensitivityChanged(float value)
    {
        if (suppress || sensitivitySlider == null)
            return;
        GameSettings.MouseSensitivity = SliderToUnit(sensitivitySlider, value);
    }

    static Slider CreateLabeledSlider(Transform parent, string objectName, string label, Vector2 position)
    {
        GameObject root = new GameObject(objectName);
        root.layer = 5;
        root.transform.SetParent(parent, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(460f, 64f);
        rootRt.anchoredPosition = position;

        CreateLabel(root.transform, objectName + "Label", label, new Vector2(0f, 22f), new Vector2(460f, 28f), 24f);

        GameObject sliderGo = new GameObject("Track");
        sliderGo.layer = 5;
        sliderGo.transform.SetParent(root.transform, false);
        RectTransform sliderRt = sliderGo.AddComponent<RectTransform>();
        sliderRt.anchorMin = sliderRt.anchorMax = sliderRt.pivot = new Vector2(0.5f, 0.5f);
        sliderRt.sizeDelta = new Vector2(420f, 22f);
        sliderRt.anchoredPosition = new Vector2(0f, -12f);

        Image background = CreateImage(sliderGo.transform, "Background", new Color(0.22f, 0.22f, 0.24f, 1f));
        Stretch(background.rectTransform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

        RectTransform fillArea = CreateRect(sliderGo.transform, "Fill Area");
        Stretch(fillArea, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        Image fill = CreateImage(fillArea, "Fill", new Color(0.92f, 0.86f, 0.55f, 1f));
        Stretch(fill.rectTransform);

        RectTransform handleArea = CreateRect(sliderGo.transform, "Handle Slide Area");
        Stretch(handleArea, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        Image handle = CreateImage(handleArea, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.value = 80f;
        return slider;
    }

    static Button CreateBackButton(Transform parent, Vector2 position)
    {
        GameObject go = new GameObject("Back");
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 40f);
        rt.anchoredPosition = position;

        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        CreateLabel(go.transform, "Label", "BACK", Vector2.zero, Vector2.zero, 24f, stretch: true);
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

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        Stretch(rt, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void SetSlider(Slider slider, float unit)
    {
        if (slider == null)
            return;

        float min = slider.minValue;
        float max = slider.maxValue;
        if (Mathf.Approximately(max, min))
            max = min + 1f;
        slider.SetValueWithoutNotify(Mathf.Lerp(min, max, Mathf.Clamp01(unit)));
    }

    static float SliderToUnit(Slider slider, float value)
    {
        float min = slider.minValue;
        float max = slider.maxValue;
        if (Mathf.Approximately(max, min))
            return 0f;
        return Mathf.Clamp01((value - min) / (max - min));
    }

    static void Wire(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
            return;
        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    static void Unwire(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
            return;
        slider.onValueChanged.RemoveListener(action);
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

    static Sprite WhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }
}
