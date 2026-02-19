using UnityEngine;
using UnityEngine.EventSystems;

public class DemolishManager : MonoBehaviour
{
    public static DemolishManager Instance { get; private set; }

    [Header("References")]
    public Camera mainCamera;
    public BuildingPlacer buildingPlacer;
    public BuildingInfoUI infoUI;

    [Header("Raycast")]
    public LayerMask demolishLayerMask = ~0;
    public float maxRaycastDistance = 1000f;
    public bool ignoreClicksOverUI = true;

    [Header("Cursor")]
    public Texture2D demolishCursor;
    public Vector2 cursorHotspot = new Vector2(8f, 8f);
    public CursorMode cursorMode = CursorMode.Auto;

    private bool isActive;

    public bool IsDemolishModeActive => isActive;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            SetDemolishMode(false);
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, demolishLayerMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Building building = hit.collider.GetComponentInParent<Building>();
        if (building != null)
        {
            building.DestroyBuilding();
            return;
        }

        RoadTile roadTile = hit.collider.GetComponentInParent<RoadTile>();
        if (roadTile != null)
        {
            Destroy(roadTile.gameObject);
        }
    }

    public void ToggleDemolishMode()
    {
        SetDemolishMode(!isActive);
    }

    public void SetDemolishMode(bool active)
    {
        if (isActive == active)
        {
            return;
        }

        isActive = active;

        if (isActive)
        {
            if (buildingPlacer != null)
            {
                buildingPlacer.CancelAllPlacement();
            }

            if (infoUI != null)
            {
                infoUI.Hide();
            }
        }

        ApplyCursor();
    }

    private void ApplyCursor()
    {
        if (isActive && demolishCursor != null)
        {
            Cursor.SetCursor(demolishCursor, cursorHotspot, cursorMode);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
        }
    }
}
