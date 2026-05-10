using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ArmyUnitAnimationSetup
{
    private const string BaseFolder = "Assets/Animations/Army";
    private const string ControllerPath = BaseFolder + "/ArmyUnit_Base.controller";
    private const string OverridePath = BaseFolder + "/ArmyUnit_Default.overrideController";
    private const string PrefabPath = "Assets/Prefab/ArmyUnit.prefab";
    private const string Rs2Path = "Assets/MeshyImports/rs2/rs2.fbx";
    private const string ThrowClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Thrown/Weapon/Spear/HumanM@ThrowSpear01_R.fbx";
    private const string SpearModelPath = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Throwing Animations/Models/Human_Javelin.fbx";
    private const string ArmyWeaponWalkClipPath = "Assets/MeshyImports/roman soldier/Meshy_AI_Cheerful_Roman_Legion_biped_Animation_Walking_withSkin.fbx";

    [MenuItem("Tools/Colony/Setup Army Unit Animation")]
    public static void SetupArmyUnitAnimation()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder(BaseFolder);

        var controller = BuildController();
        var overrideController = BuildDefaultOverride(controller);
        SetupArmyPrefab(overrideController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Army unit animation setup complete.");
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
        EnsureParam(controller, "Throw", AnimatorControllerParameterType.Trigger);

        var idle = sm.AddState("Idle");
        var move = sm.AddState("Move");
        var throwState = sm.AddState("Throw");
        sm.defaultState = idle;

        idle.motion = LoadFirstClip(Rs2Path);
        move.motion = LoadRunClipFallback();
        throwState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThrowClipPath);

        AddTransition(idle, move, false, Cond(AnimatorConditionMode.Greater, "MoveSpeed", 0.1f));
        AddTransition(move, idle, false, Cond(AnimatorConditionMode.Less, "MoveSpeed", 0.1f));
        AddAnyStateTransition(sm, throwState, Cond(AnimatorConditionMode.If, "Throw"));
        AddTransition(throwState, idle, true);

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

    private static void SetupArmyPrefab(AnimatorOverrideController overrideController)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var army = root.GetComponent<ArmyUnit>();
        Transform visual = root.transform.Find("Visual");
        if (visual == null || army == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Bounds oldBounds = CalculateBounds(visual);
        foreach (Transform child in visual)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        GameObject rs2 = AssetDatabase.LoadAssetAtPath<GameObject>(Rs2Path);
        if (rs2 != null)
        {
            GameObject model = PrefabUtility.InstantiatePrefab(rs2, visual) as GameObject;
            if (model != null)
            {
                model.name = "RS2Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                Bounds newBounds = CalculateBounds(model.transform);
                if (newBounds.size.y > 0.001f && oldBounds.size.y > 0.001f)
                {
                    float scale = oldBounds.size.y / newBounds.size.y;
                    model.transform.localScale = Vector3.one * scale;
                }

                model.transform.localPosition = new Vector3(0f, -0.34f, 0f);
                Animator anim = model.GetComponentInChildren<Animator>(true);
                if (anim != null)
                {
                    anim.runtimeAnimatorController = overrideController;
                    army.SetAnimator(anim);

                    var so = new SerializedObject(army);
                    so.FindProperty("animator").objectReferenceValue = anim;
                    so.FindProperty("moveSpeedFloat").stringValue = "MoveSpeed";
                    so.FindProperty("throwTrigger").stringValue = "Throw";
                    so.ApplyModifiedPropertiesWithoutUndo();

                    AttachStaticSpearToHand(root, anim);
                }
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AttachStaticSpearToHand(GameObject root, Animator anim)
    {
        GameObject spearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpearModelPath);
        if (anim == null || spearPrefab == null)
        {
            return;
        }

        var oldVisual = root.GetComponent<ArmySpearVisual>();
        if (oldVisual != null)
        {
            Object.DestroyImmediate(oldVisual);
        }

        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
        {
            Debug.LogWarning("Army setup: RightHand bone not found.");
            return;
        }

        Transform existing = hand.Find("EquippedSpear");
        GameObject spearObj;
        if (existing != null)
        {
            spearObj = existing.gameObject;
        }
        else
        {
            spearObj = PrefabUtility.InstantiatePrefab(spearPrefab, hand) as GameObject;
            if (spearObj == null)
            {
                return;
            }
            spearObj.name = "EquippedSpear";
        }

        spearObj.transform.localPosition = new Vector3(0.02f, 0.02f, 0.12f);
        spearObj.transform.localRotation = Quaternion.Euler(10f, 90f, 0f);
        spearObj.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        Collider[] cols = spearObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }
    }

    private static AnimationClip LoadFirstClip(string modelPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static AnimationClip LoadRunClipFallback()
    {
        AnimationClip clip = LoadFirstClip(ArmyWeaponWalkClipPath);
        if (clip != null)
        {
            return clip;
        }

        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx");
        if (clip != null)
        {
            return clip;
        }
        return LoadFirstClip(Rs2Path);
    }

    private static Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.one);
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
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
        t.exitTime = hasExitTime ? 0.95f : 0f;
        t.duration = 0.1f;
        foreach (var c in conditions)
        {
            t.AddCondition(c.mode, c.threshold, c.parameter);
        }
    }

    private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState to, params TransitionCondition[] conditions)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
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
