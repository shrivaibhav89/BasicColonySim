using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    public GameObject buildingPrefab;
    public BuildingPlacer buildingPlacer;

    public Text nameText;
    public Text costText;
    private Button button;
    private Building buildingData;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        buildingData = buildingPrefab.GetComponent<Building>();
        nameText.text = buildingData.buildingName;
        costText.text = $"F:{buildingData.foodCost} W:{buildingData.woodCost} S:{buildingData.stoneCost}";

        ResourceManager.Instance.OnResourcesChanged += UpdateAffordability;
        UpdateAffordability();
    }

    void OnClick()
    {
        buildingPlacer.StartPlacement(buildingPrefab);
    }

    void UpdateAffordability()
    {
        bool canAfford = ResourceManager.Instance.CanAfford(
            buildingData.foodCost, buildingData.woodCost, buildingData.stoneCost);

        button.interactable = canAfford;
        GetComponent<Image>().color = canAfford ? Color.white : Color.gray;
    }
}