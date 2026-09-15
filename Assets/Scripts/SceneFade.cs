using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen black fade-in when a combat level loads.
/// </summary>
public class SceneFade : MonoBehaviour
{
    [SerializeField] float fadeDuration = 1.25f;
    [SerializeField] float holdBlack = 0.15f;

    static SceneFade instance;
    CanvasGroup group;
    Coroutine routine;

    public static void PlayLevelIntro(float duration = 1.25f)
    {
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return;

        SceneFade fade = EnsureExists();
        if (fade == null)
            return;

        fade.fadeDuration = Mathf.Max(0.05f, duration);
        fade.StartFadeIn();
    }

    public static SceneFade EnsureExists()
    {
        if (instance != null)
            return instance;

        SceneFade existing = Object.FindFirstObjectByType<SceneFade>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject("SceneFade");
        instance = go.AddComponent<SceneFade>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildOverlay();
        // Cover the first rendered frames before the fade coroutine starts.
        if (group != null)
            group.alpha = 1f;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void StartFadeIn()
    {
        if (group == null)
            BuildOverlay();

        if (routine != null)
            StopCoroutine(routine);

        group.alpha = 1f;
        group.blocksRaycasts = true;
        routine = StartCoroutine(FadeInRoutine());
    }

    IEnumerator FadeInRoutine()
    {
        if (holdBlack > 0f)
            yield return new WaitForSecondsRealtime(holdBlack);

        float duration = Mathf.Max(0.05f, fadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;
        routine = null;
    }

    void BuildOverlay()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        if (gameObject.GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();

        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = true;
        group.alpha = 1f;

        Transform existingDim = transform.Find("Dim");
        if (existingDim != null)
            return;

        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(transform, false);
        Image image = dim.AddComponent<Image>();
        Texture2D tex = Texture2D.whiteTexture;
        image.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        image.color = Color.black;
        image.raycastTarget = true;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
