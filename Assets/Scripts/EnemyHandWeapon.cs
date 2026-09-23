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

    void Start()
    {
        EnemyProfile profile = GetComponent<EnemyProfile>();
        if (profile != null && profile.archetype != EnemyArchetype.Pistol)
            return;

        Transform gun = FindDeepChild(transform, "EnemyPistol");
        if (gun == null)
            gun = AttachPistol();

        if (gun == null)
            return;

        BindMuzzle(gun);
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
