using UnityEngine;

public class TownCenterProjectile : MonoBehaviour
{
    public float speed = 12f;
    public int damage = 10;
    public float hitDistance = 0.35f;
    public float hitRadius = 1.25f;
    public float lifetime = 5f;
    public float arcHeight = 4f;

    private Transform target;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float travelDuration = 1f;
    private float age;

    public void Initialize(Transform targetTransform, int projectileDamage, float projectileSpeed)
    {
        Vector3 impactPosition = targetTransform != null ? targetTransform.position + Vector3.up * 0.5f : transform.position;
        Initialize(targetTransform, projectileDamage, projectileSpeed, impactPosition);
    }

    public void Initialize(Transform targetTransform, int projectileDamage, float projectileSpeed, Vector3 predictedImpactPosition)
    {
        target = targetTransform;
        damage = projectileDamage;
        speed = projectileSpeed;

        startPosition = transform.position;
        targetPosition = predictedImpactPosition;

        float distance = Vector3.Distance(startPosition, targetPosition);
        travelDuration = Mathf.Max(0.15f, distance / Mathf.Max(0.01f, speed));
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float t = Mathf.Clamp01(age / travelDuration);
        Vector3 previousPosition = transform.position;
        Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, t);
        float height = Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = flatPosition + Vector3.up * height;

        Vector3 direction = transform.position - previousPosition;
        if (direction != Vector3.zero)
        {
            transform.forward = direction.normalized;
        }

        if (t >= 1f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        EnemyHealth enemyHealth = null;
        if (target != null)
        {
            enemyHealth = target.GetComponent<EnemyHealth>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = FindClosestEnemyAtImpact();
        }

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfxAt(SoundId.ProjectileHit, transform.position);
            }
        }

        Destroy(gameObject);
    }

    private EnemyHealth FindClosestEnemyAtImpact()
    {
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        EnemyHealth closest = null;
        float closestDistanceSqr = hitRadius * hitRadius;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - targetPosition).sqrMagnitude;
            if (distanceSqr <= closestDistanceSqr)
            {
                closest = enemy;
                closestDistanceSqr = distanceSqr;
            }
        }

        return closest;
    }
}
