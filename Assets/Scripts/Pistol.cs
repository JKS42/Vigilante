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

    Transform cam;

    protected override void Start()
    {
        base.Start();
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    protected override void FireShot()
    {
        Vector3 aimDir = cam != null ? cam.forward : transform.forward;
        Vector3 spawnPos = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : (cam != null ? cam.position + cam.forward * 0.5f : transform.position);

        Bullet.Spawn(bulletPrefab, spawnPos, aimDir, bulletSpeed, Damage, transform.root.gameObject, bulletScale);
        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }
}
