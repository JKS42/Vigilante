using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Circular radar on the HUD. Player stays centered and facing up;
/// enemies, loot, and grenades plot relative to look yaw.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] float range = 28f;
    [SerializeField] float mapSize = 168f;
    [SerializeField] Vector2 anchoredPosition = new Vector2(22f, 88f);
    [SerializeField] float iconSize = 8f;

    const int MaxEnemies = 24;
    const int MaxLoot = 12;
    const int MaxGrenades = 6;
    const float CacheInterval = 0.2f;
    const float Rim = 0.46f;

    static readonly Color PlayerColor = new Color(0.4f, 0.9f, 1f, 1f);
    static readonly Color EnemyFallback = new Color(0.95f, 0.22f, 0.18f, 0.95f);
    static readonly Color MedkitColor = new Color(0.3f, 0.92f, 0.42f, 0.95f);
    static readonly Color PickupColor = new Color(1f, 0.86f, 0.22f, 0.95f);
    static readonly Color GrenadeColor = new Color(0.35f, 0.95f, 0.28f, 0.95f);

    Transform player;
    Transform yawSource;
    RectTransform root;
    CanvasGroup group;
    PauseMenu pauseMenu;
    Image[] enemyIcons;
    Image[] lootIcons;
    Image[] grenadeIcons;
    Sprite circleSprite;
    Sprite ringSprite;
    Sprite triangleSprite;
    Texture2D circleTex;
    Texture2D ringTex;
    Texture2D triangleTex;

    EnemyAI[] cachedEnemies = System.Array.Empty<EnemyAI>();
    MedKitPickup[] cachedKits = System.Array.Empty<MedKitPickup>();
    WeaponPickup[] cachedGuns = System.Array.Empty<WeaponPickup>();
    EnemyGrenade[] cachedGrenades = System.Array.Empty<EnemyGrenade>();
    float nextCacheTime;

    public static MinimapUI EnsureExists()
    {
        MinimapUI existing = Object.FindFirstObjectByType<MinimapUI>();
        if (existing != null)
            return existing;

        Transform parent = FindHudParent();
        if (parent == null)
            return null;

        GameObject go = new GameObject("Minimap");
        go.transform.SetParent(parent, false);
        return go.AddComponent<MinimapUI>();
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
        BuildUi();
    }

    void OnDestroy()
    {
        if (circleTex != null)
            Destroy(circleTex);
        if (ringTex != null)
            Destroy(ringTex);
        if (triangleTex != null)
            Destroy(triangleTex);
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

        if (player == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                player = tagged.transform;
        }

        if (player == null)
            return;

        if (yawSource == null && Camera.main != null)
            yawSource = Camera.main.transform;

        float yaw = yawSource != null ? yawSource.eulerAngles.y : player.eulerAngles.y;
        Quaternion inverseYaw = Quaternion.Euler(0f, -yaw, 0f);

        if (Time.unscaledTime >= nextCacheTime)
            RefreshCache();

        PlotEnemies(inverseYaw);
        PlotLoot(inverseYaw);
        PlotGrenades(inverseYaw);
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

    void RefreshCache()
    {
        nextCacheTime = Time.unscaledTime + CacheInterval;
        cachedEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        cachedKits = Object.FindObjectsByType<MedKitPickup>(FindObjectsSortMode.None);
        cachedGuns = Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        cachedGrenades = Object.FindObjectsByType<EnemyGrenade>(FindObjectsSortMode.None);
    }

    void PlotEnemies(Quaternion inverseYaw)
    {
        int used = 0;
        for (int i = 0; i < cachedEnemies.Length && used < MaxEnemies; i++)
        {
            EnemyAI ai = cachedEnemies[i];
            if (ai == null || ai.IsDead)
                continue;

            Vector2 uv = ToMap(ai.transform.position, inverseYaw, clampToRim: true);
            bool boss = ai.GetComponent<BossController>() != null;
            Image icon = enemyIcons[used];
            icon.enabled = true;
            icon.color = BlipColor(ai, boss);
            Place(icon.rectTransform, uv, boss ? iconSize * 1.7f : iconSize);
            used++;
        }

        for (int i = used; i < MaxEnemies; i++)
            enemyIcons[i].enabled = false;
    }

    void PlotLoot(Quaternion inverseYaw)
    {
        int used = 0;
        used = PlotArray(cachedKits, inverseYaw, used, MaxLoot, lootIcons, MedkitColor, iconSize * 0.7f, false);
        used = PlotArray(cachedGuns, inverseYaw, used, MaxLoot, lootIcons, PickupColor, iconSize * 0.7f, false);
        for (int i = used; i < MaxLoot; i++)
            lootIcons[i].enabled = false;
    }

    void PlotGrenades(Quaternion inverseYaw)
    {
        int used = PlotArray(cachedGrenades, inverseYaw, 0, MaxGrenades, grenadeIcons, GrenadeColor, iconSize * 0.85f, true);
        for (int i = used; i < MaxGrenades; i++)
            grenadeIcons[i].enabled = false;
    }

    int PlotArray<T>(T[] items, Quaternion inverseYaw, int used, int cap, Image[] icons, Color color, float size, bool clampToRim)
        where T : Component
    {
        if (items == null)
            return used;

        for (int i = 0; i < items.Length && used < cap; i++)
        {
            if (items[i] == null)
                continue;

            Vector2 uv = ToMap(items[i].transform.position, inverseYaw, clampToRim);
            if (!clampToRim && uv.sqrMagnitude > Rim * Rim)
                continue;

            Image icon = icons[used];
            icon.enabled = true;
            icon.color = color;
            Place(icon.rectTransform, uv, size);
            used++;
        }

        return used;
    }

    Vector2 ToMap(Vector3 world, Quaternion inverseYaw, bool clampToRim)
    {
        Vector3 delta = world - player.position;
        delta.y = 0f;
        Vector3 local = inverseYaw * delta;
        Vector2 uv = new Vector2(local.x, local.z) / Mathf.Max(1f, range) * 0.5f;
        if (clampToRim && uv.sqrMagnitude > Rim * Rim)
            uv = uv.normalized * Rim;
        return uv;
    }

    static Color BlipColor(EnemyAI ai, bool boss)
    {
        EnemyProfile profile = ai.GetComponent<EnemyProfile>();
        Color c = profile != null ? profile.tint : EnemyFallback;
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max < 0.5f && max > 0.001f)
            c *= 0.5f / max;
        c.a = boss ? 1f : 0.95f;
        return c;
    }

    static void Place(RectTransform rt, Vector2 uv, float size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f + uv.x, 0.5f + uv.y);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    void BuildUi()
    {
        circleTex = MakeCircleTexture(128);
        ringTex = MakeRingTexture(128, 8f);
        triangleTex = MakeTriangleTexture(32);
        circleSprite = Sprite.Create(circleTex, new Rect(0f, 0f, circleTex.width, circleTex.height), new Vector2(0.5f, 0.5f), 100f);
        ringSprite = Sprite.Create(ringTex, new Rect(0f, 0f, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f), 100f);
        triangleSprite = Sprite.Create(triangleTex, new Rect(0f, 0f, triangleTex.width, triangleTex.height), new Vector2(0.5f, 0.35f), 100f);

        root = gameObject.GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = anchoredPosition;
        root.sizeDelta = new Vector2(mapSize, mapSize);

        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Image frame = gameObject.AddComponent<Image>();
        frame.sprite = circleSprite;
        frame.color = new Color(0.08f, 0.1f, 0.12f, 0.82f);
        frame.raycastTarget = false;

        GameObject maskGo = CreateChild("Mask");
        Image maskImage = maskGo.AddComponent<Image>();
        maskImage.sprite = circleSprite;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        Mask mask = maskGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Stretch(maskGo.GetComponent<RectTransform>(), 4f);

        GameObject ringGo = CreateChild("RangeRing", maskGo.transform);
        Image ring = ringGo.AddComponent<Image>();
        ring.sprite = circleSprite;
        ring.color = new Color(1f, 1f, 1f, 0.12f);
        ring.raycastTarget = false;
        RectTransform ringRt = ringGo.GetComponent<RectTransform>();
        ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.sizeDelta = new Vector2(mapSize * 0.5f, mapSize * 0.5f);

        GameObject iconsGo = CreateChild("Icons", maskGo.transform);
        RectTransform iconLayer = iconsGo.GetComponent<RectTransform>();
        Stretch(iconLayer, 0f);

        lootIcons = CreatePool(iconLayer, "Loot", MaxLoot, circleSprite, iconSize * 0.7f);
        grenadeIcons = CreatePool(iconLayer, "Grenade", MaxGrenades, circleSprite, iconSize * 0.85f);
        enemyIcons = CreatePool(iconLayer, "Enemy", MaxEnemies, circleSprite, iconSize);

        GameObject playerGo = CreateChild("Player", iconLayer);
        Image playerImg = playerGo.AddComponent<Image>();
        playerImg.sprite = triangleSprite;
        playerImg.color = PlayerColor;
        playerImg.raycastTarget = false;
        RectTransform playerRt = playerGo.GetComponent<RectTransform>();
        playerRt.anchorMin = playerRt.anchorMax = new Vector2(0.5f, 0.5f);
        playerRt.sizeDelta = new Vector2(iconSize * 1.8f, iconSize * 1.8f);
        playerRt.anchoredPosition = Vector2.zero;

        GameObject borderGo = CreateChild("Border");
        Image border = borderGo.AddComponent<Image>();
        border.sprite = ringSprite;
        border.color = new Color(0.85f, 0.9f, 0.95f, 0.7f);
        border.raycastTarget = false;
        Stretch(borderGo.GetComponent<RectTransform>(), 0f);
    }

    Image[] CreatePool(Transform parent, string prefix, int count, Sprite sprite, float size)
    {
        Image[] pool = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = CreateChild(prefix + i, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.enabled = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            pool[i] = img;
        }

        return pool;
    }

    GameObject CreateChild(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : transform, false);
        return go;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Texture2D MakeCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float cx = (size - 1) * 0.5f;
        float radius = cx - 1f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cx));
                float a = Mathf.Clamp01(radius + 1.2f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D MakeRingTexture(int size, float thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float cx = (size - 1) * 0.5f;
        float outer = cx - 1f;
        float inner = outer - thickness;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cx));
                float a = Mathf.Clamp01(outer + 1.1f - d) * Mathf.Clamp01(d - (inner - 1.1f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D MakeTriangleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 a = new Vector2(size * 0.5f, size * 0.92f);
        Vector2 b = new Vector2(size * 0.12f, size * 0.12f);
        Vector2 c = new Vector2(size * 0.88f, size * 0.12f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c)
                    ? Color.white
                    : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
