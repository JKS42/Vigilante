using UnityEngine;

/// <summary>
/// Shared factory for the project's cel / toon look (Shader Graphs/CellShading).
/// </summary>
public static class CelMaterial
{
    public const string ShaderName = "Shader Graphs/CellShading";

    public static Shader FindShader()
    {
        return Shader.Find(ShaderName)
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
    }

    public static Material Create(Color color, string name = "Cel")
    {
        Shader shader = FindShader();
        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = name;
        ApplyColor(mat, color);
        if (mat.HasProperty("_Min"))
            mat.SetFloat("_Min", 0.3f);
        if (mat.HasProperty("_Max"))
            mat.SetFloat("_Max", 1f);
        if (mat.HasProperty("_Shades"))
            mat.SetFloat("_Shades", 0.49f);
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
        Shader cel = Shader.Find(ShaderName);
        if (mat == null || cel == null || mat.shader == cel)
            return;

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
        if (mat.HasProperty("_Min"))
            mat.SetFloat("_Min", 0.3f);
        if (mat.HasProperty("_Max"))
            mat.SetFloat("_Max", 1f);
        if (mat.HasProperty("_Shades"))
            mat.SetFloat("_Shades", 0.49f);
    }
}
