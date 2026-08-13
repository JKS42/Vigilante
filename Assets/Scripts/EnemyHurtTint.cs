using UnityEngine;

/// <summary>
/// Flashes white on hit, then settles to a lighter tint as health drops.
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

    Color BaseTint => profile != null ? profile.tint : new Color(0.75f, 0.2f, 0.2f);

    void Awake()
    {
        health = GetComponent<Health>();
        profile = GetComponent<EnemyProfile>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
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

        if (health != null)
            health.OnDamaged += HandleDamaged;

        ApplyColor(0f);
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
    }

    void Update()
    {
        if (flash <= 0f)
            return;

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / 0.15f);
        ApplyColor(flash);
    }

    void HandleDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        flash = 1f;
        ApplyColor(flash);
    }

    void ApplyColor(float flashAmount)
    {
        if (renderers == null)
            return;

        float hp = 1f;
        if (health != null)
            hp = Mathf.Clamp01(health.CurrentHealth / Mathf.Max(1f, health.MaxHealth));

        Color wounded = Color.Lerp(BaseTint, new Color(1f, 0.72f, 0.68f), 1f - hp);
        Color color = Color.Lerp(wounded, Color.white, flashAmount * 0.85f);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(block);
            Material mat = r.sharedMaterial;
            if (mat != null && mat.HasProperty(BaseColorId))
                block.SetColor(BaseColorId, color);
            if (mat != null && (mat.HasProperty(ColorId) || mat.HasProperty("_Color")))
                block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }
    }
}
