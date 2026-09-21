using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enables one held weapon at a time. Cycle with Previous/Next (or scroll),
/// or press 1–4 for a direct slot. Only unlocked weapons can be selected;
/// slot 0 (bat) starts unlocked. No runtime Find/bootstrap — assign weapons in the Inspector.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Header("Loadout (order = keys 1–4)")]
    public GameObject[] weapons;

    [Header("Start")]
    public int startingWeaponIndex;

    [Header("Input")]
    public InputActionReference previousActionReference;
    public InputActionReference nextActionReference;
    public float scrollThreshold = 0.1f;

    [Header("Swap motion")]
    public float holsterDuration = 0.18f;
    public float equipDuration = 0.22f;

    public event Action<int, GameObject> WeaponChanged;
    public event Action<int> WeaponUnlocked;

    InputAction previousAction;
    InputAction nextAction;
    bool ownsPrevious;
    bool ownsNext;
    int currentIndex = -1;
    float scrollCooldown;
    bool[] unlocked;
    bool swapping;
    Coroutine swapRoutine;

    public int CurrentIndex => currentIndex;
    public bool IsSwapping => swapping;
    public GameObject CurrentWeapon =>
        currentIndex >= 0 && weapons != null && currentIndex < weapons.Length
            ? weapons[currentIndex]
            : null;

    public Weapon CurrentRangedWeapon =>
        CurrentWeapon != null ? CurrentWeapon.GetComponent<Weapon>() : null;

    void Awake()
    {
        InitUnlocked();
        EnsureWeaponMotion();
    }

    void EnsureWeaponMotion()
    {
        if (weapons == null)
            return;
        for (int i = 0; i < weapons.Length; i++)
            WeaponViewMotion.Ensure(weapons[i]);
    }

    void OnEnable()
    {
        BindActions();

        if (previousAction != null)
        {
            previousAction.performed += OnPrevious;
            previousAction.Enable();
        }

        if (nextAction != null)
        {
            nextAction.performed += OnNext;
            nextAction.Enable();
        }
    }

    void OnDisable()
    {
        if (swapRoutine != null)
        {
            StopCoroutine(swapRoutine);
            swapRoutine = null;
            swapping = false;
        }

        if (previousAction != null)
        {
            previousAction.performed -= OnPrevious;
            if (ownsPrevious)
            {
                previousAction.Disable();
                previousAction.Dispose();
            }
        }

        if (nextAction != null)
        {
            nextAction.performed -= OnNext;
            if (ownsNext)
            {
                nextAction.Disable();
                nextAction.Dispose();
            }
        }

        previousAction = null;
        nextAction = null;
        ownsPrevious = false;
        ownsNext = false;
    }

    void Start()
    {
        if (weapons == null || weapons.Length == 0)
            return;

        InitUnlocked();
        EnsureWeaponMotion();

        int index = Mathf.Clamp(startingWeaponIndex, 0, weapons.Length - 1);
        if (!IsUnlocked(index))
            index = FirstUnlockedIndex();

        if (index >= 0)
            SelectWeapon(index, force: true);
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (weapons == null || weapons.Length == 0)
            return;

        if (scrollCooldown > 0f)
            scrollCooldown -= Time.unscaledDeltaTime;

        HandleScroll();
        HandleNumberKeys();
    }

    public bool IsUnlocked(int index)
    {
        InitUnlocked();
        return unlocked != null && index >= 0 && index < unlocked.Length && unlocked[index];
    }

    /// <param name="lootAmmo">
    /// Rounds from an enemy mag. Negative = legacy unlock/refill (full mag to reserve on re-loot).
    /// </param>
    public bool UnlockWeapon(int index, bool equip = true, int lootAmmo = -1)
    {
        if (weapons == null || index < 0 || index >= weapons.Length)
            return false;

        InitUnlocked();

        bool newlyUnlocked = !unlocked[index];
        unlocked[index] = true;

        if (newlyUnlocked)
            WeaponUnlocked?.Invoke(index);

        if (lootAmmo >= 0)
            ApplyLootAmmo(index, lootAmmo, newlyUnlocked);
        else if (!newlyUnlocked)
            RefillWeaponReserve(index);

        if (equip)
            SelectWeapon(index, force: true);

        return newlyUnlocked;
    }

    void ApplyLootAmmo(int index, int amount, bool newlyUnlocked)
    {
        if (weapons == null || index < 0 || index >= weapons.Length || weapons[index] == null)
            return;

        Weapon weapon = weapons[index].GetComponent<Weapon>();
        if (weapon == null)
            return;

        weapon.ApplyLootAmmo(amount, asLoadedMagazine: newlyUnlocked);
    }

    void RefillWeaponReserve(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length || weapons[index] == null)
            return;

        Weapon weapon = weapons[index].GetComponent<Weapon>();
        if (weapon == null)
            return;

        weapon.AddReserveAmmo(Mathf.Max(1, weapon.MagazineSize));
    }

    void InitUnlocked()
    {
        if (weapons == null)
        {
            unlocked = null;
            return;
        }

        if (unlocked != null && unlocked.Length == weapons.Length)
            return;

        bool[] next = new bool[weapons.Length];
        if (unlocked != null)
        {
            int copy = Mathf.Min(unlocked.Length, next.Length);
            for (int i = 0; i < copy; i++)
                next[i] = unlocked[i];
        }

        if (next.Length > 0)
            next[0] = true;

        unlocked = next;
    }

    int FirstUnlockedIndex()
    {
        if (unlocked == null)
            return -1;

        for (int i = 0; i < unlocked.Length; i++)
        {
            if (unlocked[i])
                return i;
        }

        return -1;
    }

    void BindActions()
    {
        if (previousActionReference != null && previousActionReference.action != null)
        {
            previousAction = previousActionReference.action;
            ownsPrevious = false;
        }
        else
        {
            previousAction = new InputAction("PreviousWeapon", InputActionType.Button);
            previousAction.AddBinding("<Keyboard>/leftBracket");
            previousAction.AddBinding("<Gamepad>/dpad/left");
            ownsPrevious = true;
        }

        if (nextActionReference != null && nextActionReference.action != null)
        {
            nextAction = nextActionReference.action;
            ownsNext = false;
        }
        else
        {
            nextAction = new InputAction("NextWeapon", InputActionType.Button);
            nextAction.AddBinding("<Keyboard>/rightBracket");
            nextAction.AddBinding("<Gamepad>/dpad/right");
            ownsNext = true;
        }
    }

    void OnPrevious(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f || swapping)
            return;
        CycleWeapon(-1);
    }

    void OnNext(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f || swapping)
            return;
        CycleWeapon(1);
    }

    void HandleScroll()
    {
        if (swapping || scrollCooldown > 0f || Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < scrollThreshold)
            return;

        CycleWeapon(scroll > 0f ? -1 : 1);
        scrollCooldown = 0.12f;
    }

    void HandleNumberKeys()
    {
        if (swapping)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            SelectWeapon(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            SelectWeapon(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            SelectWeapon(2);
        else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            SelectWeapon(3);
    }

    public void CycleWeapon(int direction)
    {
        if (swapping || weapons == null || weapons.Length == 0)
            return;

        InitUnlocked();

        int count = weapons.Length;
        int start = currentIndex < 0 ? startingWeaponIndex : currentIndex;
        int step = direction >= 0 ? 1 : -1;

        for (int i = 1; i <= count; i++)
        {
            int next = (start + step * i) % count;
            if (next < 0)
                next += count;

            if (IsUnlocked(next))
            {
                SelectWeapon(next);
                return;
            }
        }
    }

    public void SelectWeapon(int index, bool force = false)
    {
        if (weapons == null || weapons.Length == 0)
            return;

        if (index < 0 || index >= weapons.Length)
            return;

        if (!IsUnlocked(index))
            return;

        if (!force && (index == currentIndex || swapping))
            return;

        if (force)
        {
            if (swapRoutine != null)
            {
                StopCoroutine(swapRoutine);
                swapRoutine = null;
                swapping = false;
            }

            InstantSelect(index, playSwapSound: false);
            return;
        }

        swapRoutine = StartCoroutine(SwapRoutine(index));
    }

    void InstantSelect(int index, bool playSwapSound)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null)
                continue;

            bool on = i == index;
            weapons[i].SetActive(on);
            if (on)
            {
                WeaponViewMotion motion = WeaponViewMotion.Ensure(weapons[i]);
                motion?.SnapEquipped();
            }
        }

        bool changed = currentIndex != index;
        currentIndex = index;
        WeaponChanged?.Invoke(currentIndex, CurrentWeapon);

        if (playSwapSound && changed)
            AudioManager.WeaponSwap();
    }

    IEnumerator SwapRoutine(int index)
    {
        swapping = true;
        GameObject from = CurrentWeapon;
        GameObject to = weapons[index];
        bool playSwapSound = currentIndex >= 0 && index != currentIndex;

        if (from != null && from.activeInHierarchy)
        {
            WeaponViewMotion fromMotion = WeaponViewMotion.Ensure(from);
            if (fromMotion != null)
                yield return fromMotion.PlayHolster(holsterDuration);
            from.SetActive(false);
        }

        currentIndex = index;

        if (to != null)
        {
            to.SetActive(true);
            WeaponViewMotion toMotion = WeaponViewMotion.Ensure(to);
            if (toMotion != null)
            {
                toMotion.SnapHolstered();
                yield return toMotion.PlayEquip(equipDuration);
            }
        }

        WeaponChanged?.Invoke(currentIndex, CurrentWeapon);
        if (playSwapSound)
            AudioManager.WeaponSwap();

        swapping = false;
        swapRoutine = null;
    }
}
