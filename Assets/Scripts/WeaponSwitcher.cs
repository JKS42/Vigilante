using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enables one held weapon at a time. Cycle with Previous/Next (or scroll),
/// or press 1–4 for a direct slot. No runtime Find/bootstrap — assign weapons in the Inspector.
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

    public event Action<int, GameObject> WeaponChanged;

    InputAction previousAction;
    InputAction nextAction;
    bool ownsPrevious;
    bool ownsNext;
    int currentIndex = -1;
    float scrollCooldown;

    public int CurrentIndex => currentIndex;
    public GameObject CurrentWeapon =>
        currentIndex >= 0 && weapons != null && currentIndex < weapons.Length
            ? weapons[currentIndex]
            : null;

    public Weapon CurrentRangedWeapon =>
        CurrentWeapon != null ? CurrentWeapon.GetComponent<Weapon>() : null;

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

        int index = Mathf.Clamp(startingWeaponIndex, 0, weapons.Length - 1);
        SelectWeapon(index, force: true);
    }

    void Update()
    {
        if (weapons == null || weapons.Length == 0)
            return;

        if (scrollCooldown > 0f)
            scrollCooldown -= Time.unscaledDeltaTime;

        HandleScroll();
        HandleNumberKeys();
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
        CycleWeapon(-1);
    }

    void OnNext(InputAction.CallbackContext context)
    {
        CycleWeapon(1);
    }

    void HandleScroll()
    {
        if (scrollCooldown > 0f || Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < scrollThreshold)
            return;

        CycleWeapon(scroll > 0f ? -1 : 1);
        scrollCooldown = 0.12f;
    }

    void HandleNumberKeys()
    {
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
        if (weapons == null || weapons.Length == 0)
            return;

        int count = weapons.Length;
        int next = currentIndex < 0 ? startingWeaponIndex : currentIndex;
        next = (next + direction) % count;
        if (next < 0)
            next += count;

        SelectWeapon(next);
    }

    public void SelectWeapon(int index, bool force = false)
    {
        if (weapons == null || weapons.Length == 0)
            return;

        if (index < 0 || index >= weapons.Length)
            return;

        if (!force && index == currentIndex)
            return;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                weapons[i].SetActive(i == index);
        }

        currentIndex = index;
        WeaponChanged?.Invoke(currentIndex, CurrentWeapon);
    }
}
