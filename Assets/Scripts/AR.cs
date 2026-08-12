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
            CombatVfx.SpawnMuzzleFlash(spawnPos, aimDir);

        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
