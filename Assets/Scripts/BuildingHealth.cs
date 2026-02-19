using UnityEngine;

public class BuildingHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public delegate void OnDestroyed(BuildingHealth buildingHealth);
    public event OnDestroyed onDestroyed;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (onDestroyed != null)
                onDestroyed(this);

            // Check for Town Hall defeat
            if (gameObject.CompareTag("TownHall") && GameManager.Instance != null)
            {
                GameManager.Instance.Defeat();
            }
            DestroyBuilding();
        }
    }

    private void DestroyBuilding()
    {
        Building building = GetComponent<Building>();
        if (building != null)
        {
            building.DestroyBuilding();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
