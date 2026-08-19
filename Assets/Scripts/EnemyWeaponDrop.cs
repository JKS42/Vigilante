using UnityEngine;

/// <summary>
/// Spawns a world pickup when an enemy dies so Level 1 can teach "kill → loot pistol".
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyProfile))]
public class EnemyWeaponDrop : MonoBehaviour
{
    [SerializeField] GameObject pickupPrefab;
    Health health;
    EnemyProfile profile;
    bool dropped;
    const float DropHover = 0.08f;

    void Awake()
    {
        health = GetComponent<Health>();
        profile = GetComponent<EnemyProfile>();
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
        Vector3 pos = ResolveDropPosition();
        WeaponPickup.Spawn(pos, profile.weaponDropIndex, pickupPrefab);

        CombatVfx.SpawnOnomatopoeia(pos + Vector3.up, "LOOT!");
        DialogueManager.PlayerLine("I'll take that.");
    }

    Vector3 ResolveDropPosition()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        bool found = false;
        Vector3 ground = transform.position;
        float bestY = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.transform.root == transform.root)
                continue;

            if (hits[i].point.y < bestY)
            {
                bestY = hits[i].point.y;
                ground = hits[i].point;
                found = true;
            }
        }

        if (found)
            return ground + Vector3.up * DropHover;

        return transform.position + Vector3.up * DropHover;
    }
}
