using CartoonFX;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Comic-book style onomatopoeia + impact / muzzle / wall-break effects.
/// </summary>
public static class CombatVfx
{
    static Transform root;
    static CombatVfxLibrary library;

    static Transform Root
    {
        get
        {
            if (root == null)
            {
                GameObject go = GameObject.Find("CombatVfxRoot");
                if (go == null)
                    go = new GameObject("CombatVfxRoot");
                root = go.transform;
            }
            return root;
        }
    }

    static CombatVfxLibrary Library
    {
        get
        {
            if (library == null)
                library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            return library;
        }
    }

    public static void SpawnOnomatopoeia(Vector3 position, string text)
    {
        SpawnOnomatopoeia(position, text, 6f, new Color(1f, 0.85f, 0.2f), 1.1f, 1.6f);
    }

    public static void SpawnDeathKo(Vector3 position)
    {
        SpawnOnomatopoeia(position, "KO!", 10f, new Color(0.95f, 0.15f, 0.12f), 1.15f, 2.2f);
    }

    public static void SpawnHeadshot(Vector3 position)
    {
        SpawnOnomatopoeia(position + Vector3.up * 0.25f, "HEADSHOT!", 8.5f, new Color(1f, 0.32f, 0.12f), 1.25f, 2.1f);
    }

    public static void SpawnOnomatopoeia(Vector3 position, string text, float fontSize, Color color, float lifetime, float riseSpeed)
    {
        if (TrySpawnThemedText(position, text))
            return;
        GameObject go = new GameObject("SFXText_" + text);
        go.transform.SetParent(Root, false);
        go.transform.position = position + Random.insideUnitSphere * 0.15f;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = fontSize > 7f ? new Vector2(6f, 2.2f) : new Vector2(4f, 1.5f);

        go.AddComponent<BillboardVfx>().Init(lifetime, riseSpeed);
    }

    static bool TrySpawnThemedText(Vector3 position, string text)
    {
        if (Library == null || string.IsNullOrEmpty(text))
            return false;

        string key = text.Trim().ToUpperInvariant();
        GameObject prefab = Library.boomText;
        bool tintRed = false;
        switch (key)
        {
            case "KO":
            case "KO!":
                prefab = Library.boingText;
                tintRed = true;
                break;
            case "HEAL":
            case "HEAL!":
                prefab = Library.cursedText;
                break;
        }

        if (prefab == null)
            return false;

        GameObject go = SpawnPrefabInstance(prefab, position, BillboardForward());
        if (go == null)
            return false;

        ApplyWord(go, text, tintRed);
        go.transform.localScale = Vector3.one * 0.24f;
        return true;
    }

    static void ApplyWord(GameObject go, string text, bool tintRed)
    {
#if UNITY_EDITOR
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
#endif
        CFXR_ParticleText particleText = go.GetComponent<CFXR_ParticleText>();
        if (particleText == null)
            return;

        if (tintRed)
        {
            particleText.UpdateText(
                text,
                null,
                new Color(1f, 0.12f, 0.08f, 1f),
                new Color(0.55f, 0.02f, 0.02f, 1f),
                new Color(0.22f, 0.02f, 0.02f, 1f));
        }
        else
        {
            particleText.UpdateText(text);
        }

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null || !ps.gameObject.activeInHierarchy)
                continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    static GameObject SpawnPrefabInstance(GameObject prefab, Vector3 position, Vector3 forward)
    {
        if (prefab == null)
            return null;

        GameObject go = Object.Instantiate(prefab, position, Facing(forward), Root);
        CFXR_Effect[] effects = go.GetComponentsInChildren<CFXR_Effect>(true);
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null && effects[i].cameraShake != null)
                effects[i].cameraShake.enabled = false;
        }

        return go;
    }

    static Vector3 BillboardForward()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector3.forward;
        return cam.transform.forward;
    }

    public static void SpawnImpact(Vector3 position, Vector3 normal)
    {
        SpawnFallbackFlash(position, normal, "ImpactFlash", new Color(1f, 0.7f, 0.2f, 1f), 0.18f, 0.45f, 0.18f);
    }

    public static void PlayBulletHit(Vector3 position, Vector3 normal)
    {
        if (!SpawnPrefab(Library != null ? Library.bulletHit : null, position, normal))
            SpawnImpact(position, normal);
    }

    public static void PlaySpawnBurst(Vector3 position)
    {
        SpawnPrefab(Library != null ? Library.spawnBurst : null, position + Vector3.up * 0.9f, Vector3.up);
    }

    public static void PlayWallSmoke(Vector3 position, Vector3 normal)
    {
        if (!SpawnPrefab(Library != null ? Library.wallSmoke : null, position, normal))
            SpawnFallbackFlash(position, Vector3.up, "WallSmoke", new Color(0.75f, 0.75f, 0.75f, 1f), 0.4f, 1.2f, 0.5f);
    }

    public static void SpawnMuzzleFlash(Vector3 position, Vector3 forward, float scale = 1f, Transform follow = null)
    {
        GameObject prefab = Library != null ? Library.muzzleFire : null;
        GameObject go = prefab != null ? SpawnPrefabInstance(prefab, position, forward) : null;
        if (go == null)
        {
            float size = Mathf.Max(0.02f, 0.12f * scale);
            SpawnFallbackFlash(position + forward * 0.1f, forward, "MuzzleFlash", new Color(1f, 0.9f, 0.4f), 0.08f, size * 5f, size);
            return;
        }

        if (follow != null)
            go.transform.SetParent(follow, true);

        ApplyWorldScale(go.transform, Mathf.Max(0.05f, scale));

        if (follow != null)
            StickParticlesToParent(go);
    }

    static void ApplyWorldScale(Transform target, float worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = Vector3.one * worldScale;
            return;
        }

        Vector3 lossy = parent.lossyScale;
        target.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
    }

    static void StickParticlesToParent(GameObject go)
    {
        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null || !ps.gameObject.activeInHierarchy)
                continue;

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    static bool SpawnPrefab(GameObject prefab, Vector3 position, Vector3 forward)
    {
        if (prefab == null)
            return false;

        GameObject go = SpawnPrefabInstance(prefab, position, forward);
        return go != null;
    }

    static Quaternion Facing(Vector3 forward)
    {
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(forward, up);
    }

    static void SpawnFallbackFlash(Vector3 position, Vector3 normal, string name, Color color, float life, float endScale, float startScale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(Root, false);
        go.transform.position = position + (normal.sqrMagnitude > 0.0001f ? normal.normalized * 0.05f : Vector3.zero);
        go.transform.localScale = Vector3.one * startScale;
        if (normal.sqrMagnitude > 0.0001f)
            go.transform.rotation = Facing(normal);

        Object.Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            r.sharedMaterial = mat;
        }

        go.AddComponent<FlashVfx>().Init(life, endScale);
    }

    public static void SpawnExplosion(Vector3 position, float radius)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ExplosionFlash";
        go.transform.SetParent(Root, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.4f;

        Object.Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(1f, 0.45f, 0.1f);
            r.sharedMaterial = mat;
        }

        go.AddComponent<FlashVfx>().Init(0.35f, radius * 1.6f);
        SpawnOnomatopoeia(position + Vector3.up, "BOOM!");
    }
}

public class BillboardVfx : MonoBehaviour
{
    float life;
    float rise;
    float age;
    TextMeshPro tmp;

    public void Init(float lifetime, float riseSpeed)
    {
        life = lifetime;
        rise = riseSpeed;
        tmp = GetComponent<TextMeshPro>();
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * rise * Time.deltaTime;

        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (tmp != null)
        {
            Color c = tmp.color;
            c.a = 1f - Mathf.Clamp01(age / life);
            tmp.color = c;
        }

        if (age >= life)
            Destroy(gameObject);
    }
}

public class FlashVfx : MonoBehaviour
{
    float life;
    float targetScale;
    float age;
    Vector3 startScale;

    public void Init(float lifetime, float endScale)
    {
        life = lifetime;
        targetScale = endScale;
        startScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.localScale = Vector3.Lerp(startScale, Vector3.one * targetScale, t);
        if (age >= life)
            Destroy(gameObject);
    }
}
