#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class VigilanteEnemyMenu
{
    [MenuItem("Vigilante/Create Tactical Enemy At Scene View")]
    public static void CreateEnemyAtSceneView()
    {
        Vector3 pos = GetSpawnPosition();
        GameObject enemy = EnemyFactory.Create(pos, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(enemy, "Create Tactical Enemy");
        Selection.activeGameObject = enemy;
    }

    [MenuItem("Vigilante/Create Cover Point At Scene View")]
    public static void CreateCoverPoint()
    {
        Vector3 pos = GetSpawnPosition();
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 face = view != null ? view.camera.transform.forward : Vector3.forward;
        face.y = 0f;
        CoverPoint cover = EnemyFactory.CreateCoverPoint(pos, face);
        Undo.RegisterCreatedObjectUndo(cover.gameObject, "Create Cover Point");
        Selection.activeGameObject = cover.gameObject;
    }

    [MenuItem("Vigilante/Create Enemy Spawn Point At Scene View")]
    public static void CreateEnemySpawnPoint()
    {
        Vector3 pos = GetSpawnPosition();
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 face = view != null ? view.camera.transform.forward : Vector3.forward;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f)
            face = Vector3.forward;

        EnemySpawnPoint spawn = EnemyFactory.CreateSpawnPoint(pos, face);
        Undo.RegisterCreatedObjectUndo(spawn.gameObject, "Create Enemy Spawn Point");
        Selection.activeGameObject = spawn.gameObject;
    }

    [MenuItem("Vigilante/Create Audio Manager")]
    public static void CreateAudioManager()
    {
        AudioManager existing = Object.FindFirstObjectByType<AudioManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Vigilante: AudioManager already in the scene — assign clips in the Inspector.");
            return;
        }

        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
        Undo.RegisterCreatedObjectUndo(go, "Create Audio Manager");
        Selection.activeGameObject = go;
        Debug.Log("Vigilante: AudioManager created. Assign UI / combat / swap / ambient clips, then save the scene.");
    }

    [MenuItem("Vigilante/Create Level Director")]
    public static void CreateLevelDirector()
    {
        LevelDirector existing = Object.FindFirstObjectByType<LevelDirector>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject go = new GameObject("LevelDirector");
        go.AddComponent<LevelDirector>();
        Undo.RegisterCreatedObjectUndo(go, "Create Level Director");
        Selection.activeGameObject = go;
    }

    [MenuItem("Vigilante/Create Shotgun Enemy At Scene View")]
    public static void CreateShotgunEnemy()
    {
        Vector3 pos = GetSpawnPosition();
        GameObject enemy = EnemyFactory.Create(pos, Quaternion.identity, EnemyArchetype.Shotgun);
        Undo.RegisterCreatedObjectUndo(enemy, "Create Shotgun Enemy");
        Selection.activeGameObject = enemy;
    }

    [MenuItem("Vigilante/Create Rifle Enemy At Scene View")]
    public static void CreateRifleEnemy()
    {
        Vector3 pos = GetSpawnPosition();
        GameObject enemy = EnemyFactory.Create(pos, Quaternion.identity, EnemyArchetype.Rifle);
        Undo.RegisterCreatedObjectUndo(enemy, "Create Rifle Enemy");
        Selection.activeGameObject = enemy;
    }

    [MenuItem("Vigilante/Create Boss Enemy At Scene View")]
    public static void CreateBossEnemy()
    {
        Vector3 pos = GetSpawnPosition();
        GameObject enemy = EnemyFactory.Create(pos, Quaternion.identity, EnemyArchetype.Boss);
        Undo.RegisterCreatedObjectUndo(enemy, "Create Boss Enemy");
        Selection.activeGameObject = enemy;
    }

    [MenuItem("Vigilante/Ensure Combat Bootstrap (Player Tag + Squad + NavMesh Bake)")]
    public static void EnsureBootstrap()
    {
        LevelCombatBootstrap.SetupPlayer();
        EnemySquad.EnsureExists();
        LevelCombatBootstrap.EnsureNavMesh();
        Debug.Log("Vigilante: Player tagged, EnemySquad ready, NavMesh build attempted.");
    }

    [MenuItem("Vigilante/Save Selected Enemy As Prefab")]
    public static void SaveEnemyPrefab()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<EnemyAI>() == null)
        {
            Debug.LogWarning("Select a GameObject with EnemyAI first.");
            return;
        }

        string folder = "Assets/Prefabs/Enemies";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");

        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Enemy.prefab");
        PrefabUtility.SaveAsPrefabAsset(selected, path);
        Debug.Log("Saved enemy prefab: " + path);
    }

    static Vector3 GetSpawnPosition()
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pos = view != null
            ? view.pivot
            : Vector3.zero;

        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            return hit.position;

        return pos;
    }
}
#endif
