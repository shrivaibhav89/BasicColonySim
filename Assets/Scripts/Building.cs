using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Data Asset (required)")]
    public BuildingData buildingData;

    [Header("Instance State")]
    [SerializeField] private Vector2Int originGridPos;
    [SerializeField] private bool hasGridPosition;
    public int assignedWorkers;
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
    public int GetPopulationCapacity() => buildingData != null ? buildingData.populationCapacity : 0;
    public int GetRequiredWorkers() => buildingData != null ? buildingData.requiredWorkers : 0;
    public bool IsDropoff => buildingData != null ? buildingData.isDropoff : runtimeIsDropoff;

    public Villager AssignedVillager { get; private set; }

    // Dropoff state is provided by `BuildingData`; a runtime override exists when no data asset is present.

    [HideInInspector]
    public bool isGhost;

    private bool isVillagerWorking;

    public void RegisterBuildingInPopulationManager()
    {
        PopulationManager.Instance.RegisterBuilding(this);
        string name = buildingData != null ? buildingData.buildingName : (gameObject != null ? gameObject.name : string.Empty);
        if (name == "Storage")
        {
            if (buildingData == null)
                runtimeIsDropoff = true;
            ResourceManager.Instance.IncreaseStorageCap(100);
        }

        if (name == "TownCenter" || name == "Town Center")
        {
            if (buildingData == null)
                runtimeIsDropoff = true;
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