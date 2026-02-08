using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Data Asset (preferred)")]
    public BuildingData buildingData;

    [Header("Legacy Fields (fallback only)")]
    [SerializeField] private string buildingName;
    [SerializeField] private int foodCost;
    [SerializeField] private int woodCost;
    [SerializeField] private int stoneCost;

    [SerializeField] private Vector2Int footprintSize = new Vector2Int(1, 1);
    [SerializeField] private Vector2Int originGridPos;
    [SerializeField] private bool hasGridPosition;

    [SerializeField] private int foodPerSec;
    [SerializeField] private int woodPerSec;
    [SerializeField] private int stonePerSec;

    [SerializeField] private int populationCapacity; // For houses
    [SerializeField] private int requiredWorkers;    // For production buildings
    public int assignedWorkers;

    // Accessors that prefer the BuildingData asset but fall back to legacy fields
    public string BuildingName => buildingData != null ? buildingData.buildingName : buildingName;
    public int FoodCost => buildingData != null ? buildingData.foodCost : foodCost;
    public int WoodCost => buildingData != null ? buildingData.woodCost : woodCost;
    public int StoneCost => buildingData != null ? buildingData.stoneCost : stoneCost;
    public Vector2Int FootprintSize => buildingData != null ? buildingData.footprintSize : footprintSize;
    public int GetFoodPerSec() => buildingData != null ? buildingData.foodPerSec : foodPerSec;
    public int GetWoodPerSec() => buildingData != null ? buildingData.woodPerSec : woodPerSec;
    public int GetStonePerSec() => buildingData != null ? buildingData.stonePerSec : stonePerSec;
    public int GetPopulationCapacity() => buildingData != null ? buildingData.populationCapacity : populationCapacity;
    public int GetRequiredWorkers() => buildingData != null ? buildingData.requiredWorkers : requiredWorkers;
    public bool IsDropoff
    {
        get => buildingData != null ? buildingData.isDropoff : isDropoff;
        set
        {
            if (buildingData != null)
            {
                buildingData.isDropoff = value;
            }
            else
            {
                isDropoff = value;
            }
        }
    }

    public Villager AssignedVillager { get; private set; }

    [Header("Dropoff")]
    public bool isDropoff;

    [HideInInspector]
    public bool isGhost;

    private bool isVillagerWorking;

    public void RegisterBuildingInPopulationManager()
    {
        PopulationManager.Instance.RegisterBuilding(this);

        if (BuildingName == "Storage")
        {
            IsDropoff = true;
            ResourceManager.Instance.IncreaseStorageCap(100);
        }

        if (BuildingName == "TownCenter" || BuildingName == "Town Center")
        {
            IsDropoff = true;
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
        return GetFoodPerSec() > 0 || GetWoodPerSec() > 0 || GetStonePerSec() > 0;
    }

    void OnDestroy()
    {
        // Free up workers when building is destroyed
        // Will integrate with PopulationManager in Day 7
        AssignedVillager = null;
        isVillagerWorking = false;
    }
}