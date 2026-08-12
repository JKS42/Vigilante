using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    public float FireCooldown = 0.25f;
    public bool Auto;

    [Header("Ammo")]
    public int magazineSize = 12;
    public float reloadTime = 1.5f;

    [Header("Input")]
    public InputActionReference fireActionReference;
    public InputActionReference reloadActionReference;

    float currentCooldown;
    int currentAmmo;
    bool isReloading;
    Coroutine reloadRoutine;

    InputAction fireAction;
    InputAction reloadAction;
    bool ownsFireAction;
    bool ownsReloadAction;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
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
        currentAmmo = magazineSize;
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
        if (isReloading || currentAmmo <= 0)
            return;

        if (currentCooldown > 0f)
            return;

        currentAmmo--;
        FireShot();
        currentCooldown = Mathf.Max(0.01f, FireCooldown);
    }

    void TryReload()
    {
        if (isReloading || currentAmmo >= magazineSize)
            return;

        reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        isReloading = false;
        reloadRoutine = null;
    }

    protected virtual void FireShot()
    {
    }
}
