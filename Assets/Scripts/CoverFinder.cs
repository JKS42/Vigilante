using UnityEngine;
using UnityEngine.AI;

public static class CoverFinder
{
    public static CoverPoint FindBestCover(
        Vector3 fromPos,
        Vector3 threatPos,
        float searchRadius,
        EnemyAI requester,
        CoverPoint current = null)
    {
        CoverPoint[] points = Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None);
        CoverPoint best = null;
        float bestScore = float.MinValue;

        foreach (CoverPoint point in points)
        {
            if (point == null)
                continue;

            if (point.IsOccupied && point.OccupiedBy != requester)
                continue;

            float dist = Vector3.Distance(fromPos, point.transform.position);
            if (dist > searchRadius)
                continue;

            if (!point.IsUsefulAgainst(threatPos, fromPos))
                continue;

            // Closer cover scores higher when under pressure; slight preference for unused.
            float score = (searchRadius - dist) + (point == current ? 5f : 0f);
            score += Random.Range(0f, 2f);

            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    /// <summary>
    /// Fallback when no CoverPoint markers exist: sample NavMesh for a spot
    /// that blocks LOS from the threat.
    /// </summary>
    public static bool FindDynamicCover(
        Vector3 fromPos,
        Vector3 threatPos,
        float searchRadius,
        out Vector3 coverPos)
    {
        coverPos = fromPos;
        const int samples = 16;
        float bestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < samples; i++)
        {
            float angle = (360f / samples) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(searchRadius * 0.35f, searchRadius);
            Vector3 candidate = fromPos + offset;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                continue;

            Vector3 sample = navHit.position;
            Vector3 threatEye = threatPos + Vector3.up * 1.5f;
            Vector3 coverEye = sample + Vector3.up * 1.0f;
            Vector3 dir = coverEye - threatEye;
            float dist = dir.magnitude;

            if (!Physics.Raycast(threatEye, dir.normalized, out RaycastHit hit, dist))
                continue;

            // Blocked before reaching cover eye → usable.
            if (hit.distance >= dist - 0.4f)
                continue;

            float d = Vector3.Distance(fromPos, sample);
            if (d < bestDist)
            {
                bestDist = d;
                coverPos = sample;
                found = true;
            }
        }

        return found;
    }

    public static bool FindFlankPosition(
        Vector3 selfPos,
        Vector3 threatPos,
        float flankDistance,
        bool preferLeft,
        out Vector3 flankPos)
    {
        flankPos = selfPos;
        Vector3 toThreat = threatPos - selfPos;
        toThreat.y = 0f;
        if (toThreat.sqrMagnitude < 0.01f)
            return false;

        Vector3 forward = toThreat.normalized;
        Vector3 side = (preferLeft ? -1f : 1f) * Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 candidate = threatPos + side * flankDistance + forward * -2f;

        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, flankDistance * 0.75f, NavMesh.AllAreas))
        {
            // Try opposite side.
            candidate = threatPos - side * flankDistance + forward * -2f;
            if (!NavMesh.SamplePosition(candidate, out hit, flankDistance * 0.75f, NavMesh.AllAreas))
                return false;
        }

        flankPos = hit.position;
        return true;
    }
}
