using UnityEngine;

/// <summary>
/// Irregular intensity drop on a point light, like a damaged bulb.
/// Each instance is given its own rates so two lights never pulse together.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamagedLightFlicker : MonoBehaviour
{
    Light target;
    float baseIntensity;
    float buzzRate;
    float dipRate;
    float cutInterval;
    float phase;
    float seed;

    public void Configure(int uniqueIndex)
    {
        target = GetComponent<Light>();
        if (target == null)
            return;

        // Damaged bulbs read darker than the steady lights around them.
        baseIntensity = target.intensity * 0.12f;
        target.intensity = baseIntensity;

        // Spaced so no two lights land on the same Hz, even by coincidence.
        buzzRate = 3.1f + uniqueIndex * 1.73f;
        dipRate = 0.27f + uniqueIndex * 0.19f;
        cutInterval = 0.38f + uniqueIndex * 0.091f;
        phase = uniqueIndex * 1.618034f;
        seed = 7.4f + uniqueIndex * 13.1f;
    }

    void Update()
    {
        if (target == null)
            return;

        float t = Time.time + phase;

        float buzz = Mathf.PerlinNoise(seed, t * buzzRate);
        float held = Mathf.Lerp(0.62f, 1f, buzz);

        float dip = Mathf.PerlinNoise(seed + 9.2f, t * dipRate);
        if (dip < 0.22f)
            held *= Mathf.Lerp(0.04f, 0.4f, dip / 0.22f);

        float window = Mathf.Repeat(t, cutInterval);
        float cutWidth = 0.03f + (uniqueOffset() * 0.04f);
        if (window < cutWidth)
            held *= 0.08f;

        target.intensity = baseIntensity * held;
    }

    float uniqueOffset()
    {
        return Mathf.Repeat(phase * 0.31f, 1f);
    }

    void OnDisable()
    {
        if (target != null)
            target.intensity = baseIntensity;
    }
}
