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
    public event Action<Building> OnBuildingRegistered;
    public event Action<Building> OnWorkersAssigned;
    
    public List<Building> allBuildings = new List<Building>();
    private readonly List<Building> dropoffBuildings = new List<Building>();
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    public void RegisterBuilding(Building building)
    {
        allBuildings.Add(building);

        if (building != null && building.IsDropoff)
        {
            if (!dropoffBuildings.Contains(building))
            {
                dropoffBuildings.Add(building);
            }
            ReassignDropoffs();
        }
        else
        {
            UpdateDropoffFor(building);
        }
        
        // If it's a house, increase max population
        if (building.GetPopulationCapacity() > 0)
        {
            maxPopulation += building.GetPopulationCapacity();
            // Instantly add citizens
            currentPopulation = Mathf.Min(currentPopulation + building.GetPopulationCapacity(), maxPopulation);
            OnPopulationChanged?.Invoke();
            
        }
        AssignWorkers();
        OnBuildingRegistered?.Invoke(building);
    }

    public void NotifyWorkersAssigned(Building building)
    {
        OnWorkersAssigned?.Invoke(building);
    }

    public void UnregisterBuilding(Building building)
    {
        if (building == null)
        {
            return;
        }

        allBuildings.Remove(building);

        if (dropoffBuildings.Remove(building))
        {
            ReassignDropoffs();
        }
    }

    private void ReassignDropoffs()
    {
        for (int i = 0; i < allBuildings.Count; i++)
        {
            Building building = allBuildings[i];
            if (building == null || building.IsDropoff)
            {
                continue;
            }

            UpdateDropoffFor(building);
        }
    }

    private void UpdateDropoffFor(Building building)
    {
        if (building == null)
        {
            return;
        }

        Building nearest = FindNearestDropoff(building.transform.position);
        building.SetDropoff(nearest);
    }

    private Building FindNearestDropoff(Vector3 fromPosition)
    {
        Building nearest = null;
        float bestDistance = float.MaxValue;

        for (int i = dropoffBuildings.Count - 1; i >= 0; i--)
        {
            Building building = dropoffBuildings[i];
            if (building == null)
            {
                dropoffBuildings.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(fromPosition, building.transform.position);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                nearest = building;
            }
        }

        return nearest;
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