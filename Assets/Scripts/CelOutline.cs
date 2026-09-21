using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borderlands-style inverted-hull outlines. Adds an outline material slot only —
/// never converts or mutates existing surface materials.
/// </summary>
public static class CelOutline
{
    public const string ShaderName = "Vigilante/CelOutline";
    public const string ResourcesMaterial = "CelOutline";

    static readonly Color OutlineColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    static readonly List<Material> matScratch = new List<Material>(8);
    static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

    static Material outlineTemplate;
    static Shader outlineShader;

    /// <summary>Marks a renderer that already received an outline slot.</summary>
    public sealed class Marker : MonoBehaviour { }

    public static void ApplyScene()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
            Apply(renderers[i]);
    }

    public static void ApplyHierarchy(GameObject root)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            Apply(renderers[i]);
    }

    public static void Apply(Renderer renderer)
    {
        if (renderer == null || ShouldSkip(renderer))
            return;

        if (renderer.GetComponent<Marker>() != null)
        {
            RefreshWidth(renderer);
            return;
        }

        Material outline = GetOutlineMaterial();
        if (outline == null)
            return;

        Material[] shared = renderer.sharedMaterials;
        if (shared == null || shared.Length == 0)
            return;

        for (int i = 0; i < shared.Length; i++)
        {
            if (IsOutlineMaterial(shared[i]))
            {
                EnsureMarker(renderer);
                ApplyWidthBlock(renderer, shared.Length - 1, ResolveWidth(renderer));
                return;
            }
        }

        if (!HasOpaqueSurface(shared))
            return;

        matScratch.Clear();
        for (int i = 0; i < shared.Length; i++)
        {
            if (shared[i] != null)
                matScratch.Add(shared[i]);
        }

        int outlineIndex = matScratch.Count;
        matScratch.Add(outline);
        renderer.sharedMaterials = matScratch.ToArray();
        ApplyWidthBlock(renderer, outlineIndex, ResolveWidth(renderer));
        EnsureMarker(renderer);
    }

    static void RefreshWidth(Renderer renderer)
    {
        Material[] shared = renderer.sharedMaterials;
        if (shared == null)
            return;

        for (int i = shared.Length - 1; i >= 0; i--)
        {
            if (!IsOutlineMaterial(shared[i]))
                continue;
            ApplyWidthBlock(renderer, i, ResolveWidth(renderer));
            return;
        }
    }

    static void EnsureMarker(Renderer renderer)
    {
        if (renderer.GetComponent<Marker>() == null)
            renderer.gameObject.AddComponent<Marker>();
    }

    static void ApplyWidthBlock(Renderer renderer, int materialIndex, float width)
    {
        if (materialIndex < 0)
            return;

        renderer.GetPropertyBlock(block, materialIndex);
        block.SetFloat("_OutlineWidth", width);
        block.SetColor("_OutlineColor", OutlineColor);
        renderer.SetPropertyBlock(block, materialIndex);
    }

    static float ResolveWidth(Renderer renderer)
    {
        Bounds b = renderer.bounds;
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

        // Screen-space widths (clip NDC). Keep midway thickness, no world halo gap.
        if (size < 0.5f)
            return 0.0035f;
        if (size > 6f)
            return 0.0055f;
        return 0.0045f;
    }

    static Material GetOutlineMaterial()
    {
        if (outlineTemplate != null)
            return outlineTemplate;

        outlineTemplate = Resources.Load<Material>(ResourcesMaterial);
        if (outlineTemplate != null)
            return outlineTemplate;

        if (outlineShader == null)
            outlineShader = Shader.Find(ShaderName);

        if (outlineShader == null)
            return null;

        outlineTemplate = new Material(outlineShader);
        outlineTemplate.name = "CelOutline_Runtime";
        outlineTemplate.hideFlags = HideFlags.HideAndDontSave;
        outlineTemplate.SetColor("_OutlineColor", OutlineColor);
        outlineTemplate.SetFloat("_OutlineWidth", 0.0045f);
        return outlineTemplate;
    }

    public static bool IsOutlineMaterial(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.name == ShaderName;
    }

    static bool HasOpaqueSurface(Material[] shared)
    {
        for (int i = 0; i < shared.Length; i++)
        {
            Material mat = shared[i];
            if (mat == null || mat.shader == null)
                continue;
            if (IsOutlineMaterial(mat))
                continue;
            if (IsNonOutlineableShader(mat.shader.name))
                continue;
            if (mat.renderQueue >= 3000)
                continue;
            return true;
        }

        return false;
    }

    static bool IsNonOutlineableShader(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName))
            return true;

        return shaderName.IndexOf("Particle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("UI/", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("TextMeshPro", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Sprites/", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Unlit/Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Nature/", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool ShouldSkip(Renderer renderer)
    {
        if (!renderer.enabled)
            return true;

        if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            return true;

        if (renderer.gameObject.layer == 5)
            return true;
        if (renderer.GetComponentInParent<Canvas>() != null)
            return true;

        if (renderer.GetComponentInParent<Camera>() != null)
            return true;
        if (renderer.GetComponentInParent<MouseMovement>() != null)
            return true;
        if (renderer.GetComponentInParent<WeaponSwitcher>() != null)
            return true;
        if (renderer.GetComponentInParent<Weapon>() != null)
            return true;
        if (renderer.GetComponentInParent<Melee>() != null)
            return true;

        Transform t = renderer.transform;
        string n = t.name;
        if (n.StartsWith("RuntimeNav")
            || n.Contains("CombatVfx")
            || n.Contains("EnemyHealthBar")
            || n.Contains("HealthBar")
            || n.StartsWith("SFXText")
            || n.StartsWith("Impact")
            || n.StartsWith("Muzzle")
            || n.StartsWith("Explosion")
            || n.StartsWith("DamageIndicator")
            || n.Contains("Minimap"))
            return true;

        Bounds b = renderer.bounds;
        float maxExtent = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        if (maxExtent < 0.06f)
            return true;

        return false;
    }
}
