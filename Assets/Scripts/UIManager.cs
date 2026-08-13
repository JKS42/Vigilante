using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD for the selected weapon symbol, live ammo, wave timer, and enemy counter.
/// Assign arrays in the same order as WeaponSwitcher.weapons. No runtime bootstrap.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

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
    public float lockedInventoryAlpha = 0.12f;

    [Header("Wave timer")]
    public TextMeshProUGUI timerText;
    public string timerPrefix = "Time: ";

    [Header("Enemy counter")]
    public TextMeshProUGUI enemyCountText;
    public string enemyCountPrefix = "ELIMINATE ALL ENEMIES: ";

    [Header("Melee")]
    public string meleeAmmoLabel = "∞";

    [Header("Player health")]
    public Slider healthSlider;

    int boundIndex = -1;
    Health playerHealth;
    Image damageVignette;
    GameObject deathPanel;
    bool playerDead;
    float vignetteAlpha;
    static bool healthBound;

    void Awake()
    {
        if (healthSlider != null)
            Instance = this;
        else if (Instance == null)
            Instance = this;
    }

    void OnEnable()
    {
        if (healthSlider != null)
            Instance = this;
        else if (Instance == null)
            Instance = this;

        if (weaponSwitcher != null)
        {
            weaponSwitcher.WeaponChanged += OnWeaponChanged;
            weaponSwitcher.WeaponUnlocked += OnWeaponUnlocked;
        }
    }

    void OnDisable()
    {
        if (weaponSwitcher != null)
        {
            weaponSwitcher.WeaponChanged -= OnWeaponChanged;
            weaponSwitcher.WeaponUnlocked -= OnWeaponUnlocked;
        }

        if (Instance == this)
            Instance = null;
    }

    void OnDestroy()
    {
        UnbindPlayerHealth();
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (waveManager == null)
            waveManager = WaveManager.Instance;

        BindPlayerHealth();

        if (weaponSwitcher != null && weaponSwitcher.CurrentIndex >= 0)
            OnWeaponChanged(weaponSwitcher.CurrentIndex, weaponSwitcher.CurrentWeapon);
        else
        {
            UpdateInventoryHighlight(boundIndex);
            RefreshAmmo();
        }

        RefreshWaveTimer();
        RefreshEnemyCount();

        if (enemyCountText != null)
        {
            string levelLabel = $"LEVEL {GameProgression.SelectedLevel}  ·  ";
            if (!enemyCountPrefix.Contains("LEVEL"))
                enemyCountPrefix = levelLabel + enemyCountPrefix;
        }
    }

    void Update()
    {
        RefreshAmmo();
        RefreshWaveTimer();
        RefreshEnemyCount();
        TickVignette();
    }

    void BindPlayerHealth()
    {
        if (Instance != null && Instance != this)
            return;

        if (healthBound)
            return;

        if (healthSlider == null)
        {
            GameObject sliderGo = GameObject.Find("Health");
            if (sliderGo != null)
                healthSlider = sliderGo.GetComponent<Slider>();
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<Health>();
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<Health>();

        if (playerHealth == null)
            return;

        healthBound = true;
        playerHealth.OnDamaged += HandlePlayerDamaged;
        playerHealth.OnDied += HandlePlayerDied;
        RefreshHealthSlider();
        EnsureDamageVignette();
    }

    void UnbindPlayerHealth()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnDamaged -= HandlePlayerDamaged;
        playerHealth.OnDied -= HandlePlayerDied;
        if (healthBound && playerHealth != null)
            healthBound = false;
    }

    void HandlePlayerDamaged(float amount, Vector3 hitPoint, GameObject instigator)
    {
        RefreshHealthSlider();
        vignetteAlpha = Mathf.Max(vignetteAlpha, 0.55f);
    }

    void HandlePlayerDied()
    {
        if (playerDead)
            return;

        playerDead = true;
        RefreshHealthSlider();
        vignetteAlpha = 0.75f;
        ShowDeathOverlay();
    }

    void RefreshHealthSlider()
    {
        if (healthSlider == null || playerHealth == null)
            return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = playerHealth.MaxHealth;
        healthSlider.value = playerHealth.CurrentHealth;
    }

    void TickVignette()
    {
        if (damageVignette == null)
            return;

        if (!playerDead)
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 1.6f);

        Color c = damageVignette.color;
        c.a = vignetteAlpha;
        damageVignette.color = c;
    }

    void EnsureDamageVignette()
    {
        if (damageVignette != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        GameObject go = new GameObject("DamageVignette");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        damageVignette = go.AddComponent<Image>();
        Texture2D tex = Texture2D.whiteTexture;
        damageVignette.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        damageVignette.color = new Color(0.7f, 0.05f, 0.05f, 0f);
        damageVignette.raycastTarget = false;
        RectTransform rt = damageVignette.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void ShowDeathOverlay()
    {
        PauseMenu pause = FindFirstObjectByType<PauseMenu>();
        if (pause != null)
            pause.enabled = false;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        deathPanel = new GameObject("DeathOverlay");
        deathPanel.transform.SetParent(canvas.transform, false);
        deathPanel.transform.SetAsLastSibling();
        Image bg = deathPanel.AddComponent<Image>();
        bg.sprite = CreateWhiteSprite();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform prt = bg.rectTransform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("DeathText");
        textGo.transform.SetParent(deathPanel.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "YOU DIED";
        tmp.fontSize = 64f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        RectTransform trt = tmp.rectTransform;
        trt.anchorMin = new Vector2(0.2f, 0.52f);
        trt.anchorMax = new Vector2(0.8f, 0.72f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        CreateDeathButton(deathPanel.transform, "Restart", new Vector2(0.35f, 0.32f), new Vector2(0.49f, 0.42f), () =>
        {
            GameProgression.RestartCurrentLevel();
        });

        CreateDeathButton(deathPanel.transform, "Main Menu", new Vector2(0.51f, 0.32f), new Vector2(0.65f, 0.42f), () =>
        {
            Time.timeScale = 1f;
            if (pause != null)
                pause.LoadMainMenu();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        });
    }

    static void CreateDeathButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = CreateWhiteSprite();
        img.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(action);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        RectTransform trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
    }

    void OnWeaponChanged(int index, GameObject weapon)
    {
        boundIndex = index;
        SetActiveExclusive(weaponHudIcons, index);
        UpdateInventoryHighlight(index);
        RefreshAmmo();
    }

    void OnWeaponUnlocked(int index)
    {
        UpdateInventoryHighlight(weaponSwitcher != null ? weaponSwitcher.CurrentIndex : boundIndex);
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

            bool unlocked = weaponSwitcher == null || weaponSwitcher.IsUnlocked(i);
            float alpha;
            if (!unlocked)
                alpha = lockedInventoryAlpha;
            else if (i == selectedIndex)
                alpha = selectedInventoryAlpha;
            else
                alpha = unselectedInventoryAlpha;

            Graphic[] graphics = slot.GetComponentsInChildren<Graphic>(true);
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

    public static void ResetHealthBinding()
    {
        healthBound = false;
    }
}
