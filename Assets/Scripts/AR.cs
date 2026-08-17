using UnityEngine;

public class AR : Weapon
{
    [Header("Damage")]
    public float Damage = 20f;

    [Header("Projectile")]
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public float bulletSpeed = 55f;
    public float bulletScale = 0.1f;
    public float shotNoiseRadius = 40f;

    [Header("Optional FX")]
    public ParticleSystem muzzleFlash;
    public AudioClip shotSound;

    Transform cam;
    AudioSource audioSource;

    protected override void Awake()
    {
        Auto = true;
        if (FireCooldown <= 0.01f)
            FireCooldown = 0.1f;
        if (magazineSize <= 0)
            magazineSize = 30;
        if (idleSpread <= 0f)
            idleSpread = 0.3f;
        if (movingSpread <= 0f)
            movingSpread = 2f;
        if (sprintSpread <= 0f)
            sprintSpread = 3.5f;
        if (fireBloomPerShot <= 0f)
            fireBloomPerShot = 0.45f;
        if (maxSpread <= 0f)
            maxSpread = 6.5f;
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

        Vector3 lookDir = cam != null ? cam.forward : transform.forward;
        Vector3 aimDir = GetSpreadAim(lookDir);
        Vector3 spawnPos = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : (cam != null ? cam.position + cam.forward * 0.5f : transform.position);

        Bullet.Spawn(bulletPrefab, spawnPos, aimDir, bulletSpeed, Damage, transform.root.gameObject, bulletScale);

        if (muzzleFlash != null)
        {
            muzzleFlash.transform.position = spawnPos;
            muzzleFlash.Play();
        }

        if (shotSound != null && audioSource != null)
            audioSource.PlayOneShot(shotSound);
        else
            AudioManager.EnemyGunshot(spawnPos, EnemyWeaponKind.Rifle);

        if (muzzleFlash == null)
            CombatVfx.SpawnMuzzleFlash(spawnPos, lookDir);

        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
