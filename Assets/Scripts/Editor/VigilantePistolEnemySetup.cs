#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-shot setup: builds PistolEnemy animator controller from the FBX pack,
/// swaps the capsule for the animated mesh, wires EnemyMecanim, and attaches
/// the tactical pistol to the right hand.
/// Menu: Vigilante/Setup Pistol Enemy
/// </summary>
public static class VigilantePistolEnemySetup
{
    const string Folder = "Assets/Prefabs/Enemies/PistolEnemy";
    const string PrefabPath = "Assets/Prefabs/Enemies/PistolEnemy.prefab";
    const string ControllerPath = "Assets/Prefabs/Enemies/PistolEnemy/PistolEnemy.controller";
    const string CharacterPath = "Assets/Prefabs/Enemies/character 02.fbx";

    const string IdlePath = Folder + "/Pistol Idle (2).fbx";
    const string RunPath = Folder + "/Pistol Run (1).fbx";
    const string StrafePath = Folder + "/Pistol Strafe.fbx";
    const string WalkBackPath = Folder + "/Pistol Walk Backward.fbx";
    const string ShootPath = Folder + "/Shooting.fbx";
    const string HitPath = Folder + "/Hit Reaction (1).fbx";
    const string DeathPath = Folder + "/Death (1).fbx";
    const string DeathFrontPath = Folder + "/Death From The Front (1).fbx";
    const string DeathBackPath = Folder + "/Death From The Back.fbx";
    const string DeathRightPath = Folder + "/Death From Right.fbx";
    const string DeathFrontHsPath = Folder + "/Death From Front Headshot.fbx";
    const string DeathBackHsPath = Folder + "/Death From Back Headshot.fbx";
    const string PistolWeaponPath = "Assets/Prefabs/Weapons/Tactical Pistol.fbx";

    [MenuItem("Vigilante/Setup Pistol Enemy")]
    public static void Setup()
    {
        if (!EnsurePistolPrefabExists())
            return;

        ConfigureAllFbxImports();
        AssetDatabase.Refresh();

        AnimatorController controller = BuildController();
        if (controller == null)
        {
            Debug.LogError("Vigilante: Failed to build PistolEnemy animator controller.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyVisuals(prefabRoot, controller);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        StripPistolVisualFromNonPistolPrefabs();
        Debug.Log("Vigilante: PistolEnemy mesh + animations applied. Shoot clip = Shooting.fbx.");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
    }

    static bool EnsurePistolPrefabExists()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return true;

        Debug.LogError("Vigilante: Missing " + PrefabPath + " — create your PistolEnemy capsule prefab first.");
        return false;
    }

    [MenuItem("Vigilante/Strip Pistol Mesh From Other Enemies")]
    public static void StripVariantsMenu()
    {
        StripPistolVisualFromNonPistolPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Rifle/Shotgun are prefab variants of PistolEnemy — remove inherited pistol mesh/Mecanim.
    /// </summary>
    public static void StripPistolVisualFromNonPistolPrefabs()
    {
        string[] variants =
        {
            "Assets/Prefabs/Enemies/PistolEnemy1.prefab",
            "Assets/Prefabs/Enemies/RifleEnemy Variant.prefab",
            "Assets/Prefabs/Enemies/ShotGunEnemy Variant.prefab",
            "Assets/Prefabs/Enemies/BatEnemy.prefab"
        };

        Mesh capsuleMesh = null;
        GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsuleMesh = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tempCapsule);

        for (int i = 0; i < variants.Length; i++)
        {
            string path = variants[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("PistolVisual");
                if (visual != null)
                    Object.DestroyImmediate(visual.gameObject);

                EnemyMecanim mecanim = root.GetComponent<EnemyMecanim>();
                if (mecanim != null)
                    Object.DestroyImmediate(mecanim);

                EnemyHandWeapon handWeapon = root.GetComponent<EnemyHandWeapon>();
                if (handWeapon != null)
                    Object.DestroyImmediate(handWeapon);

                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                for (int a = 0; a < animators.Length; a++)
                {
                    if (animators[a] != null)
                        Object.DestroyImmediate(animators[a]);
                }

                MeshFilter filter = root.GetComponent<MeshFilter>();
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (filter != null && capsuleMesh != null)
                    filter.sharedMesh = capsuleMesh;
                if (renderer != null)
                    renderer.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("Vigilante: Stripped pistol mesh from " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>
    /// One-shot markers: .setup_pending runs full Setup; .strip_variants_pending only strips variants.
    /// </summary>
    [InitializeOnLoadMethod]
    static void RunPendingSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string stripPath = Application.dataPath + "/Prefabs/Enemies/PistolEnemy/.strip_variants_pending";
            if (File.Exists(stripPath))
            {
                try { File.Delete(stripPath); }
                catch { return; }
                StripPistolVisualFromNonPistolPrefabs();
                AssetDatabase.SaveAssets();
                return;
            }

            string diskPath = Application.dataPath + "/Prefabs/Enemies/PistolEnemy/.setup_pending";
            if (!File.Exists(diskPath))
                return;

            try { File.Delete(diskPath); }
            catch { return; }

            Debug.Log("Vigilante: Pending PistolEnemy setup detected — applying mesh + animations.");
            Setup();
        };
    }

    static void ConfigureAllFbxImports()
    {
        string[] animPaths =
        {
            IdlePath, RunPath, StrafePath, WalkBackPath, ShootPath, HitPath,
            DeathPath, DeathFrontPath, DeathBackPath, DeathRightPath,
            DeathFrontHsPath, DeathBackHsPath
        };

        // Avatar source: Idle pack (same skeleton as all pistol clips).
        ModelImporter idleImporter = AssetImporter.GetAtPath(IdlePath) as ModelImporter;
        if (idleImporter != null)
        {
            idleImporter.animationType = ModelImporterAnimationType.Generic;
            idleImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            idleImporter.importAnimation = true;
            idleImporter.SaveAndReimport();
        }

        Avatar sourceAvatar = FindAvatar(IdlePath);

        // Character mesh shares Idle avatar when available.
        ModelImporter charImporter = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
        if (charImporter != null)
        {
            charImporter.animationType = ModelImporterAnimationType.Generic;
            charImporter.importAnimation = false;
            if (sourceAvatar != null)
            {
                charImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                charImporter.sourceAvatar = sourceAvatar;
            }
            else
            {
                charImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            charImporter.SaveAndReimport();
            if (sourceAvatar == null)
                sourceAvatar = FindAvatar(CharacterPath);
        }

        for (int i = 0; i < animPaths.Length; i++)
        {
            string path = animPaths[i];
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;

            if (path == IdlePath)
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            else if (sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
            }

            bool isLoco = path == IdlePath || path == RunPath || path == StrafePath || path == WalkBackPath;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int c = 0; c < clips.Length; c++)
                {
                    clips[c].loopTime = isLoco;
                    clips[c].loopPose = isLoco;
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }
    }

    static AnimationClip LoadClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                continue;
            best = clip;
            // Prefer non-preview, first real clip
            break;
        }
        return best;
    }

    static AnimatorController BuildController()
    {
        AnimationClip idle = LoadClip(IdlePath);
        AnimationClip run = LoadClip(RunPath);
        AnimationClip strafe = LoadClip(StrafePath);
        AnimationClip walkBack = LoadClip(WalkBackPath);
        AnimationClip shoot = LoadClip(ShootPath);
        AnimationClip death = LoadClip(DeathPath);
        AnimationClip deathFront = LoadClip(DeathFrontPath);
        AnimationClip deathBack = LoadClip(DeathBackPath);
        AnimationClip deathRight = LoadClip(DeathRightPath);
        AnimationClip deathFrontHs = LoadClip(DeathFrontHsPath);
        AnimationClip deathBackHs = LoadClip(DeathBackHsPath);

        if (idle == null)
        {
            Debug.LogError("Vigilante: Missing Idle clip at " + IdlePath);
            return null;
        }

        if (shoot == null)
            Debug.LogWarning("Vigilante: Missing Shooting clip at " + ShootPath + " — Fire will use Idle.");

        if (File.Exists(ControllerPath.Replace("Assets/", Application.dataPath + "/"))
            || AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Clear auto-created defaults and rebuild.
        while (controller.layers.Length > 0)
            controller.RemoveLayer(0);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Forward", AnimatorControllerParameterType.Float);
        controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Backing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Strafing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        // Blend trees require a float parameter.
        controller.AddParameter("DieVariant", AnimatorControllerParameterType.Float);

        controller.AddLayer("Base Layer");
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState stIdle = sm.AddState("Idle", new Vector3(300, 120, 0));
        stIdle.motion = idle;
        sm.defaultState = stIdle;

        AnimatorState stRun = sm.AddState("Run", new Vector3(300, 0, 0));
        stRun.motion = run != null ? run : idle;

        AnimatorState stStrafe = sm.AddState("Strafe", new Vector3(520, 0, 0));
        stStrafe.motion = strafe != null ? strafe : stRun.motion;

        AnimatorState stBack = sm.AddState("WalkBack", new Vector3(80, 0, 0));
        stBack.motion = walkBack != null ? walkBack : stRun.motion;

        AnimatorState stShoot = sm.AddState("Shoot", new Vector3(300, 260, 0));
        stShoot.motion = shoot != null ? shoot : idle;

        AnimatorState stDie = sm.AddState("Die", new Vector3(560, 260, 0));
        BlendTree dieTree = new BlendTree
        {
            name = "DieTree",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "DieVariant",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(dieTree, controller);
        AnimationClip dieFallback = death != null ? death : (deathFront != null ? deathFront : idle);
        dieTree.AddChild(death != null ? death : dieFallback, 0f);
        dieTree.AddChild(deathFront != null ? deathFront : dieFallback, 1f);
        dieTree.AddChild(deathBack != null ? deathBack : dieFallback, 2f);
        dieTree.AddChild(deathRight != null ? deathRight : dieFallback, 3f);
        dieTree.AddChild(deathFrontHs != null ? deathFrontHs : dieFallback, 4f);
        dieTree.AddChild(deathBackHs != null ? deathBackHs : dieFallback, 5f);
        stDie.motion = dieTree;

        // Locomotion transitions
        AddBoolTransition(stIdle, stRun, "Moving", true, "Strafing", false, "Backing", false);
        AddBoolTransition(stIdle, stStrafe, "Strafing", true);
        AddBoolTransition(stIdle, stBack, "Backing", true);

        AddBoolTransition(stRun, stIdle, "Moving", false);
        AddBoolTransition(stRun, stStrafe, "Strafing", true);
        AddBoolTransition(stRun, stBack, "Backing", true);

        AddBoolTransition(stStrafe, stIdle, "Moving", false);
        AddBoolTransition(stStrafe, stRun, "Strafing", false, "Backing", false, "Moving", true);
        AddBoolTransition(stStrafe, stBack, "Backing", true);

        AddBoolTransition(stBack, stIdle, "Moving", false);
        AddBoolTransition(stBack, stRun, "Backing", false, "Strafing", false, "Moving", true);
        AddBoolTransition(stBack, stStrafe, "Strafing", true);

        // Fire only from Idle (must be planted) → Shoot → Idle
        AnimatorStateTransition idleToShoot = stIdle.AddTransition(stShoot);
        idleToShoot.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        idleToShoot.hasExitTime = false;
        idleToShoot.duration = 0.05f;

        AnimatorStateTransition shootToIdle = stShoot.AddTransition(stIdle);
        shootToIdle.hasExitTime = true;
        shootToIdle.exitTime = 0.85f;
        shootToIdle.duration = 0.08f;

        // Die from any — terminal state
        AddTriggerAny(sm, stDie, "Die");
        stDie.writeDefaultValues = true;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddBoolTransition(AnimatorState from, AnimatorState to, string boolName, bool value)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, boolName);
        t.hasExitTime = false;
        t.duration = 0.12f;
    }

    static void AddBoolTransition(
        AnimatorState from, AnimatorState to,
        string b0, bool v0, string b1, bool v1)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(v0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b0);
        t.AddCondition(v1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b1);
        t.hasExitTime = false;
        t.duration = 0.12f;
    }

    static void AddBoolTransition(
        AnimatorState from, AnimatorState to,
        string b0, bool v0, string b1, bool v1, string b2, bool v2)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(v0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b0);
        t.AddCondition(v1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b1);
        t.AddCondition(v2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b2);
        t.hasExitTime = false;
        t.duration = 0.12f;
    }

    static void AddTriggerAny(AnimatorStateMachine sm, AnimatorState dest, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(dest);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = false;
    }

    static void ApplyVisuals(GameObject root, AnimatorController controller)
    {
        // Hide capsule mesh, keep collider for gameplay.
        MeshRenderer capsuleRend = root.GetComponent<MeshRenderer>();
        if (capsuleRend != null)
            capsuleRend.enabled = false;
        MeshFilter capsuleFilter = root.GetComponent<MeshFilter>();
        if (capsuleFilter != null)
            capsuleFilter.sharedMesh = null;

        // Hide leftover bat prop on gun enemies.
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == root.transform)
                continue;
            if (t.name.IndexOf("Bat", System.StringComparison.OrdinalIgnoreCase) >= 0
                && t.name.IndexOf("BatEnemy", System.StringComparison.OrdinalIgnoreCase) < 0)
                t.gameObject.SetActive(false);
        }

        // Remove old visual instance if re-running setup.
        Transform old = root.transform.Find("PistolVisual");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        // Prefer Idle FBX when it includes a mesh (same skeleton as clips).
        // Fall back to character 02 mesh retargeted to the Idle avatar.
        GameObject idleSource = AssetDatabase.LoadAssetAtPath<GameObject>(IdlePath);
        GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        GameObject modelSource = idleSource;
        if (idleSource == null || idleSource.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            modelSource = meshSource != null ? meshSource : idleSource;
        if (modelSource == null)
        {
            Debug.LogError("Vigilante: No character mesh found for PistolEnemy.");
            return;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelSource);
        visual.name = "PistolVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // Strip colliders on visual — root capsule handles physics.
        Collider[] cols = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.DestroyImmediate(cols[i]);

        // Animator must live on the skinned hierarchy root for Generic clips.
        Animator anim = visual.GetComponent<Animator>();
        if (anim == null)
            anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        Avatar avatar = FindAvatar(IdlePath);
        if (avatar == null)
            avatar = FindAvatar(CharacterPath);
        if (avatar != null)
            anim.avatar = avatar;

        // Remove duplicate Animators deeper in the hierarchy.
        Animator[] nested = visual.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < nested.Length; i++)
        {
            if (nested[i] != null && nested[i] != anim)
                Object.DestroyImmediate(nested[i]);
        }

        // Drive from root so NavMeshAgent velocity is available.
        EnemyMecanim mecanim = root.GetComponent<EnemyMecanim>();
        if (mecanim == null)
            mecanim = root.AddComponent<EnemyMecanim>();

        AnimationClip shootClip = LoadClip(ShootPath);
        if (shootClip != null)
            mecanim.SetShootClip(shootClip, 20);

        EnemyHandWeapon handWeapon = root.GetComponent<EnemyHandWeapon>();
        if (handWeapon == null)
            handWeapon = root.AddComponent<EnemyHandWeapon>();
        SerializedObject handSo = new SerializedObject(handWeapon);
        SerializedProperty prefabProp = handSo.FindProperty("pistolPrefab");
        if (prefabProp != null)
        {
            prefabProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PistolWeaponPath);
            handSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Persist shoot clip + release frame on the component.
        SerializedObject mecSo = new SerializedObject(mecanim);
        SerializedProperty shootProp = mecSo.FindProperty("shootClip");
        if (shootProp != null && shootClip != null)
            shootProp.objectReferenceValue = shootClip;
        SerializedProperty frameProp = mecSo.FindProperty("fireReleaseFrame");
        if (frameProp != null)
            frameProp.intValue = 20;
        mecSo.ApplyModifiedPropertiesWithoutUndo();

        // Root may also have had a leftover Animator — strip it; child owns playback.
        Animator rootAnim = root.GetComponent<Animator>();
        if (rootAnim != null && rootAnim != anim)
            Object.DestroyImmediate(rootAnim);

        EnemyAnimator procedural = root.GetComponent<EnemyAnimator>();
        if (procedural != null)
            Object.DestroyImmediate(procedural);

        // Keep imported mesh colours/textures — do not flat-tint the skinned character.
        AttachPistolToHand(root, visual);
    }

    static void AttachPistolToHand(GameObject root, GameObject visual)
    {
        Transform hand = FindBone(visual.transform, "mixamorig:RightHand", "RightHand", "Hand_R", "hand_r");
        if (hand == null)
        {
            Debug.LogWarning("Vigilante: Could not find RightHand bone on PistolEnemy visual.");
            return;
        }

        Transform existing = hand.Find("EnemyPistol");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject weaponSource = AssetDatabase.LoadAssetAtPath<GameObject>(PistolWeaponPath);
        if (weaponSource == null)
        {
            Debug.LogWarning("Vigilante: Missing pistol mesh at " + PistolWeaponPath);
            return;
        }

        GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(weaponSource);
        gun.name = "EnemyPistol";
        gun.transform.SetParent(hand, false);
        // Grip pose for Mixamo right hand + Tactical Pistol FBX forward axis.
        gun.transform.localPosition = new Vector3(-0.02f, 0.05f, 0.02f);
        gun.transform.localRotation = Quaternion.Euler(-90f, 90f, 0f);
        gun.transform.localScale = Vector3.one * 0.45f;

        Collider[] cols = gun.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.DestroyImmediate(cols[i]);

        // Strip player weapon scripts if the prefab variant carried any.
        MonoBehaviour[] behaviours = gun.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null)
                continue;
            string typeName = behaviours[i].GetType().Name;
            if (typeName == "Pistol" || typeName == "Weapon" || typeName == "WeaponViewMotion"
                || typeName == "AudioSource")
                Object.DestroyImmediate(behaviours[i]);
        }

        Transform muzzle = gun.transform.Find("Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(gun.transform, false);
            // Along typical pistol barrel (local +Z after grip rotation).
            muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            muzzle = muzzleGo.transform;
        }

        EnemyCombat combat = root.GetComponent<EnemyCombat>();
        if (combat != null)
        {
            SerializedObject so = new SerializedObject(combat);
            SerializedProperty muzzleProp = so.FindProperty("muzzle");
            if (muzzleProp != null)
            {
                muzzleProp.objectReferenceValue = muzzle;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static Transform FindBone(Transform root, params string[] names)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
        {
            string want = names[n];
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == want)
                    return all[i];
            }
        }

        // Fuzzy fallback.
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            string n = all[i].name;
            if (n.IndexOf("RightHand", System.StringComparison.OrdinalIgnoreCase) >= 0
                && n.IndexOf("Index", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Thumb", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Middle", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Ring", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Pinky", System.StringComparison.OrdinalIgnoreCase) < 0)
                return all[i];
        }
        return null;
    }

    static Avatar FindAvatar(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar av)
                return av;
        }
        return null;
    }
}
#endif
