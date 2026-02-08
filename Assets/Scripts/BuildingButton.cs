using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    public GameObject buildingPrefab;
    public BuildingPlacer buildingPlacer;

    public Text nameText;
    public Text costText;
    private Button button;
    private Building buildingComponent;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        buildingComponent = buildingPrefab.GetComponent<Building>();
        if (buildingComponent != null)
        {
            if (buildingComponent.buildingData != null)
            {
                nameText.text = buildingComponent.buildingData.buildingName;
                costText.text = $"F:{buildingComponent.buildingData.foodCost} W:{buildingComponent.buildingData.woodCost} S:{buildingComponent.buildingData.stoneCost}";
            }
            else
            {
                nameText.text = buildingComponent.gameObject != null ? buildingComponent.gameObject.name : string.Empty;
                costText.text = "F:0 W:0 S:0";
            }
        }

        ResourceManager.Instance.OnResourcesChanged += UpdateAffordability;
        UpdateAffordability();
    }

    void OnClick()
    {
        buildingPlacer.StartPlacement(buildingPrefab);
    }

    void UpdateAffordability()
    {
        if (buildingComponent == null || buildingComponent.buildingData == null)
        {
            button.interactable = false;
            return;
        }

        bool canAfford = ResourceManager.Instance.CanAfford(
            buildingComponent.buildingData.foodCost, buildingComponent.buildingData.woodCost, buildingComponent.buildingData.stoneCost);

        button.interactable = canAfford;
        GetComponent<Image>().color = canAfford ? Color.white : Color.gray;
    }
}