using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BuildingPlacer : MonoBehaviour
{
    [Header("References")]
    public GridSystem gridSystem;
    public Camera mainCamera;
    public RoadManager roadManager;
    public DemolishManager demolishManager;

    [Header("Placement Settings")]
    public GameObject currentBuildingPrefab;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    public float ghostHeight = 0.5f;
    public KeyCode roadModeHotkey = KeyCode.R;
    public KeyCode rotateBuildingHotkey = KeyCode.R;
    public float rotationStepDegrees = 90f;
    [Header("Quarry Placement")]
    public float quarryStoneSearchRadius = 7f;
    public LayerMask quarryStoneLayerMask = ~0;
    public string quarryStoneTag = "Stone";
    public string[] quarryStoneNameHints = new[] { "stone", "rock", "ore", "boulder" };

    [Header("Placement UI")]
    public Text placementErrorText;
    public float placementErrorDuration = 6f;

    private GameObject ghostObject;
    private bool isPlacing = false;
    private Vector2Int lastGridPos;
    private MeshRenderer[] ghostRenderers;
    private Coroutine placementErrorRoutine;
    private int placementRotationQuarterTurns;
    private bool ghostNeedsRefresh;

    private enum PlacementMode
    {
        None,
        Building,
        Road
    }

    private PlacementMode placementMode = PlacementMode.None;

    void Update()
    {
        if (placementMode == PlacementMode.Building && isPlacing && currentBuildingPrefab != null)
        {
            if (Input.GetKeyDown(rotateBuildingHotkey))
            {
                RotateBuildingClockwise();
            }

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

        if (placementMode != PlacementMode.Building && Input.GetKeyDown(roadModeHotkey))
        {
            ToggleRoadPlacement();
        }
    }

    public void StartPlacement(GameObject buildingPrefab)
    {
        if (demolishManager != null)
        {
            demolishManager.SetDemolishMode(false);
        }

        StopRoadPlacement();
        currentBuildingPrefab = buildingPrefab;
        isPlacing = true;
        placementMode = PlacementMode.Building;
        placementRotationQuarterTurns = 0;
        ghostNeedsRefresh = true;

        // Create ghost preview
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = Instantiate(currentBuildingPrefab);
        ghostObject.name = "Ghost_" + buildingPrefab.name;
        ghostObject.transform.rotation = GetPlacementRotation();

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
            if (gridPos != lastGridPos || ghostNeedsRefresh)
            {
                lastGridPos = gridPos;
                ghostNeedsRefresh = false;

                // Check if valid placement
                Building buildingComponent = currentBuildingPrefab.GetComponent<Building>();
                Vector2Int footprint = GetCurrentFootprint(buildingComponent);
                bool isValid = gridSystem.IsAreaValidPlacement(gridPos, footprint) &&
                               PathValidator.HasAdjacentRoadInArea(gridSystem, gridPos, footprint);

                if (isValid && !MeetsSpecialPlacementRules(buildingComponent, gridPos, footprint))
                {
                    isValid = false;
                }

                if (isValid && buildingComponent != null && buildingComponent.buildingData != null && ResourceManager.Instance != null)
                {
                    isValid = ResourceManager.Instance.CanAfford(
                        buildingComponent.buildingData.foodCost,
                        buildingComponent.buildingData.woodCost,
                        buildingComponent.buildingData.stoneCost);
                }

                // Update ghost position
                Vector3 worldPos = gridSystem.GridToWorld(gridPos);
                Vector3 footprintOffset = new Vector3(
                    (footprint.x - 1) * gridSystem.cellSize * 0.5f,
                    0f,
                    (footprint.y - 1) * gridSystem.cellSize * 0.5f);
                worldPos += footprintOffset;
                worldPos.y = ghostHeight;
                ghostObject.transform.position = worldPos;
                ghostObject.transform.rotation = GetPlacementRotation();

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
        Building buildingComponent = currentBuildingPrefab.GetComponent<Building>();
        Vector2Int footprint = GetCurrentFootprint(buildingComponent);

        if (gridSystem.IsAreaValidPlacement(lastGridPos, footprint) &&
            PathValidator.HasAdjacentRoadInArea(gridSystem, lastGridPos, footprint))
        {
            if (!MeetsSpecialPlacementRules(buildingComponent, lastGridPos, footprint))
            {
                ShowPlacementError("Quarry must be placed near stone.");
                return;
            }

            // Place actual building
            Vector3 worldPos = gridSystem.GridToWorld(lastGridPos);
            Vector3 footprintOffset = new Vector3(
                (footprint.x - 1) * gridSystem.cellSize * 0.5f,
                0f,
                (footprint.y - 1) * gridSystem.cellSize * 0.5f);
            worldPos += footprintOffset;


            // Check resources
            if (buildingComponent == null || buildingComponent.buildingData == null)
            {
                ShowPlacementError("Invalid building data!");
                return;
            }

            if (!ResourceManager.Instance.CanAfford(buildingComponent.buildingData.foodCost, buildingComponent.buildingData.woodCost, buildingComponent.buildingData.stoneCost))
            {
                ShowPlacementError("Not enough resources!");
                return;
            }


            GameObject building = Instantiate(currentBuildingPrefab, worldPos, GetPlacementRotation());
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
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfxAt(SoundId.BuildingPlaced, worldPos);
            }

            // After Instantiate, spend resources:
            if (buildingComponent != null && buildingComponent.buildingData != null)
            {
                ResourceManager.Instance.SpendResources(buildingComponent.buildingData.foodCost, buildingComponent.buildingData.woodCost, buildingComponent.buildingData.stoneCost);
            }
            // Don't cancel - can place multiple buildings
            // If you want to cancel after each placement, uncomment next line:
            // CancelPlacement();
        }
        else
        {
            ShowPlacementError("Invalid placement location! Buildings must be adjacent to a road.");
        }
    }

    private void ShowPlacementError(string message)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfx(SoundId.BuildingPlacementDenied);
        }

        if (placementErrorText == null)
        {
            return;
        }

        placementErrorText.text = message;
        placementErrorText.enabled = true;

        if (placementErrorRoutine != null)
        {
            StopCoroutine(placementErrorRoutine);
        }

        placementErrorRoutine = StartCoroutine(HidePlacementErrorAfterDelay());
    }

    private IEnumerator HidePlacementErrorAfterDelay()
    {
        yield return new WaitForSeconds(placementErrorDuration);
        if (placementErrorText != null)
        {
            placementErrorText.enabled = false;
            placementErrorText.text = string.Empty;
        }
        placementErrorRoutine = null;
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
        placementRotationQuarterTurns = 0;
        ghostNeedsRefresh = false;

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
        if (demolishManager != null)
        {
            demolishManager.SetDemolishMode(false);
        }

        CancelBuildingPlacement(false);
        placementMode = PlacementMode.Road;
        if (roadManager != null)
        {
            roadManager.SetRoadPlacementActive(true);
        }
    }

    public void CancelAllPlacement()
    {
        CancelBuildingPlacement(false);
        StopRoadPlacement();
        placementMode = PlacementMode.None;
    }

    public void ToggleRoadPlacement()
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

    private bool MeetsSpecialPlacementRules(Building buildingComponent, Vector2Int gridPos, Vector2Int footprint)
    {
        if (!IsQuarryBuilding(buildingComponent))
        {
            return true;
        }

        Vector3 worldPos = gridSystem.GridToWorld(gridPos);
        Vector3 footprintOffset = new Vector3(
            (footprint.x - 1) * gridSystem.cellSize * 0.5f,
            0f,
            (footprint.y - 1) * gridSystem.cellSize * 0.5f);
        worldPos += footprintOffset;
        worldPos.y = 0f;
        return HasNearbyStoneNode(worldPos);
    }

    private bool IsQuarryBuilding(Building buildingComponent)
    {
        if (buildingComponent == null)
        {
            return false;
        }

        if (buildingComponent.GetJobType() == JobType.Quarry)
        {
            return true;
        }

        string dataName = buildingComponent.buildingData != null ? buildingComponent.buildingData.buildingName : string.Empty;
        string goName = buildingComponent.gameObject != null ? buildingComponent.gameObject.name : string.Empty;
        string merged = (dataName + " " + goName).ToLowerInvariant();
        return merged.Contains("quarry") || merged.Contains("quary") || merged.Contains("mine");
    }

    private bool HasNearbyStoneNode(Vector3 center)
    {
        float radius = Mathf.Max(0.5f, quarryStoneSearchRadius);
        Collider[] hits = Physics.OverlapSphere(center + Vector3.up * 0.5f, radius, quarryStoneLayerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (LooksLikeStoneNode(hits[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool LooksLikeStoneNode(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        Transform t = col.transform;
        if (!string.IsNullOrEmpty(quarryStoneTag) && t.CompareTag(quarryStoneTag))
        {
            return true;
        }

        string nameLower = t.name.ToLowerInvariant();
        for (int i = 0; i < quarryStoneNameHints.Length; i++)
        {
            string hint = quarryStoneNameHints[i];
            if (!string.IsNullOrWhiteSpace(hint) && nameLower.Contains(hint.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private void RotateBuildingClockwise()
    {
        placementRotationQuarterTurns = (placementRotationQuarterTurns + 1) % 4;
        ghostNeedsRefresh = true;
        if (ghostObject != null)
        {
            ghostObject.transform.rotation = GetPlacementRotation();
        }
    }

    private Quaternion GetPlacementRotation()
    {
        return Quaternion.Euler(0f, placementRotationQuarterTurns * rotationStepDegrees, 0f);
    }

    private Vector2Int GetCurrentFootprint(Building buildingComponent)
    {
        Vector2Int footprint = buildingComponent != null && buildingComponent.buildingData != null
            ? buildingComponent.buildingData.footprintSize
            : new Vector2Int(1, 1);

        if ((placementRotationQuarterTurns & 1) == 1)
        {
            return new Vector2Int(footprint.y, footprint.x);
        }

        return footprint;
    }
}
