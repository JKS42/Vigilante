using UnityEngine;

/// <summary>
/// Lightweight procedural animation per enemy archetype when no Animator clips exist.
/// Bosses get a heavier bob / scale pulse; shotgun rushes lean forward.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    EnemyArchetype archetype = EnemyArchetype.Pistol;
    Transform visual;
    Vector3 baseScale;
    Vector3 baseLocalPos;
    float firePulse;
    float bobPhase;
    EnemyAI ai;
    UnityEngine.AI.NavMeshAgent agent;

    public void Configure(EnemyArchetype type)
    {
        archetype = type;
        CacheVisual();
    }

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        CacheVisual();
    }

    void CacheVisual()
    {
        if (visual != null)
            return;

        Renderer r = GetComponentInChildren<Renderer>();
        visual = r != null ? r.transform : transform;
        baseScale = visual.localScale;
        baseLocalPos = visual.localPosition;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void PlayFire()
    {
        firePulse = 1f;
    }

    public void PlayHurt()
    {
        firePulse = Mathf.Max(firePulse, 0.6f);
    }

    void Update()
    {
        if (visual == null)
            CacheVisual();
        if (visual == null)
            return;

        float speed = agent != null ? agent.velocity.magnitude : 0f;
        float bobAmp = archetype == EnemyArchetype.Boss ? 0.08f : 0.045f;
        float bobFreq = archetype == EnemyArchetype.Shotgun ? 9f : 6.5f;
        if (speed > 0.2f)
            bobPhase += Time.deltaTime * bobFreq;

        float bob = Mathf.Sin(bobPhase) * bobAmp * Mathf.Clamp01(speed / 3f);
        Vector3 lean = Vector3.zero;
        if (archetype == EnemyArchetype.Shotgun && speed > 1f)
            lean = visual.forward * 0.05f;

        if (archetype == EnemyArchetype.Rifle && ai != null && ai.CurrentState == EnemyState.Attack)
            bob *= 0.35f;

        firePulse = Mathf.MoveTowards(firePulse, 0f, Time.deltaTime * 4f);
        float punch = 1f - firePulse * (archetype == EnemyArchetype.Boss ? 0.18f : 0.12f);
        float tall = archetype == EnemyArchetype.Boss ? 1.25f : 1f;

        visual.localPosition = baseLocalPos + Vector3.up * bob + lean;
        visual.localScale = Vector3.Scale(baseScale, new Vector3(punch, tall * (2f - punch), punch));
    }
}
