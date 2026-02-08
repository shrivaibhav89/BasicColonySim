using UnityEngine;
using UnityEngine.UI;

public class FoodConsumptionManager : MonoBehaviour
{
    [Header("References")]
    public DayNightManager dayNightManager;

    [Header("Consumption Settings")]
    public int foodPerVillagerPerDay = 2;
    public float starvationEfficiency = 0.5f;

    [Header("Hungry Warning UI")]
    public GameObject hungryWarningRoot;
    public Text hungryWarningText;
    private bool starvationPenaltyActive;
    private int lastHungryVillagers;

    void Start()
    {
        if (dayNightManager == null)
        {
            dayNightManager = DayNightManager.Instance;
        }

        if (dayNightManager != null)
        {
            dayNightManager.OnDayEnd.AddListener(HandleDayEnd);
        }
        else
        {
            Debug.LogWarning("FoodConsumptionManager: DayNightManager not found.");
        }

        SetHungryWarningVisible(false);
    }

    private void HandleDayEnd()
    {
        ResourceManager rm = ResourceManager.Instance;
        PopulationManager pm = PopulationManager.Instance;

        if (rm == null || pm == null)
        {
            return;
        }

        int population = pm.currentPopulation;
        int foodNeeded = population * foodPerVillagerPerDay;
        int availableFood = rm.food;

        int foodConsumed = Mathf.Min(availableFood, foodNeeded);
        if (foodConsumed > 0)
        {
            rm.SpendResources(foodConsumed, 0, 0);
        }

        int deficit = foodNeeded - foodConsumed;
        lastHungryVillagers = population <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(deficit / (float)foodPerVillagerPerDay), 0, population);

        if (deficit <= 0)
        {
            ClearStarvationPenalty();
        }
        else
        {
            ApplyStarvationPenalty();
        }
    }

    private void ApplyStarvationPenalty()
    {
        starvationPenaltyActive = true;
        if (ResourceManager.Instance != null)
        {
            int population = PopulationManager.Instance != null ? PopulationManager.Instance.currentPopulation : 0;
            int fed = Mathf.Max(0, population - lastHungryVillagers);
            float efficiency = population > 0 ? (float)fed / population : 1f;
            efficiency = Mathf.Clamp01(efficiency);
            ResourceManager.Instance.SetProductionEfficiency(efficiency);
        }

        SetHungryWarningVisible(true);
    }

    private void ClearStarvationPenalty()
    {
        if (!starvationPenaltyActive)
        {
            return;
        }

        starvationPenaltyActive = false;
        lastHungryVillagers = 0;
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.SetProductionEfficiency(1f);
        }

        SetHungryWarningVisible(false);
    }

    private void SetHungryWarningVisible(bool visible)
    {
        if (hungryWarningRoot != null)
        {
            hungryWarningRoot.SetActive(visible);
        }

        if (hungryWarningText != null)
        {
            hungryWarningText.text = $"{lastHungryVillagers} villagers are hungry! Production reduced";
            hungryWarningText.enabled = visible;
        }
    }
}
