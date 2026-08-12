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
        Vector3 aimDir = cam != null ? cam.forward : transform.forward;
        Vector3 spawnPos = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : (cam != null ? cam.position + cam.forward * 0.5f : transform.position);

        Bullet.Spawn(bulletPrefab, spawnPos, aimDir, bulletSpeed, Damage, transform.root.gameObject, bulletScale);

        if (shotSound != null && audioSource != null)
            audioSource.PlayOneShot(shotSound);
        else
            AudioManager.EnemyGunshot(spawnPos, EnemyWeaponKind.Pistol);

        CombatVfx.SpawnMuzzleFlash(spawnPos, aimDir);
        CombatVfx.SpawnOnomatopoeia(spawnPos + aimDir * 0.6f, "BLAM!");
        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
