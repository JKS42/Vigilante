using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Center HUD reticle. Tick gap tracks WeaponAccuracy.CurrentSpread.
/// Bat is a static dot; guns open when the player moves.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    public WeaponAccuracy accuracy;
    public WeaponSwitcher switcher;
    public Camera aimCamera;

    [Header("Look")]
    public Color color = new Color(1f, 1f, 1f, 0.92f);
    public float minGap = 8f;
    public float maxGap = 220f;

    RectTransform root;
    RectTransform dot;
    RectTransform top;
    RectTransform bottom;
    RectTransform left;
    RectTransform right;
    Image[] tickImages;
    Image dotImage;
    CanvasGroup group;
    PauseMenu pauseMenu;
    Sprite whiteSprite;

    enum Style
    {
        Bat,
        Pistol,
        Rifle,
        Shotgun
    }

    public static CrosshairUI EnsureExists()
    {
        CrosshairUI existing = Object.FindFirstObjectByType<CrosshairUI>();
        if (existing != null)
            return existing;

        Transform parent = FindHudParent();
        if (parent == null)
            return null;

        GameObject go = new GameObject("Crosshair");
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        return go.AddComponent<CrosshairUI>();
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
        if (accuracy == null)
            accuracy = WeaponAccuracy.EnsureExists();
        if (switcher == null && accuracy != null)
            switcher = accuracy.switcher;
        if (switcher == null)
            switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (aimCamera == null)
            aimCamera = Camera.main;

        pauseMenu = FindFirstObjectByType<PauseMenu>();
        Build();
    }

    void Start()
    {
        GameObject current = switcher != null ? switcher.CurrentWeapon : null;
        ApplyStyle(ResolveStyle(current));
    }

    void OnEnable()
    {
        if (switcher != null)
            switcher.WeaponChanged += OnWeaponChanged;

        GameObject current = switcher != null ? switcher.CurrentWeapon : null;
        ApplyStyle(ResolveStyle(current));
    }

    void OnDisable()
    {
        if (switcher != null)
            switcher.WeaponChanged -= OnWeaponChanged;
    }

    void LateUpdate()
    {
        if (root == null)
            return;

        bool hide = ShouldHide();
        if (group != null)
            group.alpha = hide ? 0f : 1f;
        if (hide)
            return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        float spread = accuracy != null ? accuracy.CurrentSpread : 0f;
        bool melee = accuracy != null && accuracy.IsMelee;
        float gap = melee ? 0f : SpreadToPixels(spread);

        LayoutTicks(gap, melee);
    }

    void OnWeaponChanged(int index, GameObject weapon)
    {
        ApplyStyle(ResolveStyle(weapon));
    }

    bool ShouldHide()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return true;
        if (pauseMenu != null && pauseMenu.IsPaused)
            return true;
        if (UIManager.Instance != null && UIManager.Instance.IsPlayerDead)
            return true;
        return false;
    }

    Style ResolveStyle(GameObject weapon)
    {
        if (weapon == null)
            return Style.Bat;
        if (weapon.GetComponent<Melee>() != null)
            return Style.Bat;
        if (weapon.GetComponent<Shotgun>() != null)
            return Style.Shotgun;
        if (weapon.GetComponent<AR>() != null)
            return Style.Rifle;
        return Style.Pistol;
    }

    void ApplyStyle(Style next)
    {
        if (dot == null)
            return;

        bool melee = next == Style.Bat;
        SetTicksVisible(!melee);

        float thickness;
        float length;
        float dotSize;
        switch (next)
        {
            case Style.Shotgun:
                thickness = 3f;
                length = 18f;
                dotSize = 3f;
                minGap = 14f;
                break;
            case Style.Rifle:
                thickness = 2f;
                length = 12f;
                dotSize = 3f;
                minGap = 8f;
                break;
            case Style.Pistol:
                thickness = 2f;
                length = 10f;
                dotSize = 3f;
                minGap = 8f;
                break;
            default:
                thickness = 2f;
                length = 10f;
                dotSize = 6f;
                minGap = 0f;
                break;
        }

        SetSize(dot, new Vector2(dotSize, dotSize));
        SetSize(top, new Vector2(thickness, length));
        SetSize(bottom, new Vector2(thickness, length));
        SetSize(left, new Vector2(length, thickness));
        SetSize(right, new Vector2(length, thickness));
        LayoutTicks(melee ? 0f : minGap, melee);
    }

    void LayoutTicks(float gap, bool melee)
    {
        if (dot == null)
            return;

        gap = Mathf.Clamp(gap, melee ? 0f : minGap, maxGap);
        float topLen = top != null ? top.sizeDelta.y : 10f;
        float sideLen = left != null ? left.sizeDelta.x : 10f;

        if (dotImage != null)
            dotImage.enabled = true;
        SetTicksVisible(!melee);

        if (top != null)
            top.anchoredPosition = new Vector2(0f, gap + topLen * 0.5f);
        if (bottom != null)
            bottom.anchoredPosition = new Vector2(0f, -(gap + topLen * 0.5f));
        if (left != null)
            left.anchoredPosition = new Vector2(-(gap + sideLen * 0.5f), 0f);
        if (right != null)
            right.anchoredPosition = new Vector2(gap + sideLen * 0.5f, 0f);
    }

    void SetTicksVisible(bool visible)
    {
        if (tickImages == null)
            return;
        for (int i = 0; i < tickImages.Length; i++)
        {
            if (tickImages[i] != null)
                tickImages[i].enabled = visible;
        }
    }

    float SpreadToPixels(float spreadDegrees)
    {
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
            return minGap;

        float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        if (halfFov < 0.001f)
            return minGap;

        float screenHeight = Screen.height;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            screenHeight = canvas.pixelRect.height;

        return Mathf.Tan(spreadDegrees * Mathf.Deg2Rad) * screenHeight / (2f * Mathf.Tan(halfFov));
    }

    void Build()
    {
        whiteSprite = CreateWhiteSprite();
        root = GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 1f;

        dot = CreatePiece("Dot", out dotImage);
        top = CreatePiece("Top", out Image topImg);
        bottom = CreatePiece("Bottom", out Image bottomImg);
        left = CreatePiece("Left", out Image leftImg);
        right = CreatePiece("Right", out Image rightImg);
        tickImages = new[] { topImg, bottomImg, leftImg, rightImg };

        GameObject current = switcher != null ? switcher.CurrentWeapon : null;
        ApplyStyle(ResolveStyle(current));
    }

    RectTransform CreatePiece(string name, out Image image)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        image = go.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static void SetSize(RectTransform rt, Vector2 size)
    {
        if (rt != null)
            rt.sizeDelta = size;
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }
}
