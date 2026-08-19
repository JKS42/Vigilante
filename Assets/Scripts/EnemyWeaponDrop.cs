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
        Vector3 pos = transform.position + Vector3.up * 0.35f;
        WeaponPickup.Spawn(pos, profile.weaponDropIndex, pickupPrefab);

        CombatVfx.SpawnOnomatopoeia(pos + Vector3.up, "LOOT!");
        DialogueManager.PlayerLine("I'll take that.");
    }
}
