using UnityEngine;
using UnityEngine.UI;

public class DefeatPopupUI : MonoBehaviour
{
    public Button restartButton;

    void Awake()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
    }

    void OnRestartClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }
}
