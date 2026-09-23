using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared factory for the project's cel / toon look (Vigilante/CelShading).
/// Samples main-light shadows so roofs and covers actually darken receivers.
/// </summary>
public static class CelMaterial
{
    public const string ShaderName = "Vigilante/CelShading";
    public const string LegacyShaderName = "Shader Graphs/CellShading";

    static Material template;
    static Shader cached;

    public static Shader FindShader()
    {
        if (cached != null)
            return cached;

        cached = Shader.Find(ShaderName);
        if (cached != null)
            return cached;

        // Keep a material reference alive so Shader.Find can resolve after import.
        Material mat = GetTemplate();
        if (mat != null && mat.shader != null && mat.shader.name == ShaderName)
        {
            cached = mat.shader;
            return cached;
        }

        cached = Shader.Find(LegacyShaderName)
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");
        return cached;
    }

    static Material GetTemplate()
    {
        if (template != null)
            return template;

        template = Resources.Load<Material>("CelDefault");
        if (template == null)
            template = Resources.Load<Material>("CellShading");
        return template;
    }

    public static Material Create(Color color, string name = "Cel")
    {
        Shader shader = FindShader();
        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = name;
        ApplyColor(mat, color);
        ApplyCelDefaults(mat);
        return mat;
    }

    public static void ApplyColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        mat.color = color;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
    }

    public static void Convert(Material mat)
    {
        Shader cel = FindShader();
        if (mat == null || cel == null)
            return;
        if (mat.shader == cel)
        {
            ApplyCelDefaults(mat);
            return;
        }

        Texture tex = null;
        if (mat.HasProperty("_BaseMap"))
            tex = mat.GetTexture("_BaseMap");
        if (tex == null && mat.HasProperty("_MainTex"))
            tex = mat.GetTexture("_MainTex");
        if (tex == null && mat.HasProperty("_Texture"))
            tex = mat.GetTexture("_Texture");

        Color color = Color.white;
        if (mat.HasProperty("_BaseColor"))
            color = mat.GetColor("_BaseColor");
        else if (mat.HasProperty("_Color"))
            color = mat.GetColor("_Color");
        else
            color = mat.color;

        mat.shader = cel;
        if (tex != null && mat.HasProperty("_Texture"))
            mat.SetTexture("_Texture", tex);
        ApplyColor(mat, color);
        ApplyCelDefaults(mat);
    }

    /// <summary>
    /// Swap legacy Unlit CellShading graph materials onto the shadow-aware shader.
    /// </summary>
    public static void UpgradeSceneMaterials()
    {
        Shader cel = FindShader();
        if (cel == null || cel.name != ShaderName)
            return;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            Material[] shared = r.sharedMaterials;
            if (shared == null)
                continue;

            bool changed = false;
            for (int m = 0; m < shared.Length; m++)
            {
                Material mat = shared[m];
                if (mat == null || mat.shader == null)
                    continue;
                if (mat.shader == cel)
                    continue;
                if (mat.shader.name != LegacyShaderName && mat.shader.name != "CellShading")
                    continue;

                Convert(mat);
                changed = true;
            }

            if (changed)
                r.sharedMaterials = shared;
        }
    }

    static void ApplyCelDefaults(Material mat)
    {
        if (mat == null)
            return;
        if (mat.HasProperty("_Min"))
            mat.SetFloat("_Min", 0.3f);
        if (mat.HasProperty("_Max"))
            mat.SetFloat("_Max", 1f);
        if (mat.HasProperty("_Shades"))
            mat.SetFloat("_Shades", 0.49f);
    }
}
