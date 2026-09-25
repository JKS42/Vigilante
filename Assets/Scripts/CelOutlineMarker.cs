using UnityEngine;

/// <summary>
/// Marks a renderer that already received a CelOutline material slot.
/// Kept as a top-level type so prefab / scene refs stay stable.
/// </summary>
public sealed class CelOutlineMarker : MonoBehaviour
{
    public Color outlineColor = new Color(0.02f, 0.02f, 0.05f, 1f);
}
