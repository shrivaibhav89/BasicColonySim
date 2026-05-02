using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class QuestRuntimeState
{
    public int currentQuestIndex;
    public int buildCount;
    public int resourceCollected;
    public int assignedBuildingsCount;
    public int daysSurvived;
}

public class QuestManager : MonoBehaviour
{
    public event Action OnQuestActivated;

    [Header("Quest Chain")]
    public List<QuestData> questChain = new List<QuestData>();

    [Header("References")]
    public DayNightManager dayNightManager;

    [Header("UI")]
    public Text questTitleText;
    public Text questDescriptionText;
    public Text questProgressText;

    private int currentQuestIndex = -1;
    private QuestData currentQuest;

    private int buildCount;
    private int resourceCollected;
    private int assignedBuildingsCount;
    private int daysSurvived;

    public QuestRuntimeState CaptureRuntimeState()
    {
        return new QuestRuntimeState
        {
            currentQuestIndex = currentQuestIndex,
            buildCount = buildCount,
            resourceCollected = resourceCollected,
            assignedBuildingsCount = assignedBuildingsCount,
            daysSurvived = daysSurvived
        };
    }

    public void RestoreRuntimeState(QuestRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        currentQuestIndex = Mathf.Clamp(state.currentQuestIndex, -1, questChain.Count - 1);
        currentQuest = (currentQuestIndex >= 0 && currentQuestIndex < questChain.Count)
            ? questChain[currentQuestIndex]
            : null;

        buildCount = Mathf.Max(0, state.buildCount);
        resourceCollected = Mathf.Max(0, state.resourceCollected);
        assignedBuildingsCount = Mathf.Max(0, state.assignedBuildingsCount);
        daysSurvived = Mathf.Max(0, state.daysSurvived);

        UpdateQuestUI();
    }

    void Start()
    {
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            PopulationManager.Instance.OnBuildingRegistered += HandleBuildingRegistered;
            PopulationManager.Instance.OnWorkersAssigned += HandleWorkersAssigned;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourcesAdded += HandleResourcesAdded;
        }

        if (dayNightManager == null)
        {
            dayNightManager = DayNightManager.Instance;
        }

        if (dayNightManager != null)
        {
            dayNightManager.OnDayEnd.AddListener(HandleDayEnded);
        }

        ActivateNextQuest();
    }

    private void OnDestroy()
    {
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            PopulationManager.Instance.OnBuildingRegistered -= HandleBuildingRegistered;
            PopulationManager.Instance.OnWorkersAssigned -= HandleWorkersAssigned;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourcesAdded -= HandleResourcesAdded;
        }

        if (dayNightManager != null)
        {
            dayNightManager.OnDayEnd.RemoveListener(HandleDayEnded);
        }
    }

    private void ActivateNextQuest()
    {
        currentQuestIndex++;
        buildCount = 0;
        resourceCollected = 0;
        assignedBuildingsCount = 0;
        daysSurvived = 0;

        if (currentQuestIndex < 0 || currentQuestIndex >= questChain.Count)
        {
            currentQuest = null;
            UpdateQuestUI();
            return;
        }

        currentQuest = questChain[currentQuestIndex];
        if (currentQuest != null && currentQuest.questType == QuestType.SurviveDays && dayNightManager != null)
        {
            daysSurvived = Mathf.Max(0, dayNightManager.currentDay - 1);
        }
        UpdateQuestUI();
        OnQuestActivated?.Invoke();
        CheckQuestCompletion();
    }

    private void HandleDayEnded()
    {
        if (currentQuest == null || currentQuest.questType != QuestType.SurviveDays)
        {
            return;
        }

        if (dayNightManager != null)
        {
            daysSurvived = Mathf.Max(0, dayNightManager.currentDay);
        }

        UpdateQuestUI();
        CheckQuestCompletion();
    }

    private void HandleBuildingRegistered(Building building)
    {
        if (currentQuest == null || currentQuest.questType != QuestType.BuildSpecificBuilding)
        {
            return;
        }

        if (building == null || building.buildingData == null)
        {
            return;
        }

        if (IsTargetBuilding(building.buildingData))
        {
            buildCount++;
            UpdateQuestUI();
            CheckQuestCompletion();
        }
    }

    private void HandleWorkersAssigned(Building building)
    {
        if (currentQuest == null || currentQuest.questType != QuestType.AssignWorkers)
        {
            return;
        }

        assignedBuildingsCount = GetAssignedBuildingsCount();
        UpdateQuestUI();
        CheckQuestCompletion();
    }

    private void HandlePopulationChanged()
    {
        if (currentQuest == null || currentQuest.questType != QuestType.ReachPopulation)
        {
            return;
        }

        UpdateQuestUI();
        CheckQuestCompletion();
    }

    private void HandleResourcesAdded(int foodAdded, int woodAdded, int stoneAdded)
    {
        if (currentQuest == null || currentQuest.questType != QuestType.CollectResource)
        {
            return;
        }

        int add = 0;
        switch (currentQuest.targetResourceType)
        {
            case ResourceType.Food:
                add = foodAdded;
                break;
            case ResourceType.Wood:
                add = woodAdded;
                break;
            case ResourceType.Stone:
                add = stoneAdded;
                break;
        }

        if (add > 0)
        {
            resourceCollected += add;
            UpdateQuestUI();
            CheckQuestCompletion();
        }
    }

    private void CheckQuestCompletion()
    {
        if (currentQuest == null)
        {
            return;
        }

        bool complete = false;
        switch (currentQuest.questType)
        {
            case QuestType.BuildSpecificBuilding:
                complete = buildCount >= Mathf.Max(1, currentQuest.targetCount);
                break;
            case QuestType.ReachPopulation:
                if (PopulationManager.Instance != null)
                {
                    complete = PopulationManager.Instance.currentPopulation >= currentQuest.targetCount;
                }
                break;
            case QuestType.CollectResource:
                complete = resourceCollected >= currentQuest.targetCount;
                break;
            case QuestType.AssignWorkers:
                complete = assignedBuildingsCount >= Mathf.Max(1, currentQuest.targetCount);
                break;
            case QuestType.SurviveDays:
                complete = daysSurvived >= Mathf.Max(1, currentQuest.targetCount);
                break;
        }

        if (complete)
        {
            AwardRewards();
            if (currentQuest != null && currentQuest.isVictoryQuest)
            {
                TriggerVictory();
            }

            ActivateNextQuest();
        }
    }

    private void AwardRewards()
    {
        if (currentQuest == null || ResourceManager.Instance == null)
        {
            return;
        }

        if (currentQuest.foodReward != 0 || currentQuest.woodReward != 0 || currentQuest.stoneReward != 0)
        {
            ResourceManager.Instance.AddResources(currentQuest.foodReward, currentQuest.woodReward, currentQuest.stoneReward);
        }
    }

    private void UpdateQuestUI()
    {
        if (questTitleText != null)
        {
            questTitleText.text = currentQuest != null ? currentQuest.questTitle : "All quests completed";
        }

        if (questDescriptionText != null)
        {
            questDescriptionText.text = currentQuest != null ? currentQuest.questDescription : string.Empty;
        }

        if (questProgressText != null)
        {
            questProgressText.text = currentQuest != null ? GetProgressText() : string.Empty;
        }
    }

    private string GetProgressText()
    {
        if (currentQuest == null)
        {
            return string.Empty;
        }

        switch (currentQuest.questType)
        {
            case QuestType.BuildSpecificBuilding:
                return $"{buildCount}/{Mathf.Max(1, currentQuest.targetCount)}";
            case QuestType.ReachPopulation:
                int pop = PopulationManager.Instance != null ? PopulationManager.Instance.currentPopulation : 0;
                return $"{pop}/{currentQuest.targetCount}";
            case QuestType.CollectResource:
                return $"{resourceCollected}/{currentQuest.targetCount}";
            case QuestType.AssignWorkers:
                return $"{assignedBuildingsCount}/{Mathf.Max(1, currentQuest.targetCount)}";
            case QuestType.SurviveDays:
                return $"{daysSurvived}/{Mathf.Max(1, currentQuest.targetCount)}";
            default:
                return string.Empty;
        }
    }

    private bool IsTargetBuilding(BuildingData buildingData)
    {
        if (currentQuest == null || buildingData == null)
        {
            return false;
        }

        BuildingType type = GetBuildingType(buildingData.buildingName);
        return currentQuest.targetBuildingType == type;
    }

    private BuildingType GetBuildingType(string buildingName)
    {
        if (string.IsNullOrWhiteSpace(buildingName))
        {
            return BuildingType.None;
        }

        string lower = buildingName.ToLowerInvariant();
        if (lower.Contains("wood") && lower.Contains("cutter"))
        {
            return BuildingType.WoodCutter;
        }
        if (lower.Contains("farm"))
        {
            return BuildingType.Farm;
        }
        if (lower.Contains("quary") || lower.Contains("quarry"))
        {
            return BuildingType.Quarry;
        }
        if (lower.Contains("storage"))
        {
            return BuildingType.Storage;
        }

        return BuildingType.None;
    }

    private int GetAssignedBuildingsCount()
    {
        if (PopulationManager.Instance == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Building building in PopulationManager.Instance.allBuildings)
        {
            if (building != null && building.assignedWorkers > 0)
            {
                count++;
            }
        }

        return count;
    }

    private void TriggerVictory()
    {
        WinCondition winCondition = FindObjectOfType<WinCondition>();
        if (winCondition != null)
        {
            winCondition.TriggerWinFromQuest();
        }
    }
}
