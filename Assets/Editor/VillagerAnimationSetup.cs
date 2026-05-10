using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public static class VillagerAnimationSetup
{
    private const string BaseFolder = "Assets/Animations/Villager";
    private const string ControllerPath = BaseFolder + "/Villager_Base.controller";
    private const string OverridePath = BaseFolder + "/Villager_Default.overrideController";
    private const string VillagerPrefabPath = "Assets/Prefab/villager.prefab";
    private const string VillagerIdleClipPath = "Assets/ExplosiveLLC/Crafting Mecanim Animation Pack FREE/Animations/Crafter@Idle.FBX";
    private const string VillagerWalkClipPath = "Assets/ExplosiveLLC/Crafting Mecanim Animation Pack FREE/Animations/Crafter@WalkForward.FBX";
    private const string VillagerCarryWalkClipPath = "Assets/ExplosiveLLC/Crafting Mecanim Animation Pack FREE/Animations/Crafter@Carry-WalkForward.FBX";
    private const string VillagerWorkClipPath = "Assets/ExplosiveLLC/Crafting Mecanim Animation Pack FREE/Animations/Crafter@Carry-Handoff.FBX";
    private const string VillagerGatheringClipPath = "Assets/ExplosiveLLC/Crafting Mecanim Animation Pack FREE/Animations/Crafter@Carry-Handoff.FBX";
    private const string VillagerPanicRunClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx";
    private const string VariantsRootName = "VisualVariants";
    private static readonly string[] ModelPaths =
    {
        "Assets/MeshyImports/villager/villager.fbx",
        "Assets/MeshyImports/femaleVillager/female villager.fbx",
        "Assets/MeshyImports/baldVIllager/baldVIllager.fbx"
    };

    [MenuItem("Tools/Colony/Setup Villager Animation")]
    public static void SetupVillagerAnimation()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder(BaseFolder);

        var controller = BuildController();
        var overrideController = BuildDefaultOverride(controller);

        AssignToVillagerPrefab(overrideController);
        AssignToSceneVillagers(overrideController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Villager animation setup complete.");
    }

    private static AnimatorController BuildController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        var sm = controller.layers[0].stateMachine;
        sm.states = new ChildAnimatorState[0];
        sm.anyStateTransitions = new AnimatorStateTransition[0];

        EnsureParam(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
        EnsureParam(controller, "MoveAnimSpeed", AnimatorControllerParameterType.Float);
        EnsureParam(controller, "IsCarrying", AnimatorControllerParameterType.Bool);
        EnsureParam(controller, "IsPanicking", AnimatorControllerParameterType.Bool);
        EnsureParam(controller, "IsWorking", AnimatorControllerParameterType.Bool);
        EnsureParam(controller, "WorkType", AnimatorControllerParameterType.Int);

        var idle = sm.AddState("Idle");
        var walk = sm.AddState("Walk");
        var carry = sm.AddState("CarryWalk");
        var panicRun = sm.AddState("PanicRun");
        var farm = sm.AddState("FarmWork");
        var quarry = sm.AddState("QuarryWork");
        var wood = sm.AddState("WoodWork");
        var gathering = sm.AddState("GatheringWork");
        sm.defaultState = idle;

        idle.motion = LoadClip(VillagerIdleClipPath);
        walk.motion = LoadClip(VillagerWalkClipPath);
        carry.motion = LoadClip(VillagerCarryWalkClipPath);
        panicRun.motion = LoadClip(VillagerPanicRunClipPath);
        farm.motion = LoadClip(VillagerWorkClipPath);
        quarry.motion = LoadClip(VillagerWorkClipPath);
        wood.motion = LoadClip(VillagerWorkClipPath);
        gathering.motion = LoadClip(VillagerGatheringClipPath);

        walk.speedParameterActive = true;
        walk.speedParameter = "MoveAnimSpeed";
        carry.speedParameterActive = true;
        carry.speedParameter = "MoveAnimSpeed";
        panicRun.speedParameterActive = true;
        panicRun.speedParameter = "MoveAnimSpeed";

        AddTransition(idle, walk, false,
            Cond(AnimatorConditionMode.IfNot, "IsCarrying"),
            Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f),
            Cond(AnimatorConditionMode.IfNot, "IsWorking"));

        AddTransition(idle, carry, false,
            Cond(AnimatorConditionMode.If, "IsCarrying"),
            Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f),
            Cond(AnimatorConditionMode.IfNot, "IsWorking"));

        AddTransition(walk, idle, false, Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(carry, idle, false, Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(walk, carry, false, Cond(AnimatorConditionMode.If, "IsCarrying"));
        AddTransition(carry, walk, false, Cond(AnimatorConditionMode.IfNot, "IsCarrying"));
        AddTransition(idle, panicRun, false, Cond(AnimatorConditionMode.If, "IsPanicking"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f), Cond(AnimatorConditionMode.IfNot, "IsWorking"));
        AddTransition(walk, panicRun, false, Cond(AnimatorConditionMode.If, "IsPanicking"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f), Cond(AnimatorConditionMode.IfNot, "IsWorking"));
        AddTransition(carry, panicRun, false, Cond(AnimatorConditionMode.If, "IsPanicking"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f), Cond(AnimatorConditionMode.IfNot, "IsWorking"));
        AddTransition(panicRun, idle, false, Cond(AnimatorConditionMode.IfNot, "IsPanicking"), Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(panicRun, walk, false, Cond(AnimatorConditionMode.IfNot, "IsPanicking"), Cond(AnimatorConditionMode.IfNot, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(panicRun, carry, false, Cond(AnimatorConditionMode.IfNot, "IsPanicking"), Cond(AnimatorConditionMode.If, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));

        AddTransition(walk, farm, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 1f));
        AddTransition(walk, quarry, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 2f));
        AddTransition(walk, wood, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 3f));
        AddTransition(walk, gathering, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 4f));
        AddTransition(carry, farm, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 1f));
        AddTransition(carry, quarry, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 2f));
        AddTransition(carry, wood, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 3f));
        AddTransition(carry, gathering, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 4f));
        AddTransition(idle, farm, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 1f));
        AddTransition(idle, quarry, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 2f));
        AddTransition(idle, wood, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 3f));
        AddTransition(idle, gathering, false, Cond(AnimatorConditionMode.If, "IsWorking"), Cond(AnimatorConditionMode.Equals, "WorkType", 4f));

        AddTransition(farm, walk, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.IfNot, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(farm, carry, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.If, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(farm, idle, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(quarry, walk, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.IfNot, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(quarry, carry, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.If, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(quarry, idle, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(wood, walk, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.IfNot, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(wood, carry, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.If, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(wood, idle, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddTransition(gathering, walk, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.IfNot, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(gathering, carry, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.If, "IsCarrying"), Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(gathering, idle, false, Cond(AnimatorConditionMode.IfNot, "IsWorking"), Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));

        return controller;
    }

    private static AnimatorOverrideController BuildDefaultOverride(AnimatorController baseController)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (existing != null)
        {
            existing.runtimeAnimatorController = baseController;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var created = new AnimatorOverrideController(baseController);
        AssetDatabase.CreateAsset(created, OverridePath);
        return created;
    }

    private static void AssignToVillagerPrefab(AnimatorOverrideController overrideController)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Villager prefab not found at {VillagerPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(VillagerPrefabPath);
        var villager = root.GetComponent<Villager>();
        var modelVariants = new List<GameObject>();
        var animator = EnsureVillagerModelVariants(root, overrideController, modelVariants);
        if (villager != null && animator != null)
        {
            var so = new SerializedObject(villager);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("speedFloat").stringValue = "MoveSpeed";
            so.FindProperty("moveAnimSpeedFloat").stringValue = "MoveAnimSpeed";
            so.FindProperty("carryingBool").stringValue = "IsCarrying";
            so.FindProperty("panicBool").stringValue = "IsPanicking";
            so.FindProperty("workBool").stringValue = "IsWorking";
            so.FindProperty("workTypeInt").stringValue = "WorkType";
            so.ApplyModifiedPropertiesWithoutUndo();

            animator.runtimeAnimatorController = overrideController;

            var variant = root.GetComponent<VillagerAnimationVariant>();
            if (variant == null)
            {
                variant = root.AddComponent<VillagerAnimationVariant>();
            }

            var variantSo = new SerializedObject(variant);
            variantSo.FindProperty("animator").objectReferenceValue = animator;
            variantSo.FindProperty("overrideController").objectReferenceValue = overrideController;
            variantSo.ApplyModifiedPropertiesWithoutUndo();

            var randomizer = root.GetComponent<VillagerModelRandomizer>();
            if (randomizer == null)
            {
                randomizer = root.AddComponent<VillagerModelRandomizer>();
            }

            var randomizerSo = new SerializedObject(randomizer);
            randomizerSo.FindProperty("villager").objectReferenceValue = villager;
            randomizerSo.FindProperty("animationVariant").objectReferenceValue = variant;
            var variantsProp = randomizerSo.FindProperty("modelVariants");
            variantsProp.arraySize = modelVariants.Count;
            for (int i = 0; i < modelVariants.Count; i++)
            {
                variantsProp.GetArrayElementAtIndex(i).objectReferenceValue = modelVariants[i];
            }
            randomizerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(root, VillagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AssignToSceneVillagers(AnimatorOverrideController overrideController)
    {
        var villagers = Object.FindObjectsByType<Villager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool sceneChanged = false;
        foreach (var villager in villagers)
        {
            if (villager == null)
            {
                continue;
            }

            var animator = villager.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                continue;
            }

            var so = new SerializedObject(villager);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("speedFloat").stringValue = "MoveSpeed";
            so.FindProperty("moveAnimSpeedFloat").stringValue = "MoveAnimSpeed";
            so.FindProperty("carryingBool").stringValue = "IsCarrying";
            so.FindProperty("panicBool").stringValue = "IsPanicking";
            so.FindProperty("workBool").stringValue = "IsWorking";
            so.FindProperty("workTypeInt").stringValue = "WorkType";
            so.ApplyModifiedPropertiesWithoutUndo();

            animator.runtimeAnimatorController = overrideController;

            var variant = villager.GetComponent<VillagerAnimationVariant>();
            if (variant == null)
            {
                variant = villager.gameObject.AddComponent<VillagerAnimationVariant>();
            }

            var variantSo = new SerializedObject(variant);
            variantSo.FindProperty("animator").objectReferenceValue = animator;
            variantSo.FindProperty("overrideController").objectReferenceValue = overrideController;
            variantSo.ApplyModifiedPropertiesWithoutUndo();

            var randomizer = villager.GetComponent<VillagerModelRandomizer>();
            if (randomizer == null)
            {
                randomizer = villager.gameObject.AddComponent<VillagerModelRandomizer>();
            }
            EditorUtility.SetDirty(villager.gameObject);
            sceneChanged = true;
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkAllScenesDirty();
        }
    }

    private static AnimationClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"Animation clip not found: {path}");
        }
        return clip;
    }

    private static Animator EnsureVillagerModelVariants(GameObject root, AnimatorOverrideController overrideController, List<GameObject> modelVariants)
    {
        Animator currentAnimator = root.GetComponentInChildren<Animator>(true);
        Transform template = currentAnimator != null ? currentAnimator.transform : null;
        Vector3 targetLocalPosition = template != null ? template.localPosition : new Vector3(0f, 0.076f, 0f);
        Quaternion targetLocalRotation = template != null ? template.localRotation : Quaternion.identity;
        Vector3 targetLocalScale = template != null ? template.localScale : new Vector3(0.4f, 0.4f, 0.4f);

        Transform variantsRoot = root.transform.Find(VariantsRootName);
        if (variantsRoot == null)
        {
            var go = new GameObject(VariantsRootName);
            variantsRoot = go.transform;
            variantsRoot.SetParent(root.transform, false);
        }

        foreach (string path in ModelPaths)
        {
            string variantName = "Model_" + System.IO.Path.GetFileNameWithoutExtension(path).Replace(" ", "_");
            Transform existing = variantsRoot.Find(variantName);
            GameObject variantObject = existing != null ? existing.gameObject : null;
            if (variantObject == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null)
                {
                    Debug.LogWarning($"Villager variant source missing: {path}");
                    continue;
                }

                variantObject = PrefabUtility.InstantiatePrefab(source, variantsRoot) as GameObject;
                if (variantObject == null)
                {
                    continue;
                }
                variantObject.name = variantName;
            }

            var t = variantObject.transform;
            t.localPosition = targetLocalPosition;
            t.localRotation = targetLocalRotation;
            t.localScale = targetLocalScale;

            Animator anim = variantObject.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.runtimeAnimatorController = overrideController;
            }
            variantObject.SetActive(false);
            modelVariants.Add(variantObject);
        }

        if (currentAnimator != null && template != null && template.parent == root.transform)
        {
            template.gameObject.SetActive(false);
        }

        Animator selectedAnimator = null;
        for (int i = 0; i < modelVariants.Count; i++)
        {
            bool active = i == 0;
            modelVariants[i].SetActive(active);
            if (active && selectedAnimator == null)
            {
                selectedAnimator = modelVariants[i].GetComponentInChildren<Animator>(true);
            }
        }

        return selectedAnimator != null ? selectedAnimator : currentAnimator;
    }

    private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name)
            {
                return;
            }
        }
        controller.AddParameter(name, type);
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool hasExitTime, params TransitionCondition[] conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = hasExitTime;
        t.hasFixedDuration = true;
        t.exitTime = 0f;
        t.duration = 0.12f;
        foreach (var c in conditions)
        {
            t.AddCondition(c.mode, c.threshold, c.parameter);
        }
    }

    private static TransitionCondition Cond(AnimatorConditionMode mode, string parameter, float threshold = 0f)
    {
        return new TransitionCondition { mode = mode, parameter = parameter, threshold = threshold };
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        int sep = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, sep);
        string name = folderPath.Substring(sep + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private struct TransitionCondition
    {
        public AnimatorConditionMode mode;
        public string parameter;
        public float threshold;
    }
}
