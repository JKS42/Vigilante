using System.Collections.Generic;
using UnityEngine;

public enum SquadRole
{
    Suppressor,
    Flanker
}

public class EnemySquad : MonoBehaviour
{
    public static EnemySquad Instance { get; private set; }

    [SerializeField] float alertShareRadius = 40f;
    [SerializeField] [Range(0f, 1f)] float flankerRatio = 0.4f;

    readonly List<EnemyAI> members = new List<EnemyAI>();

    public bool IsAlerted { get; private set; }
    public Vector3 LastKnownPlayerPos { get; private set; }

    public int AliveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && !members[i].IsDead)
                    count++;
            }

            return count;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static EnemySquad EnsureExists()
    {
        if (Instance != null)
            return Instance;

        EnemySquad existing = FindFirstObjectByType<EnemySquad>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("EnemySquad");
        Instance = go.AddComponent<EnemySquad>();
        return Instance;
    }

    public void Register(EnemyAI enemy)
    {
        if (enemy == null || members.Contains(enemy))
            return;
        members.Add(enemy);
    }

    public void Unregister(EnemyAI enemy)
    {
        members.Remove(enemy);
        if (members.Count == 0)
            IsAlerted = false;
    }

    public void BroadcastAlert(EnemyAI source, Vector3 playerPos)
    {
        IsAlerted = true;
        LastKnownPlayerPos = playerPos;

        AssignRoles();

        foreach (EnemyAI member in members)
        {
            if (member == null || member == source)
                continue;

            if (Vector3.Distance(source.transform.position, member.transform.position) > alertShareRadius)
                continue;

            member.ReceiveSquadAlert(playerPos);
        }
    }

    public void UpdateLastKnown(Vector3 playerPos)
    {
        IsAlerted = true;
        LastKnownPlayerPos = playerPos;
    }

    public void ClearAlert()
    {
        IsAlerted = false;
        foreach (EnemyAI member in members)
        {
            if (member != null)
                member.AssignedRole = SquadRole.Suppressor;
        }
    }

    public void AssignRoles()
    {
        List<EnemyAI> living = new List<EnemyAI>();
        foreach (EnemyAI member in members)
        {
            if (member != null && !member.IsDead)
                living.Add(member);
        }

        if (living.Count == 0)
            return;

        // Prefer shotgun / high-aggression as flankers; rifle / cover-heavy as suppressors.
        living.Sort((a, b) => FlankScore(b).CompareTo(FlankScore(a)));

        int flankerCount = Mathf.Max(1, Mathf.RoundToInt(living.Count * flankerRatio));
        if (living.Count == 1)
            flankerCount = 0;

        for (int i = 0; i < living.Count; i++)
            living[i].AssignedRole = i < flankerCount ? SquadRole.Flanker : SquadRole.Suppressor;
    }

    static float FlankScore(EnemyAI ai)
    {
        if (ai == null)
            return 0f;

        EnemyProfile profile = ai.GetComponent<EnemyProfile>();
        if (profile == null)
            return 0.4f;

        float score = profile.flankTendency + profile.aggression * 0.5f - profile.coverPreference * 0.35f;
        if (profile.archetype == EnemyArchetype.Shotgun)
            score += 0.5f;
        if (profile.archetype == EnemyArchetype.Rifle)
            score -= 0.35f;
        if (profile.archetype == EnemyArchetype.Boss)
            score += 0.2f;
        return score;
    }

    public bool IsCoverReservedByOther(CoverPoint point, EnemyAI requester)
    {
        return point != null && point.IsOccupied && point.OccupiedBy != requester;
    }
}
