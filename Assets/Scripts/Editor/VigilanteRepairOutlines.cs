#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs pink/missing outline slots and re-applies Vigilante/CelOutline.
/// </summary>
public static class VigilanteRepairOutlines
{
    [MenuItem("Vigilante/Repair Cel Outlines (Open Scene)")]
    public static void RepairOpenScene()
    {
        CelMaterial.UpgradeSceneMaterials();
        CelOutline.RepairScene();
        Debug.Log("Vigilante: Upgraded cel materials + repaired outlines in open scene.");
    }

    [MenuItem("Vigilante/Upgrade Cel Materials To Shadow Shader")]
    public static void UpgradeCelMaterials()
    {
        Shader cel = Shader.Find(CelMaterial.ShaderName);
        if (cel == null)
        {
            Debug.LogError("Vigilante: Missing shader " + CelMaterial.ShaderName);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
                continue;
            if (mat.shader.name != CelMaterial.LegacyShaderName)
                continue;

            Undo.RecordObject(mat, "Upgrade Cel Material");
            CelMaterial.Convert(mat);
            EditorUtility.SetDirty(mat);
            count++;
        }

        CelMaterial.UpgradeSceneMaterials();
        AssetDatabase.SaveAssets();
        Debug.Log("Vigilante: Upgraded " + count + " cel materials to " + CelMaterial.ShaderName);
    }

    [MenuItem("Vigilante/Reimport Outline + CellShading Shaders")]
    public static void ReimportShaders()
    {
        AssetDatabase.ImportAsset("Assets/Shaders/CelOutline.shader", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Shaders/CelShading.shader", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Materials/CellShading.shadergraph", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Resources/CelOutline.mat", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Materials/CellShading.mat", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Materials/CelDefault.mat", ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vigilante: Reimported outline + cell shading assets.");
    }
}
#endif
