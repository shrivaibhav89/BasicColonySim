using UnityEngine;

[CreateAssetMenu(fileName = "BuildingData", menuName = "ColonySim/Building Data", order = 0)]
public class BuildingData : ScriptableObject
{
    [Header("Building Info")]
    public string buildingName;
    public int foodCost;
    public int woodCost;
    public int stoneCost;

    [Header("Footprint")]
    public Vector2Int footprintSize = new Vector2Int(1, 1);

    [Header("Production (per second)")]
    public int foodPerSec;
    public int woodPerSec;
    public int stonePerSec;

    [Header("Population")]
    public int populationCapacity; // For houses
    public int requiredWorkers;    // For production buildings

    [Header("Dropoff")]
    public bool isDropoff;
}
