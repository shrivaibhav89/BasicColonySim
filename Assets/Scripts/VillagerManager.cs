using System.Collections;
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

    [Header("Initial Spawn")]
    public bool spawnInitialVillagers = true;
    public int initialVillagerCountOverride = -1;

    private readonly Queue<Villager> pool = new Queue<Villager>();
    private readonly List<Villager> activeVillagers = new List<Villager>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        WarmPool();
    }

    void Start()
    {
        if (spawnInitialVillagers)
        {
            StartCoroutine(SpawnInitialVillagersNextFrame());
        }
    }

    private IEnumerator SpawnInitialVillagersNextFrame()
    {
        yield return null;
        SpawnInitialVillagers();
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

    public bool AssignWorkerToBuilding(Building workBuilding, out Villager villager)
    {
        villager = null;
        if (workBuilding == null || gridSystem == null)
        {
            return false;
        }

        Building home = FindNearestResidentialBuilding(workBuilding.transform.position);
        if (home == null)
        {
            home = GetNearestDropoffBuilding(workBuilding.transform.position);
        }
        if (home == null)
        {
            home = workBuilding;
        }

        Villager pooledVillager = GetAvailableVillager();
        if (pooledVillager == null)
        {
            return false;
        }

        Building storage = GetNearestDropoffBuilding(workBuilding.transform.position);

        pooledVillager.gameObject.SetActive(true);
        pooledVillager.Initialize(this, gridSystem);
        pooledVillager.AssignWork(home, workBuilding, storage);
        if (!activeVillagers.Contains(pooledVillager))
        {
            activeVillagers.Add(pooledVillager);
        }
        villager = pooledVillager;
        return true;
    }

    private Villager GetAvailableVillager()
    {
        for (int i = 0; i < activeVillagers.Count; i++)
        {
            Villager active = activeVillagers[i];
            if (active != null && active.IsAvailableForWork())
            {
                return active;
            }
        }

        int desired = PopulationManager.Instance != null ? PopulationManager.Instance.currentPopulation : 0;
        if (activeVillagers.Count < desired)
        {
            Villager spawned = GetVillagerFromPool();
            if (spawned != null)
            {
                spawned.gameObject.SetActive(true);
                spawned.Initialize(this, gridSystem);
                Vector3 spawnPos = GetInitialSpawnPosition(activeVillagers.Count);
                spawned.SetIdleAt(spawnPos);
                activeVillagers.Add(spawned);
                return spawned;
            }
        }

        return null;
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

    private void SpawnInitialVillagers()
    {
        int desired = PopulationManager.Instance != null ? PopulationManager.Instance.currentPopulation : 0;
        if (initialVillagerCountOverride >= 0)
        {
            desired = initialVillagerCountOverride;
        }

        for (int i = activeVillagers.Count; i < desired; i++)
        {
            Villager spawned = GetVillagerFromPool();
            if (spawned == null)
            {
                break;
            }

            spawned.gameObject.SetActive(true);
            spawned.Initialize(this, gridSystem);
            Vector3 spawnPos = GetInitialSpawnPosition(i);
            spawned.SetIdleAt(spawnPos);
            activeVillagers.Add(spawned);
        }
    }

    private Vector3 GetInitialSpawnPosition(int index)
    {
        Building anchor = GetNearestDropoffBuilding(Vector3.zero);
        Vector2Int baseGrid = gridSystem != null
            ? (anchor != null ? anchor.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero)
            : Vector2Int.zero;

        Vector2Int offset = GetSpawnOffset(index);
        Vector2Int gridPos = baseGrid + offset;

        if (gridSystem != null)
        {
            Vector3 world = gridSystem.GridToWorld(gridPos);
            world.y = 0f;
            return world;
        }

        Vector3 anchorPos = anchor != null ? anchor.transform.position : Vector3.zero;
        return anchorPos + new Vector3(offset.x, 0f, offset.y);
    }

    private Vector2Int GetSpawnOffset(int index)
    {
        Vector2Int[] offsets = new Vector2Int[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1)
        };

        if (index < 0)
        {
            index = 0;
        }

        return offsets[index % offsets.Length];
    }

    private Building FindNearestResidentialBuilding(Vector3 fromPosition)
    {
        return FindNearestBuilding(fromPosition, building => building.GetPopulationCapacity() > 0);
    }

    public Building GetNearestDropoffBuilding(Vector3 fromPosition)
    {
        return FindNearestBuilding(fromPosition, building =>
            building.IsDropoff ||
            NameLooksLikeDropoff(building));
    }

    public Building GetDropoffForWorkBuilding(Building workBuilding)
    {
        return workBuilding != null ? workBuilding.GetDropoff() : null;
    }

    private bool NameLooksLikeDropoff(Building building)
    {
        if (building == null)
        {
            return false;
        }

        string primary = building.buildingData != null ? building.buildingData.buildingName : (building.gameObject != null ? building.gameObject.name : string.Empty);

        if (string.IsNullOrWhiteSpace(primary))
        {
            return false;
        }

        string lower = primary.ToLowerInvariant();
        return lower.Contains("storage") || lower.Contains("townhall") || lower.Contains("center") ;
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
        Vector2Int size = building.FootprintSize;

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
