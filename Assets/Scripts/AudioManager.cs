using UnityEngine;

/// <summary>
/// Central SFX / music / dialogue beeps. Uses assigned clips when present,
/// otherwise falls back to procedural tones so the game is never silent.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("UI")]
    public AudioClip uiClick;
    public AudioClip uiBack;
    public AudioClip tutorialPopup;
    [Range(0f, 1f)] public float uiVolume = 0.42f;
    [Range(0f, 1f)] public float tutorialPopupVolume = 0.38f;

    [Header("Combat")]
    public AudioClip hitFlesh;
    public AudioClip meleeSwing;
    public AudioClip meleeHit;
    public AudioClip gunshotPistol;
    public AudioClip gunshotShotgun;
    public AudioClip gunshotRifle;
    public AudioClip explosion;
    public AudioClip breakObject;
    public AudioClip dash;
    public AudioClip weaponPickup;
    [Range(0f, 1f)] public float combatVolume = 0.55f;

    [Header("Weapons")]
    public AudioClip weaponSwap;
    public AudioClip pistolReload;
    public AudioClip emptyMag;
    public AudioClip pistolEquip;
    public AudioClip batEquip;
    [Range(0f, 1f)] public float weaponVolume = 0.42f;
    [Range(0f, 1f)] public float reloadVolume = 0.5f;
    [Range(0f, 1f)] public float emptyMagVolume = 0.55f;
    [Range(0f, 1f)] public float pistolEquipVolume = 0.48f;
    [Range(0f, 1f)] public float batEquipVolume = 0.32f;

    [Header("Voice (optional)")]
    public AudioClip enemyVoiceBeep;
    public AudioClip bossVoiceBeep;

    [Header("Music")]
    public AudioClip ambientLoop;
    public AudioClip combatMusicLoop;
    [Range(0f, 1f)] public float ambientVolume = 0.22f;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    public bool playAmbientOnStart = true;
    public bool playCombatMusicOnStart = true;

    [Header("Sources (optional — auto-created)")]
    public AudioSource sfxSource;
    public AudioSource ambientSource;
    public AudioSource musicSource;

    float nextHitSoundTime;
    float musicIntensity = 1f;
    bool generatedFallbacks;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSources();
        EnsureFallbackClips();
        ApplyMasterVolume(GameSettings.Volume);
    }

    void Start()
    {
        if (playAmbientOnStart)
            PlayAmbient();
        if (playCombatMusicOnStart)
            PlayCombatMusic();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void EnsureSources()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.ignoreListenerPause = true;
        }

        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }
    }

    void EnsureFallbackClips()
    {
        if (generatedFallbacks)
            return;
        generatedFallbacks = true;

        LoadPackagedClips();

        if (uiClick == null) uiClick = ProceduralAudio.Tone("uiClick", 880f, 0.06f, 0.3f);
        if (uiBack == null) uiBack = uiClick != null ? uiClick : ProceduralAudio.Tone("uiBack", 440f, 0.08f, 0.28f);
        if (tutorialPopup == null) tutorialPopup = uiClick;
        if (hitFlesh == null) hitFlesh = ProceduralAudio.NoiseBurst("hitFlesh", 0.12f, 0.4f);
        if (meleeSwing == null) meleeSwing = ProceduralAudio.Sweep("meleeSwing", 200f, 80f, 0.15f, 0.25f);
        if (meleeHit == null) meleeHit = ProceduralAudio.NoiseBurst("meleeHit", 0.14f, 0.45f);
        if (gunshotPistol == null) gunshotPistol = ProceduralAudio.NoiseBurst("pistol", 0.1f, 0.55f);
        if (gunshotShotgun == null) gunshotShotgun = ProceduralAudio.NoiseBurst("shotgun", 0.18f, 0.65f);
        if (gunshotRifle == null) gunshotRifle = ProceduralAudio.Tone("rifle", 180f, 0.08f, 0.4f);
        if (explosion == null) explosion = ProceduralAudio.NoiseBurst("explosion", 0.35f, 0.7f);
        if (breakObject == null) breakObject = ProceduralAudio.NoiseBurst("break", 0.2f, 0.5f);
        if (dash == null) dash = ProceduralAudio.Sweep("dash", 120f, 400f, 0.12f, 0.3f);
        if (weaponPickup == null) weaponPickup = ProceduralAudio.Tone("pickup", 660f, 0.2f, 0.35f);
        if (weaponSwap == null) weaponSwap = ProceduralAudio.Tone("swap", 520f, 0.08f, 0.3f);
        if (pistolReload == null) pistolReload = ProceduralAudio.NoiseBurst("reload", 0.22f, 0.28f);
        if (emptyMag == null) emptyMag = ProceduralAudio.Tone("emptyMag", 180f, 0.05f, 0.22f);
        if (pistolEquip == null) pistolEquip = ProceduralAudio.Tone("pistolEquip", 420f, 0.1f, 0.28f);
        if (batEquip == null) batEquip = ProceduralAudio.Sweep("batEquip", 520f, 140f, 0.18f, 0.22f);
        if (enemyVoiceBeep == null) enemyVoiceBeep = ProceduralAudio.Tone("enemyVoice", 300f, 0.09f, 0.22f);
        if (bossVoiceBeep == null) bossVoiceBeep = ProceduralAudio.Tone("bossVoice", 140f, 0.18f, 0.35f);
        if (ambientLoop == null) ambientLoop = ProceduralAudio.Tone("ambient", 70f, 1.5f, 0.08f);
        if (combatMusicLoop == null) combatMusicLoop = ProceduralAudio.CombatLoop(1f);
    }

    /// <summary>
    /// Loads clips from Assets/Resources/Audio when Inspector fields are empty.
    /// </summary>
    void LoadPackagedClips()
    {
        if (uiClick == null) uiClick = Resources.Load<AudioClip>("Audio/ui_button");
        if (uiClick == null) uiClick = Resources.Load<AudioClip>("Audio/ui_click");
        if (uiBack == null) uiBack = uiClick;
        if (tutorialPopup == null) tutorialPopup = Resources.Load<AudioClip>("Audio/tutorial_popup");
        if (gunshotPistol == null) gunshotPistol = Resources.Load<AudioClip>("Audio/pistol_shot");
        if (gunshotShotgun == null) gunshotShotgun = Resources.Load<AudioClip>("Audio/shotgun_shot");
        if (gunshotRifle == null) gunshotRifle = Resources.Load<AudioClip>("Audio/ar_shot");
        if (pistolReload == null) pistolReload = Resources.Load<AudioClip>("Audio/mag_reload");
        if (emptyMag == null) emptyMag = Resources.Load<AudioClip>("Audio/empty_click");
        if (pistolEquip == null) pistolEquip = Resources.Load<AudioClip>("Audio/pistol_cock");
        if (batEquip == null) batEquip = Resources.Load<AudioClip>("Audio/bat_whoosh");
        if (ambientLoop == null) ambientLoop = Resources.Load<AudioClip>("Audio/ambient_street");
    }

    public void PlayAmbient()
    {
        EnsureSources();
        EnsureFallbackClips();
        if (ambientLoop == null || ambientSource == null)
            return;

        ambientSource.clip = ambientLoop;
        ambientSource.volume = ambientVolume;
        ambientSource.loop = true;
        if (!ambientSource.isPlaying)
            ambientSource.Play();
    }

    public void StopAmbient()
    {
        if (ambientSource != null)
            ambientSource.Stop();
    }

    public void PlayCombatMusic()
    {
        EnsureSources();
        EnsureFallbackClips();
        if (combatMusicLoop == null || musicSource == null)
            return;

        musicSource.clip = combatMusicLoop;
        musicSource.volume = musicVolume * musicIntensity;
        musicSource.loop = true;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    void ApplyCombatMusicIntensity(float intensity)
    {
        musicIntensity = Mathf.Clamp(intensity, 0.5f, 2f);
        if (musicSource != null)
            musicSource.volume = musicVolume * musicIntensity;

        if (musicIntensity > 1.2f && combatMusicLoop != null)
        {
            // Rebuild a punchier loop for boss phases when using procedural music.
            if (combatMusicLoop.name == "CombatLoop")
            {
                combatMusicLoop = ProceduralAudio.CombatLoop(musicIntensity);
                if (musicSource != null && musicSource.isPlaying)
                {
                    float time = musicSource.time;
                    musicSource.clip = combatMusicLoop;
                    musicSource.Play();
                    if (time < combatMusicLoop.length)
                        musicSource.time = time % combatMusicLoop.length;
                }
            }
        }
    }

    public void PlayUIClick() => PlayOneShot(uiClick, uiVolume);
    public void PlayUIBack() => PlayOneShot(uiBack != null ? uiBack : uiClick, uiVolume * 0.9f);
    public void PlayTutorialPopup() => PlayOneShot(tutorialPopup != null ? tutorialPopup : uiClick, tutorialPopupVolume);
    public void PlayWeaponSwap() => PlayOneShot(weaponSwap, weaponVolume);
    public void PlayPistolReload() => PlayOneShot(pistolReload, reloadVolume);
    public void PlayEmptyMag() => PlayOneShot(emptyMag, emptyMagVolume);
    public void PlayPistolEquip() => PlayOneShot(pistolEquip != null ? pistolEquip : weaponSwap, pistolEquipVolume);
    public void PlayBatEquip() => PlayOneShot(batEquip != null ? batEquip : weaponSwap, batEquipVolume);

    public void PlayHitFlesh(Vector3 position)
    {
        if (Time.unscaledTime < nextHitSoundTime)
            return;
        nextHitSoundTime = Time.unscaledTime + 0.045f;
        PlayOneShotAt(hitFlesh, position, combatVolume);
    }

    public void PlayMeleeSwing() => PlayOneShot(meleeSwing, combatVolume);
    public void PlayMeleeHit(Vector3 position) => PlayOneShotAt(meleeHit != null ? meleeHit : hitFlesh, position, combatVolume);

    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;
        EnsureSources();
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayOneShotAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null)
            return;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volumeScale));
    }

    public void ApplyMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        if (ambientSource != null)
            ambientSource.volume = ambientVolume;
        if (musicSource != null)
            musicSource.volume = musicVolume * musicIntensity;
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }

    public static void UIClick() { EnsureExists(); Instance.PlayUIClick(); }
    public static void UIBack() { EnsureExists(); Instance.PlayUIBack(); }
    public static void TutorialPopup() { EnsureExists(); Instance.PlayTutorialPopup(); }
    public static void WeaponSwap() { EnsureExists(); Instance.PlayWeaponSwap(); }
    public static void PistolReload() { EnsureExists(); Instance.PlayPistolReload(); }
    public static void EmptyMag() { EnsureExists(); Instance.PlayEmptyMag(); }

    public static void PlayEquip(GameObject weapon)
    {
        EnsureExists();
        if (weapon != null && weapon.GetComponent<Pistol>() != null)
            Instance.PlayPistolEquip();
        else if (weapon != null && weapon.GetComponent<Melee>() != null)
            Instance.PlayBatEquip();
        else
            Instance.PlayWeaponSwap();
    }
    public static void HitFlesh(Vector3 position) { if (Instance != null) Instance.PlayHitFlesh(position); }
    public static void MeleeSwing() { if (Instance != null) Instance.PlayMeleeSwing(); }
    public static void MeleeHit(Vector3 position) { if (Instance != null) Instance.PlayMeleeHit(position); }

    public static void EnemyGunshot(Vector3 position, EnemyWeaponKind kind)
    {
        EnsureExists();
        AudioClip clip = Instance.gunshotPistol;
        if (kind == EnemyWeaponKind.Shotgun) clip = Instance.gunshotShotgun;
        else if (kind == EnemyWeaponKind.Rifle) clip = Instance.gunshotRifle;
        else if (kind == EnemyWeaponKind.BossGun) clip = Instance.gunshotRifle;
        Instance.PlayOneShotAt(clip, position, Instance.combatVolume * 0.9f);
    }

    public static void Explosion(Vector3 position)
    {
        EnsureExists();
        Instance.PlayOneShotAt(Instance.explosion, position, Instance.combatVolume);
    }

    public static void BreakObject(Vector3 position)
    {
        EnsureExists();
        Instance.PlayOneShotAt(Instance.breakObject, position, Instance.combatVolume * 0.9f);
    }

    public static void Dash()
    {
        EnsureExists();
        Instance.PlayOneShot(Instance.dash, Instance.combatVolume * 0.8f);
    }

    public static void WeaponPickup()
    {
        EnsureExists();
        Instance.PlayOneShot(Instance.weaponPickup, Instance.weaponVolume);
    }

    public static void EnemyVoice(Vector3 position)
    {
        EnsureExists();
        Instance.PlayOneShotAt(Instance.enemyVoiceBeep, position, 0.4f);
    }

    public static void BossVoice()
    {
        EnsureExists();
        Instance.PlayOneShot(Instance.bossVoiceBeep, 0.5f);
    }

    public static void SetCombatMusicIntensity(float intensity)
    {
        EnsureExists();
        Instance.ApplyCombatMusicIntensity(intensity);
    }

    public static void Play(AudioClip clip, float volumeScale = 1f)
    {
        EnsureExists();
        Instance.PlayOneShot(clip, volumeScale);
    }

    public static void PlayGunshot(AudioClip clip, Vector3 position, EnemyWeaponKind kind)
    {
        EnsureExists();
        if (clip != null)
            Instance.PlayOneShot(clip, Instance.combatVolume);
        else
            EnemyGunshot(position, kind);
    }
}
