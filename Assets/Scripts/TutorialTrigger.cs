using UnityEngine;

/// <summary>
/// Place on a trigger collider. When the player enters, fires a tutorial event
/// so TutorialPrompt can show the matching tip (showOnEvent).
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialTrigger : MonoBehaviour
{
    [Tooltip("Must match a TutorialPrompt tip's showOnEvent id.")]
    public string eventId = "near_breakable";

    [Tooltip("If true, only fires once.")]
    public bool once = true;

    bool fired;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired && once)
            return;
        if (!IsPlayer(other))
            return;

        fired = true;
        TutorialPrompt.Notify(eventId);

        if (once)
            enabled = false;
    }

    static bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;
        return other.CompareTag("Player")
               || other.transform.root.CompareTag("Player")
               || other.GetComponentInParent<PlayerMovement>() != null;
    }

    /// <summary>
    /// Runtime helper for Level 1 bootstrap.
    /// </summary>
    public static TutorialTrigger Create(Vector3 position, Vector3 size, string eventId)
    {
        GameObject go = new GameObject("TutorialTrigger_" + eventId);
        go.transform.position = position;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        TutorialTrigger trigger = go.AddComponent<TutorialTrigger>();
        trigger.eventId = eventId;
        trigger.once = true;
        return trigger;
    }
}
