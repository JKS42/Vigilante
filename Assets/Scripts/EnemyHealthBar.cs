using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// World-space HP bar above an enemy. Visible only while that enemy sits under
/// the crosshair. Built as unparented unlit quads so it never inherits enemy
/// rotation and always faces the camera.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] float height = 1.9f;
    [SerializeField] float headPadding = 0.28f;
    [SerializeField] float barWidth = 1.1f;
    [SerializeField] float barHeight = 0.12f;

    const float HoverRange = 80f;
    // How close to screen center (0–0.5) counts as "on the crosshair".
    const float ScreenHoverRadius = 0.07f;

    static readonly List<EnemyHealthBar> Active = new List<EnemyHealthBar>(32);
    static EnemyHealthBar hovered;
    static int hoverFrame = -1;

    Health health;
    Transform barRoot;
    Transform fillTf;
    Renderer bgRenderer;
    Renderer fillRenderer;
    Material bgMat;
    Material fillMat;
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

        bool shouldShow = hovered == this && health != null && !health.IsDead;
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

        float bestScore = ScreenHoverRadius * ScreenHoverRadius;
        EnemyHealthBar best = null;

        for (int i = 0; i < Active.Count; i++)
        {
            EnemyHealthBar bar = Active[i];
            if (bar == null || bar.health == null || bar.health.IsDead)
                continue;

            Vector3 world = bar.HoverPoint();
            Vector3 vp = camera.WorldToViewportPoint(world);
            if (vp.z < 0.5f || vp.z > HoverRange)
                continue;

            float dx = vp.x - 0.5f;
            float dy = vp.y - 0.5f;
            float score = dx * dx + dy * dy;
            if (score > bestScore)
                continue;

            bestScore = score;
            best = bar;
        }

        hovered = best;
    }

    Vector3 HoverPoint()
    {
        if (bodyCapsule != null)
            return transform.TransformPoint(bodyCapsule.center);
        return transform.position + Vector3.up * (CurrentHeight() * 0.5f);
    }

    void PlaceAndBillboard()
    {
        if (cam == null)
            return;

        barRoot.position = transform.TransformPoint(Vector3.up * CurrentHeight());

        // Same billboard convention as CombatVfx / TextMeshPro world text.
        Vector3 away = barRoot.position - cam.transform.position;
        if (away.sqrMagnitude > 0.0001f)
            barRoot.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    void RefreshFill()
    {
        if (fillTf == null || health == null)
            return;

        float pct = Mathf.Clamp01(health.CurrentHealth / Mathf.Max(1f, health.MaxHealth));
        fillTf.localScale = new Vector3(Mathf.Max(0.001f, barWidth * pct), barHeight * 0.7f, 1f);
        fillTf.localPosition = new Vector3((pct - 1f) * 0.5f * barWidth, 0f, -0.001f);

        if (fillMat != null)
            fillMat.color = Color.Lerp(new Color(0.85f, 0.15f, 0.12f), new Color(0.25f, 0.8f, 0.28f), pct);
    }

    void SetVisible(bool on)
    {
        visible = on;
        if (bgRenderer != null)
            bgRenderer.enabled = on;
        if (fillRenderer != null)
            fillRenderer.enabled = on;
    }

    void DestroyBar()
    {
        if (bgMat != null)
            Destroy(bgMat);
        if (fillMat != null)
            Destroy(fillMat);
        bgMat = null;
        fillMat = null;

        if (barRoot != null)
            Destroy(barRoot.gameObject);

        barRoot = null;
        fillTf = null;
        bgRenderer = null;
        fillRenderer = null;
    }

    void BuildUi()
    {
        GameObject root = new GameObject("EnemyHealthBar");
        barRoot = root.transform;
        barRoot.SetParent(null, false);

        bgMat = new Material(UnlitShader());
        bgMat.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        fillMat = new Material(UnlitShader());
        fillMat.color = new Color(0.25f, 0.8f, 0.28f, 1f);

        GameObject bgGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgGo.name = "Background";
        Object.Destroy(bgGo.GetComponent<Collider>());
        bgGo.transform.SetParent(barRoot, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localRotation = Quaternion.identity;
        bgGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        bgRenderer = bgGo.GetComponent<Renderer>();
        bgRenderer.sharedMaterial = bgMat;
        bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bgRenderer.receiveShadows = false;

        GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fillGo.name = "Fill";
        Object.Destroy(fillGo.GetComponent<Collider>());
        fillTf = fillGo.transform;
        fillTf.SetParent(barRoot, false);
        fillTf.localRotation = Quaternion.identity;
        fillTf.localScale = new Vector3(barWidth, barHeight * 0.7f, 1f);
        fillTf.localPosition = new Vector3(0f, 0f, -0.001f);
        fillRenderer = fillGo.GetComponent<Renderer>();
        fillRenderer.sharedMaterial = fillMat;
        fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fillRenderer.receiveShadows = false;

        RefreshFill();
    }

    float CurrentHeight()
    {
        if (bodyCapsule != null)
            return bodyCapsule.center.y + bodyCapsule.height * 0.5f + headPadding;

        if (agent != null)
            return agent.height / Mathf.Max(0.01f, transform.lossyScale.y) + headPadding;

        return height;
    }

    static Shader UnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
               ?? Shader.Find("Unlit/Color")
               ?? Shader.Find("Sprites/Default")
               ?? Shader.Find("Standard");
    }
}
