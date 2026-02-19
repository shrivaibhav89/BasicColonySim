using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DemolishButton : MonoBehaviour
{
    public DemolishManager demolishManager;
    public Image icon;
    public Color activeColor = new Color(1f, 0.6f, 0.6f, 1f);
    public Color inactiveColor = Color.white;

    private Button button;
    private bool lastActiveState;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    void Start()
    {
        if (demolishManager == null)
        {
            demolishManager = FindObjectOfType<DemolishManager>(true);
        }

        RefreshVisual();
    }

    void Update()
    {
        if (demolishManager == null)
        {
            return;
        }

        bool current = demolishManager.IsDemolishModeActive;
        if (current != lastActiveState)
        {
            RefreshVisual();
        }
    }

    private void HandleClick()
    {
        if (demolishManager == null)
        {
            return;
        }

        demolishManager.ToggleDemolishMode();
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (icon == null || demolishManager == null)
        {
            return;
        }

        lastActiveState = demolishManager.IsDemolishModeActive;
        icon.color = lastActiveState ? activeColor : inactiveColor;
    }
}
