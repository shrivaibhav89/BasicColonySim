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
    public static event System.Action<bool> OnWaveStateChanged;
    public static bool IsWaveInProgress { get; private set; }

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
    [Range(1, 4)] public int spawnLanes = 3;
    [Range(0f, 180f)] public float laneArc = 110f;
    public float minSpawnInterval = 0.25f;
    public float maxSpawnInterval = 0.8f;
    public float tacticalOffsetRadius = 5f;
    public int enemiesPerWave = 5;
    [Header("Enemy Ranged Throw")]
    public bool enableEnemyRangedThrow = true;
    [Range(5f, 6f)] public float enemyThrowRange = 5.5f;
    public float enemyThrowCooldown = 2.2f;
    public int enemyThrowDamage = 8;
    public float enemyThrowProjectileSpeed = 8f;
    public float enemyThrowArcHeight = 2.4f;
    public GameObject vikingAxeProjectilePrefab;
    public GameObject vikingSpearProjectilePrefab;
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
        IsWaveInProgress = false;
        OnWaveStateChanged?.Invoke(false);
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
        IsWaveInProgress = true;
        OnWaveStateChanged?.Invoke(true);
        ForceArmyUnitsToGarrison();
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
        List<Vector3> laneDirections = BuildLaneDirections();
        for (int i = 0; i < enemiesPerWave; i++)
        {
            Vector3 laneDir = laneDirections[i % laneDirections.Count];
            Vector3 spawnPos = GetRandomSpawnPosition(laneDir);
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
                ai.SetTacticalOffset(GetRandomTacticalOffset());
                ai.enableRangedAttack = enableEnemyRangedThrow;
                ai.rangedAttackRange = enemyThrowRange;
                ai.rangedAttackCooldown = enemyThrowCooldown;
                ai.rangedAttackDamage = enemyThrowDamage;
                ai.projectileSpeed = enemyThrowProjectileSpeed;
                ai.projectileArcHeight = enemyThrowArcHeight;
                ai.ConfigureRangedWeaponPrefabs(vikingAxeProjectilePrefab, vikingSpearProjectilePrefab);
            }
            yield return new WaitForSeconds(Random.Range(minSpawnInterval, maxSpawnInterval));
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
        IsWaveInProgress = false;
        OnWaveStateChanged?.Invoke(false);
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

    Vector3 GetRandomSpawnPosition(Vector3 laneDirection)
    {
        Vector3 direction = laneDirection;
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

    private List<Vector3> BuildLaneDirections()
    {
        List<Vector3> lanes = new List<Vector3>();
        int laneCount = Mathf.Max(1, spawnLanes);
        Vector3 baseDirection = spawnDirection;
        baseDirection.y = 0f;
        if (baseDirection.sqrMagnitude < 0.001f)
        {
            baseDirection = Vector3.forward;
        }

        baseDirection.Normalize();
        if (laneCount == 1 || laneArc <= 0.01f)
        {
            lanes.Add(baseDirection);
            return lanes;
        }

        float halfArc = laneArc * 0.5f;
        for (int i = 0; i < laneCount; i++)
        {
            float t = laneCount == 1 ? 0.5f : (float)i / (laneCount - 1);
            float angle = Mathf.Lerp(-halfArc, halfArc, t);
            Vector3 lane = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
            lanes.Add(lane.normalized);
        }

        return lanes;
    }

    private Vector3 GetRandomTacticalOffset()
    {
        Vector2 circle = Random.insideUnitCircle * tacticalOffsetRadius;
        return new Vector3(circle.x, 0f, circle.y);
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

    private void ForceArmyUnitsToGarrison()
    {
        ArmyUnit[] units = FindObjectsOfType<ArmyUnit>(true);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null && units[i].gameObject.activeInHierarchy)
            {
                units[i].BeginWaveGarrison();
            }
        }
    }
}
