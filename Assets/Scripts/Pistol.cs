using UnityEngine;
using UnityEngine.Serialization;

public class Pistol : Weapon
{
    [Header("Damage")]
    public float Damage = 25f;

    [Header("Projectile")]
    [FormerlySerializedAs("Bullet")]
    public GameObject bulletPrefab;
    [FormerlySerializedAs("BulletSpawnPoint")]
    public Transform bulletSpawnPoint;
    public float bulletSpeed = 40f;
    public float bulletScale = 0.1f;
    public float shotNoiseRadius = 35f;

    [Header("Optional FX")]
    public AudioClip shotSound;

    Transform cam;
    AudioSource audioSource;

    protected override void Awake()
    {
        if (magazineSize <= 0)
            magazineSize = 12;
        if (startingReserve <= 0)
            startingReserve = 36;
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
        Vector3 lookDir = cam != null ? cam.forward : transform.forward;
        Vector3 aimDir = GetSpreadAim(lookDir);
        Vector3 spawnPos = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : (cam != null ? cam.position + cam.forward * 0.5f : transform.position);

        Bullet.Spawn(bulletPrefab, spawnPos, aimDir, bulletSpeed, Damage, transform.root.gameObject, bulletScale);

        if (shotSound != null && audioSource != null)
            audioSource.PlayOneShot(shotSound);
        else
            AudioManager.EnemyGunshot(spawnPos, EnemyWeaponKind.Pistol);

        CombatVfx.SpawnMuzzleFlash(spawnPos, lookDir);
        CombatVfx.SpawnOnomatopoeia(spawnPos + lookDir * 0.6f, "BLAM!");
        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
