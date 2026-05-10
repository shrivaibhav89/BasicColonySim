using System.Collections.Generic;
using UnityEngine;

public class ArmyUnit : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveSpeedFloat = "MoveSpeed";
    [SerializeField] private string throwTrigger = "Throw";
    [SerializeField] private bool useThrowAnimationEvent = false;
    [SerializeField] private float throwAnimationDelay = 0.22f;

    [Header("Patrol")]
    public float moveSpeed = 2.8f;
    public float patrolRadius = 8f;
    public float waypointTolerance = 0.35f;
    public float threatMemorySeconds = 5f;
    public float repathInterval = 0.5f;

    [Header("Attack")]
    public float attackRange = 5.5f;
    public float attackCooldown = 1.8f;
    public int attackDamage = 10;
    public float projectileSpeed = 8f;
    public float projectileArcHeight = 2.1f;
    [Range(0.05f, 2f)] public float projectileScale = 0.3f;
    public Transform throwOrigin;
    public GameObject spearProjectilePrefab;

    [Header("Tower Garrison")]
    public bool allowTowerGarrison = true;
    public float garrisonSearchRange = 30f;
    public float garrisonEnterDistance = 1.2f;
    public float garrisonedAttackRange = 8f;
    public float towerClimbSpeed = 3.5f;

    private Transform homeTownHall;
    private GridSystem gridSystem;
    private Vector2Int patrolPointGrid;
    private readonly List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex;
    private Vector3 targetWorldPos;
    private float nextAttackTime;
    private float nextRepathTime;

    private float defaultAttackRange;
    private bool isGarrisoned;
    private TowerGarrison currentTower;
    private TowerGarrison towerTarget;
    private bool tryingToGarrison;
    private bool climbingIntoTower;
    private Transform currentTowerSlot;
    private bool waveActive;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool throwQueued;
    private Transform queuedThrowTarget;
    private bool queuedHasSpawnOverride;
    private Vector3 queuedSpawnOverride;
    private float queuedThrowAt;
    private Vector3 lastFramePosition;

    public void Initialize(Transform townHall, GameObject spearPrefab)
    {
        homeTownHall = townHall;
        spearProjectilePrefab = spearPrefab;
        gridSystem = GridSystem.Instance;
        EnsureInitCache();
        ChooseNewPatrolPoint();
    }

    private void OnEnable()
    {
        EnemyWaveManager.OnWaveStateChanged += HandleWaveStateChanged;
        waveActive = EnemyWaveManager.IsWaveInProgress;
        if (waveActive)
        {
            TryStartGarrisonFlow();
        }
    }

    private void OnDisable()
    {
        EnemyWaveManager.OnWaveStateChanged -= HandleWaveStateChanged;
        ExitGarrisonIfNeeded();
    }

    private void Start()
    {
        EnsureInitCache();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        lastFramePosition = transform.position;

        if (homeTownHall == null)
        {
            GameObject townHallObj = GameObject.FindWithTag("TownHall");
            if (townHallObj != null)
            {
                homeTownHall = townHallObj.transform;
            }
        }

        if (gridSystem == null)
        {
            gridSystem = GridSystem.Instance;
        }

        ChooseNewPatrolPoint();
    }

    private void EnsureInitCache()
    {
        if (defaultAttackRange <= 0f)
        {
            defaultAttackRange = attackRange;
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private void Update()
    {
        TryProcessQueuedThrow();

        if (GameManager.Instance != null && GameManager.Instance.IsDefeated())
        {
            UpdateMoveAnimationSpeed();
            return;
        }

        if (isGarrisoned)
        {
            UpdateGarrisonedCombat();
            UpdateMoveAnimationSpeed();
            return;
        }

        if (climbingIntoTower)
        {
            UpdateClimbIntoTower();
            UpdateMoveAnimationSpeed();
            return;
        }

        if (tryingToGarrison)
        {
            UpdateMoveToTower();
            if (tryingToGarrison)
            {
                UpdateMoveAnimationSpeed();
                return;
            }
        }

        if (waveActive)
        {
            // During wave: prefer garrison. Only ground-fight when no free tower slot exists.
            bool hasActiveTower = HasAnyActiveTower();
            if (!tryingToGarrison && !climbingIntoTower)
            {
                if (towerTarget == null || !towerTarget.gameObject.activeInHierarchy || !towerTarget.HasFreeSlot)
                {
                    towerTarget = FindNearestAvailableTower();
                }

                if (towerTarget != null)
                {
                    tryingToGarrison = true;
                    UpdateMoveToTower();
                    UpdateMoveAnimationSpeed();
                    return;
                }

                if (hasActiveTower)
                {
                    // Towers exist but currently no slot for this unit: allow ground behavior.
                }
            }
        }

        EnemyAI enemy = FindBestEnemyTarget(transform.position, attackRange);
        if (enemy != null)
        {
            EngageEnemy(enemy);
            UpdateMoveAnimationSpeed();
            return;
        }

        if (waveActive)
        {
            // If no tower available and no target, hold position during wave.
            UpdateMoveAnimationSpeed();
            return;
        }

        Patrol();
        UpdateMoveAnimationSpeed();
    }

    private void HandleWaveStateChanged(bool active)
    {
        waveActive = active;
        if (active)
        {
            TryStartGarrisonFlow();
            return;
        }

        tryingToGarrison = false;
        climbingIntoTower = false;
        towerTarget = null;
        ExitGarrisonIfNeeded();
    }

    public void BeginWaveGarrison()
    {
        waveActive = true;
        allowTowerGarrison = true;
        TryStartGarrisonFlow();
    }

    private void TryStartGarrisonFlow()
    {
        if (!allowTowerGarrison || isGarrisoned)
        {
            return;
        }

        // Pick nearest eligible tower when a wave begins.
        towerTarget = FindNearestAvailableTower();
        tryingToGarrison = towerTarget != null;
    }

    private TowerGarrison FindNearestAvailableTower()
    {
        TowerGarrison nearest = null;
        float bestSqr = float.MaxValue;
        IReadOnlyList<TowerGarrison> towers = TowerGarrison.AllTowers;
        for (int i = 0; i < towers.Count; i++)
        {
            TowerGarrison tower = towers[i];
            if (tower == null || !tower.gameObject.activeInHierarchy || !tower.HasFreeSlot)
            {
                continue;
            }

            float sqr = (tower.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = tower;
            }
        }

        return nearest;
    }

    private void UpdateMoveToTower()
    {
        if (towerTarget == null || !towerTarget.gameObject.activeInHierarchy)
        {
            tryingToGarrison = false;
            towerTarget = null;
            return;
        }

        if (!towerTarget.HasFreeSlot)
        {
            tryingToGarrison = false;
            towerTarget = null;
            return;
        }

        Vector3 towerPos = towerTarget.transform.position;
        Vector3 delta = towerPos - transform.position;
        delta.y = 0f;
        if (delta.magnitude <= garrisonEnterDistance)
        {
            if (towerTarget.TryGarrison(this))
            {
                StartTowerClimb(towerTarget);
            }

            tryingToGarrison = false;
            towerTarget = null;
            return;
        }

        // Direct move for garrisoning so units do not stall due path/road edge cases.
        Vector3 dir = delta.normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.forward = dir;
        }
    }

    private void StartTowerClimb(TowerGarrison tower)
    {
        if (tower == null)
        {
            return;
        }

        currentTower = tower;
        currentTowerSlot = currentTower.GetSlotTransformFor(this);
        climbingIntoTower = true;
    }

    private void UpdateClimbIntoTower()
    {
        if (currentTower == null || !currentTower.gameObject.activeInHierarchy)
        {
            climbingIntoTower = false;
            currentTowerSlot = null;
            currentTower = null;
            return;
        }

        Vector3 targetPos = currentTower.GetPerchPositionFor(this);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, towerClimbSpeed * Time.deltaTime);
        Vector3 lookDir = currentTower.transform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.forward = lookDir.normalized;
        }

        if ((transform.position - targetPos).sqrMagnitude <= 0.01f)
        {
            FinishEnterTower();
        }
    }

    private void FinishEnterTower()
    {
        climbingIntoTower = false;
        isGarrisoned = true;
        attackRange = garrisonedAttackRange;
        Transform perch = currentTower != null ? currentTower.transform : null;
        transform.SetParent(perch, true);
        if (currentTower != null)
        {
            transform.position = currentTower.GetPerchPositionFor(this);
        }

        // Unit should appear on tower corner but not block ground navigation/collisions.
        SetUnitVisibleAndPhysical(true);
        SetUnitColliderState(false);
    }

    private void ExitGarrisonIfNeeded()
    {
        if (!isGarrisoned && !climbingIntoTower)
        {
            return;
        }

        climbingIntoTower = false;
        Vector3 exitPos = transform.position;
        if (currentTower != null)
        {
            exitPos = currentTower.GetUngarrisonPositionFor(this);
        }

        if (currentTower != null)
        {
            currentTower.RemoveGarrison(this);
        }

        transform.SetParent(null, true);
        transform.position = exitPos;

        isGarrisoned = false;
        currentTower = null;
        currentTowerSlot = null;
        attackRange = defaultAttackRange;
        SetUnitVisibleAndPhysical(true);
    }

    public void HandleTowerDestroyed(TowerGarrison tower)
    {
        if (!isGarrisoned || currentTower != tower)
        {
            return;
        }

        Vector3 fallbackGroundPos = tower != null ? tower.GetUngarrisonPositionFor(this) : transform.position + transform.right;
        currentTower = null;
        isGarrisoned = false;
        attackRange = defaultAttackRange;
        transform.SetParent(null, true);
        transform.position = fallbackGroundPos;
        SetUnitVisibleAndPhysical(true);
        currentTowerSlot = null;
    }

    private void SetUnitVisibleAndPhysical(bool visible)
    {
        EnsureInitCache();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }

        SetUnitColliderState(visible);
    }

    private void SetUnitColliderState(bool enabled)
    {
        EnsureInitCache();
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = enabled;
            }
        }
    }

    private void UpdateGarrisonedCombat()
    {
        if (currentTower == null || !currentTower.gameObject.activeInHierarchy)
        {
            HandleTowerDestroyed(currentTower);
            return;
        }

        Vector3 fireOrigin = currentTower.GetSlotPositionFor(this) + Vector3.up * 0.35f;
        transform.position = currentTower.GetPerchPositionFor(this);
        EnemyAI enemy = FindBestEnemyTarget(fireOrigin, garrisonedAttackRange);
        if (enemy == null)
        {
            return;
        }

        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude > 0.001f)
        {
            transform.forward = toEnemy.normalized;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        RequestThrow(enemy.transform, fireOrigin);
        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
    }

    private EnemyAI FindBestEnemyTarget(Vector3 searchCenter, float range)
    {
        if (ArmyThreatTracker.TryGetRecentThreat(threatMemorySeconds, out Vector3 threat))
        {
            searchCenter = threat;
        }

        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        EnemyAI best = null;
        float bestDist = range * range;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqr = (enemy.transform.position - searchCenter).sqrMagnitude;
            if (sqr <= bestDist)
            {
                best = enemy;
                bestDist = sqr;
            }
        }

        return best;
    }

    private void EngageEnemy(EnemyAI enemy)
    {
        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        if (toEnemy.sqrMagnitude > 0.001f)
        {
            transform.forward = toEnemy.normalized;
        }

        if (distance > attackRange)
        {
            MoveTowardPosition(enemy.transform.position, preferRoad: true);
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        RequestThrow(enemy.transform);
        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
    }

    private void RequestThrow(Transform target, Vector3? spawnOverride = null)
    {
        if (target == null)
        {
            return;
        }

        if (animator == null)
        {
            ThrowSpear(target, spawnOverride);
            return;
        }

        queuedThrowTarget = target;
        queuedHasSpawnOverride = spawnOverride.HasValue;
        queuedSpawnOverride = spawnOverride ?? Vector3.zero;
        throwQueued = true;

        if (!string.IsNullOrEmpty(throwTrigger))
        {
            animator.SetTrigger(throwTrigger);
        }

        if (!useThrowAnimationEvent)
        {
            queuedThrowAt = Time.time + Mathf.Max(0.01f, throwAnimationDelay);
        }
    }

    public void Animation_ReleaseSpear()
    {
        if (!throwQueued || queuedThrowTarget == null)
        {
            return;
        }

        Vector3? spawn = queuedHasSpawnOverride ? queuedSpawnOverride : (Vector3?)null;
        ThrowSpear(queuedThrowTarget, spawn);
        ClearQueuedThrow();
    }

    private void TryProcessQueuedThrow()
    {
        if (!throwQueued || useThrowAnimationEvent)
        {
            return;
        }

        if (Time.time < queuedThrowAt)
        {
            return;
        }

        if (queuedThrowTarget != null)
        {
            Vector3? spawn = queuedHasSpawnOverride ? queuedSpawnOverride : (Vector3?)null;
            ThrowSpear(queuedThrowTarget, spawn);
        }

        ClearQueuedThrow();
    }

    private void ClearQueuedThrow()
    {
        throwQueued = false;
        queuedThrowTarget = null;
        queuedHasSpawnOverride = false;
        queuedSpawnOverride = Vector3.zero;
        queuedThrowAt = 0f;
    }

    private void UpdateMoveAnimationSpeed()
    {
        if (animator == null || string.IsNullOrEmpty(moveSpeedFloat))
        {
            lastFramePosition = transform.position;
            return;
        }

        Vector3 delta = transform.position - lastFramePosition;
        delta.y = 0f;
        float speedNow = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        float normalized = moveSpeed > 0.01f ? Mathf.Clamp01(speedNow / moveSpeed) : 0f;
        animator.SetFloat(moveSpeedFloat, normalized);
        lastFramePosition = transform.position;
    }

    private void ThrowSpear(Transform target, Vector3? spawnOverride = null)
    {
        if (target == null)
        {
            return;
        }

        Vector3 spawn = spawnOverride ?? (throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up * 1.3f + transform.forward * 0.4f);
        GameObject projectile = spearProjectilePrefab != null
            ? Instantiate(spearProjectilePrefab, spawn, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        projectile.transform.localScale = Vector3.one * projectileScale;
        if (spearProjectilePrefab == null)
        {
            Collider c = projectile.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = false;
            }
        }

        ArmySpearProjectile spear = projectile.GetComponent<ArmySpearProjectile>();
        if (spear == null)
        {
            spear = projectile.AddComponent<ArmySpearProjectile>();
        }

        spear.Initialize(target, attackDamage, projectileSpeed, projectileArcHeight);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfxAt(SoundId.ProjectileLaunch, spawn);
        }
    }

    private void Patrol()
    {
        if (homeTownHall == null || gridSystem == null)
        {
            return;
        }

        Vector2Int currentGrid = gridSystem.WorldToGrid(transform.position);
        if (currentGrid == patrolPointGrid || currentPath.Count == 0)
        {
            ChooseNewPatrolPoint();
        }

        MoveAlongPath();
    }

    private void MoveTowardPosition(Vector3 worldPos, bool preferRoad)
    {
        if (gridSystem == null)
        {
            Vector3 flat = worldPos;
            flat.y = transform.position.y;
            Vector3 dir = (flat - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.forward = dir;
            }
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            Vector2Int start = gridSystem.WorldToGrid(transform.position);
            Vector2Int target = gridSystem.WorldToGrid(worldPos);
            if (preferRoad && TryGetNearestRoadTile(target, out Vector2Int road))
            {
                target = road;
            }

            SetPathTo(target);
            nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);
        }

        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
        Vector3 delta = targetWorldPos - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > waypointTolerance * waypointTolerance)
        {
            if (delta.sqrMagnitude > 0.001f)
            {
                transform.forward = delta.normalized;
            }
            return;
        }

        pathIndex++;
        if (pathIndex >= currentPath.Count)
        {
            return;
        }

        targetWorldPos = gridSystem.GridToWorld(currentPath[pathIndex]);
        targetWorldPos.y = transform.position.y;
        Vector3 dir = targetWorldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.forward = dir;
        }
    }

    private void ChooseNewPatrolPoint()
    {
        if (homeTownHall == null || gridSystem == null)
        {
            patrolPointGrid = Vector2Int.zero;
            return;
        }

        Vector2Int homeGrid = gridSystem.WorldToGrid(homeTownHall.position);
        List<Vector2Int> roads = FindRoadTiles(homeGrid, Mathf.CeilToInt(patrolRadius / Mathf.Max(0.1f, gridSystem.cellSize)));
        if (roads.Count == 0)
        {
            patrolPointGrid = homeGrid;
            SetPathTo(homeGrid);
            return;
        }

        patrolPointGrid = roads[Random.Range(0, roads.Count)];
        SetPathTo(patrolPointGrid);
    }

    private void SetPathTo(Vector2Int targetGrid)
    {
        if (gridSystem == null)
        {
            return;
        }

        Vector2Int startGrid = gridSystem.WorldToGrid(transform.position);
        currentPath.Clear();
        currentPath.AddRange(GridPathfinder.FindPath(gridSystem, startGrid, targetGrid, true, true));
        pathIndex = 0;
        if (currentPath.Count == 0)
        {
            return;
        }

        targetWorldPos = gridSystem.GridToWorld(currentPath[0]);
        targetWorldPos.y = transform.position.y;
    }

    private List<Vector2Int> FindRoadTiles(Vector2Int center, int radius)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int tile = new Vector2Int(center.x + x, center.y + y);
                if (!gridSystem.IsRoadAt(tile))
                {
                    continue;
                }

                float dist = Vector2Int.Distance(center, tile);
                if (dist <= radius)
                {
                    list.Add(tile);
                }
            }
        }

        return list;
    }

    private bool TryGetNearestRoadTile(Vector2Int center, out Vector2Int road)
    {
        road = center;
        if (gridSystem == null)
        {
            return false;
        }

        int maxRadius = 6;
        float bestDist = float.MaxValue;
        bool found = false;
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int tile = new Vector2Int(center.x + x, center.y + y);
                    if (!gridSystem.IsRoadAt(tile))
                    {
                        continue;
                    }

                    float dist = Vector2Int.Distance(center, tile);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        road = tile;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    private bool HasAnyActiveTower()
    {
        IReadOnlyList<TowerGarrison> towers = TowerGarrison.AllTowers;
        for (int i = 0; i < towers.Count; i++)
        {
            TowerGarrison tower = towers[i];
            if (tower != null && tower.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
    }
}
