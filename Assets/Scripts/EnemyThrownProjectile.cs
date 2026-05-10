using UnityEngine;

public class EnemyThrownProjectile : MonoBehaviour
{
    public enum ProjectileForwardAxis
    {
        Forward,
        Up,
        Right
    }

    public float speed = 8f;
    public int damage = 8;
    public float lifetime = 4f;
    public float arcHeight = 2.4f;
    public float impactRadius = 0.9f;
    public ProjectileForwardAxis modelForwardAxis = ProjectileForwardAxis.Up;

    private Transform target;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float travelDuration = 1f;
    private float age;
    private float villagerRetreatSeconds = 1.5f;

    public void Initialize(Transform targetTransform, int projectileDamage, float projectileSpeed, float villagerRetreatTime)
    {
        target = targetTransform;
        damage = projectileDamage;
        speed = projectileSpeed;
        villagerRetreatSeconds = villagerRetreatTime;

        startPosition = transform.position;
        targetPosition = targetTransform != null
            ? targetTransform.position + Vector3.up * 0.6f
            : transform.position;

        float distance = Vector3.Distance(startPosition, targetPosition);
        travelDuration = Mathf.Max(0.2f, distance / Mathf.Max(0.01f, speed));
    }

    private void Update()
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
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = GetAlignedRotation(direction.normalized);
        }

        if (t >= 1f)
        {
            ApplyImpact();
        }
    }

    private void ApplyImpact()
    {
        bool hitSomething = false;

        if (target != null)
        {
            hitSomething |= TryDamageBuilding(target);
            hitSomething |= TryDisruptVillager(target);
        }

        Collider[] hits = Physics.OverlapSphere(targetPosition, impactRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            hitSomething |= TryDamageBuilding(hits[i].transform);
            hitSomething |= TryDisruptVillager(hits[i].transform);
        }

        if (hitSomething && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfxAt(SoundId.ProjectileHit, transform.position);
        }

        Destroy(gameObject);
    }

    private bool TryDamageBuilding(Transform t)
    {
        if (t == null)
        {
            return false;
        }

        BuildingHealth building = t.GetComponent<BuildingHealth>();
        if (building == null)
        {
            building = t.GetComponentInParent<BuildingHealth>();
        }

        if (building == null)
        {
            return false;
        }

        building.TakeDamage(damage);
        ArmyThreatTracker.ReportBuildingUnderAttack(building.transform.position);
        return true;
    }

    private bool TryDisruptVillager(Transform t)
    {
        if (t == null)
        {
            return false;
        }

        Villager villager = t.GetComponent<Villager>();
        if (villager == null)
        {
            villager = t.GetComponentInParent<Villager>();
        }

        if (villager == null || !villager.gameObject.activeInHierarchy)
        {
            return false;
        }

        villager.TakeDamage(damage);
        if (villagerRetreatSeconds > 0f)
        {
            villager.Invoke(nameof(Villager.ReturnToWork), villagerRetreatSeconds);
        }
        return true;
    }

    private Quaternion GetAlignedRotation(Vector3 flightDirection)
    {
        switch (modelForwardAxis)
        {
            case ProjectileForwardAxis.Up:
                return Quaternion.FromToRotation(Vector3.up, flightDirection);
            case ProjectileForwardAxis.Right:
                return Quaternion.FromToRotation(Vector3.right, flightDirection);
            default:
                return Quaternion.LookRotation(flightDirection, Vector3.up);
        }
    }
}
