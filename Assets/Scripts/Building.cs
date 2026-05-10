using System;
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

    [Header("Production")]
    [SerializeField] private bool productionEnabled = true;
    [SerializeField] private bool tintWhenDisabled = true;
    [SerializeField] private Color disabledTint = new Color(0.45f, 0.45f, 0.45f, 0.45f);
    [Range(0f, 1f)]
    [SerializeField] private float disabledAlpha = 0.45f;

    [Header("Work Points")]
    [SerializeField] private List<Transform> workPoints = new List<Transform>();
    [SerializeField] private bool autoCollectNamedWorkPoints = true;
    [SerializeField] private string workPointNameContains = "workpoint";

    [Header("Woodcutter")]
    [SerializeField] private float treeSearchRadius = 10f;
    [SerializeField] private int storedWood = 0;

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
    public JobType GetJobType() => buildingData != null ? buildingData.jobType : JobType.None;
    public bool IsProductionEnabled => productionEnabled;
    public float TreeSearchRadius => Mathf.Max(1f, treeSearchRadius);

    public Villager AssignedVillager => assignedVillagers.Count > 0 ? assignedVillagers[0] : null;
    private readonly List<Villager> assignedVillagers = new List<Villager>();

    private bool materialsCached;
    private Renderer[] cachedRenderers;
    private readonly Dictionary<Material, MaterialColorState> materialColorStates = new Dictionary<Material, MaterialColorState>();
    private readonly Dictionary<Villager, int> reservedWorkPointIndices = new Dictionary<Villager, int>();
    private readonly Dictionary<Villager, TreeResourceNode> reservedTrees = new Dictionary<Villager, TreeResourceNode>();

    // Dropoff state is provided by `BuildingData`; a runtime override exists when no data asset is present.

    [HideInInspector]
    public bool isGhost;

    private bool isVillagerWorking;

    void Awake()
    {
        RefreshWorkPoints();
        CacheMaterials();
        ApplyProductionVisuals();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RefreshWorkPoints();
    }
#endif

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
        bool isTower = nameLower.Contains("tower");

        if (isStorage)
        {
            runtimeIsDropoff = true;
            ResourceManager.Instance.IncreaseStorageCap(50);
        }

        if (isTownHall)
        {
            runtimeIsDropoff = true;
        }

        if (isTower && GetComponent<TowerGarrison>() == null)
        {
            gameObject.AddComponent<TowerGarrison>();
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

                Vector3 fallbackWorkPoint = transform.position + GetWorkerOffset(assignedWorkers - 1);
                if (TryAssignWorkPoint(villager, out Vector3 workPoint))
                {
                    villager.SetMoveOffset(Vector3.zero);
                    villager.SetWorkPoint(this, workPoint);
                }
                else
                {
                    villager.SetMoveOffset(Vector3.zero);
                    villager.SetWorkPoint(this, fallbackWorkPoint);
                }

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

    public void UnassignVillager(Villager villager)
    {
        if (villager == null)
        {
            return;
        }

        if (assignedVillagers.Remove(villager))
        {
            ReleaseWorkPoint(villager);
            ReleaseReservedTree(villager);
            assignedWorkers = Mathf.Max(0, assignedWorkers - 1);
            isVillagerWorking = false;
        }
    }

    public void SetProductionEnabled(bool enabled)
    {
        if (productionEnabled == enabled)
        {
            return;
        }

        productionEnabled = enabled;
        if (!productionEnabled)
        {
            ReleaseAllWorkers("Production disabled");
        }

        ApplyProductionVisuals();

        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.RefreshWorkerAssignments();
        }
    }

    private void ReleaseAllWorkers(string reason)
    {
        while (assignedVillagers.Count > 0)
        {
            int index = assignedVillagers.Count - 1;
            Villager villager = assignedVillagers[index];
            assignedVillagers.RemoveAt(index);
            ReleaseWorkPoint(villager);
            ReleaseReservedTree(villager);
            assignedWorkers = Mathf.Max(0, assignedWorkers - 1);
            if (villager != null)
            {
                villager.ForceIdle(reason);
            }
        }
    }

    private void CacheMaterials()
    {
        if (materialsCached)
        {
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                if (material == null || materialColorStates.ContainsKey(material))
                {
                    continue;
                }

                MaterialColorState state = new MaterialColorState
                {
                    hasBaseColor = material.HasProperty("_BaseColor"),
                    hasColor = material.HasProperty("_Color"),
                    hasSurface = material.HasProperty("_Surface"),
                    hasBlend = material.HasProperty("_Blend"),
                    hasSrcBlend = material.HasProperty("_SrcBlend"),
                    hasDstBlend = material.HasProperty("_DstBlend"),
                    hasZWrite = material.HasProperty("_ZWrite"),
                    renderQueue = material.renderQueue
                };

                if (state.hasBaseColor)
                {
                    state.baseColor = material.GetColor("_BaseColor");
                }

                if (state.hasColor)
                {
                    state.color = material.GetColor("_Color");
                }

                if (state.hasSurface)
                {
                    state.surface = material.GetFloat("_Surface");
                }

                if (state.hasBlend)
                {
                    state.blend = material.GetFloat("_Blend");
                }

                if (state.hasSrcBlend)
                {
                    state.srcBlend = material.GetFloat("_SrcBlend");
                }

                if (state.hasDstBlend)
                {
                    state.dstBlend = material.GetFloat("_DstBlend");
                }

                if (state.hasZWrite)
                {
                    state.zWrite = material.GetFloat("_ZWrite");
                }

                materialColorStates.Add(material, state);
            }
        }

        materialsCached = true;
    }

    private void ApplyProductionVisuals()
    {
        if (!tintWhenDisabled)
        {
            return;
        }

        if (!materialsCached)
        {
            CacheMaterials();
        }

        foreach (KeyValuePair<Material, MaterialColorState> kvp in materialColorStates)
        {
            Material material = kvp.Key;
            MaterialColorState state = kvp.Value;

            if (productionEnabled)
            {
                if (state.hasBaseColor)
                {
                    material.SetColor("_BaseColor", state.baseColor);
                }
                if (state.hasColor)
                {
                    material.SetColor("_Color", state.color);
                }

                RestoreMaterialSurface(material, state);
            }
            else
            {
                Color tint = GetDisabledTint();
                if (state.hasBaseColor)
                {
                    material.SetColor("_BaseColor", tint);
                }
                if (state.hasColor)
                {
                    material.SetColor("_Color", tint);
                }

                ApplyTransparentMaterialSurface(material);
            }
        }
    }

    private Color GetDisabledTint()
    {
        Color tint = disabledTint;
        tint.a = disabledAlpha <= 0.01f ? 0.45f : disabledAlpha;
        return tint;
    }

    private void ApplyTransparentMaterialSurface(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void RestoreMaterialSurface(Material material, MaterialColorState state)
    {
        if (state.hasSurface)
        {
            material.SetFloat("_Surface", state.surface);
        }

        if (state.hasBlend)
        {
            material.SetFloat("_Blend", state.blend);
        }

        if (state.hasSrcBlend)
        {
            material.SetFloat("_SrcBlend", state.srcBlend);
        }

        if (state.hasDstBlend)
        {
            material.SetFloat("_DstBlend", state.dstBlend);
        }

        if (state.hasZWrite)
        {
            material.SetFloat("_ZWrite", state.zWrite);
        }

        if (state.hasSurface && Mathf.Approximately(state.surface, 1f))
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        material.DisableKeyword("_ALPHABLEND_ON");

        material.renderQueue = state.renderQueue;
    }

    public bool TryReleaseWorker(out Villager villager, string reason = "Reassigned")
    {
        villager = null;
        if (assignedVillagers.Count == 0)
        {
            return false;
        }

        int index = assignedVillagers.Count - 1;
        villager = assignedVillagers[index];
        assignedVillagers.RemoveAt(index);
        ReleaseWorkPoint(villager);
        ReleaseReservedTree(villager);
        assignedWorkers = Mathf.Max(0, assignedWorkers - 1);

        if (villager != null)
        {
            villager.ForceIdle(reason);
        }

        return true;
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
        foreach (KeyValuePair<Villager, TreeResourceNode> kvp in reservedTrees)
        {
            if (kvp.Value != null && kvp.Key != null)
            {
                kvp.Value.Release(kvp.Key);
            }
        }
        assignedVillagers.Clear();
        reservedWorkPointIndices.Clear();
        reservedTrees.Clear();
        isVillagerWorking = false;
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.UnregisterBuilding(this);
        }
    }

    public void DestroyBuilding()
    {
        // Release all assigned workers
        while (assignedVillagers.Count > 0)
        {
            int index = assignedVillagers.Count - 1;
            Villager villager = assignedVillagers[index];
            assignedVillagers.RemoveAt(index);
            ReleaseWorkPoint(villager);
            ReleaseReservedTree(villager);
            assignedWorkers = Mathf.Max(0, assignedWorkers - 1);
            if (villager != null)
            {
                villager.ForceIdle("Building destroyed");
            }
        }

        // Unmark grid tiles
        if (hasGridPosition && GridSystem.Instance != null)
        {
            Vector2Int footprint = FootprintSize;
            GridSystem.Instance.SetAreaOccupied(originGridPos, footprint, false);
        }

        // Unregister from PopulationManager
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.UnregisterBuilding(this);
        }

        // Destroy the gameobject (no resource recovery)
        Destroy(gameObject);
    }

    [ContextMenu("Destroy Building (No Refund)")]
    private void DestroyBuildingContextMenu()
    {
        DestroyBuilding();
    }

    private Vector3 GetWorkerOffset(int index)
    {
        float radius = 0.25f;
        float angle = (index % 6) * 60f;
        float radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
    }

    public bool TryAssignWorkPoint(Villager villager, out Vector3 worldPoint)
    {
        worldPoint = transform.position;
        if (villager == null)
        {
            return false;
        }

        RefreshWorkPoints();
        if (reservedWorkPointIndices.TryGetValue(villager, out int existingIndex))
        {
            worldPoint = GetWorkPointWorldPosition(existingIndex);
            return true;
        }

        int freeIndex = FindNextFreeWorkPointIndex();
        if (freeIndex < 0)
        {
            return false;
        }

        reservedWorkPointIndices[villager] = freeIndex;
        worldPoint = GetWorkPointWorldPosition(freeIndex);
        return true;
    }

    public bool TryGetAssignedWorkPoint(Villager villager, out Vector3 worldPoint)
    {
        worldPoint = transform.position;
        if (villager == null)
        {
            return false;
        }

        RefreshWorkPoints();
        if (!reservedWorkPointIndices.TryGetValue(villager, out int index))
        {
            return false;
        }

        worldPoint = GetWorkPointWorldPosition(index);
        return true;
    }

    public void ReleaseWorkPoint(Villager villager)
    {
        if (villager == null)
        {
            return;
        }

        reservedWorkPointIndices.Remove(villager);
    }

    private int FindNextFreeWorkPointIndex()
    {
        for (int i = 0; i < workPoints.Count; i++)
        {
            Transform point = workPoints[i];
            if (point == null)
            {
                continue;
            }

            bool alreadyReserved = false;
            foreach (KeyValuePair<Villager, int> kvp in reservedWorkPointIndices)
            {
                if (kvp.Value == i)
                {
                    alreadyReserved = true;
                    break;
                }
            }

            if (!alreadyReserved)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector3 GetWorkPointWorldPosition(int index)
    {
        if (index >= 0 && index < workPoints.Count && workPoints[index] != null)
        {
            return workPoints[index].position;
        }

        return transform.position;
    }

    private void RefreshWorkPoints()
    {
        if (autoCollectNamedWorkPoints)
        {
            CollectNamedWorkPoints();
        }

        CleanupWorkPointList();
        CleanupInvalidWorkPointReservations();
    }

    private void CollectNamedWorkPoints()
    {
        string token = workPointNameContains;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        token = token.Trim().ToLowerInvariant();
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            string childName = child.name;
            if (string.IsNullOrEmpty(childName))
            {
                continue;
            }

            if (!childName.ToLowerInvariant().Contains(token))
            {
                continue;
            }

            if (!workPoints.Contains(child))
            {
                workPoints.Add(child);
            }
        }
    }

    private void CleanupWorkPointList()
    {
        for (int i = workPoints.Count - 1; i >= 0; i--)
        {
            Transform point = workPoints[i];
            if (point == null || point == transform)
            {
                workPoints.RemoveAt(i);
            }
        }

        for (int i = 0; i < workPoints.Count; i++)
        {
            Transform point = workPoints[i];
            for (int j = workPoints.Count - 1; j > i; j--)
            {
                if (workPoints[j] == point)
                {
                    workPoints.RemoveAt(j);
                }
            }
        }
    }

    private void CleanupInvalidWorkPointReservations()
    {
        if (reservedWorkPointIndices.Count == 0)
        {
            return;
        }

        List<Villager> invalidVillagers = null;
        foreach (KeyValuePair<Villager, int> kvp in reservedWorkPointIndices)
        {
            Villager villager = kvp.Key;
            int index = kvp.Value;
            if (villager == null || index < 0 || index >= workPoints.Count || workPoints[index] == null)
            {
                if (invalidVillagers == null)
                {
                    invalidVillagers = new List<Villager>();
                }

                invalidVillagers.Add(villager);
            }
        }

        if (invalidVillagers == null)
        {
            return;
        }

        for (int i = 0; i < invalidVillagers.Count; i++)
        {
            reservedWorkPointIndices.Remove(invalidVillagers[i]);
        }
    }

    public bool IsWoodcutterBuilding()
    {
        return GetJobType() == JobType.Woodcutter;
    }

    public bool TryReserveNearestTreeForVillager(Villager villager, out TreeResourceNode tree)
    {
        tree = null;
        if (!IsWoodcutterBuilding() || villager == null)
        {
            return false;
        }

        if (reservedTrees.TryGetValue(villager, out TreeResourceNode existing)
            && existing != null
            && existing.CanBeReservedBy(villager))
        {
            tree = existing;
            return true;
        }

        ReleaseReservedTree(villager);

        float bestSqr = float.MaxValue;
        float maxSqr = TreeSearchRadius * TreeSearchRadius;
        Vector3 center = transform.position;

        for (int i = 0; i < TreeResourceNode.All.Count; i++)
        {
            TreeResourceNode candidate = TreeResourceNode.All[i];
            if (candidate == null || !candidate.CanBeReservedBy(villager))
            {
                continue;
            }

            float sqr = (candidate.transform.position - center).sqrMagnitude;
            if (sqr > maxSqr)
            {
                continue;
            }

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                tree = candidate;
            }
        }

        if (tree == null || !tree.TryReserve(villager))
        {
            return false;
        }

        reservedTrees[villager] = tree;
        return true;
    }

    public void ReleaseReservedTree(Villager villager)
    {
        if (villager == null)
        {
            return;
        }

        if (reservedTrees.TryGetValue(villager, out TreeResourceNode tree))
        {
            if (tree != null)
            {
                tree.Release(villager);
            }

            reservedTrees.Remove(villager);
        }
    }

    public int DepositWoodFromVillager(int amount)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0)
        {
            return 0;
        }

        storedWood += add;
        return add;
    }

    private struct MaterialColorState
    {
        public bool hasBaseColor;
        public Color baseColor;
        public bool hasColor;
        public Color color;
        public bool hasSurface;
        public float surface;
        public bool hasBlend;
        public float blend;
        public bool hasSrcBlend;
        public float srcBlend;
        public bool hasDstBlend;
        public float dstBlend;
        public bool hasZWrite;
        public float zWrite;
        public int renderQueue;
    }
}
