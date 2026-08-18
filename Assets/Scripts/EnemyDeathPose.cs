using UnityEngine;

/// <summary>
/// Falls the enemy onto its back and sinks it so a kill is readable before despawn.
/// Stays enabled after EnemyAI is disabled.
/// </summary>
public class EnemyDeathPose : MonoBehaviour
{
    [SerializeField] float fallDuration = 0.25f;
    [SerializeField] float sinkSpeed = 0.7f;

    Quaternion startRot;
    Quaternion endRot;
    Vector3 startPos;
    float age;
    bool playing;

    public void Play()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }

        startRot = transform.rotation;
        Vector3 axis = transform.right;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f)
            axis = Vector3.right;
        endRot = Quaternion.AngleAxis(90f, axis.normalized) * startRot;
        startPos = transform.position;
        age = 0f;
        playing = true;
        enabled = true;
    }

    void Update()
    {
        if (!playing)
            return;

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(0.01f, fallDuration));
        t = t * t * (3f - 2f * t);
        transform.rotation = Quaternion.Slerp(startRot, endRot, t);

        if (t >= 1f)
            transform.position = startPos + Vector3.down * sinkSpeed * (age - fallDuration);
    }
}
