using UnityEngine;
using System.Collections.Generic;
using System;

public class PopulationManager : MonoBehaviour
{
    public static PopulationManager Instance;
    
    [Header("Population")]
    public int currentPopulation = 5;
    public int maxPopulation = 5;
    
    public event Action OnPopulationChanged;
    
    public List<Building> allBuildings = new List<Building>();
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    public void RegisterBuilding(Building building)
    {
        allBuildings.Add(building);
        
        // If it's a house, increase max population
        if (building.GetPopulationCapacity() > 0)
        {
            maxPopulation += building.GetPopulationCapacity();
            // Instantly add citizens
            currentPopulation = Mathf.Min(currentPopulation + building.GetPopulationCapacity(), maxPopulation);
            OnPopulationChanged?.Invoke();
            
        }
        AssignWorkers();
    }
    
    void AssignWorkers()
    {
        // Simple auto-assignment: fill production buildings
        foreach (var building in allBuildings)
        {
            if (building.GetRequiredWorkers() > 0 && building.assignedWorkers < building.GetRequiredWorkers())
            {
                int needed = building.GetRequiredWorkers() - building.assignedWorkers;
                int available = currentPopulation - GetTotalAssignedWorkers();
                int toAssign = Mathf.Min(needed, available);
                for (int i = 0; i < toAssign; i++)
                {
                    if (building.RequestVillagerAssignment())
                    {
                        string bname = building.buildingData != null ? building.buildingData.buildingName : (building.gameObject != null ? building.gameObject.name : "");
                        Debug.Log($"{bname} now has {building.assignedWorkers}/{building.GetRequiredWorkers()} workers");
                    }
                }
            }
        }
    }

    public int GetIdleVillagers()
    {
        return Mathf.Max(0, currentPopulation - GetTotalAssignedWorkers());
    }
    
    int GetTotalAssignedWorkers()
    {
        int total = 0;
        foreach (var building in allBuildings)
            total += building.assignedWorkers;
        return total;
    }
}