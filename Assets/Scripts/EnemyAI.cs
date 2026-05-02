using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float avoidDistance = 2f;
    public float avoidStrength = 2f;
    public LayerMask obstacleMask; // Assign this in the inspector to include building layers
    private Transform target;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public int attackDamage = 10;
    private float lastAttackTime = 0f;
    public Vector3 Velocity { get; private set; }
    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDefeated())
            return;

        Velocity = Time.deltaTime > 0f ? (transform.position - lastPosition) / Time.deltaTime : Vector3.zero;
        lastPosition = transform.position;

        if (target != null)
        {
            // Check for buildings in attack range
            Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, obstacleMask);
            BuildingHealth buildingToAttack = null;
            foreach (var col in hits)
            {
                buildingToAttack = col.GetComponent<BuildingHealth>();
                if (buildingToAttack != null)
                    break;
            }

            if (buildingToAttack != null)
            {
                // Attack building if cooldown passed
                if (Time.time - lastAttackTime > attackCooldown)
                {
                    buildingToAttack.TakeDamage(attackDamage);
                    lastAttackTime = Time.time;
                }
                // Face the building
                Vector3 lookDir = (buildingToAttack.transform.position - transform.position).normalized;
                if (lookDir != Vector3.zero)
                    transform.forward = lookDir;
                return;
            }

            // Move toward target (Town Hall) with obstacle avoidance
            Vector3 dir = (target.position - transform.position).normalized;
            RaycastHit hit;
            Vector3 avoidDir = Vector3.zero;
            if (Physics.Raycast(transform.position, transform.forward, out hit, avoidDistance, obstacleMask))
            {
                avoidDir = Vector3.Cross(Vector3.up, hit.normal).normalized;
            }
            Vector3 finalDir = (dir + avoidDir * avoidStrength).normalized;
            transform.position += finalDir * moveSpeed * Time.deltaTime;
            if (finalDir != Vector3.zero)
                transform.forward = finalDir;
        }
    }

    public void SetTarget(Transform t)
    {
        target = t;
    }
}
