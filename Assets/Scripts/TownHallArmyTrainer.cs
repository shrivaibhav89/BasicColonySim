using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TownHallTag))]
public class TownHallArmyTrainer : MonoBehaviour
{
    [Header("Training")]
    public int foodCost = 5;
    public int woodCost = 5;
    public int stoneCost = 0;
    public float trainingDuration = 5f;
    public int queuedUnits;

    [Header("Spawn")]
    public GameObject armyUnitPrefab;
    public Transform spawnPoint;
    public float spawnRadius = 1.8f;

    [Header("Army Combat")]
    public GameObject spearPrefab;
    public float armyAttackRange = 5.5f;
    public float armyAttackCooldown = 1.8f;
    public int armyAttackDamage = 10;
    public float armyProjectileSpeed = 8f;
    public float armyProjectileArcHeight = 2.1f;
    public float armyProjectileScale = 0.3f;

    private bool isTraining;
    private Building townHallBuilding;

    private void Awake()
    {
        townHallBuilding = GetComponent<Building>();
    }

    private void Update()
    {
        if (BuildingSelectionManager.CurrentSelectedBuilding == townHallBuilding && Input.GetKeyDown(KeyCode.T))
        {
            QueueTrainUnit();
        }
    }

    public bool QueueTrainUnit()
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        if (!ResourceManager.Instance.CanAfford(foodCost, woodCost, stoneCost))
        {
            return false;
        }

        ResourceManager.Instance.SpendResources(foodCost, woodCost, stoneCost);
        queuedUnits++;
        if (!isTraining)
        {
            StartCoroutine(TrainLoop());
        }

        return true;
    }

    private IEnumerator TrainLoop()
    {
        isTraining = true;
        while (queuedUnits > 0)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, trainingDuration));
            SpawnArmyUnit();
            queuedUnits--;
        }
        isTraining = false;
    }

    private void SpawnArmyUnit()
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position + transform.forward * 2f;
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = origin + new Vector3(circle.x, 0f, circle.y);

        GameObject unitObj = armyUnitPrefab != null
            ? Instantiate(armyUnitPrefab, spawnPos, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        if (armyUnitPrefab == null)
        {
            unitObj.name = "ArmyUnit";
            unitObj.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
        }

        ArmyUnit unit = unitObj.GetComponent<ArmyUnit>();
        if (unit == null)
        {
            unit = unitObj.AddComponent<ArmyUnit>();
        }

        unit.attackRange = armyAttackRange;
        unit.attackCooldown = armyAttackCooldown;
        unit.attackDamage = armyAttackDamage;
        unit.projectileSpeed = armyProjectileSpeed;
        unit.projectileArcHeight = armyProjectileArcHeight;
        unit.projectileScale = armyProjectileScale;
        unit.Initialize(transform, spearPrefab);
    }

}
