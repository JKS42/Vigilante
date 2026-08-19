using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// World-space HP bar above an enemy. Hidden until damaged, then fades out.
/// Parent to the enemy so it follows movement; LateUpdate only billboards toward the camera.
/// Drawn with ZTest Always so enemy meshes never cover it.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] float height = 1.9f;
    [SerializeField] float headPadding = 0.28f;
    [SerializeField] float hideDelay = 3f;

    Health health;
    Transform barRoot;
    Image fill;
    CanvasGroup group;
    Camera cam;
    CapsuleCollider bodyCapsule;
    NavMeshAgent agent;
    float hideAt;
    bool visible;
    static Material overlayMat;

    void Awake()
    {
        health = GetComponent<Health>();
        bodyCapsule = GetComponent<CapsuleCollider>();
        agent = GetComponent<NavMeshAgent>();
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

    void OnDestroy()
    {
        DestroyBar();
    }

    void LateUpdate()
    {
        if (barRoot == null)
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam != null)
        {
            Vector3 awayFromCam = barRoot.position - cam.transform.position;
            if (awayFromCam.sqrMagnitude > 0.0001f)
                barRoot.rotation = Quaternion.LookRotation(awayFromCam);
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
        DestroyBar();
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

    void DestroyBar()
    {
        if (barRoot == null)
            return;

        Destroy(barRoot.gameObject);
        barRoot = null;
        fill = null;
        group = null;
    }

    void BuildUi()
    {
        GameObject root = new GameObject("EnemyHealthBar");
        barRoot = root.transform;
        barRoot.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;

        RectTransform canvasRt = root.GetComponent<RectTransform>();
        canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRt.pivot = new Vector2(0.5f, 0.5f);
        canvasRt.sizeDelta = new Vector2(100f, 14f);
        canvasRt.localScale = Vector3.one * 0.01f;
        canvasRt.localRotation = Quaternion.identity;
        canvasRt.localPosition = Vector3.up * CurrentHeight();

        root.AddComponent<CanvasGroup>();
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Sprite white = CreateWhiteSprite();

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(root.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = white;
        bg.material = OverlayMaterial();
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
        fill.material = OverlayMaterial();
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

    float CurrentHeight()
    {
        if (bodyCapsule != null)
            return bodyCapsule.center.y + bodyCapsule.height * 0.5f + headPadding;

        if (agent != null)
            return agent.height / Mathf.Max(0.01f, transform.lossyScale.y) + headPadding;

        return height;
    }

    static Material OverlayMaterial()
    {
        if (overlayMat == null)
        {
            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
                return null;

            overlayMat = new Material(shader);
            overlayMat.SetInt("_ZTest", (int)CompareFunction.Always);
            overlayMat.SetInt("_ZWrite", 0);
            overlayMat.renderQueue = 4000;
        }

        return overlayMat;
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }
}
