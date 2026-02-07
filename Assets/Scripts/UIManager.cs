using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Resource Display")]
    public Text foodText;
    public Text woodText;
    public Text stoneText;
    public Text populationText;
    public Text idleVillagersText;
    void Start()
    {
        ResourceManager.Instance.OnResourcesChanged += UpdateUI;
        PopulationManager.Instance.OnPopulationChanged += UpdateUI;
        UpdateUI();
    }

    void UpdateUI()
    {
        var rm = ResourceManager.Instance;
        var pm = PopulationManager.Instance;

        // Color code resources
        foodText.text = $"🌾 Food: {rm.food}/{rm.foodCap}";
        woodText.text = $"🪵 Wood: {rm.wood}/{rm.woodCap}";
        stoneText.text = $"🪨 Stone: {rm.stone}/{rm.stoneCap}";
        populationText.text = $"👤 Pop: {pm.currentPopulation}/{pm.maxPopulation}";

        // Warning if near cap


        foodText.text = $"Food: {rm.food}/{rm.foodCap}";
        woodText.text = $"Wood: {rm.wood}/{rm.woodCap}";
        stoneText.text = $"Stone: {rm.stone}/{rm.stoneCap}";
        populationText.text = $"Pop: {pm.currentPopulation}/{pm.maxPopulation}";

        if (idleVillagersText != null)
        {
            idleVillagersText.text = $"Idle: {pm.GetIdleVillagers()}";
        }

        foodText.color = rm.food >= rm.foodCap * 0.9f ? Color.red : Color.white;
        woodText.color = rm.wood >= rm.woodCap * 0.9f ? Color.red : Color.white;
        stoneText.color = rm.stone >= rm.stoneCap * 0.9f ? Color.red : Color.white;


    }
}