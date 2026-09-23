using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies Borderlands-style outlines in combat scenes without touching surface materials.
/// </summary>
public static class CelOutlineBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnLoad()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryApplyDelayed();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryApplyDelayed();
    }

    public static void TryApplyDelayed()
    {
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return;

        GameObject host = new GameObject("~CelOutlineBootstrap");
        Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<Runner>().Run();
    }

    public static void TryApplyNow()
    {
        if (SceneManager.GetActiveScene().buildIndex < 1)
            return;

        CelMaterial.UpgradeSceneMaterials();
        CelOutline.RepairScene();
    }

    sealed class Runner : MonoBehaviour
    {
        public void Run()
        {
            StartCoroutine(ApplyRoutine());
        }

        IEnumerator ApplyRoutine()
        {
            // Wait a frame so spawners / profile tint finish first.
            yield return null;
            CelMaterial.UpgradeSceneMaterials();
            CelOutline.RepairScene();
            Destroy(gameObject);
        }
    }
}
