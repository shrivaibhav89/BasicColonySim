using UnityEngine;

public class TownCenterDefense : MonoBehaviour
{
    [Header("Targeting")]
    public float attackRange = 18f;
    public float scanInterval = 0.2f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public GameObject turretVisual;
    public float turretTurnSpeed = 12f;
    public float fireRate = 1f;
    public float projectileSpeed = 12f;
    public int projectileDamage = 10;
    public float projectileArcHeight = 4f;
    public float projectileHitRadius = 1.25f;

    private Building building;
    private EnemyAI currentTarget;
    private float nextScanTime;
    private float nextFireTime;
    private bool waveActive;

    void Awake()
    {
        building = GetComponent<Building>();
    }

    void Update()
    {
        if (building != null && !building.IsProductionEnabled)
        {
            SetTurretVisible(false);
            return;
        }

        SetTurretVisible(waveActive);
        if (!waveActive)
        {
            currentTarget = null;
            return;
        }

        if (Time.time >= nextScanTime)
        {
            currentTarget = FindNearestEnemy();
            nextScanTime = Time.time + Mathf.Max(0.05f, scanInterval);
        }

        if (currentTarget == null)
        {
            return;
        }

        AimTurretAt(PredictImpactPosition(currentTarget));
        float secondsPerShot = 1f / Mathf.Max(0.01f, fireRate);
        if (Time.time >= nextFireTime)
        {
            FireAt(currentTarget);
            nextFireTime = Time.time + secondsPerShot;
        }
    }

    public void SetWaveActive(bool active)
    {
        waveActive = active;
        if (!waveActive)
        {
            currentTarget = null;
        }

        SetTurretVisible(waveActive && (building == null || building.IsProductionEnabled));
    }

    private EnemyAI FindNearestEnemy()
    {
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        EnemyAI nearest = null;
        float nearestDistanceSqr = attackRange * attackRange;

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr <= nearestDistanceSqr)
            {
                nearest = enemy;
                nearestDistanceSqr = distanceSqr;
            }
        }

        return nearest;
    }

    private void FireAt(EnemyAI target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 predictedImpactPosition = PredictImpactPosition(target);
        AimTurretAt(predictedImpactPosition);
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        GameObject projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
            : CreateDefaultProjectile(spawnPosition);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfxAt(SoundId.ProjectileLaunch, spawnPosition);
        }

        TownCenterProjectile projectileController = projectile.GetComponent<TownCenterProjectile>();
        if (projectileController == null)
        {
            projectileController = projectile.AddComponent<TownCenterProjectile>();
        }

        projectileController.arcHeight = projectileArcHeight;
        projectileController.hitRadius = projectileHitRadius;
        projectileController.Initialize(target.transform, projectileDamage, projectileSpeed, predictedImpactPosition);
    }

    private Vector3 PredictImpactPosition(EnemyAI enemy)
    {
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 enemyAimPosition = enemy.transform.position + Vector3.up * 0.5f;
        float distance = Vector3.Distance(spawnPosition, enemyAimPosition);
        float travelTime = Mathf.Max(0.15f, distance / Mathf.Max(0.01f, projectileSpeed));
        return enemyAimPosition + enemy.Velocity * travelTime;
    }

    private void AimTurretAt(Vector3 impactPosition)
    {
        if (turretVisual == null)
        {
            return;
        }

        Vector3 launchPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 nextArcPoint = GetArcPoint(launchPosition, impactPosition, 0.08f);
        Vector3 launchDirection = nextArcPoint - launchPosition;
        if (launchDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(launchDirection.normalized, Vector3.up);
        turretVisual.transform.rotation = Quaternion.Slerp(
            turretVisual.transform.rotation,
            targetRotation,
            Mathf.Clamp01(turretTurnSpeed * Time.deltaTime));
    }

    private Vector3 GetArcPoint(Vector3 start, Vector3 end, float t)
    {
        Vector3 flatPosition = Vector3.Lerp(start, end, Mathf.Clamp01(t));
        float height = Mathf.Sin(t * Mathf.PI) * projectileArcHeight;
        return flatPosition + Vector3.up * height;
    }

    private GameObject CreateDefaultProjectile(Vector3 position)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "TownCenterProjectile";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * 0.25f;

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        Renderer projectileRenderer = projectile.GetComponent<Renderer>();
        if (projectileRenderer != null)
        {
            projectileRenderer.material.color = new Color(1f, 0.35f, 0.05f, 1f);
        }

        return projectile;
    }

    private void SetTurretVisible(bool visible)
    {
        if (turretVisual != null && turretVisual.activeSelf != visible)
        {
            turretVisual.SetActive(visible);
        }
    }
}
