#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the rifle and shotgun enemy visuals, locomotion/shoot controllers,
/// animation-timed firing setup, and hand-mounted weapons.
/// </summary>
public static class VigilanteRangedEnemySetup
{
    const string RifleFolder = "Assets/Prefabs/Enemies/RifleEnemy";
    const string ShotgunFolder = "Assets/Prefabs/Enemies/ShotgunEnemy";
    const string RiflePrefab = "Assets/Prefabs/Enemies/RifleEnemy Variant.prefab";
    const string ShotgunPrefab = "Assets/Prefabs/Enemies/ShotGunEnemy Variant.prefab";
    const string RifleWeapon = "Assets/Prefabs/Weapons/Assault Rifle.prefab";
    const string ShotgunWeapon = "Assets/Prefabs/Weapons/Pump Shotgun.prefab";
    const string SharedDeath = "Assets/Prefabs/Enemies/PistolEnemy/Death From The Front (1).fbx";

    [MenuItem("Vigilante/Setup Rifle And Shotgun Enemies")]
    public static void SetupAll()
    {
        SetupEnemy("Rifle", RifleFolder, RiflePrefab, "Rifle Idle.fbx", "Rifle Run.fbx",
            "Backwards Rifle Run.fbx", "Firing Rifle.fbx", RifleWeapon, 3,
            new Vector3(-0.02f, 0.05f, 0.02f), new Vector3(-90f, 90f, 0f), 0.225f);
        SetupEnemy("Shotgun", ShotgunFolder, ShotgunPrefab, "Rifle Idle.fbx", "Rifle Run.fbx",
            "Backwards Rifle Run.fbx", "Firing Shotgun.fbx", ShotgunWeapon, 10,
            new Vector3(-0.02f, 0.05f, 0.02f), new Vector3(-90f, 90f, 0f), 0.225f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vigilante: Rifle and shotgun enemies now use their matching animations, hand weapons, and animation-timed shots.");
    }

    [InitializeOnLoadMethod]
    static void RunPendingSetup()
    {
        EditorApplication.delayCall += RunPendingSetupWhenReady;
    }

    static void RunPendingSetupWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            return;
        }

        string marker = Application.dataPath + "/Prefabs/Enemies/.ranged_enemy_setup_pending";
        if (!File.Exists(marker))
            return;

        try { File.Delete(marker); }
        catch { return; }

        SetupAll();
    }

    static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.delayCall += RunPendingSetupWhenReady;
    }

    static void SetupEnemy(string label, string folder, string prefabPath,
        string idleName, string runName, string backName, string shootName,
        string weaponPath, int fireFrame, Vector3 weaponPosition, Vector3 weaponEuler, float weaponScale)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError("Vigilante: Missing enemy prefab " + prefabPath);
            return;
        }

        string idlePath = folder + "/" + idleName;
        string runPath = folder + "/" + runName;
        string backPath = folder + "/" + backName;
        string shootPath = folder + "/" + shootName;
        string controllerPath = folder + "/" + label + "Enemy.controller";

        Avatar avatar = ConfigureImports(idlePath, runPath, backPath, shootPath, SharedDeath);
        if (avatar == null)
        {
            Debug.LogError("Vigilante: Could not create an animation avatar for " + label + " enemy.");
            return;
        }

        AnimationClip idle = LoadClip(idlePath);
        AnimationClip run = LoadClip(runPath);
        AnimationClip back = LoadClip(backPath);
        AnimationClip shoot = LoadClip(shootPath);
        AnimationClip death = LoadClip(SharedDeath);
        if (idle == null || shoot == null)
        {
            Debug.LogError("Vigilante: Missing idle or firing clip for " + label + " enemy.");
            return;
        }

        AnimatorController controller = BuildController(controllerPath, idle, run, back, shoot, death);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ApplyVisuals(label, prefabRoot, folder, idlePath, avatar, controller, shoot,
                weaponPath, fireFrame, weaponPosition, weaponEuler, weaponScale);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static Avatar ConfigureImports(string idlePath, string runPath, string backPath, string shootPath, string deathPath)
    {
        ModelImporter idleImporter = AssetImporter.GetAtPath(idlePath) as ModelImporter;
        if (idleImporter == null)
            return null;

        idleImporter.animationType = ModelImporterAnimationType.Generic;
        idleImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        idleImporter.importAnimation = true;
        idleImporter.optimizeGameObjects = false;
        idleImporter.SaveAndReimport();
        Avatar avatar = FindAvatar(idlePath);

        string[] clipPaths = { runPath, backPath, shootPath, deathPath };
        for (int i = 0; i < clipPaths.Length; i++)
        {
            ModelImporter importer = AssetImporter.GetAtPath(clipPaths[i]) as ModelImporter;
            if (importer == null)
                continue;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            if (avatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            if (clips != null)
            {
                for (int c = 0; c < clips.Length; c++)
                {
                    clips[c].loopTime = false;
                    clips[c].loopPose = false;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        ModelImporter idleLoop = AssetImporter.GetAtPath(idlePath) as ModelImporter;
        if (idleLoop != null)
        {
            SetLoop(idleLoop, true);
            idleLoop.SaveAndReimport();
        }

        ModelImporter runLoop = AssetImporter.GetAtPath(runPath) as ModelImporter;
        if (runLoop != null)
        {
            SetLoop(runLoop, true);
            runLoop.SaveAndReimport();
        }
        ModelImporter backLoop = AssetImporter.GetAtPath(backPath) as ModelImporter;
        if (backLoop != null)
        {
            SetLoop(backLoop, true);
            backLoop.SaveAndReimport();
        }
        return FindAvatar(idlePath);
    }

    static void SetLoop(ModelImporter importer, bool loop)
    {
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.clipAnimations;
        if (clips == null)
            return;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = loop;
            clips[i].loopPose = loop;
        }
        importer.clipAnimations = clips;
    }

    static AnimationClip LoadClip(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                return clip;
        }
        return null;
    }

    static Avatar FindAvatar(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
            if (assets[i] is Avatar avatar)
                return avatar;
        return null;
    }

    static AnimatorController BuildController(string path, AnimationClip idle, AnimationClip run,
        AnimationClip back, AnimationClip shoot, AnimationClip death)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
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
        controller.AddParameter("DieVariant", AnimatorControllerParameterType.Float);
        controller.AddLayer("Base Layer");

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState stIdle = sm.AddState("Idle", new Vector3(300, 120, 0));
        stIdle.motion = idle;
        sm.defaultState = stIdle;
        AnimatorState stRun = sm.AddState("Run", new Vector3(300, 0, 0));
        stRun.motion = run != null ? run : idle;
        AnimatorState stBack = sm.AddState("WalkBack", new Vector3(80, 0, 0));
        stBack.motion = back != null ? back : stRun.motion;
        AnimatorState stShoot = sm.AddState("Shoot", new Vector3(300, 260, 0));
        stShoot.motion = shoot;
        AnimatorState stDie = sm.AddState("Die", new Vector3(560, 260, 0));
        stDie.motion = death != null ? death : shoot;

        AddBoolTransition(stIdle, stRun, "Moving", true, "Backing", false);
        AddBoolTransition(stIdle, stBack, "Backing", true);
        AddBoolTransition(stRun, stIdle, "Moving", false);
        AddBoolTransition(stRun, stBack, "Backing", true);
        AddBoolTransition(stBack, stIdle, "Moving", false);
        AddBoolTransition(stBack, stRun, "Backing", false, "Moving", true);

        AnimatorStateTransition fire = stIdle.AddTransition(stShoot);
        fire.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        fire.hasExitTime = false;
        fire.duration = 0.04f;
        AnimatorStateTransition finish = stShoot.AddTransition(stIdle);
        finish.hasExitTime = true;
        finish.exitTime = 1f;
        finish.duration = 0.04f;
        AnimatorStateTransition die = sm.AddAnyStateTransition(stDie);
        die.AddCondition(AnimatorConditionMode.If, 0, "Die");
        die.hasExitTime = false;
        die.duration = 0.05f;
        die.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    static void AddBoolTransition(AnimatorState from, AnimatorState to,
        string p0, bool v0, string p1, bool v1)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(v0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p0);
        t.AddCondition(v1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p1);
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    static void ApplyVisuals(string label, GameObject root, string folder, string idlePath, Avatar avatar,
        AnimatorController controller, AnimationClip shoot, string weaponPath, int fireFrame,
        Vector3 weaponPosition, Vector3 weaponEuler, float weaponScale)
    {
        Transform oldVisual = root.transform.Find(label + "Visual");
        if (oldVisual != null)
            UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
        Transform oldPistolVisual = root.transform.Find("PistolVisual");
        if (oldPistolVisual != null)
            UnityEngine.Object.DestroyImmediate(oldPistolVisual.gameObject);

        MeshRenderer capsuleRenderer = root.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null)
            capsuleRenderer.enabled = false;
        MeshFilter capsuleFilter = root.GetComponent<MeshFilter>();
        if (capsuleFilter != null)
            capsuleFilter.sharedMesh = null;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(idlePath);
        if (model == null)
            return;
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = label + "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Animator nested in visual.GetComponentsInChildren<Animator>(true))
            if (nested != animator)
                UnityEngine.Object.DestroyImmediate(nested);

        EnemyAnimator procedural = root.GetComponent<EnemyAnimator>();
        if (procedural != null)
            UnityEngine.Object.DestroyImmediate(procedural);
        EnemyMecanim mecanim = root.GetComponent<EnemyMecanim>();
        if (mecanim == null)
            mecanim = root.AddComponent<EnemyMecanim>();
        SerializedObject mecSo = new SerializedObject(mecanim);
        mecSo.FindProperty("shootClip").objectReferenceValue = shoot;
        mecSo.FindProperty("fireReleaseFrame").intValue = fireFrame;
        mecSo.ApplyModifiedPropertiesWithoutUndo();

        Transform hand = FindHand(visual.transform);
        GameObject weaponSource = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
        if (hand == null || weaponSource == null)
        {
            Debug.LogError("Vigilante: Could not find the hand or weapon asset for " + label + " enemy.");
            return;
        }

        Transform oldWeapon = hand.Find("EnemyWeapon");
        if (oldWeapon != null)
            UnityEngine.Object.DestroyImmediate(oldWeapon.gameObject);
        GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponSource);
        weapon.name = "EnemyWeapon";
        weapon.transform.SetParent(hand, false);
        weapon.transform.localPosition = weaponPosition;
        weapon.transform.localRotation = Quaternion.Euler(weaponEuler);
        weapon.transform.localScale = Vector3.one * weaponScale;
        foreach (Collider collider in weapon.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (MonoBehaviour behaviour in weapon.GetComponentsInChildren<MonoBehaviour>(true))
            UnityEngine.Object.DestroyImmediate(behaviour);
        foreach (AudioSource source in weapon.GetComponentsInChildren<AudioSource>(true))
            UnityEngine.Object.DestroyImmediate(source);

        Transform muzzle = weapon.transform.Find("Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleObject = new GameObject("Muzzle");
            muzzle = muzzleObject.transform;
            muzzle.SetParent(weapon.transform, false);
            muzzle.localPosition = FindMuzzleLocalPosition(weapon.transform);
            muzzle.localRotation = Quaternion.identity;
        }

        EnemyCombat combat = root.GetComponent<EnemyCombat>();
        if (combat != null)
        {
            SerializedObject combatSo = new SerializedObject(combat);
            SerializedProperty muzzleProp = combatSo.FindProperty("muzzle");
            if (muzzleProp != null)
            {
                muzzleProp.objectReferenceValue = muzzle;
                combatSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static Vector3 FindMuzzleLocalPosition(Transform weapon)
    {
        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Vector3(0f, 0f, 0.4f);

        bool hasBounds = false;
        Bounds localBounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds world = renderers[i].bounds;
            Vector3 e = world.extents;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(e, new Vector3(x, y, z));
                        Vector3 local = weapon.InverseTransformPoint(corner);
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(local, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                            localBounds.Encapsulate(local);
                    }
        }
        return hasBounds
            ? new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z + 0.025f)
            : new Vector3(0f, 0f, 0.4f);
    }

    static Transform FindHand(Transform root)
    {
        string[] names = { "mixamorig:RightHand", "RightHand", "Hand_R", "hand_r" };
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == names[n])
                    return all[i];
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            if (n.IndexOf("RightHand", StringComparison.OrdinalIgnoreCase) >= 0
                && n.IndexOf("Index", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Thumb", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Pinky", StringComparison.OrdinalIgnoreCase) < 0)
                return all[i];
        }
        return null;
    }
}
#endif
