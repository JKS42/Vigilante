using UnityEngine;

/// <summary>
/// Flashes on hit. Capsule enemies also shift tint with health;
/// textured / skinned meshes keep authored colours and only flash.
/// </summary>
public class EnemyHurtTint : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Health health;
    EnemyProfile profile;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    float flash;
    bool dead;
    bool useProfileTint;
    static readonly Color DeadTint = new Color(0.25f, 0.25f, 0.25f);

    Color BaseTint => profile != null ? profile.tint : new Color(0.75f, 0.2f, 0.2f);

    void Awake()
    {
        health = GetComponent<Health>();
        profile = GetComponent<EnemyProfile>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        useProfileTint = GetComponentInChildren<SkinnedMeshRenderer>(true) == null;
        ApplyColor(0f);
    }

    void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (profile == null)
            profile = GetComponent<EnemyProfile>();
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
        useProfileTint = GetComponentInChildren<SkinnedMeshRenderer>(true) == null;

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }

        ApplyColor(0f);
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    void Update()
    {
        if (dead || flash <= 0f)
            return;

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / 0.15f);
        ApplyColor(flash);
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        if (dead)
            return;

        flash = 1f;
        ApplyColor(flash);
    }

    void HandleDied()
    {
        dead = true;
        flash = 0f;
        ApplyColor(0f);
    }

    void ApplyColor(float flashAmount)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!useProfileTint)
            {
                ApplyTexturedOverlay(r, flashAmount);
                continue;
            }

            Color color;
            if (dead)
            {
                color = DeadTint;
            }
            else
            {
                float hp = 1f;
                if (health != null)
                    hp = Mathf.Clamp01(health.CurrentHealth / Mathf.Max(1f, health.MaxHealth));

                Color wounded = Color.Lerp(BaseTint, new Color(1f, 0.72f, 0.68f), 1f - hp);
                color = Color.Lerp(wounded, Color.white, flashAmount * 0.85f);
            }

            r.GetPropertyBlock(block);
            Material shared = r.sharedMaterial;
            if (shared != null && shared.HasProperty(BaseColorId))
                block.SetColor(BaseColorId, color);
            if (shared != null && (shared.HasProperty(ColorId) || shared.HasProperty("_Color")))
                block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }
    }

    void ApplyTexturedOverlay(Renderer r, float flashAmount)
    {
        if (!dead && flashAmount <= 0.01f)
        {
            r.SetPropertyBlock(null);
            return;
        }

        Material mat = r.sharedMaterial;
        if (mat == null)
            return;

        r.GetPropertyBlock(block);
        if (mat.HasProperty(BaseColorId))
        {
            Color baseCol = mat.GetColor(BaseColorId);
            block.SetColor(BaseColorId, dead ? DeadTint : Color.Lerp(baseCol, Color.white, flashAmount * 0.75f));
        }
        if (mat.HasProperty(ColorId) || mat.HasProperty("_Color"))
        {
            Color baseCol = mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : mat.color;
            block.SetColor(ColorId, dead ? DeadTint : Color.Lerp(baseCol, Color.white, flashAmount * 0.75f));
        }
        r.SetPropertyBlock(block);
    }
}
