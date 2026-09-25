using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Damages the player only while this bat overlaps them during the swing.
/// The overlap is sampled along the bat's movement so a fast swing cannot skip them.
/// </summary>
public class EnemyBatHit : MonoBehaviour
{
    const int SweepSteps = 4;

    readonly HashSet<Health> struck = new HashSet<Health>();
    readonly HashSet<Collider> touched = new HashSet<Collider>();
    readonly Collider[] hits = new Collider[24];
    BoxCollider box;
    EnemyCombat owner;
    bool wasSwinging;
    bool hasPose;
    Vector3 lastCenter;
    Quaternion lastRotation;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
    }

    void LateUpdate()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();
        if (owner == null)
            owner = GetComponentInParent<EnemyCombat>();
        if (owner == null || box == null)
            return;

        Vector3 scale = transform.lossyScale;
        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);
        Vector3 center = transform.TransformPoint(box.center);
        Vector3 half = Vector3.Scale(box.size * 0.5f, scale) + Vector3.one * 0.05f;
        Quaternion rotation = transform.rotation;

        bool swinging = owner.IsMeleeSwinging;
        if (swinging && !wasSwinging)
        {
            struck.Clear();
            touched.Clear();
        }
        wasSwinging = swinging;

        if (swinging)
        {
            if (!hasPose)
                Probe(center, half, rotation);
            else
            {
                for (int step = 1; step <= SweepSteps; step++)
                {
                    float t = step / (float)SweepSteps;
                    Probe(Vector3.Lerp(lastCenter, center, t), half, Quaternion.Slerp(lastRotation, rotation, t));
                }
            }
        }

        lastCenter = center;
        lastRotation = rotation;
        hasPose = true;
    }

    void Probe(Vector3 center, Vector3 half, Quaternion rotation)
    {
        int count = Physics.OverlapBoxNonAlloc(center, half, hits, rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i];
            if (col == null || !touched.Add(col))
                continue;

            Health health = col.GetComponentInParent<Health>();
            if (health != null)
            {
                if (!struck.Add(health))
                    continue;
                if (!owner.NotifyBatContact(col))
                    struck.Remove(health);
                continue;
            }

            owner.NotifyBatContact(col);
        }
    }
}
