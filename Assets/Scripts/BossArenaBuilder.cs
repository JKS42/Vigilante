using UnityEngine;

/// <summary>
/// Builds a multi-tier Uncharted-style boss arena around the player at runtime:
/// lower floor, balcony ring, destructible pillar cover, and choke points.
/// </summary>
public static class BossArenaBuilder
{
    public static void BuildAroundPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;
        center.y = 0f;

        GameObject root = new GameObject("BossArena");
        root.transform.position = center;

        // Main circular floor.
        CreateRingFloor(root.transform, center, 22f, 0f, new Color(0.18f, 0.18f, 0.2f));
        // Raised balcony ring (Uncharted verticality).
        CreateRingFloor(root.transform, center, 14f, 3.2f, new Color(0.22f, 0.2f, 0.18f), hole: 8f);
        // Inner kill-floor.
        CreateDisc(root.transform, center + Vector3.up * 0.02f, 7f, new Color(0.3f, 0.12f, 0.1f));

        // Pillars / cover columns — breakable.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 9f;
            CreateBreakablePillar(root.transform, pos, 2.8f);
            EnemyFactory.CreateCoverPoint(pos + (center - pos).normalized * 1.2f, center - pos);
        }

        // Balcony ramp stubs (visual + walkable boxes).
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 1.5f, Mathf.Sin(angle)) * 11f;
            CreateRamp(root.transform, pos, Quaternion.Euler(0f, i * 90f + 45f, -22f));
        }

        // Outer walls with breakable sections.
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 1.5f, Mathf.Sin(angle)) * 21f;
            CreateBreakableWallSegment(root.transform, pos, Quaternion.LookRotation(center - pos));
        }

        LevelCombatBootstrap.RebuildPlayableNavMesh();
        DialogueManager.Announcer("BOSS ARENA");
        CombatVfx.SpawnOnomatopoeia(center + Vector3.up * 3f, "FIGHT!");
    }

    static void CreateDisc(Transform parent, Vector3 pos, float radius, Color color)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "ArenaDisc";
        disc.transform.SetParent(parent, true);
        disc.transform.position = pos;
        disc.transform.localScale = new Vector3(radius * 2f, 0.08f, radius * 2f);
        ApplyColor(disc, color);
    }

    static void CreateRingFloor(Transform parent, Vector3 center, float radius, float height, Color color, float hole = 0f)
    {
        // Approximate ring with boxes.
        int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            float r = (radius + hole) * 0.5f;
            if (hole > 0f)
                r = (radius + hole) * 0.5f;
            else
                r = radius * 0.55f;

            Vector3 pos = center + new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)) * r;
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "ArenaRingPad";
            pad.transform.SetParent(parent, true);
            pad.transform.position = pos;
            float depth = hole > 0f ? (radius - hole) : radius;
            pad.transform.localScale = new Vector3(radius * 0.55f, 0.2f, Mathf.Max(2f, depth * 0.35f));
            pad.transform.rotation = Quaternion.LookRotation(center - new Vector3(pos.x, center.y, pos.z));
            ApplyColor(pad, color);
        }
    }

    static void CreateBreakablePillar(Transform parent, Vector3 pos, float height)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = "ArenaPillar";
        pillar.tag = "Breakable";
        pillar.transform.SetParent(parent, true);
        pillar.transform.position = pos + Vector3.up * (height * 0.5f);
        pillar.transform.localScale = new Vector3(1.1f, height, 1.1f);
        Rigidbody rb = pillar.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        Break br = pillar.AddComponent<Break>();
        br.isWallTile = true;
        br.debrisCount = 7;
        br.debrisDamage = 22f;
        ApplyColor(pillar, new Color(0.55f, 0.48f, 0.35f));
    }

    static void CreateBreakableWallSegment(Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "ArenaWallBreakable";
        wall.tag = "Breakable";
        wall.transform.SetParent(parent, true);
        wall.transform.SetPositionAndRotation(pos, rot);
        wall.transform.localScale = new Vector3(4.5f, 3.2f, 0.45f);
        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        Break br = wall.AddComponent<Break>();
        br.isWallTile = true;
        br.debrisCount = 8;
        ApplyColor(wall, new Color(0.7f, 0.6f, 0.45f));
    }

    static void CreateRamp(Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "ArenaRamp";
        ramp.transform.SetParent(parent, true);
        ramp.transform.SetPositionAndRotation(pos, rot);
        ramp.transform.localScale = new Vector3(3f, 0.25f, 6f);
        ApplyColor(ramp, new Color(0.28f, 0.26f, 0.24f));
    }

    static void ApplyColor(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        r.sharedMaterial = mat;
    }
}
