using UnityEngine;

public enum QuestType
{
    BuildSpecificBuilding,
    ReachPopulation,
    CollectResource,
    AssignWorkers
}

public enum BuildingType
{
    None,
    WoodCutter,
    Farm,
    Quarry,
    Storage
}

[CreateAssetMenu(fileName = "Quest", menuName = "ColonySim/Quest Data", order = 1)]
public class QuestData : ScriptableObject
{
    [Header("Quest Info")]
    public int questID;
    public string questTitle;
    [TextArea]
    public string questDescription;
    public QuestType questType;

    [Header("Targets")]
    public BuildingType targetBuildingType = BuildingType.None;
    public int targetCount = 1;
    public ResourceType targetResourceType;

    [Header("Rewards")]
    public int foodReward;
    public int woodReward;
    public int stoneReward;

    [Header("Flags")]
    public bool isVictoryQuest;
}
