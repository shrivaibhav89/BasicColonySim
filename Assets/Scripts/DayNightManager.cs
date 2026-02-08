using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DayNightManager : MonoBehaviour
{
    public static DayNightManager Instance { get; private set; }

    [Header("Day Cycle")]
    [Min(1f)]
    public float dayDuration = 60f;
    public int currentDay = 1;
    [SerializeField]
    private float dayTimer = 0f;

    [Header("UI")]
    public Text dayCounterText;
    public Text dayCountdownText;

    [Header("Events")]
    public UnityEvent OnDayEnd;
    public UnityEvent OnDayStart;

    [Header("Pause")]
    public bool isPaused;
    public bool pauseWhenTimeScaleZero = true;

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
    }

    void Start()
    {
        UpdateUI();
        OnDayStart?.Invoke();
    }

    void Update()
    {
        if (isPaused || (pauseWhenTimeScaleZero && Mathf.Approximately(Time.timeScale, 0f)))
        {
            return;
        }

        dayTimer += Time.deltaTime;

        if (dayTimer >= dayDuration)
        {
            OnDayEnd?.Invoke();
            currentDay++;
            dayTimer = 0f;
            OnDayStart?.Invoke();
        }

        UpdateUI();
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    public float GetDayTimer()
    {
        return dayTimer;
    }

    private void UpdateUI()
    {
        if (dayCounterText != null)
        {
            dayCounterText.text = $"Day: {currentDay}";
        }

        if (dayCountdownText != null)
        {
            float timeLeft = Mathf.Max(0f, dayDuration - dayTimer);
            int secondsLeft = Mathf.CeilToInt(timeLeft);
            dayCountdownText.text = $"Next Day: {secondsLeft}s";
        }
    }
}
