using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("References")]
    public GridSystem gridSystem;
    public Camera mainCamera;

    [Header("Placement Settings")]
    public GameObject currentBuildingPrefab;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    public float ghostHeight = 0.5f;

    private GameObject ghostObject;
    private bool isPlacing = false;
    private Vector2Int lastGridPos;
    private MeshRenderer[] ghostRenderers;

    void Update()
    {
        if (isPlacing && currentBuildingPrefab != null)
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
    }

    public void StartPlacement(GameObject buildingPrefab)
    {
        currentBuildingPrefab = buildingPrefab;
        isPlacing = true;

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
                bool isValid = gridSystem.IsValidPlacement(gridPos);

                // Update ghost position
                Vector3 worldPos = gridSystem.GridToWorld(gridPos);
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
        if (gridSystem.IsValidPlacement(lastGridPos))
        {
            // Place actual building
            Vector3 worldPos = gridSystem.GridToWorld(lastGridPos);


            // Check resources
            Building buildingData = currentBuildingPrefab.GetComponent<Building>();
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
            building.GetComponent<Building>().RegisterBuildingInPopulationManager();

            // Mark grid as occupied
            gridSystem.SetOccupied(lastGridPos, true);

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
            Debug.Log("Invalid placement location!");
        }
    }

    void CancelPlacement()
    {
        isPlacing = false;

        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }

        currentBuildingPrefab = null;
        Debug.Log("Placement cancelled");
    }
}