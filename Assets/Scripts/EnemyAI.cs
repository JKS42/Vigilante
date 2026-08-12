using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Patrol,
    Investigate,
    Chase,
    TakeCover,
    Flank,
    Attack,
    Search
}

/// <summary>
/// Full tactical enemy FSM. Behaviour weights come from EnemyProfile when present:
/// shotgun rushes, rifle holds range / cover, pistol balanced, boss mixes pressure.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    [Header("Perception")]
    [SerializeField] float sightRange = 28f;
    [SerializeField] float sightFov = 110f;
    [SerializeField] float hearRange = 40f;
    [SerializeField] float breachReactRange = 22f;
    [SerializeField] LayerMask losMask = ~0;
    [SerializeField] Transform eye;

    [Header("Movement")]
    [SerializeField] float patrolRadius = 12f;
    [SerializeField] float idleTime = 2f;
    [SerializeField] float searchGiveUpTime = 8f;
    [SerializeField] float coverSearchRadius = 16f;
    [SerializeField] float flankDistance = 10f;
    [SerializeField] float stoppingDistance = 1.2f;

    [Header("Combat Reactions")]
    [SerializeField] float hurtCoverChance = 0.75f;
    [SerializeField] float lostSightGrace = 1.25f;
    [SerializeField] float investigateLookTime = 2f;

    NavMeshAgent agent;
    Health health;
    EnemyCombat combat;
    EnemyProfile profile;
    EnemyAnimator animator;
    Transform player;

    EnemyState state = EnemyState.Idle;
    Vector3 lastKnownPlayerPos;
    Vector3 investigatePos;
    Vector3 homePos;
    Vector3 dynamicCoverPos;
    CoverPoint currentCover;
    bool usingDynamicCover;
    bool pendingAttackAfterCover;
    bool preferLeftFlank;
    float stateTimer;
    float lostSightTimer;
    float idleTimer;
    float alertBroadcastCooldown;
    float reassessTimer;
    bool hasLastKnown;
    bool wasSeeingPlayer;

    public SquadRole AssignedRole { get; set; } = SquadRole.Suppressor;
    public bool IsDead => health != null && health.IsDead;
    public EnemyState CurrentState => state;

    float Aggression => profile != null ? profile.aggression : 0.5f;
    float CoverPref => profile != null ? profile.coverPreference : 0.55f;
    float FlankTend => profile != null ? profile.flankTendency : 0.4f;
    float HoldBias => profile != null ? profile.holdDistanceBias : 0.4f;
    float PreferredDist => profile != null ? profile.preferredEngageDistance : 10f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        combat = GetComponent<EnemyCombat>();
        profile = GetComponent<EnemyProfile>();
        animator = GetComponent<EnemyAnimator>();
        if (combat == null)
            combat = gameObject.AddComponent<EnemyCombat>();

        agent.stoppingDistance = stoppingDistance;
        homePos = transform.position;

        if (eye == null)
        {
            GameObject eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(transform);
            eyeGo.transform.localPosition = new Vector3(0f, 1.5f, 0.2f);
            eye = eyeGo.transform;
        }
    }

    void OnEnable()
    {
        CombatStimulus.OnNoise += HandleNoise;
        CombatStimulus.OnBreach += HandleBreach;
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    void OnDisable()
    {
        CombatStimulus.OnNoise -= HandleNoise;
        CombatStimulus.OnBreach -= HandleBreach;
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }

        ReleaseCover();
        EnemySquad.Instance?.Unregister(this);
    }

    void Start()
    {
        if (!gameObject.CompareTag("Enemy"))
            gameObject.tag = "Enemy";

        if (profile == null)
            profile = EnemyProfile.ApplyDefaults(gameObject, EnemyArchetype.Pistol);
        else
            profile.ApplyToComponents();

        if (GetComponent<EnemyWeaponDrop>() == null)
            gameObject.AddComponent<EnemyWeaponDrop>();

        EnemySquad.EnsureExists().Register(this);
        FindPlayer();
        SetState(EnemyState.Idle);
    }

    void Update()
    {
        if (IsDead)
            return;

        if (player == null)
            FindPlayer();

        bool canSee = CanSeePlayer();
        alertBroadcastCooldown -= Time.deltaTime;
        reassessTimer -= Time.deltaTime;

        if (canSee)
        {
            lostSightTimer = 0f;
            hasLastKnown = true;
            lastKnownPlayerPos = player.position;
            EnemySquad.Instance?.UpdateLastKnown(lastKnownPlayerPos);

            if (!wasSeeingPlayer || alertBroadcastCooldown <= 0f)
            {
                EnemySquad.Instance?.BroadcastAlert(this, lastKnownPlayerPos);
                alertBroadcastCooldown = 1.5f;
            }
        }
        else if (hasLastKnown && (state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.Flank))
        {
            lostSightTimer += Time.deltaTime;
        }

        wasSeeingPlayer = canSee;

        switch (state)
        {
            case EnemyState.Idle: TickIdle(canSee); break;
            case EnemyState.Patrol: TickPatrol(canSee); break;
            case EnemyState.Investigate: TickInvestigate(canSee); break;
            case EnemyState.Chase: TickChase(canSee); break;
            case EnemyState.TakeCover: TickTakeCover(canSee); break;
            case EnemyState.Flank: TickFlank(canSee); break;
            case EnemyState.Attack: TickAttack(canSee); break;
            case EnemyState.Search: TickSearch(canSee); break;
        }
    }

    void FindPlayer()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            player = tagged.transform;
            return;
        }

        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null)
            player = movement.transform;
    }

    public void ReceiveSquadAlert(Vector3 playerPos)
    {
        if (IsDead)
            return;

        hasLastKnown = true;
        lastKnownPlayerPos = playerPos;

        if (state == EnemyState.Idle || state == EnemyState.Patrol || state == EnemyState.Search)
            SetState(EnemyState.Investigate);
        investigatePos = playerPos;
    }

    void HandleNoise(Vector3 position, float radius, StimulusType type)
    {
        if (IsDead)
            return;

        float dist = Vector3.Distance(transform.position, position);
        float effective = Mathf.Min(radius, hearRange);
        if (dist > effective)
            return;

        investigatePos = position;
        hasLastKnown = true;
        lastKnownPlayerPos = position;

        if (state == EnemyState.Attack || state == EnemyState.TakeCover || state == EnemyState.Flank || state == EnemyState.Chase)
            return;

        SetState(EnemyState.Investigate);
        EnemySquad.Instance?.BroadcastAlert(this, position);
    }

    void HandleBreach(Vector3 position)
    {
        if (IsDead)
            return;

        if (Vector3.Distance(transform.position, position) > breachReactRange)
            return;

        hasLastKnown = true;
        lastKnownPlayerPos = player != null ? player.position : position;
        investigatePos = position;
        pendingAttackAfterCover = true;

        if (Aggression > 0.75f && Random.value < Aggression)
            SetState(EnemyState.Chase);
        else
            BeginTakeCover();

        EnemySquad.Instance?.BroadcastAlert(this, lastKnownPlayerPos);
        DialogueManager.EnemyBark(transform.position, "breach");
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        if (IsDead)
            return;

        animator?.PlayHurt();

        if (instigator != null)
        {
            hasLastKnown = true;
            lastKnownPlayerPos = instigator.transform.position;
        }
        else if (player != null)
        {
            hasLastKnown = true;
            lastKnownPlayerPos = player.position;
        }

        EnemySquad.Instance?.BroadcastAlert(this, lastKnownPlayerPos);
        DialogueManager.EnemyBark(transform.position, "hurt");

        float coverRoll = hurtCoverChance * CoverPref;
        bool exposed = state != EnemyState.TakeCover && state != EnemyState.Attack;
        bool wantCover = (exposed || Random.value <= coverRoll) && Aggression < 0.85f;

        if (wantCover && state != EnemyState.TakeCover)
        {
            pendingAttackAfterCover = true;
            BeginTakeCover();
        }
        else if (Aggression > 0.7f)
        {
            SetState(EnemyState.Chase);
        }
        else if (state == EnemyState.Idle || state == EnemyState.Patrol || state == EnemyState.Investigate || state == EnemyState.Search)
        {
            SetState(EnemyState.Chase);
        }
    }

    void HandleDied()
    {
        ReleaseCover();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        CombatStimulus.NotifyEnemyDied(this);
        EnemySquad.Instance?.Unregister(this);
        DialogueManager.EnemyBark(transform.position, "death");
        CombatVfx.SpawnOnomatopoeia(transform.position + Vector3.up * 1.5f, "THUD!");
        enabled = false;
        Destroy(gameObject, 2.5f);
    }

    void TickIdle(bool canSee)
    {
        if (canSee)
        {
            EnterCombatFromSight();
            return;
        }

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
            SetState(EnemyState.Patrol);
    }

    void TickPatrol(bool canSee)
    {
        if (canSee)
        {
            EnterCombatFromSight();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            SetState(EnemyState.Idle);
    }

    void TickInvestigate(bool canSee)
    {
        if (canSee)
        {
            EnterCombatFromSight();
            return;
        }

        agent.SetDestination(investigatePos);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
        {
            stateTimer -= Time.deltaTime;
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            if (stateTimer <= 0f)
            {
                if (hasLastKnown)
                    SetState(EnemyState.Search);
                else
                    SetState(EnemyState.Patrol);
            }
        }
    }

    void EnterCombatFromSight()
    {
        // Aggressive types push; cautious types peek cover first.
        if (Random.value < CoverPref * 0.55f && Aggression < 0.7f)
        {
            pendingAttackAfterCover = true;
            BeginTakeCover();
        }
        else if (AssignedRole == SquadRole.Flanker || Random.value < FlankTend)
        {
            BeginFlank();
        }
        else
        {
            SetState(EnemyState.Chase);
        }
    }

    void TickChase(bool canSee)
    {
        if (!hasLastKnown && player == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        Vector3 dest = canSee && player != null ? player.position : lastKnownPlayerPos;

        if (canSee && player != null && HoldBias > 0.55f)
        {
            // Rifle-style: hold preferred distance instead of running into face.
            Vector3 away = (transform.position - player.position).normalized;
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < PreferredDist * 0.75f)
                dest = transform.position + away * 4f;
            else if (dist > PreferredDist * 1.2f)
                dest = player.position;
            else
                dest = transform.position + transform.right * (preferLeftFlank ? -3f : 3f);
        }

        agent.SetDestination(dest);

        if (canSee && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            bool inRange = dist <= combat.AttackRange;

            if (inRange && combat.HasLineOfFire(player))
            {
                float flankChance = FlankTend * (AssignedRole == SquadRole.Flanker ? 1.2f : 0.7f);
                if (Random.value < flankChance * 0.35f)
                {
                    BeginFlank();
                    return;
                }

                SetState(EnemyState.Attack);
                return;
            }

            // Shotgun aggression: keep closing even without perfect LOS.
            if (Aggression > 0.8f && dist > 2f && reassessTimer <= 0f)
            {
                reassessTimer = 0.6f;
                agent.SetDestination(player.position);
            }
        }
        else if (lostSightTimer >= lostSightGrace)
        {
            if (Random.value < FlankTend)
                BeginFlank();
            else
                SetState(EnemyState.Search);
        }
    }

    void TickTakeCover(bool canSee)
    {
        Vector3 dest = usingDynamicCover
            ? dynamicCoverPos
            : (currentCover != null ? currentCover.transform.position : transform.position);

        agent.SetDestination(dest);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.35f)
        {
            if (pendingAttackAfterCover)
            {
                pendingAttackAfterCover = false;
                if (Aggression > 0.75f)
                    SetState(EnemyState.Chase);
                else
                    SetState(EnemyState.Attack);
            }
            else if (AssignedRole == SquadRole.Flanker || Random.value < FlankTend)
                BeginFlank();
            else if (Aggression > 0.75f)
                SetState(EnemyState.Chase);
            else
                SetState(EnemyState.Attack);
            return;
        }

        if (canSee && player != null && Vector3.Distance(transform.position, player.position) < 3f)
            SetState(EnemyState.Attack);
    }

    void TickFlank(bool canSee)
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
        {
            SetState(EnemyState.Attack);
            return;
        }

        if (canSee && player != null && combat.HasLineOfFire(player))
        {
            FaceTarget(player.position);
            combat.TryFireAt(player);
        }

        if (!canSee && lostSightTimer >= lostSightGrace * 2f)
            SetState(EnemyState.Search);
    }

    void TickAttack(bool canSee)
    {
        if (player == null)
        {
            SetState(EnemyState.Search);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        // Aggressive close-range: keep advancing while shooting.
        bool pushIn = Aggression > 0.7f && dist > PreferredDist * 0.6f;
        bool backOff = HoldBias > 0.6f && dist < PreferredDist * 0.7f;

        if (pushIn || backOff)
        {
            agent.isStopped = false;
            if (pushIn)
                agent.SetDestination(player.position);
            else
                agent.SetDestination(transform.position + (transform.position - player.position).normalized * 4f);
        }
        else
        {
            agent.isStopped = true;
        }

        FaceTarget(canSee ? player.position : lastKnownPlayerPos);

        if (canSee)
        {
            if (combat.HasLineOfFire(player))
                combat.TryFireAt(player);
            else if (AssignedRole == SquadRole.Flanker || Random.value < FlankTend)
                BeginFlank();
            else
            {
                pendingAttackAfterCover = true;
                BeginTakeCover();
            }

            if (dist > combat.AttackRange * 1.15f)
            {
                agent.isStopped = false;
                SetState(EnemyState.Chase);
            }
            else if (reassessTimer <= 0f && Random.value < FlankTend * 0.15f)
            {
                reassessTimer = 2.5f;
                BeginFlank();
            }
        }
        else if (lostSightTimer >= lostSightGrace)
        {
            agent.isStopped = false;
            SetState(EnemyState.Search);
        }
    }

    void TickSearch(bool canSee)
    {
        if (canSee)
        {
            EnterCombatFromSight();
            return;
        }

        agent.SetDestination(lastKnownPlayerPos);
        stateTimer -= Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
        {
            if (NavMesh.SamplePosition(
                    lastKnownPlayerPos + Random.insideUnitSphere * 6f,
                    out NavMeshHit hit,
                    6f,
                    NavMesh.AllAreas))
            {
                lastKnownPlayerPos = hit.position;
                agent.SetDestination(lastKnownPlayerPos);
            }
        }

        if (stateTimer <= 0f)
        {
            hasLastKnown = false;
            SetState(EnemyState.Patrol);
        }
    }

    void BeginTakeCover()
    {
        ReleaseCover();
        usingDynamicCover = false;

        Vector3 threat = hasLastKnown
            ? lastKnownPlayerPos
            : (player != null ? player.position : transform.position + transform.forward);

        CoverPoint cover = CoverFinder.FindBestCover(transform.position, threat, coverSearchRadius, this, currentCover);
        if (cover != null && cover.TryOccupy(this))
        {
            currentCover = cover;
            SetState(EnemyState.TakeCover);
            return;
        }

        if (CoverFinder.FindDynamicCover(transform.position, threat, coverSearchRadius, out dynamicCoverPos))
        {
            usingDynamicCover = true;
            SetState(EnemyState.TakeCover);
            return;
        }

        SetState(hasLastKnown || player != null ? EnemyState.Chase : EnemyState.Investigate);
    }

    void BeginFlank()
    {
        ReleaseCover();
        preferLeftFlank = GetInstanceID() % 2 == 0;

        Vector3 threat = player != null ? player.position : lastKnownPlayerPos;
        float dist = flankDistance * (0.75f + FlankTend * 0.5f);
        if (CoverFinder.FindFlankPosition(transform.position, threat, dist, preferLeftFlank, out Vector3 flankPos))
        {
            agent.isStopped = false;
            agent.SetDestination(flankPos);
            SetState(EnemyState.Flank);
            DialogueManager.EnemyBark(transform.position, "flank");
        }
        else
        {
            SetState(EnemyState.Attack);
        }
    }

    void SetState(EnemyState next)
    {
        if (state == EnemyState.Attack && next != EnemyState.Attack && agent != null)
            agent.isStopped = false;

        state = next;
        stateTimer = 0f;

        switch (next)
        {
            case EnemyState.Idle:
                idleTimer = idleTime + Random.Range(0f, 1f);
                if (agent.isOnNavMesh)
                    agent.ResetPath();
                break;
            case EnemyState.Patrol:
                SetRandomPatrolDestination();
                break;
            case EnemyState.Investigate:
                stateTimer = investigateLookTime;
                agent.isStopped = false;
                agent.SetDestination(investigatePos);
                break;
            case EnemyState.Chase:
                agent.isStopped = false;
                break;
            case EnemyState.TakeCover:
                agent.isStopped = false;
                break;
            case EnemyState.Flank:
                agent.isStopped = false;
                break;
            case EnemyState.Attack:
                if (agent.isOnNavMesh && Aggression < 0.7f)
                    agent.isStopped = true;
                break;
            case EnemyState.Search:
                stateTimer = searchGiveUpTime;
                agent.isStopped = false;
                if (hasLastKnown)
                    agent.SetDestination(lastKnownPlayerPos);
                break;
        }
    }

    void SetRandomPatrolDestination()
    {
        Vector3 random = homePos + Random.insideUnitSphere * patrolRadius;
        random.y = homePos.y;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(homePos);
    }

    bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist > sightRange)
            return false;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        Vector3 flatDir = toTarget;
        flatDir.y = 0f;
        if (flatForward.sqrMagnitude > 0.001f && flatDir.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(flatForward, flatDir);
            if (angle > sightFov * 0.5f)
                return false;
        }

        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, dist, losMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player))
                return true;

            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null && h.transform.root.CompareTag("Player"))
                return true;

            return false;
        }

        return true;
    }

    void FaceTarget(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
    }

    void ReleaseCover()
    {
        if (currentCover != null)
        {
            currentCover.Release(this);
            currentCover = null;
        }

        usingDynamicCover = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearRange);
    }
}
