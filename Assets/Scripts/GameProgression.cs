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

    public static void StartLevel(int level)
    {
        SelectedLevel = level;
        SceneManager.LoadSceneAsync(1);
    }

    public static void CompleteCurrentLevel()
    {
        int next = Mathf.Min(3, SelectedLevel + 1);
        if (next > UnlockedLevel)
            UnlockedLevel = next;
    }

    public static void AdvanceOrReturnToMenu()
    {
        CompleteCurrentLevel();
        if (SelectedLevel >= 3)
        {
            SceneManager.LoadSceneAsync(0);
            return;
        }

        SelectedLevel += 1;
        SceneManager.LoadSceneAsync(1);
    }
}
