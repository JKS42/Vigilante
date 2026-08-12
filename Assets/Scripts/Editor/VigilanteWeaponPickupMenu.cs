#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class VigilanteWeaponPickupMenu
{
    const string PickupsFolder = "Assets/Prefabs/Weapons/Pickups";

    [MenuItem("Vigilante/Create Weapon Pickup Prefabs")]
    public static void CreateWeaponPickupPrefabs()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Weapons");
        EnsureFolder(PickupsFolder);

        CreatePickup(
            "PistolPickup",
            "Assets/Prefabs/Weapons/Tactical Pistol.fbx",
            weaponIndex: 1,
            visualScale: 1f);

        CreatePickup(
            "ShotgunPickup",
            "Assets/Prefabs/Weapons/Pump Shotgun.fbx",
            weaponIndex: 2,
            visualScale: 1f);

        CreatePickup(
            "ARPickup",
            "Assets/Prefabs/Weapons/Assault Rifle.fbx",
            weaponIndex: 3,
            visualScale: 1f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vigilante: Weapon pickup prefabs created/updated in " + PickupsFolder);
    }

    static void CreatePickup(string prefabName, string meshPath, int weaponIndex, float visualScale)
    {
        GameObject meshAsset = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
        if (meshAsset == null)
        {
            Debug.LogError("Vigilante: Missing weapon mesh at " + meshPath);
            return;
        }

        GameObject root = new GameObject(prefabName);
        try
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.25f;
            trigger.center = new Vector3(0f, 0.4f, 0f);

            WeaponPickup pickup = root.AddComponent<WeaponPickup>();
            pickup.weaponIndex = weaponIndex;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(meshAsset);
            visual.name = meshAsset.name;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            visual.transform.localRotation = Quaternion.Euler(0f, 90f, -35f);
            visual.transform.localScale = Vector3.one * visualScale;

            string path = PickupsFolder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
