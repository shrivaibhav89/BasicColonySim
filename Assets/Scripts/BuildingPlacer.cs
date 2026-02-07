using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("References")]
    public GridSystem gridSystem;
    public Camera mainCamera;
    public RoadManager roadManager;

    [Header("Placement Settings")]
    public GameObject currentBuildingPrefab;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    public float ghostHeight = 0.5f;
    public KeyCode roadModeHotkey = KeyCode.R;

    private GameObject ghostObject;
    private bool isPlacing = false;
    private Vector2Int lastGridPos;
    private MeshRenderer[] ghostRenderers;

    private enum PlacementMode
    {
        None,
        Building,
        Road
    }

    private PlacementMode placementMode = PlacementMode.None;

    void Update()
    {
        if (Input.GetKeyDown(roadModeHotkey))
        {
            ToggleRoadPlacement();
        }

        if (placementMode == PlacementMode.Building && isPlacing && currentBuildingPrefab != null)
        {
            UpdateGhostPosition();

            if (Input.GetMouseButtonDown(0)) // Left click
            {
                TryPlaceBuilding();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) // Right click or ESC
            {
                CancelPlacement();
            }
        }
        else if (placementMode == PlacementMode.Road)
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                StopRoadPlacement();
            }
        }
    }

    public void StartPlacement(GameObject buildingPrefab)
    {
        StopRoadPlacement();
        currentBuildingPrefab = buildingPrefab;
        isPlacing = true;
        placementMode = PlacementMode.Building;

        // Create ghost preview
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = Instantiate(currentBuildingPrefab);
        ghostObject.name = "Ghost_" + buildingPrefab.name;

        Building ghostBuilding = ghostObject.GetComponent<Building>();
        if (ghostBuilding != null)
        {
            ghostBuilding.isGhost = true;
        }

        // Get all mesh renderers
        ghostRenderers = ghostObject.GetComponentsInChildren<MeshRenderer>();

        // Make transparent
        foreach (MeshRenderer renderer in ghostRenderers)
        {
            renderer.material = validPlacementMaterial;
        }

        // Disable any colliders on ghost
        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    void UpdateGhostPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector2Int gridPos = gridSystem.WorldToGrid(hit.point);

            // Only update if grid position changed
            if (gridPos != lastGridPos)
            {
                lastGridPos = gridPos;

                // Check if valid placement
                Building buildingData = currentBuildingPrefab.GetComponent<Building>();
                Vector2Int footprint = buildingData != null ? buildingData.footprintSize : new Vector2Int(1, 1);
                bool isValid = gridSystem.IsAreaValidPlacement(gridPos, footprint) &&
                               PathValidator.HasAdjacentRoadInArea(gridSystem, gridPos, footprint);

                // Update ghost position
                Vector3 worldPos = gridSystem.GridToWorld(gridPos);
                Vector3 footprintOffset = new Vector3(
                    (footprint.x - 1) * gridSystem.cellSize * 0.5f,
                    0f,
                    (footprint.y - 1) * gridSystem.cellSize * 0.5f);
                worldPos += footprintOffset;
                worldPos.y = ghostHeight;
                ghostObject.transform.position = worldPos;

                // Update ghost material (green = valid, red = invalid)
                Material materialToUse = isValid ? validPlacementMaterial : invalidPlacementMaterial;
                foreach (MeshRenderer renderer in ghostRenderers)
                {
                    renderer.material = materialToUse;
                }
            }
        }
    }

    void TryPlaceBuilding()
    {
        Building buildingData = currentBuildingPrefab.GetComponent<Building>();
        Vector2Int footprint = buildingData != null ? buildingData.footprintSize : new Vector2Int(1, 1);

        if (gridSystem.IsAreaValidPlacement(lastGridPos, footprint) &&
            PathValidator.HasAdjacentRoadInArea(gridSystem, lastGridPos, footprint))
        {
            // Place actual building
            Vector3 worldPos = gridSystem.GridToWorld(lastGridPos);
            Vector3 footprintOffset = new Vector3(
                (footprint.x - 1) * gridSystem.cellSize * 0.5f,
                0f,
                (footprint.y - 1) * gridSystem.cellSize * 0.5f);
            worldPos += footprintOffset;


            // Check resources
            if (buildingData != null)
            {
                if (!ResourceManager.Instance.CanAfford(buildingData.foodCost, buildingData.woodCost, buildingData.stoneCost))
                {
                    Debug.Log("Not enough resources!");
                    return;
                }
            }


            GameObject building = Instantiate(currentBuildingPrefab, worldPos, Quaternion.identity);
            building.name = currentBuildingPrefab.name;
            Building placedBuilding = building.GetComponent<Building>();
            if (placedBuilding != null)
            {
                placedBuilding.SetGridOrigin(lastGridPos);
                placedBuilding.RegisterBuildingInPopulationManager();
            }

            // Mark grid as occupied
            gridSystem.SetAreaOccupied(lastGridPos, footprint, true);

            Debug.Log($"Building placed at {lastGridPos}");
            // After Instantiate, spend resources:
            if (buildingData != null)
            {
                ResourceManager.Instance.SpendResources(buildingData.foodCost, buildingData.woodCost, buildingData.stoneCost);
            }
            // Don't cancel - can place multiple buildings
            // If you want to cancel after each placement, uncomment next line:
            // CancelPlacement();
        }
        else
        {
            Debug.Log("Invalid placement location! Buildings must be adjacent to a road.");
        }
    }

    void CancelPlacement()
    {
        CancelBuildingPlacement(true);
        StopRoadPlacement();
        placementMode = PlacementMode.None;
    }

    private void CancelBuildingPlacement(bool logMessage)
    {
        isPlacing = false;

        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }

        currentBuildingPrefab = null;
        if (logMessage)
        {
            Debug.Log("Placement cancelled");
        }
    }

    public void StartRoadPlacement()
    {
        CancelBuildingPlacement(false);
        placementMode = PlacementMode.Road;
        if (roadManager != null)
        {
            roadManager.SetRoadPlacementActive(true);
        }
    }

    private void ToggleRoadPlacement()
    {
        if (placementMode == PlacementMode.Road)
        {
            StopRoadPlacement();
            return;
        }

        StartRoadPlacement();
    }

    private void StopRoadPlacement()
    {
        if (roadManager != null)
        {
            roadManager.SetRoadPlacementActive(false);
        }
        if (placementMode == PlacementMode.Road)
        {
            placementMode = PlacementMode.None;
        }
    }
}