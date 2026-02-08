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
        Depositing
    }

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float waypointTolerance = 0.05f;
    [Range(0.1f, 1f)]
    public float groundSpeedMultiplier = 0.5f;

    [Header("Timing")]
    public float workDuration = 2f;
    public float depositDuration = 1f;

    [Header("Carrying")]
    public int carryCapacityPerResource = 5;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleBool = "IsIdle";
    [SerializeField] private string workBool = "IsWorking";

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

    public void Initialize(VillagerManager villagerManager, GridSystem grid)
    {
        manager = villagerManager;
        gridSystem = grid;
        CurrentState = VillagerState.Idle;
        currentPath.Clear();
        pathIndex = 0;
        SetIdleAnimation();
    }

    public void AssignWork(Building home, Building work, Building storage)
    {
        homeBuilding = home;
        workBuilding = work;
        storageBuilding = storage;
        ClearCargo();

        Vector2Int homeOrigin = homeBuilding != null ? homeBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
        transform.position = gridSystem.GridToWorld(homeOrigin);

        SetPathToBuilding(workBuilding, homeBuilding);

        CurrentState = VillagerState.MovingToWork;
        SetMovingAnimation();
    }

    void Update()
    {
        switch (CurrentState)
        {
            case VillagerState.MovingToWork:
            case VillagerState.MovingToStorage:
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
            if (storageBuilding == null && manager != null && workBuilding != null)
            {
                storageBuilding = manager.GetNearestDropoffBuilding(workBuilding.transform.position);
            }

            if (IsCargoFull())
            {
                if (storageBuilding != null)
                {
                    if (workBuilding != null)
                    {
                        workBuilding.NotifyVillagerStoppedWork(this);
                    }

                    SetPathToBuilding(storageBuilding, workBuilding);
                    CurrentState = VillagerState.MovingToStorage;
                    SetMovingAnimation();
                    return;
                }

                stateTimer = workDuration;
                SetWorkAnimation();
                return;
            }

            if (storageBuilding == null)
            {
                stateTimer = workDuration;
                SetWorkAnimation();
                return;
            }

            if (!HarvestFromWorkBuilding())
            {
                stateTimer = workDuration;
                SetWorkAnimation();
                return;
            }

            if (IsCargoFull() && workBuilding != null)
            {
                workBuilding.NotifyVillagerStoppedWork(this);
            }

            if (IsCargoFull())
            {
                SetPathToBuilding(storageBuilding, workBuilding);
                CurrentState = VillagerState.MovingToStorage;
                SetMovingAnimation();
            }
            else
            {
                stateTimer = workDuration;
                SetWorkAnimation();
            }
        }
        else if (CurrentState == VillagerState.Depositing)
        {
            if (workBuilding != null)
            {
                SetPathToBuilding(workBuilding, storageBuilding);
                CurrentState = VillagerState.MovingToWork;
                SetMovingAnimation();
            }
            else
            {
                CurrentState = VillagerState.Idle;
                SetIdleAnimation();
            }
        }
    }

    private void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            CurrentState = VillagerState.Idle;
            SetIdleAnimation();
            return;
        }

        float speed = GetCurrentMoveSpeed();
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, speed * Time.deltaTime);
        float dist = Vector3.Distance(transform.position, targetWorldPos);
        if (dist > waypointTolerance)
        {
            return;
        }

        pathIndex++;
        if (pathIndex >= currentPath.Count)
        {
            OnReachedDestination();
            return;
        }

        Vector3 nextPos = gridSystem.GridToWorld(currentPath[pathIndex]);
        nextPos.y = transform.position.y;
        targetWorldPos = nextPos;
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
            stateTimer = workDuration;
            if (workBuilding != null)
            {
                workBuilding.NotifyVillagerStartedWork(this);
            }
            SetWorkAnimation();
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
            SetIdleAnimation();
        }
        else
        {
            CurrentState = VillagerState.Idle;
            SetIdleAnimation();
        }
    }

    private void SetPathTo(Vector2Int target)
    {
        Vector2Int start = gridSystem.WorldToGrid(transform.position);
        currentPath = GridPathfinder.FindPath(gridSystem, start, target, true, true);
        pathIndex = 0;
        if (currentPath.Count > 0)
        {
            Vector3 startPos = gridSystem.GridToWorld(currentPath[0]);
            startPos.y = transform.position.y;
            targetWorldPos = startPos;
        }
    }

    private void SetPathToBuilding(Building targetBuilding, Building roadPreferenceBuilding)
    {
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
        return onRoad ? moveSpeed : moveSpeed * groundSpeedMultiplier;
    }

    private bool HarvestFromWorkBuilding()
    {
        if (workBuilding == null)
        {
            return false;
        }

        int harvestedFood = CalculateHarvestAmount(workBuilding.GetFoodPerSec());
        int harvestedWood = CalculateHarvestAmount(workBuilding.GetWoodPerSec());
        int harvestedStone = CalculateHarvestAmount(workBuilding.GetStonePerSec());

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

    private int CalculateHarvestAmount(int ratePerSecond)
    {
        if (ratePerSecond <= 0)
        {
            return 0;
        }

        float amount = ratePerSecond * workDuration;
        return Mathf.Max(1, Mathf.CeilToInt(amount));
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

        ResourceManager.Instance.AddResources(carryFood, carryWood, carryStone);
        ClearCargo();
    }

    private void ClearCargo()
    {
        carryFood = 0;
        carryWood = 0;
        carryStone = 0;
    }

    private void SetIdleAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, false);
        }

        if (!string.IsNullOrEmpty(idleBool))
        {
            animator.SetBool(idleBool, true);
        }
    }

    private void SetWorkAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(idleBool))
        {
            animator.SetBool(idleBool, false);
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, true);
        }
    }

    private void SetMovingAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(idleBool))
        {
            animator.SetBool(idleBool, false);
        }

        if (!string.IsNullOrEmpty(workBool))
        {
            animator.SetBool(workBool, false);
        }
    }
}
