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
    public event Action OnJobPrioritiesChanged;

    [Header("Job Priorities")]
    [SerializeField] private List<JobPriority> jobPriorities = new List<JobPriority>();
    
    public List<Building> allBuildings = new List<Building>();
    private readonly List<Building> dropoffBuildings = new List<Building>();
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        EnsureDefaultPriorities();
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

    public void SetJobPriority(JobType jobType, int priority)
    {
        EnsureDefaultPriorities();
        JobPriority entry = jobPriorities.Find(p => p.jobType == jobType);
        if (entry == null)
        {
            entry = new JobPriority { jobType = jobType, priority = priority };
            jobPriorities.Add(entry);
        }
        else
        {
            entry.priority = priority;
        }

        AssignWorkers();
        OnJobPrioritiesChanged?.Invoke();
    }

    public void RefreshWorkerAssignments()
    {
        AssignWorkers();
    }

    public void SetPopulationState(int current, int max)
    {
        maxPopulation = Mathf.Max(0, max);
        currentPopulation = Mathf.Clamp(current, 0, maxPopulation);
        OnPopulationChanged?.Invoke();
    }

    public List<JobPriority> GetJobPrioritiesSnapshot()
    {
        EnsureDefaultPriorities();
        List<JobPriority> snapshot = new List<JobPriority>();
        for (int i = 0; i < jobPriorities.Count; i++)
        {
            JobPriority entry = jobPriorities[i];
            if (entry == null)
            {
                continue;
            }

            snapshot.Add(new JobPriority
            {
                jobType = entry.jobType,
                priority = entry.priority
            });
        }

        return snapshot;
    }

    public int GetJobPriorityValue(JobType jobType)
    {
        return GetJobPriority(jobType);
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
        int available = currentPopulation - GetTotalAssignedWorkers();

        List<Building> candidates = new List<Building>();
        foreach (var building in allBuildings)
        {
            if (building == null)
            {
                continue;
            }

            if (!building.IsProductionEnabled)
            {
                continue;
            }

            if (building.GetRequiredWorkers() > 0 && building.assignedWorkers < building.GetRequiredWorkers())
            {
                candidates.Add(building);
            }
        }

        candidates.Sort((a, b) =>
        {
            int priorityA = GetJobPriority(a != null ? a.GetJobType() : JobType.None);
            int priorityB = GetJobPriority(b != null ? b.GetJobType() : JobType.None);
            int compare = priorityB.CompareTo(priorityA);
            if (compare != 0)
            {
                return compare;
            }

            int neededA = a != null ? a.GetRequiredWorkers() - a.assignedWorkers : 0;
            int neededB = b != null ? b.GetRequiredWorkers() - b.assignedWorkers : 0;
            return neededB.CompareTo(neededA);
        });

        foreach (var building in candidates)
        {
            if (building == null)
            {
                continue;
            }

            int needed = building.GetRequiredWorkers() - building.assignedWorkers;
            for (int i = 0; i < needed; i++)
            {
                if (available <= 0)
                {
                    if (!TryStealWorkerFromLowerPriority(building))
                    {
                        // no idle workers and nothing lower priority to steal from
                        return;
                    }

                    available++;
                }

                if (building.RequestVillagerAssignment())
                {
                    available--;
                    string bname = building.buildingData != null ? building.buildingData.buildingName : (building.gameObject != null ? building.gameObject.name : "");
                    Debug.Log($"{bname} now has {building.assignedWorkers}/{building.GetRequiredWorkers()} workers");
                }
                else
                {
                    available = Mathf.Max(0, available - 1);
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

    private int GetJobPriority(JobType jobType)
    {
        EnsureDefaultPriorities();
        for (int i = 0; i < jobPriorities.Count; i++)
        {
            if (jobPriorities[i].jobType == jobType)
            {
                return jobPriorities[i].priority;
            }
        }

        return 0;
    }

    private bool TryStealWorkerFromLowerPriority(Building targetBuilding)
    {
        if (targetBuilding == null)
        {
            return false;
        }

        int targetPriority = GetJobPriority(targetBuilding.GetJobType());
        Building lowestPriorityBuilding = null;
        int lowestPriority = int.MaxValue;

        foreach (var building in allBuildings)
        {
            if (building == null || building.assignedWorkers <= 0)
            {
                continue;
            }

            int priority = GetJobPriority(building.GetJobType());
            if (priority >= targetPriority)
            {
                continue;
            }

            if (priority < lowestPriority)
            {
                lowestPriority = priority;
                lowestPriorityBuilding = building;
            }
        }

        if (lowestPriorityBuilding == null)
        {
            return false;
        }

        return lowestPriorityBuilding.TryReleaseWorker(out _ , "Reassigned to higher priority job");
    }

    private void EnsureDefaultPriorities()
    {
        if (jobPriorities == null)
        {
            jobPriorities = new List<JobPriority>();
        }

        Array values = Enum.GetValues(typeof(JobType));
        foreach (JobType type in values)
        {
            if (type == JobType.None)
            {
                continue;
            }

            bool exists = jobPriorities.Exists(p => p.jobType == type);
            if (!exists)
            {
                jobPriorities.Add(new JobPriority { jobType = type, priority = 1 });
            }
        }
    }
}

[Serializable]
public class JobPriority
{
    public JobType jobType;
    public int priority = 1;
}
