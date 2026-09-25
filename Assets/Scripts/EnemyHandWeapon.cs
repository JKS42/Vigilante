using UnityEngine;

/// <summary>
/// Parents a pistol mesh to the Mixamo right hand on pistol enemies,
/// and binds EnemyCombat fire origin to that pistol's Muzzle.
/// </summary>
public class EnemyHandWeapon : MonoBehaviour
{
    [SerializeField] GameObject pistolPrefab;
    [SerializeField] Vector3 localPosition = new Vector3(-0.02f, 0.05f, 0.02f);
    [SerializeField] Vector3 localEuler = new Vector3(-90f, 90f, 0f);
    [SerializeField] float localScale = 0.45f;
    [SerializeField] GameObject batPrefab;
    [SerializeField] Vector3 batLocalPosition = new Vector3(0f, 0.09f, 0.02f);
    [SerializeField] Vector3 batLocalEuler = new Vector3(8f, 90f, 90f);
    [SerializeField] float batLocalScale = 1f;

    void Start()
    {
        EnemyProfile profile = GetComponent<EnemyProfile>();
        if (ShouldCarryBat(profile))
        {
            HideStandIn();
            KeepPrefabBat();
            return;
        }

        if (profile != null && profile.archetype != EnemyArchetype.Pistol)
            return;

        Transform gun = FindDeepChild(transform, "EnemyPistol");
        if (gun == null)
            gun = AttachPistol();

        if (gun == null)
            return;

        BindMuzzle(gun);
    }

    void LateUpdate()
    {
        EnemyProfile profile = GetComponent<EnemyProfile>();
        if (!ShouldCarryBat(profile))
            return;

        KeepPrefabBat();
    }

    bool ShouldCarryBat(EnemyProfile profile)
    {
        if (profile != null && profile.archetype == EnemyArchetype.Melee)
            return true;
        return name.IndexOf("BatEnemy", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void HideStandIn()
    {
        Transform standIn = transform.Find("Bat");
        if (standIn != null && IsProBuilder(standIn))
            standIn.gameObject.SetActive(false);
    }

    static bool IsProBuilder(Transform t)
    {
        Component[] components = t.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                continue;
            string typeName = components[i].GetType().Name;
            if (typeName == "ProBuilderMesh" || typeName == "ProBuilderShape")
                return true;
        }
        return false;
    }

    void KeepPrefabBat()
    {
        HideStandIn();

        Transform bat = FindDeepChild(transform, "EnemyBat");
        if (bat == null)
            bat = FindDeepChild(transform, "Bat");
        if (bat == null || IsProBuilder(bat))
            return;

        bat.gameObject.SetActive(true);
        Renderer[] renderers = bat.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].enabled = true;
            renderers[i].gameObject.SetActive(true);
        }

        if (bat.GetComponentInChildren<EnemyBatHit>(true) == null)
        {
            BoxCollider box = bat.gameObject.GetComponent<BoxCollider>();
            if (box == null)
                box = bat.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (TryGetBatLocalBounds(bat, out Bounds local))
            {
                box.center = local.center;
                box.size = local.size;
            }
            bat.gameObject.AddComponent<EnemyBatHit>();
        }
    }

    static bool TryGetBatLocalBounds(Transform bat, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = bat.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds world = renderer.bounds;
            Vector3 extents = world.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 local = bat.InverseTransformPoint(corner);
                        if (!any)
                        {
                            bounds = new Bounds(local, Vector3.zero);
                            any = true;
                        }
                        else
                            bounds.Encapsulate(local);
                    }
                }
            }
        }

        return any && bounds.size.sqrMagnitude > 0.0001f;
    }

    Transform AttachPistol()
    {
        Transform hand = FindHand(transform);
        if (hand == null || pistolPrefab == null)
            return null;

        GameObject gun = Instantiate(pistolPrefab, hand);
        gun.name = "EnemyPistol";
        gun.transform.localPosition = localPosition;
        gun.transform.localRotation = Quaternion.Euler(localEuler);
        gun.transform.localScale = Vector3.one * localScale;

        Collider[] cols = gun.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                Destroy(cols[i]);
        }

        MonoBehaviour[] behaviours = gun.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i] == this)
                continue;
            System.Type t = behaviours[i].GetType();
            if (t.Name == "Pistol" || t.Name == "Weapon" || t.Name == "WeaponViewMotion")
                Destroy(behaviours[i]);
        }

        return gun.transform;
    }

    void BindMuzzle(Transform gun)
    {
        Transform muzzle = FindDeepChild(gun, "Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(gun, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            muzzle = muzzleGo.transform;
        }

        EnemyCombat combat = GetComponent<EnemyCombat>();
        if (combat != null)
            combat.SetMuzzle(muzzle);
    }

    static Transform FindHand(Transform root)
    {
        string[] names = { "mixamorig:RightHand", "RightHand", "Hand_R", "hand_r" };
        for (int n = 0; n < names.Length; n++)
        {
            Transform found = FindDeepChild(root, names[n]);
            if (found != null)
                return found;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            if (n.IndexOf("RightHand", System.StringComparison.OrdinalIgnoreCase) >= 0
                && n.IndexOf("Index", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Thumb", System.StringComparison.OrdinalIgnoreCase) < 0)
                return all[i];
        }
        return null;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
