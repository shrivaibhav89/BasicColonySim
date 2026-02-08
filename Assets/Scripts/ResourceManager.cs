using UnityEngine;
using System;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;
    
    [Header("Resources")]
    public int food = 20;
    public int wood = 30;
    public int stone = 0;
    
    [Header("Storage")]
    public int foodCap = 100;
    public int woodCap = 100;
    public int stoneCap = 100;
    
    public event Action OnResourcesChanged;
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    public bool CanAfford(int foodCost, int woodCost, int stoneCost)
    {
        return food >= foodCost && wood >= woodCost && stone >= stoneCost;
    }
    
    public void SpendResources(int foodCost, int woodCost, int stoneCost)
    {
        food -= foodCost;
        wood -= woodCost;
        stone -= stoneCost;
        OnResourcesChanged?.Invoke();
    }
    
    public void AddResources(int foodAmount, int woodAmount, int stoneAmount)
    {
        food = Mathf.Min(food + foodAmount, foodCap);
        wood = Mathf.Min(wood + woodAmount, woodCap);
        stone = Mathf.Min(stone + stoneAmount, stoneCap);
        OnResourcesChanged?.Invoke();
    }
    
    public void IncreaseStorageCap(int amount)
    {
        foodCap += amount;
        woodCap += amount;
        stoneCap += amount;
        OnResourcesChanged?.Invoke();
    }
}