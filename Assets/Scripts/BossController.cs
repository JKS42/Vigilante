using UnityEngine;

/// <summary>
/// Boss phases inspired by arena showdowns: suppress from cover, aggressive push,
/// grenade barrages, and desperate close-range finishing.
/// </summary>
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(Health))]
public class BossController : MonoBehaviour
{
    [SerializeField] float grenadeCooldown = 4.5f;
    [SerializeField] float grenadeSpeed = 12f;
    [SerializeField] float phaseAnnounceCooldown = 6f;

    Health health;
    EnemyAI ai;
    EnemyCombat combat;
    Transform player;
    float nextGrenadeTime;
    float nextBarkTime;
    int lastPhase = -1;

    public int Phase
    {
        get
        {
            if (health == null)
                return 0;
            float pct = health.CurrentHealth / Mathf.Max(1f, health.MaxHealth);
            if (pct > 0.66f) return 1;
            if (pct > 0.33f) return 2;
            return 3;
        }
    }

    void Awake()
    {
        health = GetComponent<Health>();
        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombat>();
        health.SetMaxHealth(420f, true);
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
    }

    void Start()
    {
        transform.localScale = Vector3.one * 1.35f;
        DialogueManager.BossLine("So you're the vigilante... let's finish this.");
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 2.5f, "BOSS!");
        AudioManager.SetCombatMusicIntensity(1f);
    }

    void Update()
    {
        if (health != null && health.IsDead)
            return;

        if (player == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                player = tagged.transform;
        }

        int phase = Phase;
        if (phase != lastPhase)
        {
            lastPhase = phase;
            OnPhaseChanged(phase);
        }

        if (player == null || Time.time < nextGrenadeTime)
            return;

        if (ai == null)
            return;

        bool fighting = ai.CurrentState == EnemyState.Attack
            || ai.CurrentState == EnemyState.Chase
            || ai.CurrentState == EnemyState.Flank
            || ai.CurrentState == EnemyState.TakeCover;

        if (!fighting)
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        float chance = phase == 3 ? 0.55f : phase == 2 ? 0.35f : 0.2f;
        if (dist < 18f && Random.value < chance)
            ThrowGrenade();
    }

    void OnPhaseChanged(int phase)
    {
        EnemyProfile profile = GetComponent<EnemyProfile>();
        switch (phase)
        {
            case 1:
                DialogueManager.BossLine("Stay behind cover. It won't save you.");
                if (profile != null) { profile.aggression = 0.65f; profile.coverPreference = 0.55f; }
                grenadeCooldown = 5f;
                break;
            case 2:
                DialogueManager.BossLine("Enough games — flush him out!");
                CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 2.2f, "RAGE!");
                if (profile != null) { profile.aggression = 0.85f; profile.coverPreference = 0.3f; profile.flankTendency = 0.65f; }
                grenadeCooldown = 3.4f;
                AudioManager.SetCombatMusicIntensity(1.25f);
                break;
            case 3:
                DialogueManager.BossLine("No more running!");
                CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 2.2f, "FINALE!");
                if (profile != null) { profile.aggression = 1f; profile.coverPreference = 0.15f; profile.moveSpeed = 5.2f; profile.ApplyToComponents(); }
                grenadeCooldown = 2.4f;
                AudioManager.SetCombatMusicIntensity(1.5f);
                break;
        }
    }

    void ThrowGrenade()
    {
        nextGrenadeTime = Time.time + grenadeCooldown + Random.Range(0f, 0.8f);
        Vector3 origin = transform.position + Vector3.up * 1.6f + transform.forward * 0.5f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        Vector3 to = target - origin;
        float time = Mathf.Clamp(to.magnitude / grenadeSpeed, 0.45f, 1.4f);
        Vector3 velocity = to / time + Vector3.up * (4.5f + Phase);
        EnemyGrenade.Throw(origin, velocity, gameObject);
        DialogueManager.EnemyBark(transform.position, "grenade");
        CombatVfx.SpawnOnomatopoeia(origin, "GRENADE!");
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        if (Time.time < nextBarkTime)
            return;
        nextBarkTime = Time.time + phaseAnnounceCooldown;
        DialogueManager.BossLine(Phase >= 3 ? "Is that all?!" : "You'll regret that.");
    }
}
