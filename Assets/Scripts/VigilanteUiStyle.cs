using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dialogue box chrome: solid black fill, thick white border, thinner outer
/// cel-black outline (same near-black as model CelOutline).
/// </summary>
public static class VigilanteUiStyle
{
    // Match CelOutline.OutlineColor — near-black, not pure black.
    public static readonly Color CelOutlineBlack = new Color(0.02f, 0.02f, 0.05f, 1f);
    public static readonly Color FillBlack = new Color(0f, 0f, 0f, 1f);
    public static readonly Color LineWhite = Color.white;

    const float WhiteThickness = 6f;
    const float CelThickness = 2.5f;
    const float ButtonWhiteThickness = 4f;
    const float ButtonCelThickness = 2f;
    const string FrameRootName = "UiDialogueFrame";
    const string InvertedFrameRootName = "UiInvertedFrame";

    static TMP_FontAsset militech;
    static Sprite whiteSprite;

    public static TMP_FontAsset Font
    {
        get
        {
            if (militech != null)
                return militech;

            militech = Resources.Load<TMP_FontAsset>("Fonts/Militech SDF");
            if (militech != null)
                return militech;

#if UNITY_EDITOR
            militech = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/UI/militech_r_2019-04-13 SDF.asset");
            if (militech != null)
                return militech;
#endif

            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loaded.Length; i++)
            {
                TMP_FontAsset asset = loaded[i];
                if (asset == null)
                    continue;
                if (asset.name.IndexOf("militech", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    militech = asset;
                    break;
                }
            }

            return militech;
        }
    }

    public static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }

    public static void ApplyFont(TextMeshProUGUI tmp)
    {
        if (tmp == null)
            return;

        TMP_FontAsset font = Font;
        if (font != null)
            tmp.font = font;
        tmp.color = Color.white;

        // outlineWidth on the component needs a shared material already.
        // Scene labels without one were throwing and aborting the tutorial setup.
        if (tmp.font == null)
            return;

        Material mat = tmp.fontMaterial;
        if (mat != null)
        {
            if (mat.HasProperty("_GlowColor"))
                mat.SetColor("_GlowColor", new Color(0f, 0f, 0f, 0f));
            if (mat.HasProperty("_GlowPower"))
                mat.SetFloat("_GlowPower", 0f);
            if (mat.HasProperty("_GlowOuter"))
                mat.SetFloat("_GlowOuter", 0f);
            if (mat.HasProperty("_GlowInner"))
                mat.SetFloat("_GlowInner", 0f);
            if (mat.HasProperty("_OutlineColor"))
                mat.SetColor("_OutlineColor", Color.black);
            if (mat.HasProperty("_OutlineWidth"))
                mat.SetFloat("_OutlineWidth", 0.15f);
        }
    }

    public static void ApplyFontRecursive(Transform root)
    {
        if (root == null)
            return;

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
            ApplyFont(labels[i]);
    }

    public static void StylePanel(Image panel)
    {
        if (panel == null)
            return;

        panel.sprite = WhiteSprite();
        panel.color = FillBlack;
        panel.type = Image.Type.Simple;
        panel.raycastTarget = true;
        EnsureFrame(panel.transform);
    }

    public static void StylePanel(GameObject panelGo)
    {
        if (panelGo == null)
            return;

        Image image = panelGo.GetComponent<Image>();
        if (image == null)
            image = panelGo.AddComponent<Image>();
        StylePanel(image);
    }

    /// <summary>
    /// Inverted dialogue chrome for buttons: white fill, thick black border,
    /// thinner white outer outline. Label uses black Militech text.
    /// </summary>
    public static void StyleInvertedButton(Button button, System.Action onRelease = null)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        image.sprite = WhiteSprite();
        image.color = LineWhite;
        image.type = Image.Type.Simple;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();

        EnsureInvertedFrame(button.transform);

        TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            ApplyFont(labels[i]);
            labels[i].color = FillBlack;
        }

        VigilanteUiPress press = button.GetComponent<VigilanteUiPress>();
        if (press == null)
            press = button.gameObject.AddComponent<VigilanteUiPress>();
        press.Configure(image, labels, onRelease);
    }

    public static void EnsureFrame(Transform root)
    {
        if (root == null)
            return;

        DestroyNamedChild(root, "UiDoubleFrame");

        Transform existing = root.Find(FrameRootName);
        if (existing != null)
        {
            existing.SetAsFirstSibling();
            return;
        }

        GameObject frame = new GameObject(FrameRootName, typeof(RectTransform));
        frame.layer = root.gameObject.layer;
        frame.transform.SetParent(root, false);
        frame.transform.SetAsFirstSibling();
        StretchFull(frame.GetComponent<RectTransform>());

        BuildRing(frame.transform, "CelOutline", -CelThickness, CelThickness, CelOutlineBlack);
        BuildRing(frame.transform, "White", 0f, WhiteThickness, LineWhite);
    }

    public static void EnsureInvertedFrame(Transform root)
    {
        if (root == null)
            return;

        DestroyNamedChild(root, FrameRootName);
        DestroyNamedChild(root, "UiDoubleFrame");

        Transform existing = root.Find(InvertedFrameRootName);
        if (existing != null)
        {
            existing.SetAsFirstSibling();
            return;
        }

        GameObject frame = new GameObject(InvertedFrameRootName, typeof(RectTransform));
        frame.layer = root.gameObject.layer;
        frame.transform.SetParent(root, false);
        frame.transform.SetAsFirstSibling();
        StretchFull(frame.GetComponent<RectTransform>());

        // Swapped: thin white outer, thick black inner border.
        BuildRing(frame.transform, "OuterWhite", -ButtonCelThickness, ButtonCelThickness, LineWhite);
        BuildRing(frame.transform, "Black", 0f, ButtonWhiteThickness, FillBlack);
    }

    static void DestroyNamedChild(Transform root, string name)
    {
        Transform existing = root.Find(name);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(existing.gameObject);
        else
            Object.DestroyImmediate(existing.gameObject);
    }

    static void BuildRing(Transform parent, string name, float inset, float thickness, Color color)
    {
        GameObject ring = new GameObject(name, typeof(RectTransform));
        ring.layer = parent.gameObject.layer;
        ring.transform.SetParent(parent, false);
        StretchFull(ring.GetComponent<RectTransform>());

        CreateEdge(ring.transform, "Top",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(inset, -inset - thickness), new Vector2(-inset, -inset), color);

        CreateEdge(ring.transform, "Bottom",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(inset, inset), new Vector2(-inset, inset + thickness), color);

        CreateEdge(ring.transform, "Left",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(inset, inset + thickness), new Vector2(inset + thickness, -inset - thickness), color);

        CreateEdge(ring.transform, "Right",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-inset - thickness, inset + thickness), new Vector2(-inset, -inset - thickness), color);
    }

    static void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image image = go.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = color;
        image.raycastTarget = false;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
