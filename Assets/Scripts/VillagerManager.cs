using System.Collections.Generic;
using UnityEngine;

public class VillagerManager : MonoBehaviour
{
    public static VillagerManager Instance;

    [Header("References")]
    public GridSystem gridSystem;
    public GameObject villagerPrefab;
    public Transform villagerParent;

    [Header("Pooling")]
    public int initialPoolSize = 5;

    private readonly Queue<Villager> pool = new Queue<Villager>();
    private readonly List<Villager> activeVillagers = new List<Villager>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        WarmPool();
    }

    private void WarmPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewVillagerToPool();
        }
    }

    private Villager CreateNewVillagerToPool()
    {
        if (villagerPrefab == null)
        {
            return null;
        }

        GameObject villagerObj = Instantiate(villagerPrefab, Vector3.zero, Quaternion.identity, villagerParent);
        villagerObj.SetActive(false);
        Villager villager = villagerObj.GetComponent<Villager>();
        if (villager == null)
        {
            villager = villagerObj.AddComponent<Villager>();
        }
        pool.Enqueue(villager);
        return villager;
    }

    public bool AssignWorkerToBuilding(Building workBuilding)
    {
        if (workBuilding == null || gridSystem == null)
        {
            return false;
        }

        Building home = FindNearestResidentialBuilding(workBuilding.transform.position);
        if (home == null)
        {
            Debug.Log("No residential building available to spawn villager.");
            return false;
        }

        Villager villager = GetVillagerFromPool();
        if (villager == null)
        {
            return false;
        }

        Building storage = FindNearestStorageBuilding(workBuilding.transform.position);

        villager.gameObject.SetActive(true);
        villager.Initialize(this, gridSystem);
        villager.AssignWork(home, workBuilding, storage);
        activeVillagers.Add(villager);
        return true;
    }

    private Villager GetVillagerFromPool()
    {
        if (pool.Count == 0)
        {
            return CreateNewVillagerToPool();
        }

        Villager villager = pool.Dequeue();
        return villager;
    }

    private Building FindNearestResidentialBuilding(Vector3 fromPosition)
    {
        return FindNearestBuilding(fromPosition, building => building.populationCapacity > 0);
    }

    private Building FindNearestStorageBuilding(Vector3 fromPosition)
    {
        return FindNearestBuilding(fromPosition, building => building.buildingName == "Storage");
    }

    private Building FindNearestBuilding(Vector3 fromPosition, System.Func<Building, bool> predicate)
    {
        Building nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Building building in PopulationManager.Instance.allBuildings)
        {
            if (building == null || !predicate(building))
            {
                continue;
            }

            float dist = Vector3.Distance(fromPosition, building.transform.position);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                nearest = building;
            }
        }

        return nearest;
    }

    public Vector2Int GetBestSpawnTile(Building homeBuilding)
    {
        if (TryGetNearestRoadTile(homeBuilding, out Vector2Int roadTile))
        {
            return roadTile;
        }

        return homeBuilding != null ? homeBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
    }

    public Vector2Int GetBestTargetTile(Building targetBuilding)
    {
        return targetBuilding != null ? targetBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
    }

    public bool TryGetNearestRoadTile(Building building, out Vector2Int roadTile)
    {
        if (building == null || gridSystem == null)
        {
            roadTile = Vector2Int.zero;
            return false;
        }

        Vector2Int origin = building.GetGridOriginOrFallback(gridSystem);
        Vector2Int size = building.footprintSize;

        Vector2Int bestTile = Vector2Int.zero;
        float bestDistance = float.MaxValue;
        bool found = false;

        int searchRadius = 2;
        for (int x = -searchRadius; x <= size.x + searchRadius; x++)
        {
            for (int y = -searchRadius; y <= size.y + searchRadius; y++)
            {
                Vector2Int candidate = new Vector2Int(origin.x + x, origin.y + y);
                if (!gridSystem.IsRoadAt(candidate))
                {
                    continue;
                }

                float dist = Vector2Int.Distance(origin, candidate);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTile = candidate;
                    found = true;
                }
            }
        }

        roadTile = bestTile;
        return found;
    }
}
