using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JobPriorityRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    public Text jobNameText;

    private JobType jobType;
    private PopulationManager populationManager;
    private JobPriorityUI owner;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private LayoutElement layoutElement;
    private Vector2 originalAnchoredPos;

    public JobType JobType => jobType;

    public void Initialize(JobType type, PopulationManager manager, JobPriorityUI ownerUI)
    {
        jobType = type;
        populationManager = manager;
        owner = ownerUI;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        rootCanvas = GetComponentInParent<Canvas>();

        if (jobNameText != null)
        {
            jobNameText.text = type.ToString();
        }
    }

    public void Refresh()
    {
        if (jobNameText != null)
        {
            jobNameText.text = jobType.ToString();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null)
        {
            return;
        }

        originalAnchoredPos = rectTransform.anchoredPosition;
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.8f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null || rootCanvas == null)
        {
            return;
        }

        Vector2 delta = eventData.delta / rootCanvas.scaleFactor;
        rectTransform.anchoredPosition += delta;

        owner?.HandleRowDrag(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
        }

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        owner?.HandleRowDrop(this);
    }
}
