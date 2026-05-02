using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[System.Serializable]
public class EnemyWaveRuntimeState
{
    public int currentDay;
    public bool waveStarted;
}

public class EnemyWaveManager : MonoBehaviour
{
    [Header("UI Reference")]
    public SurvivalTimerUI survivalTimerUI;
    public Text waveCountdownText;
    public EnemyWaveFeedback waveFeedback;
    public float survivalDuration = 120f; // 2 minutes
    [Header("Enemy Wave Settings")]
    public GameObject enemyPrefab;
    public float spawnRadius = 30f;
    public Vector3 spawnDirection = Vector3.forward;
    public float spawnSpread = 4f;
    public int enemiesPerWave = 5;
    public float timeBetweenWaves = 120f; // 2 minutes
    public int startWaveOnDay = 3;
    public Transform townHallTarget;

    private float waveTimer = 0f;
    [SerializeField] private int currentDay = 0;
    [SerializeField] private bool waveStarted = false;
    [SerializeField] private bool waveInProgress = false;

    public EnemyWaveRuntimeState CaptureRuntimeState()
    {
        return new EnemyWaveRuntimeState
        {
            currentDay = currentDay,
            waveStarted = waveStarted
        };
    }

    public void RestoreRuntimeState(EnemyWaveRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        StopAllCoroutines();
        if (survivalTimerUI != null)
        {
            survivalTimerUI.StopTimer();
        }

        SetTownCenterTurretsActive(false);
        if (waveFeedback != null)
        {
            waveFeedback.PlayWaveEnded();
        }

        currentDay = Mathf.Max(0, state.currentDay);
        waveStarted = state.waveStarted;
        waveInProgress = false;
        UpdateWaveCountdownUI();
    }

    void Start()
    {
        // Optionally, find the TownHall automatically if not set
        if (townHallTarget == null)
        {
            var townHallObj = GameObject.FindWithTag("TownHall");
            if (townHallObj != null)
                townHallTarget = townHallObj.transform;
        }

        if (waveFeedback == null)
        {
            waveFeedback = FindObjectOfType<EnemyWaveFeedback>();
        }

        UpdateWaveCountdownUI();
    }

    void Update()
    {
        UpdateWaveCountdownUI();
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
        waveInProgress = true;
        SetTownCenterTurretsActive(true);
        if (waveFeedback != null)
        {
            waveFeedback.PlayWaveStarted();
        }

        UpdateWaveCountdownUI();

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
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfx(SoundId.EnemyWaveWarning);
        }

        List<EnemyHealth> spawnedEnemies = new List<EnemyHealth>();
        for (int i = 0; i < enemiesPerWave; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfxAt(SoundId.EnemySpawn, spawnPos);
            }

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                enemyHealth = enemy.AddComponent<EnemyHealth>();
            }

            spawnedEnemies.Add(enemyHealth);

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
            if (AreAllEnemiesDefeated(spawnedEnemies))
            {
                break;
            }

            yield return null;
            timer -= Time.deltaTime;
        }

        // Wave finished: return villagers to work
        if (VillagerManager.Instance != null)
            VillagerManager.Instance.ReturnAllVillagersToWork();
        if (survivalTimerUI != null)
            survivalTimerUI.StopTimer();

        waveInProgress = false;
        SetTownCenterTurretsActive(false);
        if (waveFeedback != null)
        {
            waveFeedback.PlayWaveEnded();
        }

        UpdateWaveCountdownUI();
    }

    private bool AreAllEnemiesDefeated(List<EnemyHealth> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            return true;
        }

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                return false;
            }
        }

        return true;
    }

    private void SetTownCenterTurretsActive(bool active)
    {
        TownCenterDefense[] defenses = FindObjectsOfType<TownCenterDefense>(true);
        foreach (TownCenterDefense defense in defenses)
        {
            if (defense != null)
            {
                defense.SetWaveActive(active);
            }
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector3 direction = spawnDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 pos = direction * spawnRadius;
        pos += side * Random.Range(-spawnSpread, spawnSpread);
        if (townHallTarget != null)
            pos += townHallTarget.position;
        return pos;
    }

    private void UpdateWaveCountdownUI()
    {
        if (waveCountdownText == null)
        {
            return;
        }

        if (waveInProgress)
        {
            waveCountdownText.text = "Enemy Wave: Survive!";
            waveCountdownText.color = Color.red;
            waveCountdownText.enabled = true;
            return;
        }

        if (waveStarted)
        {
            waveCountdownText.text = "Enemy Wave: Complete";
            waveCountdownText.color = Color.white;
            waveCountdownText.enabled = true;
            return;
        }

        float secondsUntilWave = GetSecondsUntilFirstWave();
        if (secondsUntilWave <= 0f)
        {
            waveCountdownText.text = "Enemy Wave Incoming!";
            waveCountdownText.color = Color.red;
            waveCountdownText.enabled = true;
            return;
        }

        int seconds = Mathf.CeilToInt(secondsUntilWave);
        int minutesPart = seconds / 60;
        int secondsPart = seconds % 60;
        waveCountdownText.text = $"Enemy Wave In: {minutesPart:0}:{secondsPart:00}";
        waveCountdownText.color = Color.white;
        waveCountdownText.enabled = true;
    }

    private float GetSecondsUntilFirstWave()
    {
        DayNightManager dayNightManager = DayNightManager.Instance;
        if (dayNightManager == null)
        {
            int daysRemaining = Mathf.Max(0, startWaveOnDay - currentDay);
            return daysRemaining * 60f;
        }

        int daysRemainingIncludingToday = Mathf.Max(0, startWaveOnDay - dayNightManager.currentDay);
        float secondsRemainingToday = Mathf.Max(0f, dayNightManager.dayDuration - dayNightManager.GetDayTimer());

        if (daysRemainingIncludingToday <= 0)
        {
            return 0f;
        }

        return ((daysRemainingIncludingToday - 1) * dayNightManager.dayDuration) + secondsRemainingToday;
    }
}
