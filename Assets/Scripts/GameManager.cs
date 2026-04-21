using UnityEngine;

public class GameManager : MonoBehaviour

{
    public static GameManager Instance { get; private set; }
    public EnemyWaveManager enemyWaveManager;
    public GameObject defeatPopup;
    private bool isDefeated = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        if (defeatPopup != null)
            defeatPopup.SetActive(false);
    }

    public void Defeat()
    {
        if (isDefeated) return;
        isDefeated = true;
        if (defeatPopup != null)
            defeatPopup.SetActive(true);
        Time.timeScale = 0f; // Pause the game
    }

    public bool IsDefeated()
    {
        return isDefeated;
    }
    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void RestoreDefeatState(bool defeated)
    {
        isDefeated = defeated;
        if (defeatPopup != null)
        {
            defeatPopup.SetActive(defeated);
        }
    }
}
