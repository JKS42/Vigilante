using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Persisted master volume, brightness, and mouse look sensitivity. Applied in every scene.
/// </summary>
public static class GameSettings
{
    const string VolumeKey = "Vigilante.Volume";
    const string BrightnessKey = "Vigilante.Brightness";
    const string MouseSensitivityKey = "Vigilante.MouseSensitivity";
    const float MouseSensitivityMin = 0.01f;
    const float MouseSensitivityMax = 2f;

    static Volume brightnessVolume;
    static ColorAdjustments colorAdjustments;
    static CanvasGroup overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnLoad()
    {
        ApplyAll();
    }

    public static float Volume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyVolume();
        }
    }

    public static float Brightness
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, 1f));
        set
        {
            PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyBrightness();
        }
    }

    /// <summary>
    /// Slider unit 0–1. Default 0.5 maps to the current inspector look speed.
    /// </summary>
    public static float MouseSensitivity
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MouseSensitivityKey, 0.5f));
        set
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    public static float MouseSensitivityMultiplier =>
        Mathf.Lerp(MouseSensitivityMin, MouseSensitivityMax, MouseSensitivity);

    public static void ApplyAll()
    {
        ApplyVolume();
        ApplyBrightness();
    }

    public static void ApplyVolume()
    {
        float volume = Volume;
        AudioListener.volume = volume;
        if (AudioManager.Instance != null)
            AudioManager.Instance.ApplyMasterVolume(volume);
    }

    public static void ApplyBrightness()
    {
        float brightness = Brightness;
        EnsureBrightnessFx();

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Lerp(-1.5f, 0.4f, brightness);
        }

        if (overlay != null)
            overlay.alpha = Mathf.Clamp01(1f - brightness) * 0.7f;
    }

    static void EnsureBrightnessFx()
    {
        if (brightnessVolume == null)
        {
            GameObject go = GameObject.Find("GameSettingsVolume");
            if (go == null)
            {
                go = new GameObject("GameSettingsVolume");
                Object.DontDestroyOnLoad(go);
            }

            brightnessVolume = go.GetComponent<Volume>();
            if (brightnessVolume == null)
                brightnessVolume = go.AddComponent<Volume>();

            brightnessVolume.isGlobal = true;
            brightnessVolume.priority = 100f;
            if (brightnessVolume.profile == null)
                brightnessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            if (!brightnessVolume.profile.TryGet(out colorAdjustments))
                colorAdjustments = brightnessVolume.profile.Add<ColorAdjustments>(true);
        }
        else if (colorAdjustments == null)
        {
            brightnessVolume.profile.TryGet(out colorAdjustments);
        }

        if (overlay == null)
        {
            GameObject existing = GameObject.Find("BrightnessOverlay");
            Canvas canvas;
            if (existing != null)
            {
                overlay = existing.GetComponent<CanvasGroup>();
                if (overlay == null)
                    overlay = existing.AddComponent<CanvasGroup>();
                overlay.blocksRaycasts = false;
                overlay.interactable = false;
                return;
            }

            GameObject canvasGo = new GameObject("BrightnessOverlay");
            Object.DontDestroyOnLoad(canvasGo);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Stay behind HUD / pause / settings canvases so menus stay readable.
            canvas.sortingOrder = -50;
            overlay = canvasGo.AddComponent<CanvasGroup>();
            overlay.blocksRaycasts = false;
            overlay.interactable = false;

            GameObject imageGo = new GameObject("Dim");
            imageGo.transform.SetParent(canvasGo.transform, false);
            UnityEngine.UI.Image image = imageGo.AddComponent<UnityEngine.UI.Image>();
            Texture2D tex = Texture2D.whiteTexture;
            image.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
            image.color = Color.black;
            image.raycastTarget = false;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
