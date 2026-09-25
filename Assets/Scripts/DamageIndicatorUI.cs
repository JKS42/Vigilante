using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-edge damage direction markers. Points toward the hit / attacker
/// relative to the player's look yaw so you know where fire is coming from.
/// </summary>
public class DamageIndicatorUI : MonoBehaviour
{
    const int MaxIndicators = 8;
    const float Radius = 210f;
    const float FadeDuration = 2.1f;
    const float MergeAngleDegrees = 32f;
    const float PeakAlpha = 1f;

    struct Pulse
    {
        public float angle;
        public float life;
        public RectTransform rect;
        public Image image;
    }

    readonly List<Pulse> pulses = new List<Pulse>(MaxIndicators);
    Transform player;
    Transform yawSource;
    CanvasGroup group;
    Sprite wedgeSprite;
    Health boundHealth;
    PauseMenu pauseMenu;
    bool highlight;
    bool pendingHighlight;
    int highlightedIndex = -1;
    bool hasLastOrigin;
    Vector3 lastOrigin;
    Canvas highlightCanvas;

    public static DamageIndicatorUI EnsureExists()
    {
        DamageIndicatorUI existing = Object.FindFirstObjectByType<DamageIndicatorUI>();
        if (existing != null)
            return existing;

        Transform parent = FindHudParent();
        if (parent == null)
            return null;

        GameObject go = new GameObject("DamageIndicators");
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        return go.AddComponent<DamageIndicatorUI>();
    }

    static Transform FindHudParent()
    {
        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            return hud.transform;

        GameObject playerUi = GameObject.Find("PlayerUI");
        if (playerUi != null)
            return playerUi.transform;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    void Awake()
    {
        pauseMenu = FindFirstObjectByType<PauseMenu>();
        BuildRoot();
        BindPlayer();
    }

    void OnDestroy()
    {
        UnbindPlayer();
        if (wedgeSprite != null)
            Destroy(wedgeSprite);
    }

    void LateUpdate()
    {
        if (group == null)
            return;

        bool hide = ShouldHide();
        group.alpha = hide ? 0f : 1f;
        if (hide)
            return;

        if (player == null)
            BindPlayer();

        float dt = Time.unscaledDeltaTime;
        for (int i = pulses.Count - 1; i >= 0; i--)
        {
            Pulse p = pulses[i];
            bool held = highlight && i == highlightedIndex;
            if (!held)
                p.life -= dt;

            if (p.life <= 0f || p.rect == null)
            {
                if (p.rect != null)
                    Destroy(p.rect.gameObject);
                pulses.RemoveAt(i);
                if (highlightedIndex == i)
                    highlightedIndex = -1;
                else if (highlightedIndex > i)
                    highlightedIndex--;
                continue;
            }

            float t = Mathf.Clamp01(p.life / FadeDuration);
            float alpha = held || t > 0.45f ? PeakAlpha : Mathf.Lerp(0f, PeakAlpha, t / 0.45f);
            if (highlight && !held)
                alpha = 0f;
            if (p.image != null)
            {
                Color c = p.image.color;
                c.a = alpha;
                p.image.color = c;
            }

            float punch = held
                ? 1.55f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.12f
                : t > 0.85f ? Mathf.Lerp(1.35f, 1f, (1f - t) / 0.15f) : 1f;
            if (p.rect != null)
                p.rect.localScale = Vector3.one * punch;

            LayoutPulse(p);
            pulses[i] = p;
        }
    }

    public void BeginTutorialHighlight()
    {
        highlight = true;
        EnsureHighlightCanvas();
        if (group != null)
            group.alpha = 1f;

        if (pulses.Count == 0 && hasLastOrigin)
        {
            if (player == null)
                BindPlayer();
            if (player != null)
                PushPulse(WorldToScreenAngle(lastOrigin));
        }

        if (pulses.Count == 0)
        {
            pendingHighlight = true;
            return;
        }

        pendingHighlight = false;
        int best = 0;
        for (int i = 1; i < pulses.Count; i++)
        {
            if (pulses[i].life > pulses[best].life)
                best = i;
        }

        highlightedIndex = best;
        Pulse held = pulses[best];
        held.life = FadeDuration;
        if (held.image != null)
            held.image.color = new Color(1f, 0.08f, 0.05f, PeakAlpha);
        pulses[best] = held;
        LayoutPulse(held);
    }

    public void EndTutorialHighlight()
    {
        highlight = false;
        pendingHighlight = false;
        highlightedIndex = -1;
        if (highlightCanvas != null)
            highlightCanvas.overrideSorting = false;
    }

    public void ShowHit(Vector3 hitPoint, GameObject instigator)
    {
        if (player == null)
            BindPlayer();
        if (player == null)
            return;

        Vector3 origin = ResolveAttackOrigin(hitPoint, instigator);
        lastOrigin = origin;
        hasLastOrigin = true;
        float angle = WorldToScreenAngle(origin);
        PushPulse(angle);
        if (pendingHighlight)
            BeginTutorialHighlight();
    }

    void PushPulse(float angleDegrees)
    {
        for (int i = 0; i < pulses.Count; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(pulses[i].angle, angleDegrees));
            if (delta <= MergeAngleDegrees)
            {
                Pulse p = pulses[i];
                p.angle = Mathf.LerpAngle(p.angle, angleDegrees, 0.35f);
                p.life = FadeDuration;
                pulses[i] = p;
                LayoutPulse(p);
                return;
            }
        }

        if (pulses.Count >= MaxIndicators)
        {
            int oldest = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < pulses.Count; i++)
            {
                if (pulses[i].life < lowest)
                {
                    lowest = pulses[i].life;
                    oldest = i;
                }
            }

            if (pulses[oldest].rect != null)
                Destroy(pulses[oldest].rect.gameObject);
            pulses.RemoveAt(oldest);
        }

        Pulse created = CreatePulse(angleDegrees);
        pulses.Add(created);
        LayoutPulse(created);
    }

    Pulse CreatePulse(float angleDegrees)
    {
        GameObject go = new GameObject("HitDir");
        go.transform.SetParent(transform, false);
        Image image = go.AddComponent<Image>();
        image.sprite = wedgeSprite != null ? wedgeSprite : CreateWedgeSprite();
        image.color = new Color(1f, 0.08f, 0.05f, PeakAlpha);
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(96f, 140f);
        rt.localScale = Vector3.one * 1.35f;

        return new Pulse
        {
            angle = angleDegrees,
            life = FadeDuration,
            rect = rt,
            image = image
        };
    }

    void LayoutPulse(Pulse pulse)
    {
        if (pulse.rect == null)
            return;

        // 0° = forward (up on HUD), clockwise with world yaw-relative hit direction.
        float rad = pulse.angle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * Radius;
        pulse.rect.anchoredPosition = offset;
        pulse.rect.localRotation = Quaternion.Euler(0f, 0f, -pulse.angle);
    }

    float WorldToScreenAngle(Vector3 worldPoint)
    {
        Transform yaw = yawSource != null ? yawSource : player;
        Vector3 flatForward = yaw.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 toHit = worldPoint - player.position;
        toHit.y = 0f;
        if (toHit.sqrMagnitude < 0.0001f)
            return 0f;
        toHit.Normalize();

        float signed = Vector3.SignedAngle(flatForward, toHit, Vector3.up);
        return signed;
    }

    Vector3 ResolveAttackOrigin(Vector3 hitPoint, GameObject instigator)
    {
        if (instigator != null)
        {
            Transform root = instigator.transform.root;
            if (root != null && root != player)
                return root.position;
            if (instigator.transform != player)
                return instigator.transform.position;
        }

        if ((hitPoint - player.position).sqrMagnitude > 0.05f)
            return hitPoint;

        return player.position - player.forward;
    }

    void BindPlayer()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            player = tagged.transform;

        if (player == null)
        {
            PlayerMovement move = FindFirstObjectByType<PlayerMovement>();
            if (move != null)
                player = move.transform;
        }

        if (yawSource == null)
        {
            if (Camera.main != null)
                yawSource = Camera.main.transform;
            else
            {
                MouseMovement look = FindFirstObjectByType<MouseMovement>();
                if (look != null)
                    yawSource = look.transform;
            }
        }

        if (player == null)
            return;

        Health health = player.GetComponent<Health>();
        if (health == null)
            health = player.GetComponentInChildren<Health>();
        if (health == null || health == boundHealth)
            return;

        UnbindPlayer();
        boundHealth = health;
        boundHealth.OnDamaged += HandleDamaged;
    }

    void UnbindPlayer()
    {
        if (boundHealth == null)
            return;
        boundHealth.OnDamaged -= HandleDamaged;
        boundHealth = null;
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        if (amount <= 0f)
            return;
        ShowHit(hitPoint, instigator);

        // Flash the existing full-screen vignette harder so the hit reads immediately.
        if (UIManager.Instance != null)
            UIManager.Instance.PulseDamageFlash();
    }

    bool ShouldHide()
    {
        if (highlight)
            return false;
        if (Cursor.lockState != CursorLockMode.Locked)
            return true;
        if (pauseMenu != null && pauseMenu.IsPaused)
            return true;
        if (UIManager.Instance != null && UIManager.Instance.IsPlayerDead)
            return true;
        return false;
    }

    void BuildRoot()
    {
        RectTransform root = gameObject.GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        wedgeSprite = CreateWedgeSprite();
    }

    void EnsureHighlightCanvas()
    {
        if (highlightCanvas == null)
            highlightCanvas = gameObject.GetComponent<Canvas>();
        if (highlightCanvas == null)
            highlightCanvas = gameObject.AddComponent<Canvas>();
        highlightCanvas.overrideSorting = true;
        highlightCanvas.sortingOrder = 70;
    }

    static Sprite CreateWedgeSprite()
    {
        const int w = 64;
        const int h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
                float half = Mathf.Lerp(0.12f, 0.5f, v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w - 0.5f;
                    float edge = half;
                    float alpha = 0f;
                    if (Mathf.Abs(u) <= edge)
                    {
                        float inward = 1f - Mathf.Abs(u) / Mathf.Max(0.001f, edge);
                        float tip = Mathf.SmoothStep(0.15f, 1f, v);
                        alpha = Mathf.Pow(inward, 0.65f) * tip;
                        alpha = Mathf.Clamp01(alpha * 1.35f);
                    }

                    tex.SetPixel(x, y, Color.Lerp(clear, solid, alpha));
                }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.15f), 64f);
    }
}
