using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the Main Menu Volume and Brightness sliders to GameSettings.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Slider volumeSlider;
    public Slider brightnessSlider;

    bool suppress;

    void Awake()
    {
        FindSliders();
    }

    void OnEnable()
    {
        FindSliders();
        Wire(volumeSlider, OnVolumeChanged);
        Wire(brightnessSlider, OnBrightnessChanged);
        RefreshFromPrefs();
    }

    void OnDisable()
    {
        Unwire(volumeSlider, OnVolumeChanged);
        Unwire(brightnessSlider, OnBrightnessChanged);
    }

    public static void EnsureOn(GameObject panel)
    {
        if (panel == null)
            return;

        SettingsMenu menu = panel.GetComponent<SettingsMenu>();
        if (menu == null)
            menu = panel.AddComponent<SettingsMenu>();
        menu.FindSliders();
        menu.RefreshFromPrefs();
    }

    void FindSliders()
    {
        if (volumeSlider == null)
        {
            Transform t = FindChildByName(transform, "Volume");
            if (t != null)
                volumeSlider = t.GetComponent<Slider>();
        }

        if (brightnessSlider == null)
        {
            Transform t = FindChildByName(transform, "Brightness");
            if (t != null)
                brightnessSlider = t.GetComponent<Slider>();
        }
    }

    void RefreshFromPrefs()
    {
        suppress = true;
        SetSlider(volumeSlider, GameSettings.Volume);
        SetSlider(brightnessSlider, GameSettings.Brightness);
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
}
