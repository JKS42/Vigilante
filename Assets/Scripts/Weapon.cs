using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    public float FireCooldown = 0.25f;
    public bool Auto;

    [Header("Ammo")]
    public int magazineSize = 12;
    public int startingReserve = 36;
    public float reloadTime = 1.5f;

    [Header("Accuracy (degrees)")]
    public float idleSpread;
    public float movingSpread = 1.5f;
    public float sprintSpread = 2.5f;
    public float airSpreadBonus = 1.15f;
    public float crouchSpreadMultiplier = 0.55f;
    public float fireBloomPerShot;
    public float fireBloomRecovery = 10f;
    public float maxSpread = 5f;

    float fireBloom;

    [Header("Input")]
    public InputActionReference fireActionReference;
    public InputActionReference reloadActionReference;

    float currentCooldown;
    int currentAmmo;
    int reserveAmmo;
    bool isReloading;
    Coroutine reloadRoutine;

    InputAction fireAction;
    InputAction reloadAction;
    bool ownsFireAction;
    bool ownsReloadAction;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    protected virtual void Awake()
    {
        // Old scene data can deserialize missing inherited fields as 0.
        if (magazineSize <= 0)
            magazineSize = 12;
        if (FireCooldown < 0f)
            FireCooldown = 0.25f;
        if (reloadTime < 0f)
            reloadTime = 1.5f;

        currentCooldown = 0f;
        if (startingReserve <= 0)
            startingReserve = magazineSize * 3;
        reserveAmmo = startingReserve;
        currentAmmo = magazineSize;
        EnsureAccuracyDefaults();
    }

    protected virtual void EnsureAccuracyDefaults()
    {
        if (movingSpread <= 0f)
            movingSpread = 1.5f;
        if (sprintSpread <= 0f)
            sprintSpread = 2.5f;
        if (airSpreadBonus <= 0f)
            airSpreadBonus = 1.15f;
        if (crouchSpreadMultiplier <= 0.01f)
            crouchSpreadMultiplier = 0.55f;
        if (fireBloomRecovery <= 0f)
            fireBloomRecovery = 10f;
        if (maxSpread <= 0f)
            maxSpread = 5f;
    }

    public float EvaluateSpread(PlayerMovement move)
    {
        float spread = idleSpread;
        if (move != null)
        {
            float speed = move.HorizontalSpeed;
            float walk = Mathf.Max(0.01f, move.walkSpeed);
            float sprint = Mathf.Max(walk + 0.01f, move.sprintSpeed);

            if (speed <= walk)
                spread = Mathf.Lerp(idleSpread, movingSpread, Mathf.InverseLerp(0.35f, walk, speed));
            else
                spread = Mathf.Lerp(movingSpread, sprintSpread, Mathf.InverseLerp(walk, sprint, speed));

            if (!move.IsGrounded || move.IsDashing)
                spread += airSpreadBonus;

            if (move.IsCrouching)
                spread *= crouchSpreadMultiplier;
        }

        spread += fireBloom;
        if (maxSpread > 0f)
            spread = Mathf.Min(spread, maxSpread);
        return Mathf.Max(0f, spread);
    }

    protected Vector3 GetSpreadAim(Vector3 direction)
    {
        float cone = WeaponAccuracy.CurrentSpreadOr(EvaluateSpread(null));
        return WeaponAccuracy.ApplySpread(direction, cone);
    }

    protected virtual void Start()
    {
        if (currentAmmo <= 0 && magazineSize > 0)
            currentAmmo = magazineSize;
    }

    protected virtual void OnEnable()
    {
        BindActions();

        if (fireAction != null)
            fireAction.Enable();

        if (reloadAction != null)
        {
            reloadAction.performed += OnReloadPerformed;
            reloadAction.Enable();
        }
    }

    protected virtual void OnDisable()
    {
        if (fireAction != null)
        {
            if (ownsFireAction)
            {
                fireAction.Disable();
                fireAction.Dispose();
            }
        }

        if (reloadAction != null)
        {
            reloadAction.performed -= OnReloadPerformed;
            if (ownsReloadAction)
            {
                reloadAction.Disable();
                reloadAction.Dispose();
            }
        }

        fireAction = null;
        reloadAction = null;
        ownsFireAction = false;
        ownsReloadAction = false;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
            isReloading = false;
        }

        fireBloom = 0f;
    }

    void BindActions()
    {
        if (fireActionReference != null && fireActionReference.action != null)
        {
            fireAction = fireActionReference.action;
            ownsFireAction = false;
        }
        else
        {
            fireAction = new InputAction("Attack", InputActionType.Button);
            fireAction.AddBinding("<Mouse>/leftButton");
            fireAction.AddBinding("<Gamepad>/rightTrigger");
            ownsFireAction = true;
        }

        if (reloadActionReference != null && reloadActionReference.action != null)
        {
            reloadAction = reloadActionReference.action;
            ownsReloadAction = false;
        }
        else
        {
            reloadAction = new InputAction("Reload", InputActionType.Button);
            reloadAction.AddBinding("<Keyboard>/r");
            reloadAction.AddBinding("<Gamepad>/buttonWest");
            ownsReloadAction = true;
        }
    }

    void OnReloadPerformed(InputAction.CallbackContext context)
    {
        TryReload();
    }

    protected virtual void Update()
    {
        if (currentCooldown > 0f)
            currentCooldown -= Time.deltaTime;

        if (fireBloom > 0f)
            fireBloom = Mathf.MoveTowards(fireBloom, 0f, fireBloomRecovery * Time.deltaTime);

        if (fireAction == null)
            return;

        if (Auto)
        {
            if (fireAction.IsPressed())
                TryShoot();
        }
        else if (fireAction.WasPressedThisFrame())
        {
            TryShoot();
        }
    }

    void TryShoot()
    {
        if (Time.timeScale <= 0f)
            return;

        if (isReloading || currentAmmo <= 0)
            return;

        if (currentCooldown > 0f)
            return;

        currentAmmo--;
        FireShot();
        if (fireBloomPerShot > 0f)
        {
            fireBloom += fireBloomPerShot;
            if (maxSpread > 0f)
                fireBloom = Mathf.Min(fireBloom, maxSpread);
        }
        currentCooldown = Mathf.Max(0.01f, FireCooldown);

        if (currentAmmo <= 0)
            TryReload();
    }

    void TryReload()
    {
        if (isReloading || currentAmmo >= magazineSize || reserveAmmo <= 0)
            return;

        reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        int needed = magazineSize - currentAmmo;
        int take = Mathf.Min(needed, reserveAmmo);
        currentAmmo += take;
        reserveAmmo -= take;
        isReloading = false;
        reloadRoutine = null;
    }

    public int AddReserveAmmo(int amount)
    {
        if (amount <= 0)
            return 0;

        int cap = Mathf.Max(startingReserve, magazineSize * 6);
        int room = Mathf.Max(0, cap - reserveAmmo);
        int added = Mathf.Min(amount, room);
        reserveAmmo += added;
        return added;
    }

    protected virtual void FireShot()
    {
    }
}
