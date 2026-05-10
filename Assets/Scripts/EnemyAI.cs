using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public enum ProjectileForwardAxis
    {
        Forward,
        Up,
        Right
    }

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float avoidDistance = 2f;
    public float avoidStrength = 2f;
    public float separationRadius = 1.35f;
    public float separationStrength = 1.6f;
    public float wobbleAmplitude = 0.65f;
    public float wobbleFrequency = 0.9f;
    public float laneOffsetStrength = 0.55f;
    public float hesitationChance = 0.08f;
    public float hesitationDuration = 0.2f;

    [Header("Melee")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public int attackDamage = 10;

    [Header("Ranged Throw")]
    public bool enableRangedAttack = true;
    public float rangedAttackRange = 5.5f;
    public float rangedAttackCooldown = 2.2f;
    public int rangedAttackDamage = 8;
    public float projectileSpeed = 8f;
    public float projectileArcHeight = 2.4f;
    public float villagerRetreatSeconds = 1.5f;
    [Range(0.05f, 2f)] public float thrownProjectileScale = 0.3f;
    public ProjectileForwardAxis projectileForwardAxis = ProjectileForwardAxis.Up;
    public Transform throwOrigin;
    public GameObject[] throwableWeaponPrefabs;

    public LayerMask obstacleMask; // Assign this in the inspector to include building layers
    private Transform target;
    private Vector3 tacticalTargetOffset;
    private Vector3 laneOffset;
    private float wobbleSeed;
    private float hesitationUntil;
    private bool initialized;
    private float lastMeleeAttackTime = -999f;
    private float lastRangedAttackTime = -999f;
    public Vector3 Velocity { get; private set; }
    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
        InitializeBehavior();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDefeated())
            return;

        Velocity = Time.deltaTime > 0f ? (transform.position - lastPosition) / Time.deltaTime : Vector3.zero;
        lastPosition = transform.position;

        if (target != null)
        {
            if (!initialized)
            {
                InitializeBehavior();
            }

            Transform combatTarget = FindBestCombatTarget(rangedAttackRange);
            float distanceToCombatTarget = combatTarget != null
                ? Vector3.Distance(transform.position, combatTarget.position)
                : float.MaxValue;

            if (combatTarget != null && distanceToCombatTarget <= attackRange)
            {
                BuildingHealth buildingToAttack = combatTarget.GetComponent<BuildingHealth>();
                if (buildingToAttack != null && Time.time - lastMeleeAttackTime > attackCooldown)
                {
                    buildingToAttack.TakeDamage(attackDamage);
                    lastMeleeAttackTime = Time.time;
                    ArmyThreatTracker.ReportBuildingUnderAttack(buildingToAttack.transform.position);
                }

                Vector3 lookDir = (combatTarget.position - transform.position).normalized;
                if (lookDir != Vector3.zero)
                {
                    transform.forward = lookDir;
                }
                return;
            }

            if (combatTarget != null && enableRangedAttack && distanceToCombatTarget <= rangedAttackRange)
            {
                Vector3 lookDir = (combatTarget.position - transform.position).normalized;
                if (lookDir != Vector3.zero)
                {
                    transform.forward = lookDir;
                }

                if (Time.time - lastRangedAttackTime >= rangedAttackCooldown)
                {
                    ThrowProjectileAt(combatTarget);
                    lastRangedAttackTime = Time.time;
                }
                return;
            }

            if (Time.time < hesitationUntil)
            {
                return;
            }

            if (Random.value < hesitationChance * Time.deltaTime)
            {
                hesitationUntil = Time.time + hesitationDuration;
                return;
            }

            // Move toward target (Town Hall) with obstacle avoidance
            Vector3 desiredPoint = target.position + tacticalTargetOffset;
            desiredPoint += laneOffset * laneOffsetStrength;
            Vector3 toTarget = desiredPoint - transform.position;
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

            RaycastHit hit;
            Vector3 avoidDir = Vector3.zero;
            if (Physics.Raycast(transform.position, transform.forward, out hit, avoidDistance, obstacleMask))
            {
                avoidDir = Vector3.Cross(Vector3.up, hit.normal).normalized;
            }

            Vector3 separation = GetSeparationVector();
            Vector3 wobble = GetWobbleVector();
            Vector3 finalDir = (dir + avoidDir * avoidStrength + separation * separationStrength + wobble).normalized;
            transform.position += finalDir * moveSpeed * Time.deltaTime;
            if (finalDir != Vector3.zero)
                transform.forward = finalDir;
        }
    }

    public void SetTarget(Transform t)
    {
        target = t;
        InitializeBehavior();
    }

    public void SetTacticalOffset(Vector3 offset)
    {
        tacticalTargetOffset = new Vector3(offset.x, 0f, offset.z);
    }

    public void ConfigureRangedWeaponPrefabs(params GameObject[] prefabs)
    {
        throwableWeaponPrefabs = prefabs;
    }

    private void InitializeBehavior()
    {
        if (initialized)
        {
            return;
        }

        wobbleSeed = Random.Range(0f, 1000f);
        laneOffset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        laneOffset = laneOffset.sqrMagnitude > 0.01f ? laneOffset.normalized : Vector3.right;
        hesitationUntil = 0f;
        initialized = true;
    }

    private Vector3 GetSeparationVector()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius);
        Vector3 push = Vector3.zero;
        int count = 0;

        for (int i = 0; i < nearby.Length; i++)
        {
            EnemyAI other = nearby[i].GetComponent<EnemyAI>();
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.001f)
            {
                continue;
            }

            push += away / Mathf.Max(0.25f, dist);
            count++;
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        push /= count;
        return push.normalized;
    }

    private Vector3 GetWobbleVector()
    {
        float t = Time.time * wobbleFrequency + wobbleSeed;
        float x = Mathf.PerlinNoise(t, wobbleSeed) - 0.5f;
        float z = Mathf.PerlinNoise(wobbleSeed, t) - 0.5f;
        Vector3 wobble = new Vector3(x, 0f, z);
        return wobble * wobbleAmplitude;
    }

    private Transform FindBestCombatTarget(float maxRange)
    {
        Transform bestTarget = null;
        float bestSqrDistance = maxRange * maxRange;

        Collider[] buildingHits = Physics.OverlapSphere(transform.position, maxRange, obstacleMask);
        for (int i = 0; i < buildingHits.Length; i++)
        {
            BuildingHealth building = buildingHits[i].GetComponent<BuildingHealth>();
            if (building == null || !building.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqr = (building.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqrDistance)
            {
                bestSqrDistance = sqr;
                bestTarget = building.transform;
            }
        }

        if (VillagerManager.Instance != null)
        {
            var villagers = VillagerManager.Instance.GetActiveVillagers();
            for (int i = 0; i < villagers.Count; i++)
            {
                Villager villager = villagers[i];
                if (villager == null || !villager.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float sqr = (villager.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqrDistance)
                {
                    bestSqrDistance = sqr;
                    bestTarget = villager.transform;
                }
            }
        }

        return bestTarget;
    }

    private void ThrowProjectileAt(Transform throwTarget)
    {
        if (throwTarget == null)
        {
            return;
        }

        Vector3 spawnPosition = throwOrigin != null
            ? throwOrigin.position
            : transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;

        GameObject projectilePrefab = PickThrowablePrefab();
        GameObject projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        projectile.transform.localScale = Vector3.one * thrownProjectileScale;

        if (projectilePrefab == null)
        {
            projectile.name = "EnemyThrownProjectile";
            Collider fallbackCollider = projectile.GetComponent<Collider>();
            if (fallbackCollider != null)
            {
                fallbackCollider.enabled = false;
            }
        }

        EnemyThrownProjectile projectileController = projectile.GetComponent<EnemyThrownProjectile>();
        if (projectileController == null)
        {
            projectileController = projectile.AddComponent<EnemyThrownProjectile>();
        }

        projectileController.arcHeight = projectileArcHeight;
        projectileController.modelForwardAxis = (EnemyThrownProjectile.ProjectileForwardAxis)projectileForwardAxis;
        projectileController.Initialize(throwTarget, rangedAttackDamage, projectileSpeed, villagerRetreatSeconds);
    }

    private GameObject PickThrowablePrefab()
    {
        if (throwableWeaponPrefabs == null || throwableWeaponPrefabs.Length == 0)
        {
            return null;
        }

        int nonNullCount = 0;
        for (int i = 0; i < throwableWeaponPrefabs.Length; i++)
        {
            if (throwableWeaponPrefabs[i] != null)
            {
                nonNullCount++;
            }
        }

        if (nonNullCount == 0)
        {
            return null;
        }

        int pick = Random.Range(0, nonNullCount);
        for (int i = 0; i < throwableWeaponPrefabs.Length; i++)
        {
            if (throwableWeaponPrefabs[i] == null)
            {
                continue;
            }

            if (pick == 0)
            {
                return throwableWeaponPrefabs[i];
            }
            pick--;
        }

        return null;
    }
}
