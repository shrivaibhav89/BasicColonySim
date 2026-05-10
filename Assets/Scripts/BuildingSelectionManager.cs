using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingSelectionManager : MonoBehaviour
{
    public static Building CurrentSelectedBuilding { get; private set; }

    [Header("References")]
    public Camera mainCamera;
    public BuildingInfoUI infoUI;

    [Header("Selection")]
    public LayerMask buildingLayer = ~0;
    public float maxRaycastDistance = 1000f;
    public bool ignoreClicksOverUI = true;

    [Header("Highlight")]
    public bool highlightSelection = true;
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);
    [Min(0f)] public float emissionIntensity = 1.5f;

    private Building currentSelection;
    private BuildingHighlighter currentHighlighter;

    void Awake()
    {
        if (infoUI == null)
        {
            infoUI = FindObjectOfType<BuildingInfoUI>(true);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (DemolishManager.Instance != null && DemolishManager.Instance.IsDemolishModeActive)
        {
            if (infoUI != null)
            {
                infoUI.Hide();
            }
            ClearSelection();
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
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, buildingLayer, QueryTriggerInteraction.Ignore))
        {
            Building building = hit.collider.GetComponentInParent<Building>();
            if (building != null && infoUI != null)
            {
                infoUI.Show(building);
                SetSelection(building);
                return;
            }
        }

        if (infoUI != null)
        {
            infoUI.Hide();
        }

        ClearSelection();
    }

    private void SetSelection(Building building)
    {
        if (!highlightSelection)
        {
            currentSelection = building;
            currentHighlighter = null;
            CurrentSelectedBuilding = currentSelection;
            return;
        }

        if (currentSelection == building && currentHighlighter != null)
        {
            return;
        }

        ClearSelection();
        currentSelection = building;
        CurrentSelectedBuilding = currentSelection;
        if (currentSelection == null)
        {
            return;
        }

        currentHighlighter = currentSelection.GetComponentInChildren<BuildingHighlighter>(true);
        if (currentHighlighter == null)
        {
            currentHighlighter = currentSelection.gameObject.AddComponent<BuildingHighlighter>();
        }

        currentHighlighter.Configure(highlightColor, emissionIntensity);
        currentHighlighter.SetHighlighted(true);
    }

    private void ClearSelection()
    {
        if (currentHighlighter != null)
        {
            currentHighlighter.SetHighlighted(false);
        }

        currentSelection = null;
        currentHighlighter = null;
        CurrentSelectedBuilding = null;
    }
}
