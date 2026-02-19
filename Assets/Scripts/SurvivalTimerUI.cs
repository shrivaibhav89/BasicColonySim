using UnityEngine;
using UnityEngine.UI;

public class SurvivalTimerUI : MonoBehaviour
{
    public Text timerText;
    private float timer;
    private bool isActive = false;

    public void StartTimer(float duration)
    {
        timer = duration;
        isActive = true;
        gameObject.SetActive(true);
    }

    public void StopTimer()
    {
        isActive = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isActive) return;
        timer -= Time.deltaTime;
        timerText.text = Mathf.Max(0, Mathf.CeilToInt(timer)).ToString() + "s";
        if (timer <= 0)
        {
            StopTimer();
        }
    }
}
