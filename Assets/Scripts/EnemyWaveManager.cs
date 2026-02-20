using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyWaveManager : MonoBehaviour
{
    [Header("UI Reference")]
    public SurvivalTimerUI survivalTimerUI;
    public float survivalDuration = 120f; // 2 minutes
    [Header("Enemy Wave Settings")]
    public GameObject enemyPrefab;
    public float spawnRadius = 30f;
    public int enemiesPerWave = 5;
    public float timeBetweenWaves = 120f; // 2 minutes
    public int startWaveOnDay = 3;
    public Transform townHallTarget;

    private float waveTimer = 0f;
    [SerializeField] private int currentDay = 0;
    [SerializeField] private bool waveStarted = false;

    void Start()
    {
        // Optionally, find the TownHall automatically if not set
        if (townHallTarget == null)
        {
            var townHallObj = GameObject.FindWithTag("TownHall");
            if (townHallObj != null)
                townHallTarget = townHallObj.transform;
        }
    }

    public void StartEnemyWaves()
    {
        if (!waveStarted && currentDay >= startWaveOnDay)
        {
            waveStarted = true;
            StartCoroutine(SpawnEnemyWave());
        }


        // waveTimer = 0f; // Start immediately
    }

    public void SetDay(int day)
    {
        currentDay = day;
        StartEnemyWaves();
    }

    IEnumerator SpawnEnemyWave()
    {
        // Ensure townHallTarget is assigned at runtime before spawning enemies
        if (townHallTarget == null)
        {
            var townHallObj = GameObject.FindWithTag("TownHall");
            if (townHallObj != null)
                townHallTarget = townHallObj.transform;
            else
                Debug.LogWarning("EnemyWaveManager: TownHall object not found when spawning enemies!");
        }

        // Start survival timer and send villagers home
        if (survivalTimerUI != null)
            survivalTimerUI.StartTimer(survivalDuration);
        if (VillagerManager.Instance != null)
            VillagerManager.Instance.SendAllVillagersHome();

        Debug.Log($"Enemy Wave Triggered! Day: {currentDay}, Enemies: {enemiesPerWave}");
        for (int i = 0; i < enemiesPerWave; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && townHallTarget != null)
            {
                ai.SetTarget(townHallTarget);
            }
            yield return new WaitForSeconds(0.5f); // Stagger spawns
        }

        // Wait for survival timer to finish
        float timer = survivalDuration;
        while (timer > 0)
        {
            yield return null;
            timer -= Time.deltaTime;
        }

        // Wave finished: return villagers to work
        if (VillagerManager.Instance != null)
            VillagerManager.Instance.ReturnAllVillagersToWork();
        if (survivalTimerUI != null)
            survivalTimerUI.StopTimer();
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector2 circle = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 pos = new Vector3(circle.x, 0, circle.y);
        if (townHallTarget != null)
            pos += townHallTarget.position;
        return pos;
    }
}
