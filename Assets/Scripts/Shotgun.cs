using UnityEngine;

public class Shotgun : Weapon
{
    [Header("Damage")]
    public float DamagePerPellet = 10f;

    [Header("Projectile")]
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public float bulletSpeed = 45f;
    public float bulletScale = 0.08f;
    public float shotNoiseRadius = 45f;

    [Header("Spread")]
    public int pelletCount = 8;
    public float spreadAngle = 6f;

    [Header("Optional FX")]
    public AudioClip shotSound;

    Transform cam;
    AudioSource audioSource;

    protected override void Awake()
    {
        Auto = false;
        if (FireCooldown <= 0.01f)
            FireCooldown = 0.9f;
        if (magazineSize <= 0)
            magazineSize = 6;
        if (idleSpread <= 0f)
            idleSpread = spreadAngle;
        if (movingSpread <= 0f)
            movingSpread = spreadAngle + 1.2f;
        if (sprintSpread <= 0f)
            sprintSpread = spreadAngle + 2.2f;
        if (fireBloomPerShot <= 0f)
            fireBloomPerShot = 0.5f;
        if (maxSpread <= 0f)
            maxSpread = spreadAngle + 4f;
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        cam = Camera.main != null ? Camera.main.transform : null;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && shotSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    protected override void FireShot()
    {
        if (cam == null)
            cam = Camera.main != null ? Camera.main.transform : null;

        Vector3 aimDir = cam != null ? cam.forward : transform.forward;
        Vector3 spawnPos = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : (cam != null ? cam.position + cam.forward * 0.5f : transform.position);

        GameObject instigator = transform.root.gameObject;
        int count = Mathf.Max(1, pelletCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 pelletDir = GetSpreadAim(aimDir);
            Bullet.Spawn(bulletPrefab, spawnPos, pelletDir, bulletSpeed, DamagePerPellet, instigator, bulletScale);
        }

        if (shotSound != null && audioSource != null)
            audioSource.PlayOneShot(shotSound);
        else
            AudioManager.EnemyGunshot(spawnPos, EnemyWeaponKind.Shotgun);

        CombatVfx.SpawnMuzzleFlash(spawnPos, aimDir);
        CombatVfx.SpawnOnomatopoeia(spawnPos + aimDir * 0.5f, "BOOM!");
        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
