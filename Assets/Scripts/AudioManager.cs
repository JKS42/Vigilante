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
    [Range(0f, 1f)] public float uiVolume = 0.7f;

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
    [Range(0f, 1f)] public float combatVolume = 1f;

    [Header("Weapons")]
    public AudioClip weaponSwap;
    [Range(0f, 1f)] public float weaponVolume = 0.85f;

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
        if (uiClick == null) uiClick = Resources.Load<AudioClip>("Audio/ui_click");
        if (uiBack == null) uiBack = uiClick;
        if (gunshotPistol == null) gunshotPistol = Resources.Load<AudioClip>("Audio/pistol_shot");
        if (gunshotShotgun == null) gunshotShotgun = Resources.Load<AudioClip>("Audio/shotgun_shot");
        if (gunshotRifle == null) gunshotRifle = Resources.Load<AudioClip>("Audio/ar_shot");
        if (ambientLoop == null) ambientLoop = Resources.Load<AudioClip>("Audio/ambient_street");
    }

    public void PlayAmbient()
    {
        EnsureSources();
        EnsureFallbackClips();
        if (ambientLoop == null || ambientSource == null)
            return;

        ambientSource.clip = ambientLoop;
        ambientSource.volume = ambientVolume * GameSettings.Volume;
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
        musicSource.volume = musicVolume * musicIntensity * GameSettings.Volume;
        musicSource.loop = true;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    void ApplyCombatMusicIntensity(float intensity)
    {
        musicIntensity = Mathf.Clamp(intensity, 0.5f, 2f);
        if (musicSource != null)
            musicSource.volume = musicVolume * musicIntensity * GameSettings.Volume;

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
    public void PlayUIBack() => PlayOneShot(uiBack != null ? uiBack : uiClick, uiVolume);
    public void PlayWeaponSwap() => PlayOneShot(weaponSwap, weaponVolume);

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
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * GameSettings.Volume);
    }

    public void PlayOneShotAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null)
            return;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volumeScale) * GameSettings.Volume);
    }

    public void ApplyMasterVolume(float volume)
    {
        float master = Mathf.Clamp01(volume);
        AudioListener.volume = master;
        if (ambientSource != null)
            ambientSource.volume = ambientVolume * master;
        if (musicSource != null)
            musicSource.volume = musicVolume * musicIntensity * master;
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }

    public static void UIClick() { if (Instance != null) Instance.PlayUIClick(); }
    public static void UIBack() { if (Instance != null) Instance.PlayUIBack(); }
    public static void WeaponSwap() { if (Instance != null) Instance.PlayWeaponSwap(); }
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
        Instance.PlayOneShotAt(clip, position, Instance.combatVolume);
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
        Instance.PlayOneShotAt(Instance.enemyVoiceBeep, position, 0.55f);
    }

    public static void BossVoice()
    {
        EnsureExists();
        Instance.PlayOneShot(Instance.bossVoiceBeep, 0.8f);
    }

    public static void SetCombatMusicIntensity(float intensity)
    {
        EnsureExists();
        Instance.ApplyCombatMusicIntensity(intensity);
    }

    public static void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance != null)
            Instance.PlayOneShot(clip, volumeScale);
        else if (clip != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volumeScale);
    }
}
