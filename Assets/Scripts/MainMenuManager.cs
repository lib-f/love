using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance;

    [Header("UI引用")]
    public Button StartButton;
    public Button SavedGamesButton;
    public Button ExitButton;

    [Header("模式选择UI")]
    public GameObject ModePanel;
    public GameObject ModeToggleGroup;
    public Toggle TwoPlayerToggle;
    public Toggle AIToggle;
    public GameObject AISettingsGroup;
    public Toggle AIColorRed;
    public Toggle AIColorBlack;
    public Dropdown DifficultyDropdown;
    public Button AIConfirmButton;
    public Sprite AIColorSelectedSprite;
    public Sprite AIColorUnselectedSprite;

    [Header("场景名称")]
    public string GameSceneName = "Game";

    // 静态配置，供GameManager读取
    public static bool IsAIMode = false;
    public static bool AIIsRed = true;
    public static int AIDepth = 3;

    Image redColorButtonImage;
    Image blackColorButtonImage;
    readonly float[] difficultyOptionYPositions = { 267f, 206f, 147f };

    void Awake()
    {
        Instance = this;
        SetModePanelVisible(false);
    }

    void Start()
    {
        ResolveHomeButtons();
        ResolveModeObjects();
        ResolveAIColorObjects();
        EnsureAIColorSprites();
        ResolveAIConfirmButton();
        EnsureAISettingPanelBorder();
        BindButton(StartButton, OnStartButtonClicked);
        BindButton(SavedGamesButton, OnShowSavedGames);
        BindButton(ExitButton, OnExitGame);
        BindButton(AIConfirmButton, OnConfirmAISettings);

        if (TwoPlayerToggle != null)
            TwoPlayerToggle.SetIsOnWithoutNotify(false);
        if (AIToggle != null)
            AIToggle.SetIsOnWithoutNotify(false);
        if (ModeToggleGroup != null)
            ModeToggleGroup.SetActive(true);
        if (AISettingsGroup != null)
            AISettingsGroup.SetActive(false);
        SetAIColorSelection(true);
        if (DifficultyDropdown != null)
        {
            DifficultyDropdown.SetValueWithoutNotify(1);
            ConfigureDifficultyDropdownStyle();
        }

        if (TwoPlayerToggle != null)
            TwoPlayerToggle.onValueChanged.AddListener(OnTwoPlayerToggleChanged);
        if (AIToggle != null)
            AIToggle.onValueChanged.AddListener(OnAIToggleChanged);
        if (AIColorRed != null)
            AIColorRed.onValueChanged.AddListener(OnRedColorChanged);
        if (AIColorBlack != null)
            AIColorBlack.onValueChanged.AddListener(OnBlackColorChanged);
        BindAIColorButton(AIColorRed, true);
        BindAIColorButton(AIColorBlack, false);
        if (DifficultyDropdown != null)
            DifficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        OnModeChanged(false);
        SetModePanelVisible(false);
    }

    void LateUpdate()
    {
        ArrangeDifficultyDropdownItems();
    }

    public void OnTwoPlayerToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        IsAIMode = false;
        if (AISettingsGroup != null)
            AISettingsGroup.SetActive(false);

        OpenSavedGamesOnStart = false;
        SceneManager.LoadScene(GameSceneName);
    }

    public void OnAIToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        IsAIMode = true;
        if (ModeToggleGroup != null)
            ModeToggleGroup.SetActive(false);
        if (AISettingsGroup != null)
            AISettingsGroup.SetActive(true);
    }

    void OnModeChanged(bool _)
    {
        bool isAI = AIToggle != null && AIToggle.isOn;
        if (AISettingsGroup != null)
            AISettingsGroup.SetActive(isAI);
        IsAIMode = isAI;
    }

    void OnRedColorChanged(bool isOn)
    {
        if (isOn)
            SetAIColorSelection(true);
    }

    void OnBlackColorChanged(bool isOn)
    {
        if (isOn)
            SetAIColorSelection(false);
    }

    void OnDifficultyChanged(int index)
    {
        int[] depths = { 2, 3, 4 };
        if (index < 0 || index >= depths.Length)
            index = 1;
        AIDepth = depths[index];
    }

    void ConfigureDifficultyDropdownStyle()
    {
        if (DifficultyDropdown == null)
            return;

        Color backgroundColor = new Color(0.08235294f, 0.14901961f, 0.23529412f, 0.34901962f);
        Color textColor = new Color(0.8901961f, 0.7490196f, 0.47058824f, 1f);

        RectTransform dropdownRect = DifficultyDropdown.GetComponent<RectTransform>();
        if (dropdownRect != null)
            dropdownRect.sizeDelta = new Vector2(420f, dropdownRect.sizeDelta.y);

        Image dropdownBackground = DifficultyDropdown.targetGraphic as Image;
        if (dropdownBackground != null)
            dropdownBackground.color = backgroundColor;

        if (DifficultyDropdown.captionText != null)
            DifficultyDropdown.captionText.color = textColor;

        if (DifficultyDropdown.template != null)
        {
            float itemHeight = 60f;
            float listHeight = Mathf.Max(itemHeight, DifficultyDropdown.options.Count * itemHeight);
            DifficultyDropdown.template.sizeDelta = new Vector2(DifficultyDropdown.template.sizeDelta.x, listHeight);

            Image templateBackground = DifficultyDropdown.template.GetComponent<Image>();
            if (templateBackground != null)
                templateBackground.color = backgroundColor;

            ScrollRect scrollRect = DifficultyDropdown.template.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalScrollbar = null;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }

            Scrollbar scrollbar = DifficultyDropdown.template.GetComponentInChildren<Scrollbar>(true);
            if (scrollbar != null)
                scrollbar.gameObject.SetActive(false);

            Toggle itemToggle = DifficultyDropdown.template.GetComponentInChildren<Toggle>(true);
            if (itemToggle != null)
            {
                RectTransform itemRect = itemToggle.GetComponent<RectTransform>();
                if (itemRect != null)
                    itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, itemHeight);

                ColorBlock colors = itemToggle.colors;
                colors.normalColor = backgroundColor;
                colors.highlightedColor = backgroundColor;
                colors.pressedColor = backgroundColor;
                colors.selectedColor = backgroundColor;
                itemToggle.colors = colors;

                Image itemBackground = itemToggle.targetGraphic as Image;
                if (itemBackground != null)
                    itemBackground.color = backgroundColor;
            }

            Transform content = DifficultyDropdown.template.Find("Viewport/Content");
            if (content is RectTransform contentRect)
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, listHeight);

            if (itemToggle != null)
            {
                RectTransform itemRect = itemToggle.GetComponent<RectTransform>();
                if (itemRect != null)
                    itemRect.anchoredPosition = new Vector2(itemRect.anchoredPosition.x, difficultyOptionYPositions[0]);
            }
        }

        if (DifficultyDropdown.itemText != null)
        {
            DifficultyDropdown.itemText.color = textColor;
            DifficultyDropdown.itemText.fontSize = 48;
        }
    }

    void ArrangeDifficultyDropdownItems()
    {
        if (DifficultyDropdown == null)
            return;

        Transform dropdownList = DifficultyDropdown.transform.Find("Dropdown List");
        if (dropdownList == null)
            return;

        Transform content = dropdownList.Find("Viewport/Content");
        if (content == null)
            return;

        int optionIndex = 0;
        for (int i = 0; i < content.childCount && optionIndex < difficultyOptionYPositions.Length; i++)
        {
            RectTransform itemRect = content.GetChild(i) as RectTransform;
            if (itemRect == null || !itemRect.gameObject.activeInHierarchy)
                continue;

            itemRect.anchoredPosition = new Vector2(itemRect.anchoredPosition.x, difficultyOptionYPositions[optionIndex]);
            optionIndex++;
        }
    }

    public void OnStartButtonClicked()
    {
        if (ModePanel != null && ModePanel.activeSelf && TwoPlayerToggle != null && TwoPlayerToggle.isOn)
        {
            OnStartGame();
            return;
        }

        ShowModePanel();
    }

    public void ShowModePanel()
    {
        if (TwoPlayerToggle != null)
            TwoPlayerToggle.SetIsOnWithoutNotify(false);
        if (AIToggle != null)
            AIToggle.SetIsOnWithoutNotify(false);
        if (ModeToggleGroup != null)
            ModeToggleGroup.SetActive(true);
        if (AISettingsGroup != null)
            AISettingsGroup.SetActive(false);
        SetAIColorSelection(true);
        SetModePanelVisible(true);
    }

    public void OnStartGame()
    {
        if (ModePanel == null || !ModePanel.activeSelf)
        {
            ShowModePanel();
            return;
        }

        if (AIToggle != null && AIToggle.isOn)
        {
            OnAIToggleChanged(true);
            return;
        }

        if (TwoPlayerToggle != null && !TwoPlayerToggle.isOn)
        {
            ShowModePanel();
            return;
        }

        IsAIMode = AIToggle != null && AIToggle.isOn;
        AIIsRed = AIColorRed == null || AIColorRed.isOn;
        int difficultyIndex = DifficultyDropdown != null ? DifficultyDropdown.value : 1;
        OnDifficultyChanged(difficultyIndex);

        OpenSavedGamesOnStart = false;
        SceneManager.LoadScene(GameSceneName);
    }

    public void OnConfirmAISettings()
    {
        IsAIMode = true;
        AIIsRed = AIColorRed == null || AIColorRed.isOn;

        int difficultyIndex = DifficultyDropdown != null ? DifficultyDropdown.value : 1;
        OnDifficultyChanged(difficultyIndex);

        OpenSavedGamesOnStart = false;
        SceneManager.LoadScene(GameSceneName);
    }

    void SetModePanelVisible(bool visible)
    {
        if (ModePanel != null)
            ModePanel.SetActive(visible);
    }

    void ResolveModeObjects()
    {
        if (ModePanel == null)
        {
            Transform modePanel = FindTransformByName("ModePanel");
            if (modePanel != null)
                ModePanel = modePanel.gameObject;
        }

        if (ModeToggleGroup == null)
        {
            Transform modeToggleGroup = FindTransformByName("ModeToggleGroup");
            if (modeToggleGroup != null)
                ModeToggleGroup = modeToggleGroup.gameObject;
        }

        if (AISettingsGroup == null)
        {
            Transform aiSettingPanel = FindTransformByName("AISettingPanel");
            if (aiSettingPanel != null)
                AISettingsGroup = aiSettingPanel.gameObject;
        }
    }

    void ResolveAIColorObjects()
    {
        if (AIColorRed == null)
        {
            Transform redColor = FindTransformByName("RedColor");
            if (redColor != null)
                AIColorRed = redColor.GetComponent<Toggle>();
        }

        if (AIColorBlack == null)
        {
            Transform blackColor = FindTransformByName("BlackColor");
            if (blackColor != null)
                AIColorBlack = blackColor.GetComponent<Toggle>();
        }

        redColorButtonImage = FindColorButtonImage(AIColorRed);
        blackColorButtonImage = FindColorButtonImage(AIColorBlack);
        DisallowEmptyAIColorSelection();
    }

    void EnsureAIColorSprites()
    {
#if UNITY_EDITOR
        if (AIColorSelectedSprite == null)
            AIColorSelectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/image_card(5).png");
        if (AIColorUnselectedSprite == null)
            AIColorUnselectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/image_card(6).png");
#endif
    }

    void DisallowEmptyAIColorSelection()
    {
        ToggleGroup colorToggleGroup = null;
        if (AIColorRed != null)
            colorToggleGroup = AIColorRed.group;
        if (colorToggleGroup == null && AIColorBlack != null)
            colorToggleGroup = AIColorBlack.group;
        if (colorToggleGroup != null)
            colorToggleGroup.allowSwitchOff = false;
    }

    void ResolveAIConfirmButton()
    {
        if (AIConfirmButton != null)
            return;

        Transform confirm = FindTransformByName("Confirm");
        if (confirm == null)
            return;

        AIConfirmButton = confirm.GetComponent<Button>();
        if (AIConfirmButton == null)
        {
            AIConfirmButton = confirm.gameObject.AddComponent<Button>();
            AIConfirmButton.targetGraphic = confirm.GetComponent<Graphic>();
        }
    }

    Image FindColorButtonImage(Toggle colorToggle)
    {
        if (colorToggle == null)
            return null;

        Transform button = colorToggle.transform.Find("Button");
        if (button != null)
        {
            button.gameObject.SetActive(true);
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                colorToggle.targetGraphic = buttonImage;
                return buttonImage;
            }
        }

        Transform background = colorToggle.transform.Find("Background");
        return background != null ? background.GetComponent<Image>() : null;
    }

    void BindAIColorButton(Toggle colorToggle, bool redSelected)
    {
        if (colorToggle == null)
            return;

        Transform button = colorToggle.transform.Find("Button");
        if (button == null)
            return;

        Button clickButton = button.GetComponent<Button>();
        if (clickButton == null)
            clickButton = button.gameObject.AddComponent<Button>();

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            clickButton.targetGraphic = buttonImage;
            ColorBlock colors = clickButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            clickButton.colors = colors;
        }

        clickButton.onClick.RemoveAllListeners();
        clickButton.onClick.AddListener(() => SetAIColorSelection(redSelected));
    }

    void SetAIColorSelection(bool redSelected)
    {
        AIIsRed = redSelected;

        if (AIColorRed != null)
            AIColorRed.SetIsOnWithoutNotify(redSelected);
        if (AIColorBlack != null)
            AIColorBlack.SetIsOnWithoutNotify(!redSelected);

        // RedColor selected: Red button uses image_card(5), Black button uses image_card(6).
        // BlackColor selected: swap them, and update the AI side at the same time.
        SetColorButtonState(redColorButtonImage, redSelected);
        SetColorButtonState(blackColorButtonImage, !redSelected);
    }

    void SetColorButtonState(Image buttonImage, bool selected)
    {
        if (buttonImage == null)
            return;

        Sprite sprite = selected ? AIColorSelectedSprite : AIColorUnselectedSprite;
        buttonImage.sprite = sprite;
        buttonImage.color = sprite != null ? Color.white : Color.clear;
    }

    Transform FindTransformByName(string objectName)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform item in transforms)
        {
            if (item.name == objectName)
                return item;
        }

        return null;
    }

    void EnsureAISettingPanelBorder()
    {
        if (AISettingsGroup == null || AISettingsGroup.transform.Find("GoldBorder") != null)
            return;

        GameObject border = new GameObject("GoldBorder", typeof(RectTransform));
        border.transform.SetParent(AISettingsGroup.transform, false);
        border.transform.SetAsLastSibling();

        RectTransform rectTransform = border.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Color gold = new Color(1f, 0.78f, 0.22f, 1f);
        float thickness = 8f;
        CreateBorderLine(border.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, gold);
        CreateBorderLine(border.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), gold);
        CreateBorderLine(border.transform, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), gold);
        CreateBorderLine(border.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero, gold);
    }

    void CreateBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.transform.SetParent(parent, false);

        RectTransform rectTransform = line.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    void OnShowSavedGames()
    {
        OpenSavedGamesOnStart = true;
        SceneManager.LoadScene(GameSceneName);
    }

    void OnExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ResolveHomeButtons()
    {
        StartButton = ResolveButton(StartButton, "\u5F00\u59CB", "StartButton");
        SavedGamesButton = ResolveButton(SavedGamesButton, "\u6536\u85CF", "SavedGamesButton", "StarButton", "FavoriteButton", "CollectButton");
        ExitButton = ResolveButton(ExitButton, "\u9000\u51FA", "ExitButton", "QuitButton");
    }

    Button ResolveButton(Button assignedButton, string labelText, params string[] objectNames)
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        Button namedButton = null;
        Button labeledButton = null;

        foreach (Button button in buttons)
        {
            foreach (string objectName in objectNames)
            {
                if (button.name == objectName)
                {
                    namedButton = PickBetterCandidate(namedButton, button);
                    break;
                }
            }
        }

        foreach (Button button in buttons)
        {
            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null && tmpText.text.Trim() == labelText)
            {
                labeledButton = PickBetterCandidate(labeledButton, button);
                continue;
            }

            Text uiText = button.GetComponentInChildren<Text>(true);
            if (uiText != null && uiText.text.Trim() == labelText)
            {
                labeledButton = PickBetterCandidate(labeledButton, button);
            }
        }

        if (labeledButton != null)
            return labeledButton;

        if (namedButton != null)
            return namedButton;

        if (assignedButton != null)
            return assignedButton;

        Debug.LogWarning($"MainMenuManager could not find button: {labelText}");
        return null;
    }

    Button PickBetterCandidate(Button current, Button candidate)
    {
        if (current == null)
            return candidate;

        if (candidate.gameObject.activeInHierarchy && !current.gameObject.activeInHierarchy)
            return candidate;

        return current;
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    public static bool OpenSavedGamesOnStart = false;
}
