using UnityEngine;

/// <summary>
/// Shared hip-fire cone for the current gun. Movement opens the cone; the bat stays at 0.
/// Guns and the HUD both read CurrentSpread so the reticle matches the shot.
/// </summary>
public class WeaponAccuracy : MonoBehaviour
{
    public static WeaponAccuracy Instance { get; private set; }

    [Header("Sources")]
    public PlayerMovement movement;
    public WeaponSwitcher switcher;

    [Header("Reticle catch-up (degrees / sec)")]
    public float expandSpeed = 26f;
    public float recoverSpeed = 12f;

    float currentSpread;
    int lastWeaponIndex = int.MinValue;

    public float CurrentSpread => currentSpread;
    public bool IsMelee { get; private set; }
    public GameObject CurrentWeapon => switcher != null ? switcher.CurrentWeapon : null;

    public static WeaponAccuracy EnsureExists()
    {
        if (Instance != null)
            return Instance;

        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move == null)
            return null;

        Instance = move.GetComponent<WeaponAccuracy>();
        if (Instance == null)
            Instance = move.gameObject.AddComponent<WeaponAccuracy>();
        return Instance;
    }

    public static float CurrentSpreadOr(float fallback)
    {
        WeaponAccuracy accuracy = Instance != null ? Instance : EnsureExists();
        return accuracy != null ? accuracy.CurrentSpread : fallback;
    }

    public static Vector3 ApplySpread(Vector3 direction, float angleDegrees)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        Vector3 dir = direction.normalized;
        if (angleDegrees <= 0.01f)
            return dir;

        Quaternion aim = Quaternion.LookRotation(dir);
        Vector3 right = aim * Vector3.right;
        Vector3 up = aim * Vector3.up;
        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-angleDegrees, angleDegrees), up);
        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-angleDegrees, angleDegrees), right);
        return (yaw * pitch * dir).normalized;
    }

    void Awake()
    {
        Instance = this;
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
        if (switcher == null)
            switcher = GetComponent<WeaponSwitcher>();
        if (switcher == null)
            switcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher == null)
            switcher = FindFirstObjectByType<WeaponSwitcher>();

        currentSpread = EvaluateTargetSpread();
        lastWeaponIndex = switcher != null ? switcher.CurrentIndex : int.MinValue;
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        float target = EvaluateTargetSpread();
        int index = switcher != null ? switcher.CurrentIndex : -1;
        if (index != lastWeaponIndex)
        {
            currentSpread = target;
            lastWeaponIndex = index;
            return;
        }

        float speed = target > currentSpread ? expandSpeed : recoverSpeed;
        currentSpread = Mathf.MoveTowards(currentSpread, target, speed * Time.deltaTime);
    }

    float EvaluateTargetSpread()
    {
        Weapon gun = switcher != null ? switcher.CurrentRangedWeapon : null;
        if (gun == null)
        {
            IsMelee = true;
            return 0f;
        }

        IsMelee = false;
        return gun.EvaluateSpread(movement);
    }
}
