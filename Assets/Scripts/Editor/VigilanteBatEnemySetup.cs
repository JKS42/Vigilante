#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the bat enemy from the baseball clips: run on the legs, idle on the torso,
/// and the cricket bat parented to the right hand.
/// Menu: Vigilante/Setup Bat Enemy
/// </summary>
public static class VigilanteBatEnemySetup
{
    const string Folder = "Assets/Prefabs/Enemies/BatEnemy";
    const string PrefabPath = "Assets/Resources/Enemies/BatEnemy.prefab";
    const string SourcePrefabPath = "Assets/Prefabs/Enemies/PistolEnemy.prefab";
    const string ControllerPath = Folder + "/BatEnemy.controller";
    const string MaskPath = Folder + "/UpperIdle.mask";
    const string CharacterPath = "Assets/Prefabs/Enemies/character 02.fbx";
    const string BatPath = "Assets/Prefabs/Weapons/Bat.fbx";

    const string IdlePath = Folder + "/Baseball Idle.fbx";
    const string RunPath = Folder + "/Running.fbx";
    const string BackPath = Folder + "/Run Backward.fbx";
    const string StrikePath = Folder + "/Baseball Strike.fbx";
    const string DeathPath = Folder + "/Two Handed Sword Death.fbx";

    [MenuItem("Vigilante/Setup Bat Enemy")]
    public static void Setup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
        {
            Debug.LogError("Vigilante: Missing " + SourcePrefabPath);
            return;
        }

        ConfigureImports();
        AssetDatabase.Refresh();

        AnimatorController controller = BuildController();
        if (controller == null)
            return;

        Directory.CreateDirectory(Application.dataPath + "/Resources/Enemies");
        AssetDatabase.Refresh();

        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
        try
        {
            ApplyVisuals(root, controller);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vigilante: Bat enemy ready. Legs use the run clips, torso stays on baseball idle, bat is on the right hand.");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
    }

    [InitializeOnLoadMethod]
    static void RunPendingSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string diskPath = Application.dataPath + "/Prefabs/Enemies/BatEnemy/.setup_pending";
            if (!File.Exists(diskPath))
                return;

            try { File.Delete(diskPath); }
            catch { return; }

            Debug.Log("Vigilante: Pending bat enemy setup — applying mesh, animations, and cricket bat.");
            Setup();
        };
    }

    static void ConfigureImports()
    {
        string[] paths = { IdlePath, RunPath, BackPath, StrikePath, DeathPath };
        ModelImporter idleImporter = AssetImporter.GetAtPath(IdlePath) as ModelImporter;
        if (idleImporter != null)
        {
            idleImporter.animationType = ModelImporterAnimationType.Generic;
            idleImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            idleImporter.importAnimation = true;
            idleImporter.SaveAndReimport();
        }

        Avatar sourceAvatar = FindAvatar(IdlePath);
        if (sourceAvatar == null)
            sourceAvatar = FindAvatar("Assets/Prefabs/Enemies/PistolEnemy/Pistol Idle (2).fbx");

        ModelImporter character = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
        if (character != null && sourceAvatar != null)
        {
            character.animationType = ModelImporterAnimationType.Generic;
            character.importAnimation = false;
            character.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            character.sourceAvatar = sourceAvatar;
            character.SaveAndReimport();
        }

        for (int i = 0; i < paths.Length; i++)
        {
            ModelImporter importer = AssetImporter.GetAtPath(paths[i]) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            if (paths[i] == IdlePath)
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            else if (sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
            }

            bool loop = paths[i] == IdlePath || paths[i] == RunPath || paths[i] == BackPath;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int c = 0; c < clips.Length; c++)
                {
                    clips[c].loopTime = loop;
                    clips[c].loopPose = loop;
                    if (paths[i] == StrikePath)
                        clips[c].lastFrame = 120f;
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }
    }

    static AnimatorController BuildController()
    {
        AnimationClip idle = LoadClip(IdlePath);
        AnimationClip run = LoadClip(RunPath);
        AnimationClip back = LoadClip(BackPath);
        AnimationClip strike = LoadClip(StrikePath);
        AnimationClip death = LoadClip(DeathPath);
        if (idle == null)
        {
            Debug.LogError("Vigilante: Missing baseball idle at " + IdlePath);
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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

        AnimatorState stIdle = sm.AddState("Idle", new Vector3(280, 80, 0));
        stIdle.motion = idle;
        stIdle.writeDefaultValues = false;
        sm.defaultState = stIdle;

        AnimatorState stRun = sm.AddState("Run", new Vector3(280, -40, 0));
        stRun.motion = run != null ? run : idle;
        stRun.writeDefaultValues = false;

        AnimatorState stBack = sm.AddState("WalkBack", new Vector3(80, -40, 0));
        stBack.motion = back != null ? back : stRun.motion;
        stBack.writeDefaultValues = false;

        AnimatorState stStrike = sm.AddState("Strike", new Vector3(480, 80, 0));
        stStrike.motion = strike != null ? strike : idle;
        stStrike.writeDefaultValues = false;

        AnimatorState stDie = sm.AddState("Die", new Vector3(480, 200, 0));
        stDie.motion = death != null ? death : idle;
        stDie.writeDefaultValues = false;

        AddBool(stIdle, stRun, "Moving", true, "Backing", false);
        AddBool(stIdle, stBack, "Backing", true);
        AddBool(stRun, stIdle, "Moving", false);
        AddBool(stRun, stBack, "Backing", true);
        AddBool(stBack, stIdle, "Moving", false);
        AddBool(stBack, stRun, "Backing", false, "Moving", true);

        AnimatorStateTransition anyStrike = sm.AddAnyStateTransition(stStrike);
        anyStrike.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        anyStrike.hasExitTime = false;
        anyStrike.duration = 0.05f;
        anyStrike.canTransitionToSelf = false;

        AnimatorStateTransition strikeToIdle = stStrike.AddTransition(stIdle);
        strikeToIdle.hasExitTime = true;
        strikeToIdle.exitTime = 0.9f;
        strikeToIdle.duration = 0.08f;

        AnimatorStateTransition anyDie = sm.AddAnyStateTransition(stDie);
        anyDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        anyDie.hasExitTime = false;
        anyDie.duration = 0.05f;
        anyDie.canTransitionToSelf = false;

        AvatarMask mask = BuildUpperMask();
        controller.AddLayer("UpperIdle");
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer upper = layers[layers.Length - 1];
        upper.name = "UpperIdle";
        upper.avatarMask = mask;
        upper.blendingMode = AnimatorLayerBlendingMode.Override;
        upper.defaultWeight = 0f;
        AnimatorState upperIdle = upper.stateMachine.AddState("UpperIdle", new Vector3(280, 40, 0));
        upperIdle.motion = idle;
        upperIdle.writeDefaultValues = false;
        upper.stateMachine.defaultState = upperIdle;
        layers[layers.Length - 1] = upper;
        controller.layers = layers;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddBool(AnimatorState from, AnimatorState to, string name, bool value)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, name);
        t.hasExitTime = false;
        t.duration = 0.12f;
    }

    static void AddBool(AnimatorState from, AnimatorState to, string a, bool av, string b, bool bv)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(av ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, a);
        t.AddCondition(bv ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, b);
        t.hasExitTime = false;
        t.duration = 0.12f;
    }

    static AvatarMask BuildUpperMask()
    {
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath) != null)
            AssetDatabase.DeleteAsset(MaskPath);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(IdlePath);
        if (model == null || model.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);

        AvatarMask mask = new AvatarMask();
        List<string> paths = new List<string>();
        if (model != null)
            CollectPaths(model.transform, "", paths);

        mask.transformCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            mask.SetTransformPath(i, paths[i]);
            mask.SetTransformActive(i, IsUpperBody(paths[i]));
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

        AssetDatabase.CreateAsset(mask, MaskPath);
        return mask;
    }

    static void CollectPaths(Transform t, string path, List<string> paths)
    {
        paths.Add(path);
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            string next = string.IsNullOrEmpty(path) ? child.name : path + "/" + child.name;
            CollectPaths(child, next, paths);
        }
    }

    static bool IsUpperBody(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string name = path;
        int slash = path.LastIndexOf('/');
        if (slash >= 0)
            name = path.Substring(slash + 1);
        int colon = name.LastIndexOf(':');
        if (colon >= 0)
            name = name.Substring(colon + 1);

        if (name.StartsWith("Spine") || name.StartsWith("Neck") || name.StartsWith("Head"))
            return true;
        if (name.Contains("Shoulder") || name.Contains("Arm") || name.Contains("Hand"))
            return true;
        if (name.Contains("Finger") || name.Contains("Thumb") || name.Contains("Index")
            || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Pinky"))
            return true;
        return false;
    }

    static void ApplyVisuals(GameObject root, AnimatorController controller)
    {
        MeshRenderer capsule = root.GetComponent<MeshRenderer>();
        if (capsule != null)
            capsule.enabled = false;
        MeshFilter filter = root.GetComponent<MeshFilter>();
        if (filter != null)
            filter.sharedMesh = null;

        Transform oldGun = root.transform.Find("PistolVisual");
        if (oldGun != null)
            Object.DestroyImmediate(oldGun.gameObject);
        Transform oldBatVisual = root.transform.Find("BatVisual");
        if (oldBatVisual != null)
            Object.DestroyImmediate(oldBatVisual.gameObject);

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == root.transform)
                continue;
            if (t.name == "Bat")
                t.gameObject.SetActive(false);
        }

        GameObject idleSource = AssetDatabase.LoadAssetAtPath<GameObject>(IdlePath);
        GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        GameObject modelSource = idleSource;
        if (idleSource == null || idleSource.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            modelSource = meshSource != null ? meshSource : idleSource;
        if (modelSource == null)
        {
            Debug.LogError("Vigilante: No character mesh for the bat enemy.");
            return;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelSource);
        visual.name = "BatVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Collider[] cols = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.DestroyImmediate(cols[i]);

        Animator anim = visual.GetComponent<Animator>();
        if (anim == null)
            anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Avatar avatar = FindAvatar(IdlePath);
        if (avatar == null)
            avatar = FindAvatar(CharacterPath);
        if (avatar != null)
            anim.avatar = avatar;

        Animator[] nested = visual.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < nested.Length; i++)
        {
            if (nested[i] != null && nested[i] != anim)
                Object.DestroyImmediate(nested[i]);
        }

        Animator rootAnim = root.GetComponent<Animator>();
        if (rootAnim != null && rootAnim != anim)
            Object.DestroyImmediate(rootAnim);

        EnemyMecanim mecanim = root.GetComponent<EnemyMecanim>();
        if (mecanim == null)
            mecanim = root.AddComponent<EnemyMecanim>();
        AnimationClip strike = LoadClip(StrikePath);
        if (strike != null)
            mecanim.SetShootClip(strike, 12);

        EnemyHandWeapon hand = root.GetComponent<EnemyHandWeapon>();
        if (hand == null)
            hand = root.AddComponent<EnemyHandWeapon>();
        SerializedObject handSo = new SerializedObject(hand);
        SerializedProperty batProp = handSo.FindProperty("batPrefab");
        if (batProp != null)
            batProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BatPath);
        handSo.ApplyModifiedPropertiesWithoutUndo();

        AttachBat(visual);
    }

    static void AttachBat(GameObject visual)
    {
        Transform hand = FindBone(visual.transform, "mixamorig:RightHand", "RightHand", "Hand_R", "hand_r");
        if (hand == null)
        {
            Debug.LogWarning("Vigilante: Bat enemy has no right-hand bone.");
            return;
        }

        Transform existing = hand.Find("EnemyBat");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject batSource = AssetDatabase.LoadAssetAtPath<GameObject>(BatPath);
        if (batSource == null)
        {
            Debug.LogWarning("Vigilante: Missing cricket bat at " + BatPath);
            return;
        }

        GameObject bat = (GameObject)PrefabUtility.InstantiatePrefab(batSource);
        bat.name = "EnemyBat";
        bat.transform.SetParent(hand, false);
        bat.transform.localPosition = new Vector3(0f, 0.09f, 0.02f);
        bat.transform.localRotation = Quaternion.Euler(8f, 90f, 90f);
        bat.transform.localScale = Vector3.one;

        Collider[] cols = bat.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.DestroyImmediate(cols[i]);
    }

    static Transform FindBone(Transform root, params string[] names)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == names[n])
                    return all[i];
            }
        }

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

    static AnimationClip LoadClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                continue;
            return clip;
        }
        return null;
    }

    static Avatar FindAvatar(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null)
                return avatar;
        }
        return null;
    }
}
#endif
