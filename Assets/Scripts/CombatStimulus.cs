using System;
using UnityEngine;

public enum StimulusType
{
    Gunfire,
    Footstep,
    Breach,
    Impact
}

public static class CombatStimulus
{
    public static event Action<Vector3, float, StimulusType> OnNoise;
    public static event Action<Vector3> OnBreach;
    public static event Action<EnemyAI> OnEnemyDied;

    public static void EmitNoise(Vector3 position, float radius, StimulusType type)
    {
        OnNoise?.Invoke(position, radius, type);
    }

    public static void EmitBreach(Vector3 position)
    {
        OnBreach?.Invoke(position);
        EmitNoise(position, 25f, StimulusType.Breach);
    }

    public static void NotifyEnemyDied(EnemyAI enemy)
    {
        OnEnemyDied?.Invoke(enemy);
    }
}
