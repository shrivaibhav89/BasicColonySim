using UnityEngine;
using UnityEngine.UI;

public class WinCondition : MonoBehaviour
{
    private int targetPopulation = 100;
    public GameObject winPanel;
    public Text winText;
    
    private bool won = false;
    
    void Start()
    {
        winPanel.SetActive(false);
       // PopulationManager.Instance.OnPopulationChanged += CheckWin;
    }
    
    void CheckWin()
    {
        if (!won && PopulationManager.Instance.currentPopulation >= targetPopulation)
        {
            won = true;
            ShowWin();
        }
    }

    public void TriggerWinFromQuest()
    {
        if (won)
        {
            return;
        }

        won = true;
        ShowWin();
    }
    
    void ShowWin()
    {
        winPanel.SetActive(true);
        winText.text = $"🎉 VICTORY! 🎉\n\nYou reached {targetPopulation} population!\n\nTime: {Time.timeSinceLevelLoad:F0}s";
        Time.timeScale = 0.5f; // Slow motion effect!
    }
}