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

    public void Initialize(VillagerManager villagerManager, GridSystem grid)
    {
        manager = villagerManager;
        gridSystem = grid;
        CurrentState = VillagerState.Idle;
        currentPath.Clear();
        pathIndex = 0;
    }

    public void AssignWork(Building home, Building work, Building storage)
    {
        homeBuilding = home;
        workBuilding = work;
        storageBuilding = storage;

        Vector2Int homeOrigin = homeBuilding != null ? homeBuilding.GetGridOriginOrFallback(gridSystem) : Vector2Int.zero;
        transform.position = gridSystem.GridToWorld(homeOrigin);

        Vector2Int workTarget = manager.GetBestTargetTile(workBuilding);
        if (manager.TryGetNearestRoadTile(homeBuilding, out Vector2Int roadTile) && roadTile != homeOrigin)
        {
            pendingTarget = workTarget;
            hasPendingTarget = true;
            SetPathTo(roadTile);
        }
        else
        {
            SetPathTo(workTarget);
        }

        CurrentState = VillagerState.MovingToWork;
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
            if (storageBuilding != null)
            {
                SetPathTo(manager.GetBestTargetTile(storageBuilding));
                CurrentState = VillagerState.MovingToStorage;
            }
            else
            {
                CurrentState = VillagerState.Idle;
            }
        }
        else if (CurrentState == VillagerState.Depositing)
        {
            if (workBuilding != null)
            {
                SetPathTo(manager.GetBestTargetTile(workBuilding));
                CurrentState = VillagerState.MovingToWork;
            }
            else
            {
                CurrentState = VillagerState.Idle;
            }
        }
    }

    private void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            CurrentState = VillagerState.Idle;
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
        }
        else
        {
            CurrentState = VillagerState.Idle;
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
}
