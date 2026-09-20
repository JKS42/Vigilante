using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persisted master volume, brightness, and mouse look sensitivity. Applied in every scene.
/// Brightness 0 = fully black, 1 = no dim.
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
    static Image overlayImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnLoad()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyAll();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
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
        float brightness = Mathf.Clamp01(Brightness);
        EnsureBrightnessFx();
        EnsureCameraPostProcessing();

        // Soft exposure nudge for mid values (needs URP post-processing on the camera).
        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Lerp(-2f, 0.25f, brightness);
        }

        // Source of truth: full-screen black veil. 0 = fully black, 1 = clear.
        if (overlay != null)
            overlay.alpha = 1f - brightness;

        if (overlayImage != null)
            overlayImage.color = Color.black;
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
            brightnessVolume.weight = 1f;
            if (brightnessVolume.profile == null)
                brightnessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            if (!brightnessVolume.profile.TryGet(out colorAdjustments))
                colorAdjustments = brightnessVolume.profile.Add<ColorAdjustments>(true);
        }
        else if (colorAdjustments == null && brightnessVolume.profile != null)
        {
            if (!brightnessVolume.profile.TryGet(out colorAdjustments))
                colorAdjustments = brightnessVolume.profile.Add<ColorAdjustments>(true);
        }

        if (overlay == null)
        {
            GameObject canvasGo = GameObject.Find("BrightnessOverlay");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("BrightnessOverlay");
                Object.DontDestroyOnLoad(canvasGo);
            }
            else
            {
                Object.DontDestroyOnLoad(canvasGo);
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
                canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under HUD / pause / settings so menus stay readable at low brightness.
            canvas.sortingOrder = -20;

            overlay = canvasGo.GetComponent<CanvasGroup>();
            if (overlay == null)
                overlay = canvasGo.AddComponent<CanvasGroup>();
            overlay.blocksRaycasts = false;
            overlay.interactable = false;

            Transform dimTf = canvasGo.transform.Find("Dim");
            GameObject imageGo = dimTf != null ? dimTf.gameObject : null;
            if (imageGo == null)
            {
                imageGo = new GameObject("Dim");
                imageGo.transform.SetParent(canvasGo.transform, false);
            }

            overlayImage = imageGo.GetComponent<Image>();
            if (overlayImage == null)
                overlayImage = imageGo.AddComponent<Image>();

            Texture2D tex = Texture2D.whiteTexture;
            overlayImage.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
            overlayImage.color = Color.black;
            overlayImage.raycastTarget = false;

            RectTransform rt = overlayImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        else
        {
            Canvas canvas = overlay.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = -20;

            if (overlayImage == null)
            {
                Transform dimTf = overlay.transform.Find("Dim");
                if (dimTf != null)
                    overlayImage = dimTf.GetComponent<Image>();
            }
        }
    }

    static void EnsureCameraPostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            MouseMovement look = Object.FindFirstObjectByType<MouseMovement>();
            if (look != null)
                cam = look.GetComponentInChildren<Camera>();
        }

        if (cam == null)
            return;

        UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
            data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        data.renderPostProcessing = true;
    }
}
