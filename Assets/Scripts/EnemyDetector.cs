using System;
using UnityEngine;

/// <summary>
/// Reports how many enemies are still alive via EnemySquad.
///
/// Scene wiring:
/// 1. Empty GameObject, add this component.
/// 2. Assign this reference on UIManager (and any other listeners).
/// </summary>
public class EnemyDetector : MonoBehaviour
{
    [SerializeField] bool logCount;

    int lastCount = -1;
    bool allDeadFired;

    public int AliveCount => EnemySquad.Instance != null ? EnemySquad.Instance.AliveCount : 0;

    public event Action<int> OnCountChanged;
    public event Action OnAllDead;

    void OnEnable()
    {
        CombatStimulus.OnEnemyDied += HandleEnemyDied;
        Refresh(force: true);
    }

    void OnDisable()
    {
        CombatStimulus.OnEnemyDied -= HandleEnemyDied;
    }

    void Update()
    {
        Refresh(force: false);
    }

    void HandleEnemyDied(EnemyAI _)
    {
        Refresh(force: false);
    }

    void Refresh(bool force)
    {
        int count = AliveCount;
        if (!force && count == lastCount)
            return;

        lastCount = count;
        OnCountChanged?.Invoke(count);

        if (logCount)
            Debug.Log($"EnemyDetector: {count} alive");

        if (count == 0)
        {
            if (!allDeadFired)
            {
                allDeadFired = true;
                OnAllDead?.Invoke();
            }
        }
        else
        {
            allDeadFired = false;
        }
    }
}
