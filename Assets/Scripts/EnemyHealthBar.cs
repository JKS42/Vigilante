using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// World-space HP bar above an enemy. Visible while the crosshair ray hits
/// that enemy's collider.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] float height = 1.9f;
    [SerializeField] float headPadding = 0.28f;
    [SerializeField] float barWidth = 1.1f;
    [SerializeField] float barHeight = 0.12f;
    [SerializeField] float borderPadding = 0.018f;

    const float HoverRange = 80f;

    static readonly List<EnemyHealthBar> Active = new List<EnemyHealthBar>(32);
    static EnemyHealthBar hovered;
    static int hoverFrame = -1;
    static readonly RaycastHit[] HoverHits = new RaycastHit[16];

    Health health;
    Transform barRoot;
    Canvas barCanvas;
    Image fillImage;
    Camera cam;
    CapsuleCollider bodyCapsule;
    NavMeshAgent agent;
    bool visible;

    void Awake()
    {
        health = GetComponent<Health>();
        bodyCapsule = GetComponent<CapsuleCollider>();
        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        agent = GetComponent<NavMeshAgent>();
        BuildUi();
        SetVisible(false);
    }

    void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (!Active.Contains(this))
            Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (hovered == this)
            hovered = null;
        SetVisible(false);
    }

    void OnDestroy()
    {
        Active.Remove(this);
        if (hovered == this)
            hovered = null;
        DestroyBar();
    }

    void LateUpdate()
    {
        if (barRoot == null)
            return;

        if (cam == null)
            cam = ResolveCamera();

        RefreshHover();

        bool shouldShow = GameUiVisibility.IsVisible && hovered == this && health != null && !health.IsDead;
        if (shouldShow)
        {
            PlaceAndBillboard();
            RefreshFill();
            if (!visible)
                SetVisible(true);
        }
        else if (visible)
        {
            SetVisible(false);
        }
    }

    static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        MouseMovement look = Object.FindFirstObjectByType<MouseMovement>();
        if (look != null)
        {
            Camera child = look.GetComponentInChildren<Camera>();
            if (child != null)
                return child;
        }

        return Object.FindFirstObjectByType<Camera>();
    }

    static void RefreshHover()
    {
        if (hoverFrame == Time.frameCount)
            return;

        hoverFrame = Time.frameCount;
        hovered = null;

        Camera camera = ResolveCamera();
        if (camera == null || Active.Count == 0)
            return;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, HoverHits, HoverRange, ~0, QueryTriggerInteraction.Ignore);
        if (count <= 0)
            return;

        float bestDist = float.MaxValue;
        EnemyHealthBar best = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HoverHits[i];
            if (hit.collider == null)
                continue;

            EnemyHealthBar bar = hit.collider.GetComponentInParent<EnemyHealthBar>();
            if (bar == null || bar.health == null || bar.health.IsDead)
                continue;

            if (hit.distance >= bestDist)
                continue;

            bestDist = hit.distance;
            best = bar;
        }

        hovered = best;
    }

    void PlaceAndBillboard()
    {
        if (cam == null)
            return;

        barRoot.position = transform.position + Vector3.up * CurrentHeight();

        Vector3 away = barRoot.position - cam.transform.position;
        if (away.sqrMagnitude > 0.0001f)
            barRoot.rotation = Quaternion.LookRotation(away, Vector3.up);
        barCanvas.worldCamera = cam;
    }

    void RefreshFill()
    {
        if (fillImage == null || health == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(health.CurrentHealth / Mathf.Max(1f, health.MaxHealth));
    }

    void SetVisible(bool on)
    {
        visible = on;
        if (barCanvas != null)
            barCanvas.enabled = on;
    }

    void DestroyBar()
    {
        if (barRoot != null)
            Destroy(barRoot.gameObject);

        barRoot = null;
        barCanvas = null;
        fillImage = null;
    }

    void BuildUi()
    {
        GameObject root = new GameObject("EnemyHealthBar", typeof(RectTransform), typeof(Canvas));
        barRoot = root.transform;
        barRoot.SetParent(null, false);

        barCanvas = root.GetComponent<Canvas>();
        barCanvas.renderMode = RenderMode.WorldSpace;
        barCanvas.worldCamera = cam != null ? cam : ResolveCamera();
        barCanvas.overrideSorting = true;
        barCanvas.sortingOrder = 50;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);
        barRoot.localScale = Vector3.one * 0.01f;

        GameObject bgGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(barRoot, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.sprite = VigilanteUiStyle.WhiteSprite();
        bgImage.color = Color.black;
        bgImage.raycastTarget = false;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barRoot, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        float inset = Mathf.Max(1f, borderPadding * 100f);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(inset, inset);
        fillRect.offsetMax = new Vector2(-inset, -inset);
        fillImage = fillGo.GetComponent<Image>();
        fillImage.sprite = VigilanteUiStyle.WhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;
    }

    float CurrentHeight()
    {
        Collider body = bodyCapsule != null ? bodyCapsule : GetComponentInChildren<Collider>();
        if (body != null)
            return body.bounds.max.y - transform.position.y + headPadding;

        if (agent != null)
            return agent.height + headPadding;

        return height;
    }
}
