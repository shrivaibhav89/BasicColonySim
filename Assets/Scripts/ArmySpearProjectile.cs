using UnityEngine;

public class ArmySpearProjectile : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 10;
    public float lifetime = 4f;
    public float arcHeight = 2.2f;
    public float impactRadius = 0.8f;

    private Transform target;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float travelDuration = 1f;
    private float age;

    public void Initialize(Transform targetTransform, int projectileDamage, float projectileSpeed, float projectileArcHeight)
    {
        target = targetTransform;
        damage = projectileDamage;
        speed = projectileSpeed;
        arcHeight = projectileArcHeight;

        startPosition = transform.position;
        targetPosition = targetTransform != null ? targetTransform.position + Vector3.up * 0.6f : transform.position;
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
        Vector3 previous = transform.position;
        Vector3 flat = Vector3.Lerp(startPosition, targetPosition, t);
        float height = Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = flat + Vector3.up * height;

        Vector3 direction = transform.position - previous;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        if (t >= 1f)
        {
            Impact();
        }
    }

    private void Impact()
    {
        EnemyHealth primary = target != null ? target.GetComponent<EnemyHealth>() : null;
        if (primary != null)
        {
            primary.TakeDamage(damage);
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(targetPosition, impactRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null)
                {
                    continue;
                }

                enemy.TakeDamage(damage);
                break;
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfxAt(SoundId.ProjectileHit, transform.position);
        }

        Destroy(gameObject);
    }
}
