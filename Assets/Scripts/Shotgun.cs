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

    Transform cam;

    protected override void Awake()
    {
        Auto = false;
        if (FireCooldown <= 0.01f)
            FireCooldown = 0.9f;
        if (magazineSize <= 0)
            magazineSize = 6;
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        cam = Camera.main != null ? Camera.main.transform : null;
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
            Vector3 pelletDir = ApplySpread(aimDir, spreadAngle);
            Bullet.Spawn(bulletPrefab, spawnPos, pelletDir, bulletSpeed, DamagePerPellet, instigator, bulletScale);
        }

        NoiseEmitter.Emit(spawnPos, shotNoiseRadius, StimulusType.Gunfire);
    }

    static Vector3 ApplySpread(Vector3 direction, float angleDegrees)
    {
        if (angleDegrees <= 0f)
            return direction.normalized;

        // Spread relative to aim direction, not world axes.
        Quaternion aim = Quaternion.LookRotation(direction.normalized);
        Vector3 right = aim * Vector3.right;
        Vector3 up = aim * Vector3.up;
        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-angleDegrees, angleDegrees), up);
        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-angleDegrees, angleDegrees), right);
        return (yaw * pitch * direction.normalized).normalized;
    }
}
