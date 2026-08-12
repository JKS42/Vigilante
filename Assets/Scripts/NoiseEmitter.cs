using UnityEngine;

public class NoiseEmitter : MonoBehaviour
{
    [SerializeField] float defaultRadius = 20f;
    [SerializeField] StimulusType defaultType = StimulusType.Gunfire;

    public static void Emit(Vector3 position, float radius, StimulusType type)
    {
        CombatStimulus.EmitNoise(position, radius, type);
    }

    public void EmitFromSelf()
    {
        Emit(transform.position, defaultRadius, defaultType);
    }

    public void EmitFromSelf(float radius, StimulusType type)
    {
        Emit(transform.position, radius, type);
    }
}
