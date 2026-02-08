using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildingDataGenerator
{
    [MenuItem("Tools/Generate BuildingData Assets From Prefabs")]
    public static void Generate()
    {
        string prefabFolder = "Assets/Prefab";
        string outputFolder = "Assets/Data/BuildingData";

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Data/BuildingData"));
            AssetDatabase.Refresh();
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        int created = 0;

        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            if (prefab == null)
                continue;

            Building building = prefab.GetComponent<Building>();
            if (building != null)
            {
                // Skip if already has BuildingData assigned
                if (building.buildingData != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    PrefabUtility.UnloadPrefabContents(prefab);
                    continue;
                }

                // Parse legacy serialized values from the prefab YAML so generator works even after code refactors
                string prefabText = File.ReadAllText(path);

                BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
                // buildingName
                data.buildingName = ExtractString(prefabText, "buildingName") ?? prefab.name;
                data.foodCost = ExtractInt(prefabText, "foodCost");
                data.woodCost = ExtractInt(prefabText, "woodCost");
                data.stoneCost = ExtractInt(prefabText, "stoneCost");
                data.footprintSize = ExtractFootprint(prefabText, "footprintSize") ?? new Vector2Int(1,1);
                data.foodPerSec = ExtractInt(prefabText, "foodPerSec");
                data.woodPerSec = ExtractInt(prefabText, "woodPerSec");
                data.stonePerSec = ExtractInt(prefabText, "stonePerSec");
                data.populationCapacity = ExtractInt(prefabText, "populationCapacity");
                data.requiredWorkers = ExtractInt(prefabText, "requiredWorkers");
                // Infer dropoff by name if not explicitly set in YAML
                string nameLower = prefab.name.ToLowerInvariant();
                data.isDropoff = nameLower.Contains("storage") || nameLower.Contains("townhall") || nameLower.Contains("center");

                string assetPath = Path.Combine(outputFolder, prefab.name + "_Data.asset");
                AssetDatabase.CreateAsset(data, assetPath);

                // Assign asset to prefab and save
                building.buildingData = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
                created++;
            }
            else
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("BuildingData Generator", $"Created {created} BuildingData assets.", "OK");
    }

    private static string ExtractString(string text, string key)
    {
        string pattern = $"\n  {key}: ";
        int idx = text.IndexOf(pattern);
        if (idx < 0) return null;
        int start = idx + pattern.Length;
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        string val = text.Substring(start, end - start).Trim();
        return val;
    }

    private static int ExtractInt(string text, string key)
    {
        string s = ExtractString(text, key);
        if (string.IsNullOrEmpty(s)) return 0;
        if (int.TryParse(s, out int v)) return v;
        return 0;
    }

    private static Vector2Int? ExtractFootprint(string text, string key)
    {
        // footprintSize: {x: 3, y: 3}
        string pattern = key + ": ";
        int idx = text.IndexOf(pattern);
        if (idx < 0) return null;
        int brace = text.IndexOf('{', idx);
        if (brace < 0) return null;
        int close = text.IndexOf('}', brace);
        if (close < 0) return null;
        string inside = text.Substring(brace + 1, close - brace - 1);
        // look for x: N, y: N
        int x = 1, y = 1;
        foreach (var part in inside.Split(','))
        {
            var p = part.Split(':');
            if (p.Length < 2) continue;
            string k = p[0].Trim();
            string v = p[1].Trim();
            if (k == "x") int.TryParse(v, out x);
            if (k == "y") int.TryParse(v, out y);
        }
        return new Vector2Int(x, y);
    }
}
