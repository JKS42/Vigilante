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
    [SerializeField] float proximityRange = 2.5f;
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
    EnemyMecanim mecanim;
    Transform player;

    EnemyState state = EnemyState.Idle;
    Vector3 lastKnownPlayerPos;
    Vector3 investigatePos;
    Vector3 homePos;
    Vector3 dynamicCoverPos;
    Vector3 lastHitPoint;
    Vector3 lastHitDir;
    CoverPoint currentCover;
    bool usingDynamicCover;
    bool pendingAttackAfterCover;
    bool preferLeftFlank;
    float stateTimer;
    float lostSightTimer;
    float idleTimer;
    float alertBroadcastCooldown;
    float reassessTimer;
    float stuckTimer;
    float plantTimer;
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
    bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        combat = GetComponent<EnemyCombat>();
        profile = GetComponent<EnemyProfile>();
        mecanim = GetComponent<EnemyMecanim>();
        if (mecanim == null)
            mecanim = GetComponentInChildren<EnemyMecanim>();

        if (profile != null && profile.archetype != EnemyArchetype.Pistol)
        {
            Transform pistolVisual = transform.Find("PistolVisual");
            if (pistolVisual != null)
                Destroy(pistolVisual.gameObject);

            EnemyMecanim[] mecanims = GetComponentsInChildren<EnemyMecanim>(true);
            for (int i = 0; i < mecanims.Length; i++)
            {
                if (mecanims[i] != null)
                    Destroy(mecanims[i]);
            }
            mecanim = null;
        }

        EnemyAnimator leftover = GetComponent<EnemyAnimator>();
        if (leftover != null)
        {
            leftover.enabled = false;
            Destroy(leftover);
        }

        if (combat == null)
            combat = gameObject.AddComponent<EnemyCombat>();

        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = 360f;
        agent.updateRotation = true;
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
        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
        if (GetComponent<EnemyHurtTint>() == null)
            gameObject.AddComponent<EnemyHurtTint>();

        CelOutline.RepairHierarchy(gameObject);

        EnemySquad.EnsureExists().Register(this);
        FindPlayer();
        PlaceOnNavMesh();
        SetState(EnemyState.Idle);
    }

    void Update()
    {
        if (IsDead)
            return;

        if (!PlaceOnNavMesh())
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

        TickStuckWatchdog(canSee);

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

    void TickStuckWatchdog(bool canSee)
    {
        if (state == EnemyState.Idle || state == EnemyState.Patrol)
        {
            stuckTimer = 0f;
            return;
        }

        bool idlePlant = !AgentReady
            || agent.isStopped
            || agent.velocity.sqrMagnitude < 0.04f;

        // Attack plant is only healthy while a shot anim is actually running.
        if (state == EnemyState.Attack)
        {
            bool shooting = mecanim != null && mecanim.IsPlayingShoot;
            if (canSee && shooting)
            {
                stuckTimer = 0f;
                return;
            }

            if (idlePlant)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer < 1.75f)
                return;

            UnstickToChase(canSee);
            return;
        }

        // Investigate/Search/Cover/Flank/Chase: no progress = stuck.
        bool progressing = AgentReady && agent.hasPath && !agent.isStopped
            && agent.remainingDistance > agent.stoppingDistance + 0.35f
            && agent.velocity.sqrMagnitude > 0.04f;

        if (progressing)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < 2f)
            return;

        UnstickToChase(canSee);
    }

    void UnstickToChase(bool canSee)
    {
        stuckTimer = 0f;
        plantTimer = 0f;
        ReleaseCover();
        if (mecanim != null)
            mecanim.CancelFireLock();
        SetAgentStopped(false);
        if (AgentReady)
            agent.ResetPath();
        PlaceOnNavMesh();

        if (canSee && player != null)
            SetState(EnemyState.Chase);
        else if (hasLastKnown)
            SetState(EnemyState.Search);
        else
            SetState(EnemyState.Patrol);
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

        if (instigator != null)
        {
            hasLastKnown = true;
            lastKnownPlayerPos = instigator.transform.position;
            lastHitPoint = hitPoint;
            lastHitDir = (transform.position - hitPoint).sqrMagnitude > 0.001f
                ? (transform.position - hitPoint).normalized
                : -transform.forward;
        }
        else if (player != null)
        {
            hasLastKnown = true;
            lastKnownPlayerPos = player.position;
            lastHitPoint = hitPoint;
            lastHitDir = (transform.position - hitPoint).normalized;
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

        float despawnDelay = 0.85f;
        if (mecanim != null && mecanim.HasAnimator)
        {
            bool headshot = lastHitPoint.y > transform.position.y + 1.35f;
            int variant = EnemyMecanim.ResolveDeathVariant(transform, lastHitPoint, lastHitDir, headshot);
            mecanim.PlayDeath(variant);
            despawnDelay = 2.6f;
        }
        else
        {
            EnemyDeathPose pose = GetComponent<EnemyDeathPose>();
            if (pose == null)
                pose = gameObject.AddComponent<EnemyDeathPose>();
            pose.Play();
        }

        CombatStimulus.NotifyEnemyDied(this);
        EnemySquad.Instance?.Unregister(this);
        DialogueManager.EnemyBark(transform.position, "death");
        CombatVfx.SpawnDeathKo(transform.position + Vector3.up * 1.5f);
        TutorialPrompt.Notify("enemy_killed");
        enabled = false;
        Destroy(gameObject, despawnDelay);
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

        if (ReachedDestination(0.15f))
            SetState(EnemyState.Idle);
    }

    void TickInvestigate(bool canSee)
    {
        if (canSee)
        {
            EnterCombatFromSight();
            return;
        }

        // Always count down — don't require arriving before the look timer expires.
        stateTimer -= Time.deltaTime;
        MoveTo(investigatePos);

        if (ReachedDestination(0.5f))
            FaceTarget(investigatePos);

        if (stateTimer <= 0f)
        {
            if (hasLastKnown)
                SetState(EnemyState.Search);
            else
                SetState(EnemyState.Patrol);
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

        MoveTo(dest);

        if (canSee && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            bool inRange = dist <= combat.AttackRange;

            if (combat.MeleeOnly)
            {
                MoveTo(player.position);
                if (dist <= combat.MeleeRange + 1.25f)
                    SetState(EnemyState.Attack);
                return;
            }

            if (dist <= combat.MeleeRange || (inRange && combat.HasLineOfFire(player)))
            {
                float flankChance = FlankTend * (AssignedRole == SquadRole.Flanker ? 1.2f : 0.7f);
                if (dist > combat.MeleeRange && Random.value < flankChance * 0.35f)
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
                MoveTo(player.position);
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

        MoveTo(dest);

        if (ReachedDestination(0.35f))
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
        if (ReachedDestination(0.4f))
        {
            SetState(EnemyState.Attack);
            return;
        }

        if (canSee && player != null)
            FaceTarget(player.position);

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
        bool usesMecanimShoot = mecanim != null && mecanim.HasAnimator && !combat.MeleeOnly;

        if (usesMecanimShoot)
        {
            if (dist > combat.AttackRange * 1.15f)
            {
                plantTimer = 0f;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                SetState(EnemyState.Chase);
                return;
            }

            // Hold preferred band — don't freeze permanently if too close/far.
            if (dist < PreferredDist * 0.55f)
            {
                plantTimer = 0f;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                Vector3 away = (transform.position - player.position).normalized;
                MoveTo(transform.position + away * 3.5f);
                FaceTarget(player.position);
                return;
            }

            if (dist > PreferredDist * 1.35f && dist <= combat.AttackRange)
            {
                plantTimer = 0f;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                MoveTo(player.position);
                FaceTarget(canSee ? player.position : lastKnownPlayerPos);
                return;
            }

            SetAgentStopped(true);
            if (AgentReady)
                agent.updateRotation = false;
            FaceTarget(canSee ? player.position : lastKnownPlayerPos);

            if (canSee)
            {
                plantTimer += Time.deltaTime;

                if (dist <= combat.MeleeRange)
                {
                    combat.TryAttack(player);
                    plantTimer = 0f;
                }
                else if (combat.HasLineOfFire(player) && !mecanim.IsMoving)
                {
                    if (!mecanim.IsFiring)
                        combat.TryFireAt(player);
                    if (mecanim.IsPlayingShoot)
                        plantTimer = 0f;
                }
                else if (!combat.HasLineOfFire(player) || plantTimer > 1.8f)
                {
                    plantTimer = 0f;
                    SetAgentStopped(false);
                    if (AgentReady)
                        agent.updateRotation = true;
                    SetState(EnemyState.Chase);
                }
            }
            else if (lostSightTimer >= lostSightGrace)
            {
                plantTimer = 0f;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                SetState(EnemyState.Search);
            }
            return;
        }

        // Aggressive close-range: keep advancing while shooting.
        bool pushIn = Aggression > 0.7f && dist > PreferredDist * 0.6f;
        bool backOff = HoldBias > 0.6f && dist < PreferredDist * 0.7f;

        if (pushIn || backOff)
        {
            SetAgentStopped(false);
            if (pushIn)
                MoveTo(player.position);
            else
                MoveTo(transform.position + (transform.position - player.position).normalized * 4f);
        }
        else
        {
            SetAgentStopped(true);
        }

        FaceTarget(canSee ? player.position : lastKnownPlayerPos);

        if (canSee)
        {
            if (combat.MeleeOnly)
            {
                if (dist <= combat.MeleeRange)
                    combat.TryAttack(player);
            }
            else if (dist <= combat.MeleeRange || combat.HasLineOfFire(player))
                combat.TryAttack(player);
            else if (AssignedRole == SquadRole.Flanker || Random.value < FlankTend)
                BeginFlank();
            else
            {
                pendingAttackAfterCover = true;
                BeginTakeCover();
            }

            if (dist > combat.AttackRange * 1.15f)
            {
                SetAgentStopped(false);
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
            SetAgentStopped(false);
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

        MoveTo(lastKnownPlayerPos);
        stateTimer -= Time.deltaTime;

        if (ReachedDestination(0.4f))
        {
            if (NavMesh.SamplePosition(
                    lastKnownPlayerPos + Random.insideUnitSphere * 6f,
                    out NavMeshHit hit,
                    6f,
                    NavMesh.AllAreas))
            {
                lastKnownPlayerPos = hit.position;
                MoveTo(lastKnownPlayerPos);
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
        preferLeftFlank = (EntityId.ToULong(GetEntityId()) & 1UL) == 0UL;

        Vector3 threat = player != null ? player.position : lastKnownPlayerPos;
        float dist = flankDistance * (0.75f + FlankTend * 0.5f);
        if (CoverFinder.FindFlankPosition(transform.position, threat, dist, preferLeftFlank, out Vector3 flankPos))
        {
            SetAgentStopped(false);
            MoveTo(flankPos);
            SetState(EnemyState.Flank);
            DialogueManager.EnemyBark(transform.position, "flank");
        }
        else
        {
            SetState(EnemyState.Chase);
        }
    }

    void SetState(EnemyState next)
    {
        if (state == EnemyState.Attack && next != EnemyState.Attack)
        {
            SetAgentStopped(false);
            if (AgentReady)
                agent.updateRotation = true;
        }

        state = next;
        stateTimer = 0f;
        stuckTimer = 0f;
        plantTimer = 0f;

        switch (next)
        {
            case EnemyState.Idle:
                idleTimer = idleTime + Random.Range(0f, 1f);
                if (AgentReady)
                {
                    agent.updateRotation = true;
                    agent.ResetPath();
                }
                break;
            case EnemyState.Patrol:
                if (AgentReady)
                    agent.updateRotation = true;
                SetRandomPatrolDestination();
                break;
            case EnemyState.Investigate:
                stateTimer = investigateLookTime;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                MoveTo(investigatePos);
                break;
            case EnemyState.Chase:
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                break;
            case EnemyState.TakeCover:
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                break;
            case EnemyState.Flank:
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                break;
            case EnemyState.Attack:
                if (Aggression < 0.7f)
                    SetAgentStopped(true);
                break;
            case EnemyState.Search:
                stateTimer = searchGiveUpTime;
                SetAgentStopped(false);
                if (AgentReady)
                    agent.updateRotation = true;
                if (hasLastKnown)
                    MoveTo(lastKnownPlayerPos);
                break;
        }
    }

    void SetRandomPatrolDestination()
    {
        Vector3 random = homePos + Random.insideUnitSphere * patrolRadius;
        random.y = homePos.y;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            MoveTo(hit.position);
        else
            MoveTo(homePos);
    }

    public bool PlaceOnNavMesh()
    {
        if (agent == null)
            return false;

        if (agent.enabled && agent.isOnNavMesh)
            return true;

        Vector3 probe = LevelCombatBootstrap.SnapToFloor(transform.position);
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 4f, NavMesh.AllAreas)
            && !NavMesh.SamplePosition(LevelCombatBootstrap.SnapToFloor(homePos), out hit, 6f, NavMesh.AllAreas)
            && !NavMesh.SamplePosition(probe, out hit, 12f, NavMesh.AllAreas))
        {
            // Keep trying next frames — don't permanently disable the agent.
            return false;
        }

        transform.position = hit.position;
        agent.enabled = true;
        agent.Warp(hit.position);
        homePos = hit.position;
        return agent.isOnNavMesh;
    }

    bool ReachedDestination(float slack)
    {
        if (!AgentReady || agent.pathPending)
            return false;
        return agent.remainingDistance <= agent.stoppingDistance + slack;
    }

    void MoveTo(Vector3 destination)
    {
        if (!AgentReady)
            return;
        agent.SetDestination(destination);
    }

    void SetAgentStopped(bool stopped)
    {
        if (!AgentReady)
            return;
        agent.isStopped = stopped;
    }

    bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
        Vector3 target = EnemyCombat.GetAimPoint(player);
        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist > sightRange)
            return false;

        bool closeEnoughToSense = dist <= proximityRange;
        if (!closeEnoughToSense)
        {
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
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, toTarget.normalized, dist + 0.25f, losMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.transform.root == transform.root)
                continue;

            if (hits[i].transform == player || hits[i].transform.IsChildOf(player))
                return true;

            Health h = col.GetComponentInParent<Health>();
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
