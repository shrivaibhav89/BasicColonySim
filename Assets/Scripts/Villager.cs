using System.Collections.Generic;
using UnityEngine;

public class Villager : MonoBehaviour
{
    public enum VillagerState
    {
        Idle,
        MovingToWork,
        Working,
        MovingToStorage,
        Depositing,
        ReturningHome
    }

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float waypointTolerance = 0.05f;
    public float rotationSpeed = 10f;
    public float panicSpeedMultiplier = 1.8f;
    [Range(0.1f, 1f)]
    public float groundSpeedMultiplier = 0.5f;

    [Header("Timing")]
    public float workDuration = 2f;
    public float depositDuration = 1f;

    [Header("Carrying")]
    public int carryCapacityPerResource = 5;
    [SerializeField] private GameObject carryFoodObject;
    [SerializeField] private GameObject carryWoodObject;
    [SerializeField] private GameObject carryStoneObject;
    [SerializeField] private string autoFindCarryFoodObjectName = "Haystack_01";

    [Header("Combat")]
    public int maxHealth = 30;
    [SerializeField] private int currentHealth;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedFloat = "MoveSpeed";
    [SerializeField] private string moveAnimSpeedFloat = "MoveAnimSpeed";
    [SerializeField] private string carryingBool = "IsCarrying";
    [SerializeField] private string panicBool = "IsPanicking";
    [SerializeField] private string workBool = "IsWorking";
    [SerializeField] private string workTypeInt = "WorkType";

    public VillagerState CurrentState { get; private set; } = VillagerState.Idle;

    private GridSystem gridSystem;
    private VillagerManager manager;

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex;
    private Vector3 targetWorldPos;
    private Vector2Int pendingTarget;
    private bool hasPendingTarget;

    private Building homeBuilding;
    private Building workBuilding;
    private Building storageBuilding;

    private float stateTimer;
    private int carryFood;
    private int carryWood;
    private int carryStone;
    private Vector3 moveOffset;
    private bool hasAssignedWorkPoint;
    private Vector3 assignedWorkPointWorld;
    private bool hasPreciseTargetAfterPath;
    private Vector3 preciseTargetAfterPath;
    private bool movingToPreciseTarget;
    private TreeResourceNode assignedTreeNode;
    private bool isCarryingForAnimation;
    private bool isPanicking;

    private enum AnimationWorkType
    {
        None = 0,
        Farm = 1,
        Quarry = 2,
        Wood = 3,
        Gathering = 4
    }

    public void Initialize(VillagerManager villagerManager, GridSystem grid)
    {
        manager = villagerManager;
        gridSystem = grid;
        currentHealth = Mathf.Max(1, maxHealth);
        CacheCarryVisualObjects();
        SetIdleAt(transform.position);
    }

    void OnEnable()
    {
        if (currentHealth <= 0)
        {
            currentHealth = Mathf.Max(1, maxHealth);
        }

        CacheCarryVisualObjects();
        UpdateCarryVisual();
    }

    public void SetMoveOffset(Vector3 offset)
    {
        moveOffset = offset;
    }

    public void SetWorkPoint(Building sourceBuilding, Vector3 worldPoint)
    {
        if (sourceBuilding == null || workBuilding == null || sourceBuilding != workBuilding)
        {
            return;
        }

        hasAssignedWorkPoint = true;
        assignedWorkPointWorld = worldPoint;

        if (CurrentState == VillagerState.MovingToWork || CurrentState == VillagerState.Working)
        {
            MoveToWorkDestination();
            CurrentState = VillagerState.MovingToWork;
            isPanicking = false;
            isCarryingForAnimation = false;
            SetMovingAnimation();
            UpdateCarryVisual();
        }
    }

    public bool IsAvailableForWork()
    {
        return CurrentState == VillagerState.Idle && workBuilding == null;
    }

    public void SetIdleAt(Vector3 worldPosition)
    {
        ClearAssignedTreeReservation();
        homeBuilding = null;
        workBuilding = null;
        storageBuilding = null;
        hasAssignedWorkPoint = false;
        assignedWorkPointWorld = Vector3.zero;
        hasPreciseTargetAfterPath = false;
        movingToPreciseTarget = false;
        ClearCargo();
        currentPath.Clear();
        pathIndex = 0;
        hasPendingTarget = false;
        targetWorldPos = worldPosition;
        transform.position = worldPosition;
        CurrentState = VillagerState.Idle;
        isPanicking = false;
        SetIdleAnimation();
        UpdateCarryVisual();
    }

    public void Teleport(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    public void AssignWork(Building home, Building work, Building storage)
    {
        ClearAssignedTreeReservation();
        homeBuilding = home;
        workBuilding = work;
        storageBuilding = storage;
        hasAssignedWorkPoint = false;
        assignedWorkPointWorld = Vector3.zero;
        hasPreciseTargetAfterPath = false;
        movingToPreciseTarget = false;
        ClearCargo();

        Vector2Int homeOrigin = homeBuilding != null ? homeBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
        transform.position = gridSystem.GridToWorld(homeOrigin);

        MoveToWorkDestination();

        CurrentState = VillagerState.MovingToWork;
        isPanicking = false;
        isCarryingForAnimation = false;
        SetMovingAnimation();
        UpdateCarryVisual();
    }
    public void GoInsideHome()
    {
        ClearAssignedTreeReservation();
        if (gridSystem == null)
        {
            return;
        }

        if (homeBuilding == null && manager != null)
        {
            homeBuilding = manager.GetNearestResidentialBuilding(transform.position);
            if (homeBuilding == null)
            {
                homeBuilding = manager.GetNearestDropoffBuilding(transform.position);
            }
        }

        if (homeBuilding == null)
        {
            CurrentState = VillagerState.Idle;
            isPanicking = false;
            SetIdleAnimation();
            UpdateCarryVisual();
            return;
        }

        SetPathTo(GetHomeStandTile());
        CurrentState = VillagerState.ReturningHome;
        isPanicking = true;
        isCarryingForAnimation = false;
        SetMovingAnimation();
        UpdateCarryVisual();
    }

    public void ReturnToWork()
    {
        if (workBuilding == null)
        {
            CurrentState = VillagerState.Idle;
            isPanicking = false;
            SetIdleAnimation();
            UpdateCarryVisual();
            return;
        }

        MoveToWorkDestination();
        CurrentState = VillagerState.MovingToWork;
        isPanicking = false;
        isCarryingForAnimation = false;
        SetMovingAnimation();
        UpdateCarryVisual();
    }
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDefeated())
            return;

        if (workBuilding != null && !workBuilding.IsProductionEnabled)
        {
            ResetWorkState("Production disabled");
            return;
        }

        if (CurrentState == VillagerState.Idle && workBuilding == null)
        {
            TryReturnHome();
        }

        switch (CurrentState)
        {
            case VillagerState.MovingToWork:
            case VillagerState.MovingToStorage:
            case VillagerState.ReturningHome:
                MoveAlongPath();
                break;
            case VillagerState.Working:
            case VillagerState.Depositing:
                TickStateTimer();
                break;
        }
    }

    private void TickStateTimer()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        if (CurrentState == VillagerState.Working)
        {
            if (IsWoodcutterWorkflow())
            {
                TickWoodcutterWork();
                return;
            }

            if (storageBuilding == null && manager != null)
            {
                storageBuilding = manager.GetDropoffForWorkBuilding(workBuilding);
                if (storageBuilding == null)
                {
                    storageBuilding = manager.GetNearestDropoffBuilding(transform.position);
                }
            }

            if (IsCargoFull())
            {
                if (manager != null)
                {
                    storageBuilding = manager.GetDropoffForWorkBuilding(workBuilding);
                    if (storageBuilding == null)
                    {
                        storageBuilding = manager.GetNearestDropoffBuilding(transform.position);
                    }
                }

                if (storageBuilding != null)
                {
                    if (workBuilding != null)
                    {
                        workBuilding.NotifyVillagerStoppedWork(this);
                    }

                    SetPathToBuilding(storageBuilding, workBuilding);
                    CurrentState = VillagerState.MovingToStorage;
                    isCarryingForAnimation = true;
                    SetMovingAnimation();
                    UpdateCarryVisual();
                    return;
                }

                stateTimer = GetWorkDuration();
                SetWorkAnimation();
                return;
            }

            if (storageBuilding == null)
            {
                stateTimer = GetWorkDuration();
                SetWorkAnimation();
                return;
            }

            if (!HarvestFromWorkBuilding())
            {
                stateTimer = GetWorkDuration();
                SetWorkAnimation();
                return;
            }

            if (IsCargoFull() && workBuilding != null)
            {
                workBuilding.NotifyVillagerStoppedWork(this);
            }

            if (IsCargoFull())
            {
                if (manager != null)
                {
                    storageBuilding = manager.GetDropoffForWorkBuilding(workBuilding) ?? storageBuilding;
                    if (storageBuilding == null)
                    {
                        storageBuilding = manager.GetNearestDropoffBuilding(transform.position);
                    }
                }

                if (storageBuilding == null)
                {
                    stateTimer = workDuration;
                    SetWorkAnimation();
                    return;
                }

                SetPathToBuilding(storageBuilding, workBuilding);
                CurrentState = VillagerState.MovingToStorage;
                isCarryingForAnimation = true;
                SetMovingAnimation();
                UpdateCarryVisual();
            }
            else
            {
                stateTimer = GetWorkDuration();
                SetWorkAnimation();
            }
        }
        else if (CurrentState == VillagerState.Depositing)
        {
            if (workBuilding != null)
            {
                MoveToWorkDestination(storageBuilding);
                CurrentState = VillagerState.MovingToWork;
                isCarryingForAnimation = false;
                SetMovingAnimation();
                UpdateCarryVisual();
            }
            else
            {
                CurrentState = VillagerState.Idle;
                SetIdleAnimation();
                UpdateCarryVisual();
            }
        }
    }

    private void MoveAlongPath()
    {
        if (movingToPreciseTarget)
        {
            float preciseSpeed = GetCurrentMoveSpeed();
            SetMovementAnimationSpeed();
            RotateTowardsTarget();
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, preciseSpeed * Time.deltaTime);
            float preciseDist = Vector3.Distance(transform.position, targetWorldPos);
            if (preciseDist <= waypointTolerance)
            {
                movingToPreciseTarget = false;
                OnReachedDestination();
            }

            return;
        }

        if (currentPath == null || currentPath.Count == 0)
        {
            ResetWorkState("No path available");
            return;
        }

        float speed = GetCurrentMoveSpeed();
        SetMovementAnimationSpeed();
        RotateTowardsTarget();
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, speed * Time.deltaTime);
        float dist = Vector3.Distance(transform.position, targetWorldPos);
        if (dist > waypointTolerance)
        {
            return;
        }

        pathIndex++;
        if (pathIndex >= currentPath.Count)
        {
            if (TryStartPreciseTargetMove())
            {
                return;
            }

            OnReachedDestination();
            return;
        }

        Vector3 nextPos = gridSystem.GridToWorld(currentPath[pathIndex]);
        nextPos.y = transform.position.y;
        targetWorldPos = nextPos + new Vector3(moveOffset.x, 0f, moveOffset.z);
    }

    private void SetMovementAnimationSpeed()
    {
        if (animator == null || string.IsNullOrEmpty(moveAnimSpeedFloat) || gridSystem == null)
        {
            return;
        }

        Vector2Int gridPos = gridSystem.WorldToGrid(transform.position);
        bool onRoad = gridSystem.IsRoadAt(gridPos);
        animator.SetFloat(moveAnimSpeedFloat, onRoad ? 2f : 1f);
    }

    private void RotateTowardsTarget()
    {
        Vector3 direction = targetWorldPos - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
    }

    private void OnReachedDestination()
    {
        if (CurrentState == VillagerState.MovingToWork)
        {
            if (hasPendingTarget)
            {
                hasPendingTarget = false;
                SetPathTo(pendingTarget);
                return;
            }

            CurrentState = VillagerState.Working;
            stateTimer = GetWorkDuration();
            if (workBuilding != null)
            {
                workBuilding.NotifyVillagerStartedWork(this);
            }
            SetWorkAnimation();
            UpdateCarryVisual();
        }
        else if (CurrentState == VillagerState.MovingToStorage)
        {
            if (hasPendingTarget)
            {
                hasPendingTarget = false;
                SetPathTo(pendingTarget);
                return;
            }

            CurrentState = VillagerState.Depositing;
            stateTimer = depositDuration;
            DepositCargo();
            isCarryingForAnimation = false;
            SetIdleAnimation();
            UpdateCarryVisual();
        }
        else if (CurrentState == VillagerState.ReturningHome)
        {
            CurrentState = VillagerState.Idle;
            isPanicking = false;
            SetIdleAnimation();
            UpdateCarryVisual();
        }
        else
        {
            CurrentState = VillagerState.Idle;
            SetIdleAnimation();
            UpdateCarryVisual();
        }
    }

    private void TryReturnHome()
    {
        if (gridSystem == null)
        {
            return;
        }

        if (homeBuilding == null && manager != null)
        {
            homeBuilding = manager.GetNearestResidentialBuilding(transform.position);
            if (homeBuilding == null)
            {
                homeBuilding = manager.GetNearestDropoffBuilding(transform.position);
            }
        }

        if (homeBuilding == null)
        {
            return;
        }

        Vector2Int target = GetHomeStandTile();
        Vector2Int current = gridSystem.WorldToGrid(transform.position);
        if (current == target)
        {
            return;
        }

        SetPathTo(target);
        CurrentState = VillagerState.ReturningHome;
        isPanicking = false;
        isCarryingForAnimation = false;
        SetMovingAnimation();
        UpdateCarryVisual();
    }

    private Vector2Int GetHomeStandTile()
    {
        if (homeBuilding == null || gridSystem == null)
        {
            return Vector2Int.zero;
        }

        if (manager != null && manager.TryGetNearestRoadTile(homeBuilding, out Vector2Int roadTile))
        {
            return roadTile;
        }

        return homeBuilding.GetGridOriginOrFallback(gridSystem);
    }

    private void SetPathTo(Vector2Int target)
    {
        movingToPreciseTarget = false;
        Vector2Int start = gridSystem.WorldToGrid(transform.position);
        if (start == target)
        {
            currentPath = new List<Vector2Int> { target };
            pathIndex = 0;
            Vector3 startPos = gridSystem.GridToWorld(target);
            startPos.y = transform.position.y;
            targetWorldPos = startPos + new Vector3(moveOffset.x, 0f, moveOffset.z);
            return;
        }
        currentPath = GridPathfinder.FindPath(gridSystem, start, target, true, true);
        pathIndex = 0;
        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning($"Villager '{gameObject.name}' could not find path from {start} to {target}");
            return;
        }

        if (currentPath != null && currentPath.Count > 0)
        {
            Vector3 startPos = gridSystem.GridToWorld(currentPath[0]);
            startPos.y = transform.position.y;
            targetWorldPos = startPos + new Vector3(moveOffset.x, 0f, moveOffset.z);
        }
    }

    private void MoveToWorkDestination(Building roadPreferenceBuilding = null)
    {
        if (workBuilding == null)
        {
            return;
        }

        if (IsWoodcutterWorkflow() && TryAcquireTreeAndMove(roadPreferenceBuilding))
        {
            return;
        }

        Vector3 targetWorldPoint = hasAssignedWorkPoint
            ? assignedWorkPointWorld
            : workBuilding.transform.position + new Vector3(moveOffset.x, 0f, moveOffset.z);

        Building preferredRoadSource = roadPreferenceBuilding != null ? roadPreferenceBuilding : homeBuilding;
        SetPathToBuildingTargetWorld(workBuilding, preferredRoadSource, targetWorldPoint, true);
    }

    private void SetPathToBuildingTargetWorld(Building targetBuilding, Building roadPreferenceBuilding, Vector3 targetWorld, bool addPreciseMoveAfterPath)
    {
        if (gridSystem == null)
        {
            return;
        }

        if (manager == null)
        {
            Vector2Int fallback = targetBuilding != null
                ? targetBuilding.GetGridOriginOrFallback(gridSystem)
                : gridSystem.WorldToGrid(targetWorld);
            SetPathTo(fallback);
            if (addPreciseMoveAfterPath)
            {
                SetPreciseTargetAfterPath(targetWorld);
            }
            return;
        }

        Vector2Int targetTile = manager.GetBestTargetTileForWorldPosition(targetBuilding, targetWorld);
        Vector2Int start = gridSystem.WorldToGrid(transform.position);

        Building roadSearchSource = roadPreferenceBuilding != null ? roadPreferenceBuilding : targetBuilding;
        if (manager.TryGetNearestRoadTile(roadSearchSource, out Vector2Int roadTile)
            && roadTile != start
            && roadTile != targetTile)
        {
            pendingTarget = targetTile;
            hasPendingTarget = true;
            SetPathTo(roadTile);
        }
        else
        {
            hasPendingTarget = false;
            SetPathTo(targetTile);
        }

        if (addPreciseMoveAfterPath)
        {
            SetPreciseTargetAfterPath(targetWorld);
        }
    }

    private void SetPreciseTargetAfterPath(Vector3 worldPosition)
    {
        hasPreciseTargetAfterPath = true;
        preciseTargetAfterPath = worldPosition;
        preciseTargetAfterPath.y = transform.position.y;
    }

    private bool TryStartPreciseTargetMove()
    {
        if (!hasPreciseTargetAfterPath)
        {
            return false;
        }

        hasPreciseTargetAfterPath = false;
        movingToPreciseTarget = true;
        targetWorldPos = preciseTargetAfterPath;
        targetWorldPos.y = transform.position.y;
        return true;
    }

    private void ResetWorkState(string reason)
    {
        ClearAssignedTreeReservation();
        if (workBuilding != null)
        {
            workBuilding.NotifyVillagerStoppedWork(this);
            workBuilding.UnassignVillager(this);
        }

        workBuilding = null;
        storageBuilding = null;
        hasAssignedWorkPoint = false;
        assignedWorkPointWorld = Vector3.zero;
        hasPreciseTargetAfterPath = false;
        movingToPreciseTarget = false;
        currentPath.Clear();
        pathIndex = 0;
        hasPendingTarget = false;
        CurrentState = VillagerState.Idle;
        isPanicking = false;
        isCarryingForAnimation = false;
        SetIdleAnimation();
        UpdateCarryVisual();
        Debug.LogWarning($"Villager '{gameObject.name}' reset to idle. Reason: {reason}");
    }

    public void ForceIdle(string reason)
    {
        ResetWorkState(reason);
    }

    public void TakeDamage(int amount)
    {
        int dmg = Mathf.Max(0, amount);
        if (dmg <= 0 || !gameObject.activeInHierarchy)
        {
            return;
        }

        currentHealth -= dmg;
        if (currentHealth > 0)
        {
            ForceIdle("Damaged by enemy projectile");
            return;
        }

        currentHealth = 0;
        ForceIdle("Villager defeated");
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.NotifyVillagerDied();
        }

        if (manager != null)
        {
            manager.NotifyVillagerDied(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    [ContextMenu("Log Debug State")]
    public void LogDebugState()
    {
        string pathInfo = currentPath == null ? "null" : currentPath.Count.ToString();
        Vector2Int gridPos = gridSystem != null ? gridSystem.WorldToGrid(transform.position) : Vector2Int.zero;
        Debug.Log($"[Villager Debug] {gameObject.name} State={CurrentState} Grid={gridPos} PathCount={pathInfo} PathIndex={pathIndex} HasPending={hasPendingTarget} Pending={pendingTarget} TargetWorld={targetWorldPos} Work={(workBuilding != null ? workBuilding.name : "null")} Storage={(storageBuilding != null ? storageBuilding.name : "null")} StateTimer={stateTimer} Carry=F{carryFood},W{carryWood},S{carryStone}");
    }

    void OnDrawGizmosSelected()
    {
        if (gridSystem == null || currentPath == null) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = transform.position;
        for (int i = pathIndex; i < currentPath.Count; i++)
        {
            Vector3 wp = gridSystem.GridToWorld(currentPath[i]);
            wp.y = transform.position.y;
            Gizmos.DrawLine(prev, wp);
            Gizmos.DrawSphere(wp, 0.1f);
            prev = wp;
        }

        if (hasPendingTarget)
        {
            Gizmos.color = Color.yellow;
            Vector3 p = gridSystem.GridToWorld(pendingTarget);
            p.y = transform.position.y;
            Gizmos.DrawWireSphere(p, 0.2f);
        }
    }

    private void SetPathToBuilding(Building targetBuilding, Building roadPreferenceBuilding)
    {
        hasPreciseTargetAfterPath = false;
        movingToPreciseTarget = false;
        hasPendingTarget = false;
        if (manager == null)
        {
            Vector2Int fallback = targetBuilding != null ? targetBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
            SetPathTo(fallback);
            return;
        }

        Vector2Int target = manager.GetBestTargetTile(targetBuilding);
        Vector2Int start = gridSystem.WorldToGrid(transform.position);

        Building roadSearchSource = roadPreferenceBuilding != null ? roadPreferenceBuilding : targetBuilding;
        if (manager.TryGetNearestRoadTile(roadSearchSource, out Vector2Int roadTile)
            && roadTile != start
            && roadTile != target)
        {
            pendingTarget = target;
            hasPendingTarget = true;
            SetPathTo(roadTile);
            return;
        }

        SetPathTo(target);
    }

    private float GetCurrentMoveSpeed()
    {
        if (gridSystem == null)
        {
            return moveSpeed;
        }

        Vector2Int gridPos = gridSystem.WorldToGrid(transform.position);
        bool onRoad = gridSystem.IsRoadAt(gridPos);
        float speed = onRoad ? moveSpeed : moveSpeed * groundSpeedMultiplier;
        if (isPanicking && CurrentState == VillagerState.ReturningHome)
        {
            speed *= Mathf.Max(1f, panicSpeedMultiplier);
        }

        return speed;
    }

    private bool HarvestFromWorkBuilding()
    {
        if (workBuilding == null)
        {
            return false;
        }

        int perHarvestFood = CalculateHarvestAmount(workBuilding.GetFoodPerHarvest(), workBuilding.GetFoodPerSec());
        int perHarvestWood = CalculateHarvestAmount(workBuilding.GetWoodPerHarvest(), workBuilding.GetWoodPerSec());
        int perHarvestStone = CalculateHarvestAmount(workBuilding.GetStonePerHarvest(), workBuilding.GetStonePerSec());

        workBuilding.HarvestShared(perHarvestFood, perHarvestWood, perHarvestStone, out int harvestedFood, out int harvestedWood, out int harvestedStone);

        bool collected = false;

        if (harvestedFood > 0 && carryFood < carryCapacityPerResource)
        {
            int space = carryCapacityPerResource - carryFood;
            int add = Mathf.Min(space, harvestedFood);
            carryFood += add;
            collected |= add > 0;
        }

        if (harvestedWood > 0 && carryWood < carryCapacityPerResource)
        {
            int space = carryCapacityPerResource - carryWood;
            int add = Mathf.Min(space, harvestedWood);
            carryWood += add;
            collected |= add > 0;
        }

        if (harvestedStone > 0 && carryStone < carryCapacityPerResource)
        {
            int space = carryCapacityPerResource - carryStone;
            int add = Mathf.Min(space, harvestedStone);
            carryStone += add;
            collected |= add > 0;
        }

        return collected;
    }

    private int CalculateHarvestAmount(int perHarvestAmount, int ratePerSecondFallback)
    {
        if (perHarvestAmount > 0)
        {
            return perHarvestAmount;
        }

        if (ratePerSecondFallback <= 0)
        {
            return 0;
        }

        float amount = ratePerSecondFallback * GetWorkDuration();
        return Mathf.Max(1, Mathf.CeilToInt(amount));
    }

    private float GetWorkDuration()
    {
        if (workBuilding != null)
        {
            float duration = workBuilding.GetHarvestDuration();
            if (duration > 0f)
            {
                return duration;
            }
        }

        return workDuration;
    }

    private bool IsCargoFull()
    {
        return carryFood >= carryCapacityPerResource
            || carryWood >= carryCapacityPerResource
            || carryStone >= carryCapacityPerResource;
    }

    private void DepositCargo()
    {
        if (carryFood == 0 && carryWood == 0 && carryStone == 0)
        {
            return;
        }

        int depositFood = carryFood;
        int depositWood = carryWood;
        int depositStone = carryStone;

        if (IsWoodcutterWorkflow() && workBuilding != null)
        {
            depositWood = workBuilding.DepositWoodFromVillager(carryWood);
        }

        ResourceManager.Instance.AddProductionResources(depositFood, depositWood, depositStone);
        ClearCargo();
    }

    private void ClearCargo()
    {
        carryFood = 0;
        carryWood = 0;
        carryStone = 0;
        UpdateCarryVisual();
    }

    private void SetIdleAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(speedFloat))
        {
            animator.SetFloat(speedFloat, 0f);
        }
        if (!string.IsNullOrEmpty(moveAnimSpeedFloat))
        {
            animator.SetFloat(moveAnimSpeedFloat, 1f);
        }

        if (!string.IsNullOrEmpty(carryingBool))
        {
            animator.SetBool(carryingBool, false);
        }
        if (!string.IsNullOrEmpty(panicBool))
        {
            animator.SetBool(panicBool, false);
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, false);
        }

        if (!string.IsNullOrEmpty(workTypeInt))
        {
            animator.SetInteger(workTypeInt, (int)AnimationWorkType.None);
        }
    }

    private void SetWorkAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(speedFloat))
        {
            animator.SetFloat(speedFloat, 0f);
        }
        if (!string.IsNullOrEmpty(moveAnimSpeedFloat))
        {
            animator.SetFloat(moveAnimSpeedFloat, 1f);
        }

        if (!string.IsNullOrEmpty(carryingBool))
        {
            animator.SetBool(carryingBool, false);
        }
        if (!string.IsNullOrEmpty(panicBool))
        {
            animator.SetBool(panicBool, false);
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, true);
        }

        if (!string.IsNullOrEmpty(workTypeInt))
        {
            animator.SetInteger(workTypeInt, (int)GetWorkTypeForBuilding());
        }
    }

    private void SetMovingAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(speedFloat))
        {
            animator.SetFloat(speedFloat, 1f);
        }
        if (!string.IsNullOrEmpty(moveAnimSpeedFloat))
        {
            animator.SetFloat(moveAnimSpeedFloat, 1f);
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, false);
        }

        if (!string.IsNullOrEmpty(carryingBool))
        {
            animator.SetBool(carryingBool, isCarryingForAnimation);
        }
        if (!string.IsNullOrEmpty(panicBool))
        {
            animator.SetBool(panicBool, isPanicking && CurrentState == VillagerState.ReturningHome);
        }

        if (!string.IsNullOrEmpty(workTypeInt))
        {
            animator.SetInteger(workTypeInt, (int)AnimationWorkType.None);
        }
    }

    private AnimationWorkType GetWorkTypeForBuilding()
    {
        if (workBuilding == null)
        {
            return AnimationWorkType.Gathering;
        }

        string buildingName = workBuilding.gameObject != null ? workBuilding.gameObject.name : string.Empty;
        string dataName = workBuilding.buildingData != null ? workBuilding.buildingData.buildingName : string.Empty;
        string merged = (buildingName + " " + dataName).ToLowerInvariant();
        if (merged.Contains("farm"))
        {
            return AnimationWorkType.Farm;
        }

        if (merged.Contains("quary") || merged.Contains("quarry") || merged.Contains("mine") || merged.Contains("stone"))
        {
            return AnimationWorkType.Quarry;
        }

        if (merged.Contains("wood") || merged.Contains("lumber") || merged.Contains("log") || merged.Contains("tree"))
        {
            return AnimationWorkType.Wood;
        }

        return AnimationWorkType.Gathering;
    }

    private bool IsWoodcutterWorkflow()
    {
        return workBuilding != null && workBuilding.IsWoodcutterBuilding();
    }

    private void TickWoodcutterWork()
    {
        if (workBuilding == null)
        {
            CurrentState = VillagerState.Idle;
            SetIdleAnimation();
            UpdateCarryVisual();
            return;
        }

        if (assignedTreeNode == null || !assignedTreeNode.CanBeReservedBy(this))
        {
            if (TryAcquireTreeAndMove(workBuilding))
            {
                CurrentState = VillagerState.MovingToWork;
                isCarryingForAnimation = false;
                SetMovingAnimation();
                UpdateCarryVisual();
                return;
            }

            if (carryWood > 0)
            {
                SetPathToBuilding(workBuilding, workBuilding);
                CurrentState = VillagerState.MovingToStorage;
                isCarryingForAnimation = true;
                SetMovingAnimation();
                UpdateCarryVisual();
                return;
            }

            stateTimer = Mathf.Max(0.2f, GetWorkDuration());
            SetWorkAnimation();
            return;
        }

        int chopAmount = Mathf.Max(1, CalculateHarvestAmount(workBuilding.GetWoodPerHarvest(), workBuilding.GetWoodPerSec()));
        bool chopped = assignedTreeNode.TryChop(this, chopAmount, out int harvestedWood);
        if (chopped && harvestedWood > 0)
        {
            int space = Mathf.Max(0, carryCapacityPerResource - carryWood);
            int add = Mathf.Min(space, harvestedWood);
            carryWood += add;
        }

        if (assignedTreeNode == null || assignedTreeNode.IsDepleted || !assignedTreeNode.gameObject.activeInHierarchy)
        {
            ClearAssignedTreeReservation();
        }

        if (carryWood >= carryCapacityPerResource || !chopped)
        {
            if (carryWood > 0)
            {
                SetPathToBuilding(workBuilding, workBuilding);
                CurrentState = VillagerState.MovingToStorage;
                isCarryingForAnimation = true;
                SetMovingAnimation();
                UpdateCarryVisual();
                return;
            }

            if (TryAcquireTreeAndMove(workBuilding))
            {
                CurrentState = VillagerState.MovingToWork;
                isCarryingForAnimation = false;
                SetMovingAnimation();
                UpdateCarryVisual();
                return;
            }
        }

        stateTimer = Mathf.Max(0.2f, GetWorkDuration());
        SetWorkAnimation();
    }

    private bool TryAcquireTreeAndMove(Building roadPreferenceBuilding)
    {
        if (!IsWoodcutterWorkflow())
        {
            return false;
        }

        if (!workBuilding.TryReserveNearestTreeForVillager(this, out TreeResourceNode tree) || tree == null)
        {
            return false;
        }

        assignedTreeNode = tree;
        Vector3 treeTarget = tree.transform.position;
        treeTarget.y = transform.position.y;
        SetPathToBuildingTargetWorld(workBuilding, roadPreferenceBuilding, treeTarget, true);
        return true;
    }

    private void ClearAssignedTreeReservation()
    {
        if (workBuilding != null)
        {
            workBuilding.ReleaseReservedTree(this);
        }

        assignedTreeNode = null;
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
    }

    private void CacheCarryVisualObjects()
    {
        if (carryFoodObject == null && !string.IsNullOrEmpty(autoFindCarryFoodObjectName))
        {
            Transform found = FindChildRecursive(transform, autoFindCarryFoodObjectName);
            if (found != null)
            {
                carryFoodObject = found.gameObject;
            }
        }
    }

    private void UpdateCarryVisual()
    {
        bool shouldShowCarry = CurrentState == VillagerState.MovingToStorage && HasAnyCargo();
        bool showFood = shouldShowCarry && carryFood >= carryWood && carryFood >= carryStone && carryFood > 0;
        bool showWood = shouldShowCarry && !showFood && carryWood >= carryStone && carryWood > 0;
        bool showStone = shouldShowCarry && !showFood && !showWood && carryStone > 0;

        if (carryFoodObject != null)
        {
            carryFoodObject.SetActive(showFood);
        }

        if (carryWoodObject != null)
        {
            carryWoodObject.SetActive(showWood);
        }

        if (carryStoneObject != null)
        {
            carryStoneObject.SetActive(showStone);
        }
    }

    private bool HasAnyCargo()
    {
        return carryFood > 0 || carryWood > 0 || carryStone > 0;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
