using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class QuestPanelToggle : MonoBehaviour
{
    [Header("References")]
    public QuestManager questManager;
    public GameObject questPanel;
    public Button toggleButton;
    public GameObject notificationDot;

    [Header("Attention Feedback")]
    public float pulseScale = 1.12f;
    public float pulseDuration = 0.18f;
    public float pulseInterval = 1.2f;

    private Vector3 originalScale;
    private Coroutine attentionRoutine;

    void Awake()
    {
        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }

        originalScale = transform.localScale;
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleQuestPanel);
            toggleButton.onClick.AddListener(ToggleQuestPanel);
        }

        SetNotification(false);
    }

    void OnEnable()
    {
        if (questManager == null)
        {
            questManager = FindObjectOfType<QuestManager>();
        }

        if (questManager != null)
        {
            questManager.OnQuestActivated += HandleQuestActivated;
        }
    }

    void OnDisable()
    {
        if (questManager != null)
        {
            questManager.OnQuestActivated -= HandleQuestActivated;
        }
    }

    public void ToggleQuestPanel()
    {
        if (questPanel == null)
        {
            return;
        }

        bool shouldOpen = !questPanel.activeSelf;
        questPanel.SetActive(shouldOpen);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfx(SoundId.ButtonClick);
        }

        if (shouldOpen)
        {
            SetNotification(false);
        }
    }

    private void HandleQuestActivated()
    {
        if (questPanel != null && !questPanel.activeSelf)
        {
            SetNotification(true);
        }
    }

    private void SetNotification(bool visible)
    {
        if (notificationDot != null)
        {
            notificationDot.SetActive(visible);
        }

        if (visible)
        {
            if (attentionRoutine == null)
            {
                attentionRoutine = StartCoroutine(PulseAttention());
            }
        }
        else
        {
            if (attentionRoutine != null)
            {
                StopCoroutine(attentionRoutine);
                attentionRoutine = null;
            }

            transform.localScale = originalScale;
        }
    }

    private IEnumerator PulseAttention()
    {
        WaitForSeconds wait = new WaitForSeconds(pulseInterval);
        while (true)
        {
            yield return ScaleOverTime(originalScale, originalScale * pulseScale, pulseDuration);
            yield return ScaleOverTime(originalScale * pulseScale, originalScale, pulseDuration);
            yield return wait;
        }
    }

    private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.localScale = to;
    }
}
