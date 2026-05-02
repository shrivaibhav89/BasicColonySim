using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 20;
    [SerializeField] private int currentHealth;

    void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= Mathf.Max(0, amount);
        if (currentHealth <= 0)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfxAt(SoundId.EnemyDeath, transform.position);
            }

            Destroy(gameObject);
        }
    }
}
