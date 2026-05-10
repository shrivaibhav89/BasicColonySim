using UnityEditor;
using UnityEngine;

public static class TreePrefabSetup
{
    [MenuItem("Tools/Colony/Create Tree Prefab")]
    public static void CreateTreePrefab()
    {
        const string folder = "Assets/Prefab";
        const string path = "Assets/Prefab/Tree.prefab";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        GameObject root = new GameObject("Tree");
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localScale = new Vector3(0.35f, 1.2f, 0.35f);
        trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Crown";
        crown.transform.SetParent(root.transform, false);
        crown.transform.localScale = new Vector3(1.8f, 1.6f, 1.8f);
        crown.transform.localPosition = new Vector3(0f, 2.8f, 0f);

        TreeResourceNode node = root.AddComponent<TreeResourceNode>();
        var so = new SerializedObject(node);
        so.FindProperty("maxDurability").intValue = 30;
        so.FindProperty("woodPerChop").intValue = 1;
        so.FindProperty("destroyWhenDepleted").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Tree prefab created at " + path);
    }
}
