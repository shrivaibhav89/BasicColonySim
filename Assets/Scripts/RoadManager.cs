using UnityEngine;

public class RoadManager : MonoBehaviour
{
    [Header("References")]
    public GridSystem gridSystem;
    public Camera mainCamera;
    public GameObject roadPrefab;

    [Header("Road Settings")]
    public float roadHeight = 0f;
    public int roadWoodCost = RoadTile.WoodCost;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    private bool isActive;
    private bool isDragging;
    private Vector2Int lastPlacedGridPos = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastGhostGridPos = new Vector2Int(int.MinValue, int.MinValue);
    private GameObject ghostObject;
    private MeshRenderer[] ghostRenderers;

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        UpdateGhostPosition();

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            TryPlaceRoadAtMouse();
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            TryPlaceRoadAtMouse();
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
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
            DestroyGhost();
            return;
        }

        CreateGhost();
    }

    public bool IsActive()
    {
        return isActive;
    }

    private void TryPlaceRoadAtMouse()
    {
        if (mainCamera == null || gridSystem == null || roadPrefab == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
        {
            return;
        }

        Vector2Int gridPos = gridSystem.WorldToGrid(hit.point);

        if (gridPos == lastPlacedGridPos)
        {
            return;
        }

        lastPlacedGridPos = gridPos;

        if (!gridSystem.IsValidPlacement(gridPos))
        {
            return;
        }

        if (ResourceManager.Instance == null)
        {
            return;
        }

        if (!ResourceManager.Instance.CanAfford(0, roadWoodCost, 0))
        {
            Debug.Log("Not enough wood to place road!");
            isDragging = false;
            return;
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
        ResourceManager.Instance.SpendResources(0, roadWoodCost, 0);
    }

    private void CreateGhost()
    {
        if (roadPrefab == null || ghostObject != null)
        {
            return;
        }

        ghostObject = Instantiate(roadPrefab);
        ghostObject.name = "Ghost_" + roadPrefab.name;

        ghostRenderers = ghostObject.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in ghostRenderers)
        {
            renderer.material = validPlacementMaterial;
        }

        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    private void DestroyGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
        ghostRenderers = null;
        lastGhostGridPos = new Vector2Int(int.MinValue, int.MinValue);
    }

    private void UpdateGhostPosition()
    {
        if (ghostObject == null || mainCamera == null || gridSystem == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
        {
            return;
        }

        Vector2Int gridPos = gridSystem.WorldToGrid(hit.point);
        if (gridPos == lastGhostGridPos)
        {
            return;
        }

        lastGhostGridPos = gridPos;

        bool isValid = gridSystem.IsValidPlacement(gridPos);
        if (ResourceManager.Instance != null)
        {
            isValid = isValid && ResourceManager.Instance.CanAfford(0, roadWoodCost, 0);
        }

        Vector3 worldPos = gridSystem.GridToWorld(gridPos);
        worldPos.y = roadHeight;
        ghostObject.transform.position = worldPos;

        Material materialToUse = isValid ? validPlacementMaterial : invalidPlacementMaterial;
        if (ghostRenderers != null)
        {
            foreach (MeshRenderer renderer in ghostRenderers)
            {
                renderer.material = materialToUse;
            }
        }
    }
}
