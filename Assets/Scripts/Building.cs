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

    [HideInInspector]
    public bool isGhost;

    // Production tick system
    private float productionTimer = 0f;
    private const float PRODUCTION_INTERVAL = 1f; // 1 second

    public void RegisterBuildingInPopulationManager()
    {

        PopulationManager.Instance.RegisterBuilding(this);

        // Start production if it's a production building
        if (foodPerSec > 0 || woodPerSec > 0 || stonePerSec > 0)
        {
            InvokeRepeating("ProduceResources", 1f, 1f);
        }
        if (buildingName == "Storage")
        {
            ResourceManager.Instance.IncreaseStorageCap(100);
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
    void ProduceResources()
    {
        // Only produce if has workers
        if (assignedWorkers >= requiredWorkers)
        {
            ResourceManager.Instance.AddResources(foodPerSec, woodPerSec, stonePerSec);
        }
    }

    void Update()
    {
        if (isGhost)
        {
            return;
        }

        // Only production buildings generate resources
        if (IsProductionBuilding())
        {
            HandleProduction();
        }
    }

    void HandleProduction()
    {
        // Increment timer
        productionTimer += Time.deltaTime;

        // Check if 1 second has passed
        if (productionTimer >= PRODUCTION_INTERVAL)
        {
            // Reset timer
            productionTimer = 0f;

            // Only produce if we have enough workers assigned
            if (assignedWorkers >= requiredWorkers)
            {
                // Add resources to the manager
                if (foodPerSec > 0)
                    ResourceManager.Instance.AddResources(foodPerSec, 0, 0);

                if (woodPerSec > 0)
                    ResourceManager.Instance.AddResources(0, woodPerSec, 0);

                if (stonePerSec > 0)
                    ResourceManager.Instance.AddResources(0, 0, stonePerSec);
            }
        }
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
    }
}