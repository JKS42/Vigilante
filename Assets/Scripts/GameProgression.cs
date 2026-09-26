using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists selected campaign level across MainMenu → LevelDemo.
/// Levels: 1 tutorial pistols, 2 mixed shotgun/rifle (bigger), 3 boss arena.
/// </summary>
public static class GameProgression
{
    const string LevelKey = "Vigilante.SelectedLevel";
    const string UnlockedKey = "Vigilante.UnlockedLevel";

    static bool startedThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        startedThisSession = false;
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

    /// <summary>
    /// Level to run for this play. If nothing was started via the menu this
    /// session, force Level 1 so stale PlayerPrefs don't skip the tutorial.
    /// </summary>
    public static int ActiveLevel
    {
        get
        {
            if (!startedThisSession)
                return 1;
            return SelectedLevel;
        }
    }

    public static void StartLevel(int level)
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
        Time.timeScale = 1f;
        UIManager.ResetHealthBinding();
        int scene = SceneManager.GetActiveScene().buildIndex;
        if (scene < 1)
            scene = 1;
        SceneManager.LoadScene(scene);
    }

    public static void CompleteCurrentLevel()
    {
        int current = ActiveLevel;
        int next = Mathf.Min(3, current + 1);
        if (next > UnlockedLevel)
            UnlockedLevel = next;
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

        StartLevel(current + 1);
    }
}
