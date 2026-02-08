using UnityEngine;
using System.Collections.Generic;

public class Building : MonoBehaviour
{
    [Header("Building Data Asset (required)")]
    public BuildingData buildingData;

    [Header("Instance State")]
    [SerializeField] private Vector2Int originGridPos;
    [SerializeField] private bool hasGridPosition;
    public int assignedWorkers;
    private bool isRegistered;
    [SerializeField]private Building cachedDropoff;
    private float harvestRemainderFood;
    private float harvestRemainderWood;
    private float harvestRemainderStone;
    // Used for temporary runtime override when no BuildingData exists
    private bool runtimeIsDropoff = false;

    // Accessors that prefer the BuildingData asset but fall back to legacy fields
    // Accessors read directly from BuildingData. BuildingData is expected to be present for all
    // runtime instances to avoid per-instance duplication of static data.
    // Prefer reading directly from the shared `BuildingData` asset to avoid per-instance duplication.
    public Vector2Int FootprintSize => buildingData != null ? buildingData.footprintSize : new Vector2Int(1, 1);
    public int GetFoodPerSec() => buildingData != null ? buildingData.foodPerSec : 0;
    public int GetWoodPerSec() => buildingData != null ? buildingData.woodPerSec : 0;
    public int GetStonePerSec() => buildingData != null ? buildingData.stonePerSec : 0;
    public int GetFoodPerHarvest() => buildingData != null ? buildingData.foodPerHarvest : 0;
    public int GetWoodPerHarvest() => buildingData != null ? buildingData.woodPerHarvest : 0;
    public int GetStonePerHarvest() => buildingData != null ? buildingData.stonePerHarvest : 0;
    public float GetHarvestDuration() => buildingData != null ? buildingData.harvestDuration : 0f;
    public int GetPopulationCapacity() => buildingData != null ? buildingData.populationCapacity : 0;
    public int GetRequiredWorkers() => buildingData != null ? buildingData.requiredWorkers : 0;
    public bool IsDropoff => buildingData != null ? (buildingData.isDropoff || runtimeIsDropoff) : runtimeIsDropoff;

    public Villager AssignedVillager => assignedVillagers.Count > 0 ? assignedVillagers[0] : null;
    private readonly List<Villager> assignedVillagers = new List<Villager>();

    // Dropoff state is provided by `BuildingData`; a runtime override exists when no data asset is present.

    [HideInInspector]
    public bool isGhost;

    private bool isVillagerWorking;

    void Start()
    {
        if (!isGhost)
        {
            RegisterBuildingInPopulationManager();
        }
    }

    public void RegisterBuildingInPopulationManager()
    {
        if (isRegistered || PopulationManager.Instance == null)
        {
            return;
        }

        isRegistered = true;
        string name = buildingData != null ? buildingData.buildingName : (gameObject != null ? gameObject.name : string.Empty);
        string nameLower = string.IsNullOrWhiteSpace(name) ? string.Empty : name.ToLowerInvariant();
        bool isStorage = nameLower.Contains("storage");
        bool isTownHall = nameLower.Contains("townhall") || nameLower.Contains("town hall") || nameLower.Contains("towncenter") || nameLower.Contains("town center") || nameLower.Contains("center");

        if (isStorage)
        {
            runtimeIsDropoff = true;
            ResourceManager.Instance.IncreaseStorageCap(50);
        }

        if (isTownHall)
        {
            runtimeIsDropoff = true;
        }

        PopulationManager.Instance.RegisterBuilding(this);
    }

    public void SetDropoff(Building dropoff)
    {
        cachedDropoff = dropoff;
    }

    public Building GetDropoff()
    {
        return cachedDropoff;
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
        if (VillagerManager.Instance == null)
        {
            return false;
        }

        if (VillagerManager.Instance.AssignWorkerToBuilding(this, out Villager villager))
        {
            if (villager != null && !assignedVillagers.Contains(villager))
            {
                assignedVillagers.Add(villager);
                assignedWorkers += 1;
                villager.SetMoveOffset(GetWorkerOffset(assignedWorkers - 1));
                if (PopulationManager.Instance != null)
                {
                    PopulationManager.Instance.NotifyWorkersAssigned(this);
                }
            }
            return true;
        }

        return false;
    }

    public void NotifyVillagerStartedWork(Villager villager)
    {
        if (villager == null || !assignedVillagers.Contains(villager))
        {
            return;
        }

        isVillagerWorking = true;
    }

    public void NotifyVillagerStoppedWork(Villager villager)
    {
        if (villager == null || !assignedVillagers.Contains(villager))
        {
            return;
        }

        isVillagerWorking = false;
    }

    public void HarvestShared(int foodPerHarvest, int woodPerHarvest, int stonePerHarvest, out int food, out int wood, out int stone)
    {
        int workerCount = Mathf.Max(1, GetRequiredWorkers());

        float foodShare = foodPerHarvest > 0 ? (float)foodPerHarvest / workerCount : 0f;
        float woodShare = woodPerHarvest > 0 ? (float)woodPerHarvest / workerCount : 0f;
        float stoneShare = stonePerHarvest > 0 ? (float)stonePerHarvest / workerCount : 0f;

        harvestRemainderFood += foodShare;
        harvestRemainderWood += woodShare;
        harvestRemainderStone += stoneShare;

        food = Mathf.FloorToInt(harvestRemainderFood);
        wood = Mathf.FloorToInt(harvestRemainderWood);
        stone = Mathf.FloorToInt(harvestRemainderStone);

        harvestRemainderFood -= food;
        harvestRemainderWood -= wood;
        harvestRemainderStone -= stone;
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
        assignedVillagers.Clear();
        isVillagerWorking = false;
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.UnregisterBuilding(this);
        }
    }

    private Vector3 GetWorkerOffset(int index)
    {
        float radius = 0.25f;
        float angle = (index % 6) * 60f;
        float radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
    }
}