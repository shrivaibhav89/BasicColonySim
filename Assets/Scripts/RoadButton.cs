using UnityEngine;
using UnityEngine.UI;

public class RoadButton : MonoBehaviour
{
    public BuildingPlacer buildingPlacer;
    public RoadManager roadManager;

    public Text costText;

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        UpdateCostText();

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourcesChanged += UpdateAffordability;
        }

        UpdateAffordability();
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourcesChanged -= UpdateAffordability;
        }
    }

    private void OnClick()
    {
        if (!CanAffordRoad())
        {
            return;
        }

        if (buildingPlacer != null)
        {
            buildingPlacer.StartRoadPlacement();
        }
    }

    private void UpdateAffordability()
    {
        if (button == null)
        {
            return;
        }

        bool canAfford = CanAffordRoad();
        button.interactable = canAfford;

        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.color = canAfford ? Color.white : Color.gray;
        }
    }

    private bool CanAffordRoad()
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        int woodCost = roadManager != null ? roadManager.roadWoodCost : RoadTile.WoodCost;
        return ResourceManager.Instance.CanAfford(0, woodCost, 0);
    }

    private void UpdateCostText()
    {
        if (costText == null)
        {
            return;
        }

        int woodCost = roadManager != null ? roadManager.roadWoodCost : RoadTile.WoodCost;
        costText.text = $"W:{woodCost}";
    }
}
