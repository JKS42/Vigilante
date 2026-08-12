using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD for the selected weapon symbol, live ammo, wave timer, and enemy counter.
/// Assign arrays in the same order as WeaponSwitcher.weapons. No runtime bootstrap.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Source")]
    public WeaponSwitcher weaponSwitcher;
    public WaveManager waveManager;

    [Header("Selected weapon symbol (one active at a time)")]
    public GameObject[] weaponHudIcons;

    [Header("Ammo text (same order as icons / loadout)")]
    public TextMeshProUGUI[] ammoTexts;

    [Header("Inventory bar (optional — all stay visible)")]
    public GameObject[] inventorySlots;
    public float selectedInventoryAlpha = 1f;
    public float unselectedInventoryAlpha = 0.35f;

    [Header("Wave timer")]
    public TextMeshProUGUI timerText;
    public string timerPrefix = "Time: ";

    [Header("Enemy counter")]
    public TextMeshProUGUI enemyCountText;
    public string enemyCountPrefix = "ELIMINATE ALL ENEMIES: ";

    [Header("Melee")]
    public string meleeAmmoLabel = "∞";

    int boundIndex = -1;

    void OnEnable()
    {
        if (weaponSwitcher != null)
            weaponSwitcher.WeaponChanged += OnWeaponChanged;
    }

    void OnDisable()
    {
        if (weaponSwitcher != null)
            weaponSwitcher.WeaponChanged -= OnWeaponChanged;
    }

    void Start()
    {
        if (waveManager == null)
            waveManager = WaveManager.Instance;

        if (weaponSwitcher != null && weaponSwitcher.CurrentIndex >= 0)
            OnWeaponChanged(weaponSwitcher.CurrentIndex, weaponSwitcher.CurrentWeapon);
        else
            RefreshAmmo();

        RefreshWaveTimer();
        RefreshEnemyCount();
    }

    void Update()
    {
        RefreshAmmo();
        RefreshWaveTimer();
        RefreshEnemyCount();
    }

    void OnWeaponChanged(int index, GameObject weapon)
    {
        boundIndex = index;
        SetActiveExclusive(weaponHudIcons, index);
        UpdateInventoryHighlight(index);
        RefreshAmmo();
    }

    void RefreshAmmo()
    {
        if (ammoTexts == null || ammoTexts.Length == 0)
            return;

        int index = weaponSwitcher != null ? weaponSwitcher.CurrentIndex : boundIndex;
        if (index < 0 || index >= ammoTexts.Length || ammoTexts[index] == null)
            return;

        Weapon ranged = weaponSwitcher != null ? weaponSwitcher.CurrentRangedWeapon : null;
        TextMeshProUGUI label = ammoTexts[index];
        if (!label.gameObject.activeSelf)
            label.gameObject.SetActive(true);

        if (ranged != null)
        {
            label.text = ranged.IsReloading
                ? $".../{ranged.MagazineSize}"
                : $"{ranged.CurrentAmmo}/{ranged.MagazineSize}";
            return;
        }

        // Melee / non-Weapon slot
        label.text = meleeAmmoLabel;
    }

    void RefreshWaveTimer()
    {
        if (timerText == null)
            return;

        if (waveManager == null)
            waveManager = WaveManager.Instance;

        float seconds = waveManager != null ? waveManager.TimeRemaining : 0f;
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        timerText.text = $"{timerPrefix}{minutes:00}:{secs:00}";
    }

    void RefreshEnemyCount()
    {
        if (enemyCountText == null)
            return;

        if (waveManager == null)
            waveManager = WaveManager.Instance;

        int killed = waveManager != null ? waveManager.EnemiesKilled : 0;
        int total = waveManager != null ? waveManager.TotalEnemyCount : 0;
        enemyCountText.text = $"{enemyCountPrefix}{killed}/{total}";
    }

    void UpdateInventoryHighlight(int selectedIndex)
    {
        if (inventorySlots == null)
            return;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            GameObject slot = inventorySlots[i];
            if (slot == null)
                continue;

            if (!slot.activeSelf)
                slot.SetActive(true);

            Graphic[] graphics = slot.GetComponentsInChildren<Graphic>(true);
            float alpha = i == selectedIndex ? selectedInventoryAlpha : unselectedInventoryAlpha;
            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] == null)
                    continue;
                Color c = graphics[g].color;
                c.a = alpha;
                graphics[g].color = c;
            }
        }
    }

    static void SetActiveExclusive(GameObject[] objects, int activeIndex)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(i == activeIndex);
        }
    }
}
