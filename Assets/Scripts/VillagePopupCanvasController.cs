using UnityEngine;
using UnityEngine.UI;

public class VillagePopupCanvasController : MonoBehaviour
{
    [Header("References")]
    public GameObject panelRoot;
    public InputField keyInput;
    public Text myKeyText;
    public Text statusText;
    public Button submitButton;
    public Button closeButton;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.M;

    private bool suppressInputChange;

    private void Awake()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => SetPanelVisible(false));
        }

        if (keyInput != null)
        {
            keyInput.characterLimit = 6;
            keyInput.contentType = InputField.ContentType.Alphanumeric;
            keyInput.onValueChanged.AddListener(OnInputChanged);
        }
    }

    private void OnEnable()
    {
        NakamaSaveSyncManager.VillageKeyChanged += OnVillageKeyChanged;
        RefreshMyVillageKeyLabel();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        NakamaSaveSyncManager.VillageKeyChanged -= OnVillageKeyChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }

        if (panelRoot != null && panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            SetPanelVisible(false);
        }
    }

    private void OnVillageKeyChanged(string key)
    {
        RefreshMyVillageKeyLabel();
    }

    private void TogglePanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        bool nextVisible = !panelRoot.activeSelf;
        SetPanelVisible(nextVisible);
        if (nextVisible)
        {
            RefreshMyVillageKeyLabel();
            SetStatus(string.Empty, Color.white);
            if (keyInput != null)
            {
                keyInput.text = string.Empty;
                keyInput.ActivateInputField();
            }
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void OnInputChanged(string value)
    {
        if (suppressInputChange)
        {
            return;
        }

        string normalized = SaveLoadManager.NormalizeVillageKey(value);
        if (normalized == value)
        {
            return;
        }

        suppressInputChange = true;
        keyInput.text = normalized;
        suppressInputChange = false;
    }

    private void OnSubmit()
    {
        if (keyInput == null)
        {
            SetStatus("Input is missing.", Color.red);
            return;
        }

        string normalized = SaveLoadManager.NormalizeVillageKey(keyInput.text);
        keyInput.text = normalized;
        if (!SaveLoadManager.IsValidVillageKey(normalized))
        {
            SetStatus("Invalid key. Use 6 chars: A-Z and 0-9.", new Color(1f, 0.7f, 0.2f, 1f));
            return;
        }

        bool started = SaveLoadManager.RequestLoadVillageByKey(normalized);
        if (!started)
        {
            SetStatus("Load failed to start. Check console.", Color.red);
            return;
        }

        SetStatus("Loading village...", new Color(0.4f, 0.95f, 0.4f, 1f));
        SetPanelVisible(false);
    }

    private void RefreshMyVillageKeyLabel()
    {
        if (myKeyText == null)
        {
            return;
        }

        string key = NakamaSaveSyncManager.CurrentVillageKey;
        if (SaveLoadManager.IsValidVillageKey(key))
        {
            myKeyText.text = "Your Village Key: " + key;
        }
        else
        {
            myKeyText.text = "Your Village Key: GENERATING...";
        }
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = color;
    }
}
