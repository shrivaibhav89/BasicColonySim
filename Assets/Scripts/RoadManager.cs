using UnityEngine;
using System.Collections.Generic;

public class RoadManager : MonoBehaviour
{
    [Header("References")]
    public GridSystem gridSystem;
    public Camera mainCamera;
    public GameObject roadPrefab;

    [Header("Road Settings")]
    public float roadHeight = 0f;
    public int roadWoodCost = 1;

    [Header("Materials")]
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    private bool isActive;
    private bool isDragging;
    private Vector2Int dragStartGridPos = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int currentMouseGridPos = new Vector2Int(int.MinValue, int.MinValue);
    private List<GameObject> previewGhosts = new List<GameObject>();
    private int totalPreviewCost = 0;

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartGridPos = GetMouseGridPos();
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            UpdateDragPreview();
        }
        else if (!isDragging)
        {
            // Show a single ghost at the current mouse position
            Vector2Int mouseGridPos = GetMouseGridPos();
            if (mouseGridPos != currentMouseGridPos)
            {
                currentMouseGridPos = mouseGridPos;
                ClearPreview();
                if (mouseGridPos != new Vector2Int(int.MinValue, int.MinValue))
                {
                    bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(0, roadWoodCost, 0);
                    bool isValid = gridSystem.IsValidPlacement(mouseGridPos) && canAfford;
                    GameObject ghost = CreatePreviewGhost(mouseGridPos, isValid);
                    previewGhosts.Add(ghost);
                }
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            PlaceDraggedRoads();
            isDragging = false;
            dragStartGridPos = new Vector2Int(int.MinValue, int.MinValue);
            ClearPreview();
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            SetRoadPlacementActive(false);
        }
    }

    public void SetRoadPlacementActive(bool active)
    {
        isActive = active;
        if (!isActive)
        {
            isDragging = false;
            ClearPreview();
            dragStartGridPos = new Vector2Int(int.MinValue, int.MinValue);
        }
    }

    public bool IsActive()
    {
        return isActive;
    }

    private Vector2Int GetMouseGridPos()
    {
        if (mainCamera == null || gridSystem == null)
        {
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
        {
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        return gridSystem.WorldToGrid(hit.point);
    }

    private void UpdateDragPreview()
    {
        currentMouseGridPos = GetMouseGridPos();
        if (currentMouseGridPos == new Vector2Int(int.MinValue, int.MinValue) || 
            dragStartGridPos == new Vector2Int(int.MinValue, int.MinValue))
        {
            return;
        }

        // Get path from start to current mouse position (horizontal/vertical only)
        List<Vector2Int> roadPath = GetHorizontalVerticalPath(dragStartGridPos, currentMouseGridPos);

        // Calculate total cost
        totalPreviewCost = CalculatePathCost(roadPath);

        // Check if affordable
        bool canAfford = ResourceManager.Instance != null && 
                        ResourceManager.Instance.CanAfford(0, totalPreviewCost, 0);

        // Create/update preview ghosts
        ClearPreview();
        foreach (Vector2Int gridPos in roadPath)
        {
            if (!gridSystem.IsValidPlacement(gridPos))
            {
                canAfford = false;
            }

            GameObject ghost = CreatePreviewGhost(gridPos, canAfford);
            previewGhosts.Add(ghost);
        }
    }

    private List<Vector2Int> GetHorizontalVerticalPath(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        // Determine direction (horizontal first, then vertical)
        if (startX != endX)
        {
            // Move horizontally first
            int step = startX < endX ? 1 : -1;
            for (int x = startX; x != endX; x += step)
            {
                path.Add(new Vector2Int(x, startY));
            }
            path.Add(new Vector2Int(endX, startY));

            // Then move vertically
            if (startY != endY)
            {
                step = startY < endY ? 1 : -1;
                for (int y = startY + step; y != endY + step; y += step)
                {
                    path.Add(new Vector2Int(endX, y));
                }
            }
        }
        else if (startY != endY)
        {
            // Move vertically only
            int step = startY < endY ? 1 : -1;
            for (int y = startY; y != endY; y += step)
            {
                path.Add(new Vector2Int(startX, y));
            }
            path.Add(new Vector2Int(startX, endY));
        }
        else
        {
            path.Add(start);
        }

        return path;
    }

    private int CalculatePathCost(List<Vector2Int> path)
    {
        return path.Count * roadWoodCost;
    }

    private GameObject CreatePreviewGhost(Vector2Int gridPos, bool isValid)
    {
        if (roadPrefab == null)
        {
            return null;
        }

        GameObject ghost = Instantiate(roadPrefab);
        ghost.name = "Ghost_Road_" + gridPos;

        Vector3 worldPos = gridSystem.GridToWorld(gridPos);
        worldPos.y = roadHeight;
        ghost.transform.position = worldPos;

        MeshRenderer[] renderers = ghost.GetComponentsInChildren<MeshRenderer>();
        Material materialToUse = isValid ? validPlacementMaterial : invalidPlacementMaterial;
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.material = materialToUse;
        }

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        return ghost;
    }

    private void ClearPreview()
    {
        foreach (GameObject ghost in previewGhosts)
        {
            if (ghost != null)
            {
                Destroy(ghost);
            }
        }
        previewGhosts.Clear();
        totalPreviewCost = 0;
    }

    private void PlaceDraggedRoads()
    {
        if (dragStartGridPos == new Vector2Int(int.MinValue, int.MinValue) || 
            currentMouseGridPos == new Vector2Int(int.MinValue, int.MinValue))
        {
            return;
        }

        List<Vector2Int> roadPath = GetHorizontalVerticalPath(dragStartGridPos, currentMouseGridPos);
        int totalCost = CalculatePathCost(roadPath);

        // Check if can afford
        if (ResourceManager.Instance == null || !ResourceManager.Instance.CanAfford(0, totalCost, 0))
        {
            Debug.Log("Not enough wood to place roads!");
            return;
        }

        // Place all roads
        foreach (Vector2Int gridPos in roadPath)
        {
            if (!gridSystem.IsValidPlacement(gridPos))
            {
                continue; // Skip occupied tiles
            }

            Vector3 worldPos = gridSystem.GridToWorld(gridPos);
            worldPos.y = roadHeight;

            GameObject road = Instantiate(roadPrefab, worldPos, Quaternion.identity);
            road.name = roadPrefab.name;

            GridObject gridObject = road.GetComponent<GridObject>();
            if (gridObject != null)
            {
                gridObject.Initialize(gridSystem, gridPos);
            }

            gridSystem.SetOccupied(gridPos, true);
            gridSystem.SetRoad(gridPos, true);
        }

        // Spend resources once for entire drag
        ResourceManager.Instance.SpendResources(0, totalCost, 0);
        Debug.Log($"Placed {roadPath.Count} road tiles for {totalCost} wood");
    }
}

