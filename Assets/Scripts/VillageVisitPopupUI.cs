using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-1500)]
public class VillageVisitPopupUI : MonoBehaviour
{
    private static VillageVisitPopupUI instance;

    private GameObject popupRoot;
    private InputField keyInput;
    private Text myKeyText;
    private Text statusText;
    private bool suppressInputChange;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void TogglePopupRequest()
    {
        EnsureInstance();
        if (instance != null)
        {
            instance.TogglePopup();
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        VillageVisitPopupUI existing = FindObjectOfType<VillageVisitPopupUI>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject go = new GameObject("VillageVisitPopupUI");
        instance = go.AddComponent<VillageVisitPopupUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (popupRoot == null)
        {
            BuildPopup();
            SetPopupVisible(false);
            RefreshMyVillageKeyLabel();
        }

        NakamaSaveSyncManager.VillageKeyChanged += HandleVillageKeyChanged;
    }

    private void OnDestroy()
    {
        NakamaSaveSyncManager.VillageKeyChanged -= HandleVillageKeyChanged;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (popupRoot != null && popupRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            SetPopupVisible(false);
        }
    }

    private void HandleVillageKeyChanged(string key)
    {
        RefreshMyVillageKeyLabel();
    }

    private void BuildPopup()
    {
        if (popupRoot != null)
        {
            return;
        }

        EnsureEventSystem();

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject canvasObject = new GameObject("VillagePopupCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        popupRoot = new GameObject("VillagePopupRoot");
        popupRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform popupRootRect = popupRoot.AddComponent<RectTransform>();
        popupRootRect.anchorMin = Vector2.zero;
        popupRootRect.anchorMax = Vector2.one;
        popupRootRect.offsetMin = Vector2.zero;
        popupRootRect.offsetMax = Vector2.zero;

        Image backdrop = popupRoot.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject panel = CreatePanel("Panel", popupRoot.transform, new Vector2(540f, 320f), new Color(0.12f, 0.13f, 0.16f, 0.96f));

        Text title = CreateText("Title", panel.transform, font, "Load Village", 30, TextAnchor.MiddleCenter, Color.white);
        SetRect((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(460f, 40f));

        myKeyText = CreateText("MyKey", panel.transform, font, "Your Village Key: ----", 20, TextAnchor.MiddleCenter, new Color(0.8f, 0.95f, 1f, 1f));
        SetRect((RectTransform)myKeyText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(460f, 32f));

        Text promptText = CreateText("Prompt", panel.transform, font, "Enter 6-char key (A-Z, 0-9)", 19, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f, 1f));
        SetRect((RectTransform)promptText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(460f, 30f));

        keyInput = CreateInputField(panel.transform, font);
        SetRect((RectTransform)keyInput.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(300f, 44f));
        keyInput.characterLimit = 6;
        keyInput.onValueChanged.AddListener(HandleInputChanged);

        statusText = CreateText("Status", panel.transform, font, "", 18, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.35f, 1f));
        SetRect((RectTransform)statusText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(470f, 30f));

        Button submitButton = CreateButton(panel.transform, font, "Submit", new Color(0.2f, 0.55f, 0.25f, 1f));
        SetRect((RectTransform)submitButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(180f, 52f));
        submitButton.onClick.AddListener(SubmitVillageKey);

        Button closeButton = CreateButton(panel.transform, font, "Close", new Color(0.35f, 0.35f, 0.38f, 1f));
        SetRect((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -36f), new Vector2(66f, 40f));
        closeButton.onClick.AddListener(() => SetPopupVisible(false));
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private void TogglePopup()
    {
        if (popupRoot == null)
        {
            return;
        }

        bool shouldShow = !popupRoot.activeSelf;
        SetPopupVisible(shouldShow);

        if (shouldShow)
        {
            RefreshMyVillageKeyLabel();
            statusText.text = string.Empty;
            keyInput.text = string.Empty;
            keyInput.ActivateInputField();
        }
    }

    private void SetPopupVisible(bool visible)
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(visible);
        }
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
            return;
        }

        myKeyText.text = "Your Village Key: GENERATING...";
    }

    private void HandleInputChanged(string value)
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

    private void SubmitVillageKey()
    {
        string normalized = SaveLoadManager.NormalizeVillageKey(keyInput.text);
        keyInput.text = normalized;

        if (!SaveLoadManager.IsValidVillageKey(normalized))
        {
            statusText.text = "Invalid key. Use 6 chars: A-Z, 0-9.";
            return;
        }

        bool started = SaveLoadManager.RequestLoadVillageByKey(normalized);
        if (!started)
        {
            statusText.text = "Could not start load. Check console.";
            return;
        }

        statusText.text = "Loading village...";
        SetPopupVisible(false);
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = obj.AddComponent<Image>();
        image.color = color;
        return obj;
    }

    private static Text CreateText(string name, Transform parent, Font font, string content, int size, TextAnchor alignment, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static InputField CreateInputField(Transform parent, Font font)
    {
        GameObject root = new GameObject("VillageKeyInput");
        root.transform.SetParent(parent, false);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.93f, 0.93f, 0.93f, 1f);

        InputField inputField = root.AddComponent<InputField>();
        inputField.contentType = InputField.ContentType.Alphanumeric;
        inputField.lineType = InputField.LineType.SingleLine;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(root.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        RectTransform textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(root.transform, false);
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.font = font;
        placeholder.fontSize = 22;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.45f, 0.45f, 0.45f, 0.9f);
        placeholder.text = "ABC123";
        RectTransform placeholderRect = (RectTransform)placeholder.transform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(12f, 4f);
        placeholderRect.offsetMax = new Vector2(-12f, -4f);

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        return inputField;
    }

    private static Button CreateButton(Transform parent, Font font, string label, Color color)
    {
        GameObject buttonObj = new GameObject(label + "Button");
        buttonObj.transform.SetParent(parent, false);

        Image image = buttonObj.AddComponent<Image>();
        image.color = color;

        Button button = buttonObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.1f;
        colors.pressedColor = color * 0.9f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        button.colors = colors;

        Text labelText = CreateText("Label", buttonObj.transform, font, label, 20, TextAnchor.MiddleCenter, Color.white);
        SetRect((RectTransform)labelText.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }
}
