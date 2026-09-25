using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a world pickup when an enemy dies so Level 1 can teach "kill → loot pistol".
/// The pickup appears only after the death animation has finished.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyProfile))]
public class EnemyWeaponDrop : MonoBehaviour
{
    [SerializeField] GameObject pickupPrefab;
    Health health;
    EnemyProfile profile;
    EnemyCombat combat;
    bool dropped;
    const float DropHover = 0.08f;

    void Awake()
    {
        health = GetComponent<Health>();
        profile = GetComponent<EnemyProfile>();
        combat = GetComponent<EnemyCombat>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void HandleDied()
    {
        if (dropped || profile == null)
            return;

        if (profile.weaponDropChance <= 0f || Random.value > profile.weaponDropChance)
            return;

        dropped = true;
        int ammo = ResolveDropAmmo();
        int index = profile.weaponDropIndex;
        if (index == 1)
            PistolIntroCinematic.HoldFirstPistolWave();
        StartCoroutine(DropWhenDeathEnds(ammo, index));
    }

    IEnumerator DropWhenDeathEnds(int ammo, int index)
    {
        EnemyMecanim mecanim = GetComponent<EnemyMecanim>();
        float elapsed = 0f;
        yield return null;

        while (elapsed < 12f)
        {
            if (mecanim == null || !mecanim.HasAnimator)
            {
                if (elapsed >= 0.35f)
                    break;
            }
            else if (mecanim.DeathAnimationFinished)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (this == null)
            yield break;

        Vector3 pos = ResolveDropPosition();
        WeaponPickup pickup = WeaponPickup.Spawn(pos, index, pickupPrefab, ammo);
        PistolIntroCinematic.NotifyDropped(pickup, index);
        TutorialPrompt.Notify("weapon_drop");

        CombatVfx.SpawnOnomatopoeia(pos + Vector3.up, "LOOT!");
        DialogueManager.PlayerLine("I'll take that.");
    }

    int ResolveDropAmmo()
    {
        if (combat == null)
            combat = GetComponent<EnemyCombat>();

        if (combat == null || combat.MeleeOnly)
            return 0;

        // Pass whatever is left in the mag (0 if they died mid-reload).
        if (combat.MagazineSize <= 0)
            return 0;

        int ammo = combat.AmmoInMagazine;

        // AR (slot 3): convert enemy mag leftovers at 3:1, rounded up.
        if (profile != null && profile.weaponDropIndex == 3)
            ammo = Mathf.CeilToInt(ammo / 3f);

        return ammo;
    }

    Vector3 ResolveDropPosition()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        const float ahead = 1.05f;
        Vector3 chest = transform.position + Vector3.up * 1.1f;
        float travel = ahead;
        RaycastHit[] wallHits = Physics.RaycastAll(chest, forward, ahead, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < wallHits.Length; i++)
        {
            Collider col = wallHits[i].collider;
            if (col == null || col.isTrigger || IsCharacterOrPickup(col))
                continue;
            travel = Mathf.Min(travel, Mathf.Max(0.35f, wallHits[i].distance - 0.3f));
        }

        Vector3 drop = chest + forward * travel;
        return SnapToGround(drop) + Vector3.up * DropHover;
    }

    static bool IsCharacterOrPickup(Collider col)
    {
        return col.GetComponentInParent<EnemyAI>() != null
            || col.GetComponentInParent<PlayerMovement>() != null
            || col.GetComponentInParent<WeaponPickup>() != null;
    }

    Vector3 SnapToGround(Vector3 drop)
    {
        Vector3 origin = drop + Vector3.up * 1.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        bool found = false;
        Vector3 ground = drop;
        float bestY = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.isTrigger || col.transform.root == transform.root)
                continue;

            if (hits[i].point.y < bestY)
            {
                bestY = hits[i].point.y;
                ground = hits[i].point;
                found = true;
            }
        }

        return found ? ground : drop;
    }
}
