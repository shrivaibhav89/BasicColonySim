using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Info")]
    public string buildingName;
    public int foodCost;
    public int woodCost;
    public int stoneCost;

    [Header("Footprint")]
    public Vector2Int footprintSize = new Vector2Int(1, 1);
    public Vector2Int originGridPos;
    public bool hasGridPosition;

    [Header("Production (per second)")]
    public int foodPerSec;
    public int woodPerSec;
    public int stonePerSec;

    [Header("Population")]
    public int populationCapacity; // For houses
    public int requiredWorkers;    // For production buildings
    public int assignedWorkers;

    public Villager AssignedVillager { get; private set; }

    [Header("Dropoff")]
    public bool isDropoff;

    [HideInInspector]
    public bool isGhost;

    private bool isVillagerWorking;

    public void RegisterBuildingInPopulationManager()
    {

        PopulationManager.Instance.RegisterBuilding(this);

        if (buildingName == "Storage")
        {
            isDropoff = true;
            ResourceManager.Instance.IncreaseStorageCap(100);
        }

        if (buildingName == "TownCenter" || buildingName == "Town Center")
        {
            isDropoff = true;
        }
    }

    public void SetGridOrigin(Vector2Int origin)
    {
        originGridPos = origin;
        hasGridPosition = true;
    }

    public Vector2Int GetGridOriginOrFallback(GridSystem gridSystem)
    {
        if (hasGridPosition)
        {
            return originGridPos;
        }

        if (gridSystem != null)
        {
            return gridSystem.WorldToGrid(transform.position);
        }

        return Vector2Int.zero;
    }
    public bool RequestVillagerAssignment()
    {
        if (AssignedVillager != null)
        {
            return false;
        }

        if (VillagerManager.Instance == null)
        {
            return false;
        }

        if (VillagerManager.Instance.AssignWorkerToBuilding(this, out Villager villager))
        {
            AssignedVillager = villager;
            assignedWorkers += 1;
            return true;
        }

        return false;
    }

    public void NotifyVillagerStartedWork(Villager villager)
    {
        if (villager == null || villager != AssignedVillager)
        {
            return;
        }

        isVillagerWorking = true;
    }

    public void NotifyVillagerStoppedWork(Villager villager)
    {
        if (villager == null || villager != AssignedVillager)
        {
            return;
        }

        isVillagerWorking = false;
    }

    bool IsProductionBuilding()
    {
        // A building is a production building if it generates any resources
        return foodPerSec > 0 || woodPerSec > 0 || stonePerSec > 0;
    }

    void OnDestroy()
    {
        // Free up workers when building is destroyed
        // Will integrate with PopulationManager in Day 7
        AssignedVillager = null;
        isVillagerWorking = false;
    }
}