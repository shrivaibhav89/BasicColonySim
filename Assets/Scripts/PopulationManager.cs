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
        if (building.populationCapacity > 0)
        {
            maxPopulation += building.populationCapacity;
            // Instantly add citizens
            currentPopulation = Mathf.Min(currentPopulation + building.populationCapacity, maxPopulation);
            OnPopulationChanged?.Invoke();
            
        }
        AssignWorkers();
    }
    
    void AssignWorkers()
    {
        // Simple auto-assignment: fill production buildings
        foreach (var building in allBuildings)
        {
            if (building.requiredWorkers > 0 && building.assignedWorkers < building.requiredWorkers)
            {
                int needed = building.requiredWorkers - building.assignedWorkers;
                int available = currentPopulation - GetTotalAssignedWorkers();
                int toAssign = Mathf.Min(needed, available);
                for (int i = 0; i < toAssign; i++)
                {
                    if (VillagerManager.Instance != null && VillagerManager.Instance.AssignWorkerToBuilding(building))
                    {
                        building.assignedWorkers += 1;
                        Debug.Log($"{building.buildingName} now has {building.assignedWorkers}/{building.requiredWorkers} workers");
                    }
                }
            }
        }
    }
    
    int GetTotalAssignedWorkers()
    {
        int total = 0;
        foreach (var building in allBuildings)
            total += building.assignedWorkers;
        return total;
    }
}