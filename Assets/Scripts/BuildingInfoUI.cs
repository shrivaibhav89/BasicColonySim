using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BuildingInfoUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Text")]
    public Text titleText;
    public Text detailsText;

    [Header("Image")]
    public Image buildingImage;

    [Header("Production Toggle")]
    public Toggle productionToggle;
    public Text productionToggleLabel;

    private Building currentBuilding;
    private bool suppressToggleCallback;

    void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (productionToggle != null)
        {
            productionToggle.onValueChanged.RemoveListener(HandleProductionToggleChanged);
            productionToggle.onValueChanged.AddListener(HandleProductionToggleChanged);
        }

        Hide();
    }

    public void Show(Building building)
    {
        if (building == null)
        {
            Hide();
            return;
        }

        currentBuilding = building;

        if (root != null)
        {
            root.SetActive(true);
        }

        string displayName = GetDisplayName(building);
        if (titleText != null)
        {
            titleText.text = displayName;
        }

        if (detailsText != null)
        {
            detailsText.text = BuildDetails(building);
        }

        if (buildingImage != null)
        {
            Sprite sprite = building.buildingData != null ? building.buildingData.buildingImage : null;
            buildingImage.sprite = sprite;
            buildingImage.enabled = sprite != null;
        }

        UpdateProductionToggle(building);
    }

    public void Hide()
    {
        currentBuilding = null;
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void UpdateProductionToggle(Building building)
    {
        if (productionToggle == null)
        {
            return;
        }

        bool canToggle = IsProductionCapable(building);
        productionToggle.gameObject.SetActive(canToggle);
        if (productionToggleLabel != null)
        {
            productionToggleLabel.text = "Operational";
        }

        if (!canToggle)
        {
            return;
        }

        suppressToggleCallback = true;
        productionToggle.isOn = building.IsProductionEnabled;
        suppressToggleCallback = false;
    }

    private void HandleProductionToggleChanged(bool value)
    {
        if (suppressToggleCallback || currentBuilding == null)
        {
            return;
        }

        currentBuilding.SetProductionEnabled(value);
    }

    private bool IsProductionCapable(Building building)
    {
        if (building == null)
        {
            return false;
        }

        if (building.GetRequiredWorkers() > 0)
        {
            return true;
        }

        return building.GetFoodPerSec() > 0
            || building.GetWoodPerSec() > 0
            || building.GetStonePerSec() > 0
            || building.GetFoodPerHarvest() > 0
            || building.GetWoodPerHarvest() > 0
            || building.GetStonePerHarvest() > 0;
    }

    private string GetDisplayName(Building building)
    {
        if (building.buildingData != null && !string.IsNullOrWhiteSpace(building.buildingData.buildingName))
        {
            return building.buildingData.buildingName;
        }

        return building.gameObject != null ? building.gameObject.name : "Building";
    }

    private string BuildDetails(Building building)
    {
        StringBuilder sb = new StringBuilder();

        int requiredWorkers = building.GetRequiredWorkers();
        if (requiredWorkers > 0)
        {
            sb.AppendLine($"Workers: {building.assignedWorkers}/{requiredWorkers}");
        }

        int populationCapacity = building.GetPopulationCapacity();
        if (populationCapacity > 0)
        {
            sb.AppendLine($"Population Capacity: {populationCapacity}");
        }

        if (building.IsDropoff)
        {
            sb.AppendLine("Drop-off: Yes");
        }

        AppendProductionPerSecond(sb, building);
        AppendProductionPerHarvest(sb, building);

        float harvestDuration = building.GetHarvestDuration();
        if (harvestDuration > 0f && HasHarvestProduction(building))
        {
            sb.AppendLine($"Harvest Time: {harvestDuration:0.##}s");
        }

        TownHallArmyTrainer trainer = building.GetComponent<TownHallArmyTrainer>();
        if (trainer != null)
        {
            sb.AppendLine();
            sb.AppendLine("Train Army Unit: Press T");
            sb.AppendLine($"Cost: Food {trainer.foodCost}, Wood {trainer.woodCost}, Stone {trainer.stoneCost}");
            sb.AppendLine($"Train Time: {trainer.trainingDuration:0.##}s");
            sb.AppendLine($"Queue: {trainer.queuedUnits}");
        }

        return sb.ToString().TrimEnd();
    }

    private void AppendProductionPerSecond(StringBuilder sb, Building building)
    {
        int food = building.GetFoodPerSec();
        int wood = building.GetWoodPerSec();
        int stone = building.GetStonePerSec();

        if (food == 0 && wood == 0 && stone == 0)
        {
            return;
        }

        sb.Append("Produces per sec: ");
        AppendResourceList(sb, food, wood, stone);
        sb.AppendLine();
    }

    private void AppendProductionPerHarvest(StringBuilder sb, Building building)
    {
        int food = building.GetFoodPerHarvest();
        int wood = building.GetWoodPerHarvest();
        int stone = building.GetStonePerHarvest();

        if (food == 0 && wood == 0 && stone == 0)
        {
            return;
        }

        sb.Append("Produces per harvest: ");
        AppendResourceList(sb, food, wood, stone);
        sb.AppendLine();
    }

    private bool HasHarvestProduction(Building building)
    {
        return building.GetFoodPerHarvest() > 0 || building.GetWoodPerHarvest() > 0 || building.GetStonePerHarvest() > 0;
    }

    private void AppendResourceList(StringBuilder sb, int food, int wood, int stone)
    {
        bool first = true;
        if (food > 0)
        {
            sb.Append($"Food {food}");
            first = false;
        }
        if (wood > 0)
        {
            if (!first) sb.Append(", ");
            sb.Append($"Wood {wood}");
            first = false;
        }
        if (stone > 0)
        {
            if (!first) sb.Append(", ");
            sb.Append($"Stone {stone}");
        }
    }
}
