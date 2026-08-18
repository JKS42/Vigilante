using UnityEngine;
using TMPro;

/// <summary>
/// Comic-book style onomatopoeia + simple impact / muzzle / explosion flashes.
/// Creates runtime primitives/TextMeshPro so no VFX assets are required.
/// </summary>
public static class CombatVfx
{
    static Transform root;

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

    public static void SpawnOnomatopoeia(Vector3 position, string text)
    {
        SpawnOnomatopoeia(position, text, 6f, new Color(1f, 0.85f, 0.2f), 1.1f, 1.6f);
    }

    public static void SpawnDeathKo(Vector3 position)
    {
        SpawnOnomatopoeia(position, "KO!", 10f, new Color(0.95f, 0.15f, 0.12f), 1.15f, 2.2f);
    }

    public static void SpawnOnomatopoeia(Vector3 position, string text, float fontSize, Color color, float lifetime, float riseSpeed)
    {
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

    public static void SpawnImpact(Vector3 position, Vector3 normal)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ImpactFlash";
        go.transform.SetParent(Root, false);
        go.transform.position = position + normal.normalized * 0.05f;
        go.transform.localScale = Vector3.one * 0.18f;

        Object.Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(1f, 0.7f, 0.2f, 1f);
            r.sharedMaterial = mat;
        }

        go.AddComponent<FlashVfx>().Init(0.18f, 0.45f);
    }

    public static void SpawnMuzzleFlash(Vector3 position, Vector3 forward)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MuzzleFlash";
        go.transform.SetParent(Root, false);
        go.transform.position = position + forward * 0.1f;
        go.transform.localScale = new Vector3(0.12f, 0.12f, 0.28f);
        go.transform.rotation = Quaternion.LookRotation(forward);

        Object.Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(1f, 0.9f, 0.4f);
            r.sharedMaterial = mat;
        }

        go.AddComponent<FlashVfx>().Init(0.08f, 0.7f);
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
