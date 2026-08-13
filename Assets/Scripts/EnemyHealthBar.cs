using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space HP bar above an enemy. Hidden until damaged, then fades out.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] float height = 1.9f;
    [SerializeField] float hideDelay = 3f;

    Health health;
    Transform barRoot;
    Image fill;
    CanvasGroup group;
    float hideAt;
    bool visible;

    void Awake()
    {
        health = GetComponent<Health>();
        BuildUi();
        SetVisible(false);
    }

    void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    void LateUpdate()
    {
        if (barRoot == null)
            return;

        barRoot.position = transform.position + Vector3.up * height;

        Camera cam = Camera.main;
        if (cam != null)
        {
            barRoot.rotation = Quaternion.LookRotation(barRoot.position - cam.transform.position);
            float dist = Vector3.Distance(cam.transform.position, barRoot.position);
            float scale = Mathf.Clamp(dist * 0.014f, 0.01f, 0.028f);
            barRoot.localScale = Vector3.one * scale;
        }

        if (visible && Time.time >= hideAt)
            SetVisible(false);
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        RefreshFill();
        hideAt = Time.time + hideDelay;
        SetVisible(true);
    }

    void HandleDied()
    {
        SetVisible(false);
    }

    void RefreshFill()
    {
        if (fill == null || health == null)
            return;

        float pct = health.CurrentHealth / Mathf.Max(1f, health.MaxHealth);
        fill.fillAmount = Mathf.Clamp01(pct);
        fill.color = Color.Lerp(new Color(0.85f, 0.15f, 0.12f), new Color(0.25f, 0.8f, 0.28f), pct);
    }

    void SetVisible(bool on)
    {
        visible = on;
        if (group != null)
            group.alpha = on ? 1f : 0f;
        if (barRoot != null)
            barRoot.gameObject.SetActive(on);
    }

    void BuildUi()
    {
        GameObject root = new GameObject("EnemyHealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.up * height;
        barRoot = root.transform;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRt = root.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(160f, 22f);
        root.transform.localScale = Vector3.one * 0.012f;

        root.AddComponent<CanvasGroup>();
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Sprite white = CreateWhiteSprite();

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(root.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        RectTransform bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        fill = fillGo.AddComponent<Image>();
        fill.sprite = white;
        fill.color = new Color(0.25f, 0.8f, 0.28f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        RectTransform fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }
}
