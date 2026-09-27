using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists selected campaign level across MainMenu → LevelDemo.
/// Levels: L1 tutorial, L2 mixed shotgun/rifle, L3 boss arena.
/// </summary>
public static class GameProgression
{
    const string LevelKey = "Vigilante.SelectedLevel";
    const string UnlockedKey = "Vigilante.UnlockedLevel";

    static bool startedThisSession;
    static float elapsedBeforeCurrentLevel;
    static float currentLevelStartedAt;
    static bool currentLevelTimerRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        startedThisSession = false;
        elapsedBeforeCurrentLevel = 0f;
        currentLevelStartedAt = 0f;
        currentLevelTimerRunning = false;
    }

    public static int SelectedLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, 1), 1, 3);
        set
        {
            PlayerPrefs.SetInt(LevelKey, Mathf.Clamp(value, 1, 3));
            PlayerPrefs.Save();
        }
    }

    public static int UnlockedLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, 3);
        set
        {
            PlayerPrefs.SetInt(UnlockedKey, Mathf.Clamp(value, 1, 3));
            PlayerPrefs.Save();
        }
    }

    public static int ActiveLevel
    {
        get
        {
            if (!startedThisSession)
                return 1;
            return SelectedLevel;
        }
    }

    public static float CurrentLevelElapsedSeconds => currentLevelTimerRunning
        ? Mathf.Max(0f, Time.time - currentLevelStartedAt)
        : 0f;

    public static float TotalElapsedSeconds => elapsedBeforeCurrentLevel + CurrentLevelElapsedSeconds;
    public static bool IsCurrentLevelTimerRunning => currentLevelTimerRunning;

    public static void BeginCurrentLevelTimer()
    {
        if (currentLevelTimerRunning)
            return;

        currentLevelStartedAt = Time.time;
        currentLevelTimerRunning = true;
    }

    /// <summary>Begins a fresh run at the selected level.</summary>
    public static void StartLevel(int level)
    {
        elapsedBeforeCurrentLevel = 0f;
        currentLevelTimerRunning = false;
        LoadLevel(level);
    }

    static void LoadLevel(int level)
    {
        SelectedLevel = level;
        startedThisSession = true;
        Time.timeScale = 1f;
        UIManager.ResetHealthBinding();
        SceneManager.LoadScene(1);
    }

    public static void RestartCurrentLevel()
    {
        int level = ActiveLevel;
        SelectedLevel = level;
        startedThisSession = true;
        currentLevelTimerRunning = false;
        Time.timeScale = 1f;
        UIManager.ResetHealthBinding();
        int scene = SceneManager.GetActiveScene().buildIndex;
        if (scene < 1)
            scene = 1;
        SceneManager.LoadScene(scene);
    }

    public static void CompleteCurrentLevel()
    {
        if (currentLevelTimerRunning)
        {
            elapsedBeforeCurrentLevel += CurrentLevelElapsedSeconds;
            currentLevelTimerRunning = false;
        }

        int current = ActiveLevel;
        int next = Mathf.Min(3, current + 1);
        if (next > UnlockedLevel)
            UnlockedLevel = next;
    }

    public static void ReturnToMainMenu()
    {
        startedThisSession = false;
        elapsedBeforeCurrentLevel = 0f;
        currentLevelTimerRunning = false;
        Time.timeScale = 1f;
        UIManager.ResetHealthBinding();
        SceneManager.LoadSceneAsync(0);
    }

    public static void AdvanceOrReturnToMenu()
    {
        CompleteCurrentLevel();
        int current = ActiveLevel;
        if (current >= 3)
        {
            startedThisSession = false;
            SceneManager.LoadSceneAsync(0);
            return;
        }

        // Continue the current run without clearing its accumulated level time.
        LoadLevel(current + 1);
    }
}
