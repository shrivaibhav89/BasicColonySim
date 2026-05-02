using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-2000)]
public class SaveLoadManager : MonoBehaviour
{
    public delegate IEnumerator CloudLoadProviderDelegate(Action<ColonySaveData> onLoaded, Action<string> onError);
    public delegate IEnumerator CloudVillageLoadProviderDelegate(string villageKey, Action<ColonySaveData> onLoaded, Action<string> onError);

    private const string SaveFileName = "colony_save.dat";
    private const string EncryptionSeed = "BasicColonySim_Save_Key_v1";
    private const int SaveVersion = 1;

    private static SaveLoadManager instance;
    public static event Action<ColonySaveData, string> SaveCompleted;
    public static CloudLoadProviderDelegate CloudLoadProvider;
    public static CloudVillageLoadProviderDelegate CloudVillageLoadProvider;

    private ColonySaveData pendingLoadData;
    private bool isApplyingLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("SaveLoadManager");
        instance = go.AddComponent<SaveLoadManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveGame();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadGame();
        }

    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    private void SaveGame()
    {
        try
        {
            ColonySaveData data = CaptureCurrentState();
            string json = JsonUtility.ToJson(data);
            byte[] encrypted = Encrypt(json);
            File.WriteAllBytes(GetSavePath(), encrypted);

            string encryptedBase64 = Convert.ToBase64String(encrypted);
            try
            {
                SaveCompleted?.Invoke(data, encryptedBase64);
            }
            catch (Exception callbackException)
            {
                Debug.LogError("Save callback failed: " + callbackException.Message);
            }

            Debug.Log("Game saved (encrypted) at: " + GetSavePath());

            string shareVillageKey = NakamaSaveSyncManager.CurrentVillageKey;
            if (IsValidVillageKey(shareVillageKey))
            {
                Debug.Log("Village key: " + shareVillageKey + " (share this key to let others load your village).");
            }
            else
            {
                Debug.Log("Village key is not ready yet. Wait a moment and save again.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Save failed: " + ex.Message);
        }
    }

    private void LoadGame()
    {
        if (isApplyingLoad)
        {
            return;
        }

        if (CloudLoadProvider == null)
        {
            Debug.LogWarning("Cloud load failed: no cloud provider available.");
            return;
        }

        StartCoroutine(LoadGameFromCloudCoroutine());
    }

    public static bool RequestLoadVillageByKey(string villageKey)
    {
        if (instance == null)
        {
            Debug.LogWarning("Village load failed: SaveLoadManager is not ready.");
            return false;
        }

        return instance.LoadVillageByKey(villageKey);
    }

    private bool LoadVillageByKey(string villageKey)
    {
        if (isApplyingLoad)
        {
            Debug.LogWarning("Village load skipped: another load operation is already running.");
            return false;
        }

        if (CloudVillageLoadProvider == null)
        {
            Debug.LogWarning("Village load failed: no cloud village provider available.");
            return false;
        }

        string normalized = NormalizeVillageKey(villageKey);
        if (!IsValidVillageKey(normalized))
        {
            Debug.LogWarning("Village load failed: key must be 6 chars (A-Z, 0-9).");
            return false;
        }

        StartCoroutine(LoadVillageFromCloudCoroutine(normalized));
        return true;
    }

    private IEnumerator LoadGameFromCloudCoroutine()
    {
        if (CloudLoadProvider == null)
        {
            yield break;
        }

        ColonySaveData loadedData = null;
        string loadError = null;

        Action<ColonySaveData> onLoaded = data => loadedData = data;
        Action<string> onError = error => loadError = error;

        IEnumerator cloudRoutine = null;
        try
        {
            cloudRoutine = CloudLoadProvider(onLoaded, onError);
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
        }

        if (cloudRoutine != null)
        {
            yield return StartCoroutine(cloudRoutine);
        }

        if (!string.IsNullOrEmpty(loadError))
        {
            Debug.LogWarning("Cloud load failed: " + loadError);
            yield break;
        }

        if (loadedData == null)
        {
            Debug.LogWarning("Cloud load failed: no save data returned.");
            yield break;
        }

        BeginApplyLoadedData(loadedData);
    }

    private IEnumerator LoadVillageFromCloudCoroutine(string villageKey)
    {
        if (CloudVillageLoadProvider == null)
        {
            yield break;
        }

        ColonySaveData loadedData = null;
        string loadError = null;

        Action<ColonySaveData> onLoaded = data => loadedData = data;
        Action<string> onError = error => loadError = error;

        IEnumerator cloudRoutine = null;
        try
        {
            cloudRoutine = CloudVillageLoadProvider(villageKey, onLoaded, onError);
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
        }

        if (cloudRoutine != null)
        {
            yield return StartCoroutine(cloudRoutine);
        }

        if (!string.IsNullOrEmpty(loadError))
        {
            Debug.LogWarning("Village load failed: " + loadError);
            yield break;
        }

        if (loadedData == null)
        {
            Debug.LogWarning("Village load failed: no save data returned.");
            yield break;
        }

        BeginApplyLoadedData(loadedData);
    }

    private void BeginApplyLoadedData(ColonySaveData data)
    {
        pendingLoadData = data;
        isApplyingLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isApplyingLoad || pendingLoadData == null)
        {
            return;
        }

        StartCoroutine(ApplyLoadedStateCoroutine());
    }

    private IEnumerator ApplyLoadedStateCoroutine()
    {
        // Wait for scene objects and manager Start methods.
        yield return null;
        yield return null;

        try
        {
            yield return StartCoroutine(ApplyLoadedState());
            Debug.Log("Game loaded successfully from encrypted save.");
        }
        finally
        {
            pendingLoadData = null;
            isApplyingLoad = false;
        }
    }

    private IEnumerator ApplyLoadedState()
    {
        ColonySaveData data = pendingLoadData;
        if (data == null)
        {
            yield break;
        }

        GridSystem grid = FindObjectOfType<GridSystem>();
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        PopulationManager populationManager = FindObjectOfType<PopulationManager>();
        DayNightManager dayNightManager = FindObjectOfType<DayNightManager>();
        QuestManager questManager = FindObjectOfType<QuestManager>();
        VillagerManager villagerManager = FindObjectOfType<VillagerManager>();
        RoadManager roadManager = FindObjectOfType<RoadManager>();
        EnemyWaveManager enemyWaveManager = FindObjectOfType<EnemyWaveManager>();
        GameManager gameManager = FindObjectOfType<GameManager>();
        WinCondition winCondition = FindObjectOfType<WinCondition>();
        BuildingPlacer buildingPlacer = FindObjectOfType<BuildingPlacer>();
        DemolishManager demolishManager = FindObjectOfType<DemolishManager>();

        if (grid == null || resourceManager == null || populationManager == null || villagerManager == null)
        {
            Debug.LogError("Load failed: required managers are missing in scene.");
            yield break;
        }

        if (buildingPlacer != null)
        {
            buildingPlacer.CancelAllPlacement();
        }

        if (demolishManager != null)
        {
            demolishManager.SetDemolishMode(false);
        }

        Dictionary<string, GameObject> buildingPrefabMap = BuildBuildingPrefabMap();

        Building[] allBuildingsBefore = FindObjectsOfType<Building>(true);
        Building townHall = FindTownHallBuilding(allBuildingsBefore);

        // Clear moving entities and hostile units first.
        if (villagerManager != null)
        {
            villagerManager.DespawnAllVillagers();
        }

        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                Destroy(enemies[i].gameObject);
            }
        }

        RoadTile[] roads = FindObjectsOfType<RoadTile>(true);
        for (int i = 0; i < roads.Length; i++)
        {
            if (roads[i] != null)
            {
                Destroy(roads[i].gameObject);
            }
        }

        BuildingState townHallState = null;
        for (int i = 0; i < data.buildings.Count; i++)
        {
            if (data.buildings[i] != null && data.buildings[i].isTownHall)
            {
                townHallState = data.buildings[i];
                break;
            }
        }

        for (int i = 0; i < allBuildingsBefore.Length; i++)
        {
            Building building = allBuildingsBefore[i];
            if (building == null)
            {
                continue;
            }

            if (townHall != null && building == townHall && townHallState != null)
            {
                continue;
            }

            building.DestroyBuilding();
        }

        yield return null;

        grid.ClearAllOccupancy();

        if (townHall != null && townHallState != null)
        {
            ApplyBuildingState(townHall, townHallState, grid);
            grid.SetAreaOccupied(townHallState.gridOrigin, townHall.FootprintSize, true);
        }

        if (roadManager != null && roadManager.roadPrefab != null)
        {
            for (int i = 0; i < data.roads.Count; i++)
            {
                RoadState roadState = data.roads[i];
                if (roadState == null)
                {
                    continue;
                }

                Vector3 worldPos = grid.GridToWorld(roadState.gridPosition);
                worldPos.y = roadManager.roadHeight;
                GameObject roadObj = Instantiate(roadManager.roadPrefab, worldPos, Quaternion.identity);
                roadObj.name = roadManager.roadPrefab.name;

                GridObject gridObject = roadObj.GetComponent<GridObject>();
                if (gridObject != null)
                {
                    gridObject.Initialize(grid, roadState.gridPosition);
                }

                grid.SetOccupied(roadState.gridPosition, true);
                grid.SetRoad(roadState.gridPosition, true);
            }
        }

        for (int i = 0; i < data.buildings.Count; i++)
        {
            BuildingState state = data.buildings[i];
            if (state == null || state.isTownHall)
            {
                continue;
            }

            GameObject prefab;
            if (!buildingPrefabMap.TryGetValue(state.key, out prefab) || prefab == null)
            {
                Debug.LogWarning("Load skipped unknown building type: " + state.key);
                continue;
            }

            GameObject obj = Instantiate(prefab, state.position, state.rotation);
            obj.name = prefab.name;

            Building building = obj.GetComponent<Building>();
            if (building == null)
            {
                Destroy(obj);
                continue;
            }

            building.SetGridOrigin(state.gridOrigin);
            building.RegisterBuildingInPopulationManager();
            grid.SetAreaOccupied(state.gridOrigin, building.FootprintSize, true);

            if (!state.productionEnabled)
            {
                building.SetProductionEnabled(false);
            }

            BuildingHealth health = building.GetComponent<BuildingHealth>();
            if (health != null)
            {
                health.maxHealth = Mathf.Max(1, state.maxHealth);
                health.currentHealth = Mathf.Clamp(state.currentHealth, 0, health.maxHealth);
            }
        }

        resourceManager.SetResourceState(
            data.resources.food,
            data.resources.wood,
            data.resources.stone,
            data.resources.foodCap,
            data.resources.woodCap,
            data.resources.stoneCap,
            data.resources.productionEfficiency);

        populationManager.SetPopulationState(data.population.currentPopulation, data.population.maxPopulation);

        for (int i = 0; i < data.population.jobPriorities.Count; i++)
        {
            JobPriorityState prio = data.population.jobPriorities[i];
            if (prio == null)
            {
                continue;
            }

            populationManager.SetJobPriority(prio.jobType, prio.priority);
        }

        populationManager.RefreshWorkerAssignments();

        if (villagerManager != null)
        {
            int targetVillagers = Mathf.Max(data.population.currentPopulation, data.villagers.Count);
            villagerManager.EnsureVillagerCount(targetVillagers);
            List<Villager> activeVillagers = villagerManager.GetActiveVillagers();

            int count = Mathf.Min(activeVillagers.Count, data.villagers.Count);
            for (int i = 0; i < count; i++)
            {
                if (activeVillagers[i] != null && data.villagers[i] != null)
                {
                    activeVillagers[i].Teleport(data.villagers[i].position);
                }
            }
        }

        if (dayNightManager != null)
        {
            dayNightManager.SetDayState(data.day.currentDay, data.day.dayTimer);
            dayNightManager.SetPaused(data.day.isPaused);
        }

        if (questManager != null && data.quest != null)
        {
            questManager.RestoreRuntimeState(data.quest);
        }

        if (enemyWaveManager != null && data.enemyWave != null)
        {
            enemyWaveManager.RestoreRuntimeState(data.enemyWave);
            enemyWaveManager.SetDay(Mathf.Max(data.enemyWave.currentDay, data.day.currentDay));
        }

        if (gameManager != null)
        {
            gameManager.RestoreDefeatState(data.defeated);
        }

        if (winCondition != null)
        {
            winCondition.RestoreWinState(data.won);
        }

        Time.timeScale = Mathf.Clamp(data.timeScale, 0f, 4f);
    }

    private ColonySaveData CaptureCurrentState()
    {
        ColonySaveData data = new ColonySaveData();
        data.version = SaveVersion;
        data.sceneName = SceneManager.GetActiveScene().name;
        data.savedAtUtc = DateTime.UtcNow.ToString("o");
        data.timeScale = Time.timeScale;

        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager != null)
        {
            data.resources.food = resourceManager.food;
            data.resources.wood = resourceManager.wood;
            data.resources.stone = resourceManager.stone;
            data.resources.foodCap = resourceManager.foodCap;
            data.resources.woodCap = resourceManager.woodCap;
            data.resources.stoneCap = resourceManager.stoneCap;
            data.resources.productionEfficiency = resourceManager.productionEfficiency;
        }

        PopulationManager populationManager = FindObjectOfType<PopulationManager>();
        if (populationManager != null)
        {
            data.population.currentPopulation = populationManager.currentPopulation;
            data.population.maxPopulation = populationManager.maxPopulation;

            List<JobPriority> priorities = populationManager.GetJobPrioritiesSnapshot();
            for (int i = 0; i < priorities.Count; i++)
            {
                JobPriority entry = priorities[i];
                if (entry == null)
                {
                    continue;
                }

                JobPriorityState state = new JobPriorityState();
                state.jobType = entry.jobType;
                state.priority = entry.priority;
                data.population.jobPriorities.Add(state);
            }
        }

        DayNightManager dayNightManager = FindObjectOfType<DayNightManager>();
        if (dayNightManager != null)
        {
            data.day.currentDay = dayNightManager.currentDay;
            data.day.dayTimer = dayNightManager.GetDayTimer();
            data.day.isPaused = dayNightManager.isPaused;
        }

        QuestManager questManager = FindObjectOfType<QuestManager>();
        if (questManager != null)
        {
            data.quest = questManager.CaptureRuntimeState();
        }

        EnemyWaveManager enemyWaveManager = FindObjectOfType<EnemyWaveManager>();
        if (enemyWaveManager != null)
        {
            data.enemyWave = enemyWaveManager.CaptureRuntimeState();
        }

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            data.defeated = gameManager.IsDefeated();
        }

        WinCondition winCondition = FindObjectOfType<WinCondition>();
        if (winCondition != null)
        {
            data.won = winCondition.IsWon;
        }

        GridSystem grid = FindObjectOfType<GridSystem>();
        Building[] buildings = FindObjectsOfType<Building>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            Building building = buildings[i];
            if (building == null || building.isGhost)
            {
                continue;
            }

            BuildingState state = new BuildingState();
            state.key = GetBuildingKey(building);
            state.position = building.transform.position;
            state.rotation = building.transform.rotation;
            state.gridOrigin = building.GetGridOriginOrFallback(grid);
            state.productionEnabled = building.IsProductionEnabled;
            state.isTownHall = IsTownHall(building, state.key);

            BuildingHealth health = building.GetComponent<BuildingHealth>();
            if (health != null)
            {
                state.currentHealth = health.currentHealth;
                state.maxHealth = health.maxHealth;
            }
            else
            {
                state.currentHealth = 100;
                state.maxHealth = 100;
            }

            data.buildings.Add(state);
        }

        RoadTile[] roads = FindObjectsOfType<RoadTile>(true);
        for (int i = 0; i < roads.Length; i++)
        {
            if (roads[i] == null)
            {
                continue;
            }

            RoadState road = new RoadState();
            road.gridPosition = roads[i].GridPosition;
            data.roads.Add(road);
        }

        VillagerManager villagerManager = FindObjectOfType<VillagerManager>();
        if (villagerManager != null)
        {
            List<Villager> activeVillagers = villagerManager.GetActiveVillagers();
            for (int i = 0; i < activeVillagers.Count; i++)
            {
                Villager villager = activeVillagers[i];
                if (villager == null)
                {
                    continue;
                }

                VillagerState state = new VillagerState();
                state.position = villager.transform.position;
                state.state = (int)villager.CurrentState;
                data.villagers.Add(state);
            }
        }

        return data;
    }

    private Dictionary<string, GameObject> BuildBuildingPrefabMap()
    {
        Dictionary<string, GameObject> map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        BuildingButton[] buttons = FindObjectsOfType<BuildingButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            BuildingButton button = buttons[i];
            if (button == null || button.buildingPrefab == null)
            {
                continue;
            }

            Building prefabBuilding = button.buildingPrefab.GetComponent<Building>();
            if (prefabBuilding == null)
            {
                continue;
            }

            string key = GetBuildingKey(prefabBuilding);
            if (!map.ContainsKey(key))
            {
                map.Add(key, button.buildingPrefab);
            }
        }

        return map;
    }

    private static Building FindTownHallBuilding(Building[] buildings)
    {
        for (int i = 0; i < buildings.Length; i++)
        {
            Building building = buildings[i];
            if (building == null)
            {
                continue;
            }

            if (IsTownHall(building, null))
            {
                return building;
            }
        }

        return null;
    }

    private static bool IsTownHall(Building building, string key)
    {
        if (building == null)
        {
            return false;
        }

        if (building.GetComponent<TownHallTag>() != null || building.CompareTag("TownHall"))
        {
            return true;
        }

        string lookup = string.IsNullOrEmpty(key) ? GetBuildingKey(building) : key;
        if (string.IsNullOrWhiteSpace(lookup))
        {
            return false;
        }

        string lower = lookup.ToLowerInvariant();
        return lower.Contains("townhall") || lower.Contains("town hall") || lower.Contains("towncenter") || lower.Contains("town center");
    }

    private static string GetBuildingKey(Building building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        if (building.buildingData != null && !string.IsNullOrWhiteSpace(building.buildingData.buildingName))
        {
            return building.buildingData.buildingName.Trim();
        }

        return CleanCloneName(building.gameObject.name);
    }

    private static string CleanCloneName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return name.Replace("(Clone)", string.Empty).Trim();
    }

    private static void ApplyBuildingState(Building building, BuildingState state, GridSystem grid)
    {
        if (building == null || state == null)
        {
            return;
        }

        building.transform.position = state.position;
        building.transform.rotation = state.rotation;
        building.SetGridOrigin(state.gridOrigin);
        building.RegisterBuildingInPopulationManager();

        if (!state.productionEnabled)
        {
            building.SetProductionEnabled(false);
        }

        BuildingHealth health = building.GetComponent<BuildingHealth>();
        if (health != null)
        {
            health.maxHealth = Mathf.Max(1, state.maxHealth);
            health.currentHealth = Mathf.Clamp(state.currentHealth, 0, health.maxHealth);
        }
    }

    private static byte[] Encrypt(string plainText)
    {
        byte[] key = DeriveKey(EncryptionSeed);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                memoryStream.Write(aes.IV, 0, aes.IV.Length);
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                }

                return memoryStream.ToArray();
            }
        }
    }

    private static string Decrypt(byte[] encryptedBytes)
    {
        if (encryptedBytes == null || encryptedBytes.Length <= 16)
        {
            throw new InvalidDataException("Encrypted data is invalid.");
        }

        byte[] key = DeriveKey(EncryptionSeed);
        byte[] iv = new byte[16];
        Buffer.BlockCopy(encryptedBytes, 0, iv, 0, iv.Length);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            int cipherOffset = iv.Length;
            int cipherLength = encryptedBytes.Length - cipherOffset;
            using (MemoryStream cipherStream = new MemoryStream(encryptedBytes, cipherOffset, cipherLength))
            using (CryptoStream cryptoStream = new CryptoStream(cipherStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (MemoryStream plainStream = new MemoryStream())
            {
                cryptoStream.CopyTo(plainStream);
                return Encoding.UTF8.GetString(plainStream.ToArray());
            }
        }
    }

    private static byte[] DeriveKey(string seed)
    {
        using (SHA256 sha = SHA256.Create())
        {
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }
    }

    public static bool IsValidVillageKey(string villageKey)
    {
        if (string.IsNullOrEmpty(villageKey) || villageKey.Length != 6)
        {
            return false;
        }

        for (int i = 0; i < villageKey.Length; i++)
        {
            char c = villageKey[i];
            bool isDigit = c >= '0' && c <= '9';
            bool isUpper = c >= 'A' && c <= 'Z';
            if (!isDigit && !isUpper)
            {
                return false;
            }
        }

        return true;
    }

    public static string NormalizeVillageKey(string villageKey)
    {
        if (string.IsNullOrEmpty(villageKey))
        {
            return string.Empty;
        }

        string upper = villageKey.ToUpperInvariant();
        StringBuilder builder = new StringBuilder(6);
        for (int i = 0; i < upper.Length; i++)
        {
            char c = upper[i];
            bool isDigit = c >= '0' && c <= '9';
            bool isUpper = c >= 'A' && c <= 'Z';
            if (isDigit || isUpper)
            {
                builder.Append(c);
            }

            if (builder.Length >= 6)
            {
                break;
            }
        }

        return builder.ToString();
    }

    public static bool TryDecodeSaveDataFromEncryptedBase64(string encryptedBase64, out ColonySaveData data, out string error)
    {
        data = null;
        error = null;

        if (string.IsNullOrWhiteSpace(encryptedBase64))
        {
            error = "Encrypted payload is empty.";
            return false;
        }

        try
        {
            byte[] encrypted = Convert.FromBase64String(encryptedBase64);
            string json = Decrypt(encrypted);
            data = JsonUtility.FromJson<ColonySaveData>(json);
            if (data == null)
            {
                error = "Decoded save payload is invalid.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

[Serializable]
public class ColonySaveData
{
    public int version = 1;
    public string sceneName = string.Empty;
    public string savedAtUtc = string.Empty;
    public float timeScale = 1f;
    public bool defeated;
    public bool won;

    public ResourceState resources = new ResourceState();
    public PopulationState population = new PopulationState();
    public DayState day = new DayState();
    public QuestRuntimeState quest = new QuestRuntimeState();
    public EnemyWaveRuntimeState enemyWave = new EnemyWaveRuntimeState();

    public List<BuildingState> buildings = new List<BuildingState>();
    public List<RoadState> roads = new List<RoadState>();
    public List<VillagerState> villagers = new List<VillagerState>();
}

[Serializable]
public class ResourceState
{
    public int food;
    public int wood;
    public int stone;
    public int foodCap;
    public int woodCap;
    public int stoneCap;
    public float productionEfficiency = 1f;
}

[Serializable]
public class PopulationState
{
    public int currentPopulation;
    public int maxPopulation;
    public List<JobPriorityState> jobPriorities = new List<JobPriorityState>();
}

[Serializable]
public class JobPriorityState
{
    public JobType jobType;
    public int priority;
}

[Serializable]
public class DayState
{
    public int currentDay = 1;
    public float dayTimer;
    public bool isPaused;
}

[Serializable]
public class BuildingState
{
    public string key;
    public Vector3 position;
    public Quaternion rotation;
    public Vector2Int gridOrigin;
    public bool productionEnabled = true;
    public int currentHealth = 100;
    public int maxHealth = 100;
    public bool isTownHall;
}

[Serializable]
public class RoadState
{
    public Vector2Int gridPosition;
}

[Serializable]
public class VillagerState
{
    public Vector3 position;
    public int state;
}
