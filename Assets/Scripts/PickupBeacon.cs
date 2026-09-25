using UnityEngine;

/// <summary>
/// Soft white point light on world pickups so they read in a dark room.
/// </summary>
public sealed class PickupBeacon : MonoBehaviour
{
    Light glow;
    float baseIntensity;
    float phase;

    public static void Attach(Transform pickup)
    {
        if (pickup == null)
            return;
        if (pickup.GetComponentInChildren<PickupBeacon>(true) != null)
            return;

        GameObject root = new GameObject("PickupGlow");
        root.transform.SetParent(pickup, false);
        root.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        root.AddComponent<PickupBeacon>().Build();
    }

    void Build()
    {
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = Color.white;
        glow.intensity = 6f;
        glow.range = 3.4f;
        glow.shadows = LightShadows.None;
        baseIntensity = glow.intensity;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (glow == null)
            return;

        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 2.6f + phase);
        glow.intensity = baseIntensity * pulse;
    }
}
