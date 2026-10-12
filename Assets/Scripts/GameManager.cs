using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static bool BoardFlipped { get; private set; }

    public ChessPiece selectedPiece = null;
    private List<ChessPiece> allPieces = new List<ChessPiece>();
    public ChessPiece.Side CurrentTurn = ChessPiece.Side.Red;

    // 棋谱引用
    public ChessRecord ChessRecord;

    // UI引用
    public TextMeshProUGUI TurnText;
    public GameObject GameOverPanel;
    public TextMeshProUGUI ResultText;
    public Button UndoButton;
    public Button ExitButton;
    public TextMeshProUGUI JiangJunText;

    [Header("返回菜单")]
    public Button ReturnMenuButton;     // 在GameOverPanel上添加
    public string MainMenuSceneName = "MainMenu";

    // ========== 收藏功能 ==========
    [Header("收藏功能")]
    public Button SaveGameButton;           // 收藏本局按钮（在GameOverPanel上）
    public GameObject SaveNameInputPanel;   // 输入棋局名称的弹窗
    public TMP_InputField SaveNameInput;    // 名称输入框
    public Button SaveConfirmButton;        // 确认保存按钮
    public Button SaveCancelButton;         // 取消按钮

    private bool isStalemateGame = false;   // 标记是否为困毙和棋
    private ChessPiece.Side gameWinner;     // 记录获胜方
                                            // ==============================

    // ========== 回放模式 ==========
    [HideInInspector]
    public bool IsReplayMode = false;
    // ==============================

    // ========== AI 对战 ==========
    [Header("AI设置")]
    public bool enableAI = false;          // 是否启用AI
    public ChessPiece.Side aiSide = ChessPiece.Side.Black; // AI执黑
    private bool isAIPlaying = false;     // 防止AI重复调用

    [Header("头像显示")]
    public Image TopAvatarImage;
    public Image BottomAvatarImage;
    public Sprite AIAvatarSprite;
    public Sprite UserAvatarSprite;
    // ===========================

    private bool gameOver = false;
    private Stack<MoveRecord> moveHistory = new Stack<MoveRecord>();

    [Header("棋子预制体（供悔棋恢复使用）")]
    public GameObject redJiangPrefab;
    public GameObject redShiPrefab;
    public GameObject redXiangPrefab;
    public GameObject redMaPrefab;
    public GameObject redJuPrefab;
    public GameObject redPaoPrefab;
    public GameObject redZuPrefab;
    public GameObject blackJiangPrefab;
    public GameObject blackShiPrefab;
    public GameObject blackXiangPrefab;
    public GameObject blackMaPrefab;
    public GameObject blackJuPrefab;
    public GameObject blackPaoPrefab;
    public GameObject blackZuPrefab;

    void Awake()
    {
        Instance = this;
        BoardFlipped = MainMenuManager.IsAIMode && MainMenuManager.AIIsRed;
    }

    void Start()
    {
        ResolveNavigationButtons();
        BindButton(ExitButton, OnReturnToMenu);
        BindButton(ReturnMenuButton, OnReturnToMenu);
        ConfigureGameOverPanelStyle();

        ChessPiece[] pieces = FindObjectsOfType<ChessPiece>();
        allPieces.AddRange(pieces);

        foreach (var piece in allPieces)
        {
            Button btn = piece.GetComponent<Button>();
            if (btn != null)
            {
                ChessPiece p = piece;
                btn.onClick.AddListener(() => OnPieceClicked(p));
            }
        }

        if (UndoButton != null)
        {
            UndoButton.onClick.AddListener(OnUndo);
        }

        UpdateTurnText();
        UpdateUndoButton();
        Debug.Log("游戏开始！红方先行");

        // ========== 绑定收藏按钮 ==========
        if (SaveGameButton != null)
        {
            SaveGameButton.onClick.AddListener(OnSaveGameButtonClicked);
            SaveGameButton.gameObject.SetActive(false); // 默认隐藏
        }

        if (SaveConfirmButton != null)
            SaveConfirmButton.onClick.AddListener(OnConfirmSave);

        if (SaveCancelButton != null)
            SaveCancelButton.onClick.AddListener(OnCancelSave);
        // ===================================

        // ========== 新增：检查是否从主菜单的收藏列表进入 ==========
        if (MainMenuManager.OpenSavedGamesOnStart)
        {
            // 延迟一帧执行，确保所有UI初始化完成
            Invoke(nameof(OpenSavedGamesPanel), 0.1f);
            // 重置标记
            MainMenuManager.OpenSavedGamesOnStart = false;
        }
        // =========================================================
        // 备份初始状态
        ReplayManager.Instance?.BackupInitialState();

        // ========== 人机对战初始化 ==========
        if (MainMenuManager.IsAIMode)
        {
            enableAI = true;
            aiSide = MainMenuManager.AIIsRed ? ChessPiece.Side.Red : ChessPiece.Side.Black;
            BoardFlipped = aiSide == ChessPiece.Side.Red;
            Debug.Log($"人机模式启用，AI方: {aiSide}, 搜索深度: {MainMenuManager.AIDepth}");

            ChessAI ai = EnsureChessAI();
            if (ai != null)
            {
                ai.searchDepth = MainMenuManager.AIDepth;
                ai.mySide = aiSide;   // 关键：设置AI执棋方
                ai.enabled = true;
            }
            else
            {
                Debug.LogError("ChessAI组件初始化失败，AI功能将不可用。");
                enableAI = false;
            }
        }
        else
        {
            enableAI = false;
            BoardFlipped = false;
            Debug.Log("双人模式");
        }

        ApplyAvatarMode();
        RefreshAllPiecePositions(false);

        // 如果AI执红且红方先走，则AI自动走第一步
        if (enableAI && CurrentTurn == aiSide && !gameOver && !IsReplayMode)
        {
            StartCoroutine(DelayedAIMove());
        }
    }

    void ApplyAvatarMode()
    {
        TopAvatarImage = ResolveImage(TopAvatarImage, "AgentAI");
        BottomAvatarImage = ResolveImage(BottomAvatarImage, "UserName");

        Sprite aiSprite = AIAvatarSprite != null ? AIAvatarSprite : (TopAvatarImage != null ? TopAvatarImage.sprite : null);
        Sprite userSprite = UserAvatarSprite != null ? UserAvatarSprite : (BottomAvatarImage != null ? BottomAvatarImage.sprite : null);
        Sprite topSprite = enableAI ? aiSprite : userSprite;
        Sprite bottomSprite = userSprite;

        ApplyAvatarSprite(TopAvatarImage, topSprite);
        ApplyAvatarSprite(BottomAvatarImage, bottomSprite);
    }

    Image ResolveImage(Image assignedImage, string objectName)
    {
        if (assignedImage != null)
            return assignedImage;

        Image[] images = FindObjectsOfType<Image>(true);
        foreach (Image image in images)
        {
            if (image.name == objectName)
                return image;
        }

        return null;
    }

    void ApplyAvatarSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.preserveAspect = true;
    }

    ChessAI EnsureChessAI()
    {
        ChessAI ai = ChessAI.Instance;
        if (ai == null)
            ai = FindObjectOfType<ChessAI>(true);

        if (ai == null)
        {
            GameObject aiObject = new GameObject("ChessAI");
            ai = aiObject.AddComponent<ChessAI>();
        }

        if (!ai.gameObject.activeSelf)
            ai.gameObject.SetActive(true);

        ChessAI.Instance = ai;
        return ai;
    }

    IEnumerator DelayedAIMove()
    {
        yield return new WaitForSeconds(0.5f);
        if (enableAI && CurrentTurn == aiSide && !gameOver && !IsReplayMode && ChessAI.Instance != null)
            ChessAI.Instance.MakeAIMove();
    }

    public static Vector2 GridToAnchoredPosition(int gridX, int gridY)
    {
        int visualX = BoardFlipped ? 8 - gridX : gridX;
        int visualY = BoardFlipped ? 9 - gridY : gridY;
        return new Vector2((visualX - 4) * 100f, (4.5f - visualY) * 100f);
    }

    public static Vector2Int VisualGridToLogicalGrid(int visualX, int visualY)
    {
        int gridX = BoardFlipped ? 8 - visualX : visualX;
        int gridY = BoardFlipped ? 9 - visualY : visualY;
        return new Vector2Int(gridX, gridY);
    }

    void RefreshAllPiecePositions(bool animate)
    {
        foreach (ChessPiece piece in allPieces)
        {
            if (piece != null)
                piece.UpdatePosition(animate);
        }
    }

    /// <summary>
    /// 打开收藏列表面板
    /// </summary>
    void OpenSavedGamesPanel()
    {
        SavedGamesUI.Instance?.ShowSavedGames();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void OnReturnToMenu()
    {
        MainMenuManager.OpenSavedGamesOnStart = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    void ResolveNavigationButtons()
    {
        ExitButton = ResolveButton(ExitButton, "ExitButton", "BackButton", "ReturnButton");
        ReturnMenuButton = ResolveButton(ReturnMenuButton, "ReturnMenuButton", "RetuenMenuButton");
    }

    Button ResolveButton(Button assignedButton, params string[] objectNames)
    {
        if (assignedButton != null)
            return assignedButton;

        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            foreach (string objectName in objectNames)
            {
                if (button.name == objectName)
                    return button;
            }
        }

        return null;
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        if (button.onClick.GetPersistentEventCount() == 0)
            button.onClick.AddListener(action);
    }

    void ConfigureGameOverPanelStyle()
    {
        if (GameOverPanel == null)
            return;

        RectTransform panelRect = GameOverPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(640f, 430f);
        }

        Image panelImage = GameOverPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0.08f, 0.035f, 0.015f, 0.92f);
            panelImage.raycastTarget = true;
        }

        // 不再额外显示标题，避免在棋盘上方挤出一小块“棋局终了”。
        Transform oldTitle = GameOverPanel.transform.Find("Title");
        if (oldTitle != null)
            oldTitle.gameObject.SetActive(false);

        if (ResultText != null)
        {
            RectTransform resultRect = ResultText.GetComponent<RectTransform>();
            if (resultRect != null)
            {
                resultRect.anchorMin = new Vector2(0.5f, 0.5f);
                resultRect.anchorMax = new Vector2(0.5f, 0.5f);
                resultRect.pivot = new Vector2(0.5f, 0.5f);
                resultRect.anchoredPosition = new Vector2(0f, 95f);
                resultRect.sizeDelta = new Vector2(580f, 120f);
            }

            ResultText.fontSize = 52f;
            ResultText.color = new Color(1f, 0.86f, 0.46f, 1f);
            ResultText.alignment = TextAlignmentOptions.Center;
            ResultText.enableWordWrapping = true;
        }

        StyleGameOverButton(FindButtonByName("RestartButton"), new Vector2(-190f, -130f), "再来一局");
        StyleGameOverButton(SaveGameButton, new Vector2(0f, -130f), "收藏本局");
        StyleGameOverButton(ReturnMenuButton, new Vector2(190f, -130f), "返回菜单");
        EnsureGameOverBorder();
    }

    TextMeshProUGUI EnsureGameOverTitle()
    {
        Transform title = GameOverPanel.transform.Find("Title");
        if (title == null)
        {
            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(GameOverPanel.transform, false);
            title = titleObject.transform;
        }

        RectTransform titleRect = title.GetComponent<RectTransform>();
        if (titleRect != null)
        {
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 155f);
            titleRect.sizeDelta = new Vector2(560f, 70f);
        }

        TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
        if (titleText != null && ResultText != null && titleText.font == null)
        {
            titleText.font = ResultText.font;
            titleText.fontSharedMaterial = ResultText.fontSharedMaterial;
        }

        return titleText;
    }

    void StyleGameOverButton(Button button, Vector2 anchoredPosition, string label)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(170f, 58f);
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.42f, 0.18f, 0.07f, 0.95f);
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.55f, 1f);
        colors.pressedColor = new Color(0.78f, 0.46f, 0.22f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = 25f;
            text.color = new Color(1f, 0.88f, 0.58f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
        }
    }

    Button FindButtonByName(string objectName)
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == objectName)
                return button;
        }

        return null;
    }

    void EnsureGameOverBorder()
    {
        if (GameOverPanel.transform.Find("GoldBorder") != null)
            return;

        GameObject border = new GameObject("GoldBorder", typeof(RectTransform));
        border.transform.SetParent(GameOverPanel.transform, false);
        border.transform.SetAsLastSibling();

        RectTransform rectTransform = border.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Color gold = new Color(0.95f, 0.68f, 0.22f, 1f);
        float thickness = 6f;
        CreateGameOverBorderLine(border.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, gold);
        CreateGameOverBorderLine(border.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), gold);
        CreateGameOverBorderLine(border.transform, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), gold);
        CreateGameOverBorderLine(border.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero, gold);
    }

    void CreateGameOverBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
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

    public void OnPieceClicked(ChessPiece clickedPiece)
    {
        if (gameOver || IsReplayMode) return;  // 回放模式下禁止交互

        if (clickedPiece.PlayerSide == CurrentTurn)
        {
            if (selectedPiece != null)
                selectedPiece.SetSelected(false);

            selectedPiece = clickedPiece;
            selectedPiece.SetSelected(true);
        }
        else if (selectedPiece != null)
        {
            TryMoveOrEat(selectedPiece, clickedPiece.GridX, clickedPiece.GridY);
        }
    }

    public void TryMoveOrEat(ChessPiece piece, int targetX, int targetY)
    {
        if (gameOver || IsReplayMode) return;  // 回放模式下禁止交互


        ChessPiece targetPiece = GetPieceAt(targetX, targetY);
        if (targetPiece != null && targetPiece.PlayerSide == piece.PlayerSide)
        {
            Debug.Log("不能吃自己的棋子！");
            return;
        }

        if (!IsValidMove(piece, targetX, targetY))
        {
            Debug.Log("不符合走棋规则！");
            return;
        }

        // 模拟移动，检查是否送将
        int oldX = piece.GridX;
        int oldY = piece.GridY;

        piece.GridX = targetX;
        piece.GridY = targetY;

        ChessPiece tempEaten = null;
        if (targetPiece != null)
        {
            tempEaten = targetPiece;
            allPieces.Remove(tempEaten);
        }

        bool isSelfInCheck = IsInCheck(piece.PlayerSide);

        piece.GridX = oldX;
        piece.GridY = oldY;
        if (tempEaten != null)
        {
            allPieces.Add(tempEaten);
        }

        if (isSelfInCheck)
        {
            Debug.Log("不能送将！");
            // ========== 找到被将军的将/帅，传它的位置 ==========
            Vector2 kingPos = Vector2.zero;
            ChessPiece king = GetKingPiece(CurrentTurn);
            if (king != null)
            {
                kingPos = GridToAnchoredPosition(king.GridX, king.GridY);
            }
            // ================================================
            ShowJiangJunHint("不能送将！", piece.PlayerSide, kingPos);
            return;
        }

        // 正式记录这一步
        MoveRecord record = new MoveRecord();
        record.PieceType = piece.Type;
        record.PieceSide = piece.PlayerSide;
        record.FromX = oldX;
        record.FromY = oldY;
        record.ToX = targetX;
        record.ToY = targetY;
        record.TurnBeforeMove = CurrentTurn;

        if (targetPiece != null)
        {
            record.HasEatenPiece = true;
            record.EatenType = targetPiece.Type;
            record.EatenSide = targetPiece.PlayerSide;
            record.EatenX = targetPiece.GridX;
            record.EatenY = targetPiece.GridY;
        }
        else
        {
            record.HasEatenPiece = false;
        }

        moveHistory.Push(record);

        // ========== 播放走子拖尾 ==========
        // 起点：原位置
        Vector2 startPos = GridToAnchoredPosition(oldX, oldY);
        // 终点：新位置
        Vector2 endPos = GridToAnchoredPosition(targetX, targetY);
        EffectManager.Instance?.PlayMoveTrail(startPos, endPos);
        // =================================
        // 正式执行移动
        if (targetPiece != null)
        {
            if (targetPiece.Type == ChessPiece.PieceType.Jiang)
            {
                GameOver(piece.PlayerSide);
            }
            // 播放吃子效果（在被吃的棋子位置）
            RectTransform targetRT = targetPiece.GetComponent<RectTransform>();
            EffectManager.Instance?.PlayEatEffect(targetRT.anchoredPosition);
            // 执行吃子
            allPieces.Remove(targetPiece);
            Destroy(targetPiece.gameObject);

            // 播放吃子音效
            AudioManager.Instance?.PlayEatSound();
        }
        else
        {

            AudioManager.Instance?.PlayMoveSound();
        }

        // ========== 生成记谱 ==========
        string notation = NotationConverter.ToNotation(piece, oldX, oldY, targetX, targetY, targetPiece != null);
        Debug.Log($"生成记谱: {notation}, 侧: {piece.PlayerSide}");
        ChessRecord?.AddMove(piece.PlayerSide, notation);
        // =============================

        piece.GridX = targetX;
        piece.GridY = targetY;
        piece.UpdatePosition(true);
        piece.SetSelected(false);
        selectedPiece = null;

        if (!gameOver)
        {
            SwitchTurn();

            // ========== 检查当前回合方状态 ==========
            if (IsInCheck(CurrentTurn))
            {
                Debug.Log($"{CurrentTurn}方被将军！");

                Vector2 kingPos = Vector2.zero;
                ChessPiece king = GetKingPiece(CurrentTurn);
                if (king != null)
                {
                    kingPos = GridToAnchoredPosition(king.GridX, king.GridY);
                }

                ShowJiangJunHint("将军！", CurrentTurn, kingPos);

                // 检查是否将死
                if (IsCheckmate(CurrentTurn))
                {
                    ChessPiece.Side winner = (CurrentTurn == ChessPiece.Side.Red)
                        ? ChessPiece.Side.Black
                        : ChessPiece.Side.Red;
                    GameOver(winner, true, false);
                }
            }
            // ========== 新增：困毙检测 ==========
            else if (IsStalemate())
            {
                Debug.Log("困毙！和棋！");
                ShowStalemateHint();
                GameOver(ChessPiece.Side.Red, false, true); // 和棋，无胜负方
            }
            // ===================================
        }

        UpdateUndoButton();
    }

    // 将军提示（增加kingPos参数）
    void ShowJiangJunHint(string message, ChessPiece.Side side, Vector2 kingAnchoredPos)
    {
        // UI文字显示
        if (JiangJunText != null)
        {
            JiangJunText.text = message;
            JiangJunText.gameObject.SetActive(true);

            if (side == ChessPiece.Side.Red)
                JiangJunText.color = new Color(0.77f, 0.12f, 0.23f);
            else
                JiangJunText.color = new Color(0.1f, 0.1f, 0.1f);

            CancelInvoke(nameof(HideJiangJunHint));
            Invoke(nameof(HideJiangJunHint), 2f);
        }

        // ========== 播放将军闪光效果（在将/帅位置）==========
        EffectManager.Instance?.PlayJiangJunEffect(kingAnchoredPos);

        // UI文字震动
        if (JiangJunText != null)
        {
            EffectManager.Instance?.ShakeJiangJunText(JiangJunText.GetComponent<RectTransform>());
        }
    }

    // ========== 困毙提示 ==========
    void ShowStalemateHint()
    {
        if (JiangJunText != null)
        {
            JiangJunText.text = "困毙！和棋！";
            JiangJunText.color = new Color(0.4f, 0.4f, 0.4f); // 灰色
            JiangJunText.gameObject.SetActive(true);

            CancelInvoke(nameof(HideJiangJunHint));
            Invoke(nameof(HideJiangJunHint), 3f);
        }
    }
    // ==============================
    void HideJiangJunHint()
    {
        if (JiangJunText != null)
        {
            JiangJunText.gameObject.SetActive(false);
        }
    }

    // 判断是否被将军
    public bool IsInCheck(ChessPiece.Side side)
    {
        allPieces.RemoveAll(item => item == null);
        ChessPiece king = GetKingPiece(side);
        if (king == null) return false;

        ChessPiece.Side opponent = (side == ChessPiece.Side.Red) ? ChessPiece.Side.Black : ChessPiece.Side.Red;
        foreach (var p in allPieces)
        {
            if (p != null && p.PlayerSide == opponent)
            {
                if (CanAttack(p, king.GridX, king.GridY))
                {
                    return true;
                }
            }
        }

        // 将/帅对面
        ChessPiece enemyKing = null;
        foreach (var p in allPieces)
        {
            if (p != null && p.Type == ChessPiece.PieceType.Jiang && p.PlayerSide == opponent)
            {
                enemyKing = p;
                break;
            }
        }

        if (enemyKing != null && king.GridX == enemyKing.GridX)
        {
            int minY = Mathf.Min(king.GridY, enemyKing.GridY);
            int maxY = Mathf.Max(king.GridY, enemyKing.GridY);
            for (int y = minY + 1; y < maxY; y++)
            {
                if (GetPieceAt(king.GridX, y) != null)
                    return false;
            }
            return true;
        }

        return false;
    }

    // ========== 困毙检测 ==========

    /// <summary>
    /// 困毙检测：当前回合方不被将军，但没有任何合法走法
    /// </summary>
    public bool IsStalemate()
    {
        // 1. 如果被将军，不是困毙（是将死/绝杀的前兆）
        if (IsInCheck(CurrentTurn))
            return false;

        // 2. 遍历当前方所有棋子，检查是否有任何合法走法
        allPieces.RemoveAll(item => item == null);

        foreach (var piece in allPieces)
        {
            if (piece == null || piece.PlayerSide != CurrentTurn)
                continue;

            // 遍历棋盘所有位置
            for (int tx = 0; tx <= 8; tx++)
            {
                for (int ty = 0; ty <= 9; ty++)
                {
                    if (piece.GridX == tx && piece.GridY == ty)
                        continue;

                    ChessPiece target = GetPieceAt(tx, ty);
                    if (target != null && target.PlayerSide == CurrentTurn)
                        continue;

                    // 走法规则验证
                    if (!IsValidMove(piece, tx, ty))
                        continue;

                    // 模拟走棋，检查是否走入将军（送将）
                    if (IsMoveLegalForStalemate(piece, tx, ty))
                        return false; // 找到一个合法走法，不是困毙
                }
            }
        }

        return true; // 没有任何合法走法 → 困毙
    }

    /// <summary>
    /// 用于困毙检测的走法合法性判断（模拟走棋后不被将军）
    /// </summary>
    private bool IsMoveLegalForStalemate(ChessPiece piece, int targetX, int targetY)
    {
        int oldX = piece.GridX;
        int oldY = piece.GridY;

        piece.GridX = targetX;
        piece.GridY = targetY;

        ChessPiece targetPiece = GetPieceAt(targetX, targetY);
        bool hadEaten = false;
        int eatenIndex = -1;

        if (targetPiece != null && targetPiece.PlayerSide != piece.PlayerSide)
        {
            eatenIndex = allPieces.IndexOf(targetPiece);
            if (eatenIndex >= 0)
            {
                hadEaten = true;
                allPieces[eatenIndex] = null;
            }
        }

        bool isSelfInCheck = IsInCheck(piece.PlayerSide);

        // 恢复
        piece.GridX = oldX;
        piece.GridY = oldY;

        if (hadEaten && eatenIndex >= 0)
        {
            allPieces[eatenIndex] = targetPiece;
        }

        return !isSelfInCheck;
    }

    // ========== 找到对应颜色将子==========
    ChessPiece GetKingPiece(ChessPiece.Side side)
    {
        ChessPiece king = null;
        foreach (var p in allPieces)
        {
            if (p != null && p.Type == ChessPiece.PieceType.Jiang && p.PlayerSide == side)
            {
                king = p;
                break;
            }
        }
        return king;
    }

    // 将死判定
    // 在 GameManager.cs 中替换 IsCheckmate 方法

    bool IsCheckmate(ChessPiece.Side side)
    {
        Debug.Log($"========== 检查 {side} 方是否被将死 ==========");

        // 清理null，但用副本操作
        allPieces.RemoveAll(item => item == null);
        List<ChessPiece> piecesCopy = new List<ChessPiece>(allPieces);

        foreach (var piece in piecesCopy)
        {
            if (piece == null) continue;
            if (piece.PlayerSide != side) continue;

            for (int tx = 0; tx <= 8; tx++)
            {
                for (int ty = 0; ty <= 9; ty++)
                {
                    if (piece.GridX == tx && piece.GridY == ty) continue;

                    ChessPiece target = GetPieceAt(tx, ty);
                    if (target != null && target.PlayerSide == side) continue;

                    if (!IsValidMove(piece, tx, ty)) continue;

                    // ========== 模拟走棋（修复版）==========
                    int oldX = piece.GridX;
                    int oldY = piece.GridY;

                    piece.GridX = tx;
                    piece.GridY = ty;

                    ChessPiece eatenPiece = null;
                    if (target != null && target.PlayerSide != side)
                    {
                        eatenPiece = target;
                        allPieces.Remove(target);  // 直接从列表移除，不用索引
                    }

                    bool stillInCheck = IsInCheck(side);

                    // 恢复状态
                    piece.GridX = oldX;
                    piece.GridY = oldY;

                    if (eatenPiece != null)
                    {
                        allPieces.Add(eatenPiece);  // 直接添加回来
                    }
                    // =======================================

                    if (!stillInCheck)
                    {
                        Debug.Log($"找到解围走法：{piece.Type} 从 ({oldX},{oldY}) 到 ({tx},{ty})");
                        return false;  // 有解将走法，不是将死
                    }
                }
            }
        }

        Debug.Log($"========== {side} 方被将死 ==========");
        return true;
    }

    // 判断棋子能否攻击到目标位置
    bool CanAttack(ChessPiece piece, int targetX, int targetY)
    {
        if (piece == null) return false;

        int fx = piece.GridX, fy = piece.GridY;
        int dx = targetX - fx, dy = targetY - fy;
        int adx = Mathf.Abs(dx), ady = Mathf.Abs(dy);

        if (fx == targetX && fy == targetY) return false;

        switch (piece.Type)
        {
            case ChessPiece.PieceType.Jiang:
                if (!((adx == 1 && ady == 0) || (adx == 0 && ady == 1))) return false;
                if (targetX < 3 || targetX > 5) return false;
                if (piece.PlayerSide == ChessPiece.Side.Red && targetY < 7) return false;
                if (piece.PlayerSide == ChessPiece.Side.Black && targetY > 2) return false;
                return true;

            case ChessPiece.PieceType.Shi:
                return adx == 1 && ady == 1 && targetX >= 3 && targetX <= 5 &&
                       ((piece.PlayerSide == ChessPiece.Side.Red && targetY >= 7) ||
                        (piece.PlayerSide == ChessPiece.Side.Black && targetY <= 2));

            case ChessPiece.PieceType.Xiang:
                if (adx != 2 || ady != 2) return false;
                if (piece.PlayerSide == ChessPiece.Side.Red && targetY < 5) return false;
                if (piece.PlayerSide == ChessPiece.Side.Black && targetY > 4) return false;
                int eyeX = (fx + targetX) / 2;
                int eyeY = (fy + targetY) / 2;
                return GetPieceAt(eyeX, eyeY) == null;

            case ChessPiece.PieceType.Ma:
                if (!((adx == 1 && ady == 2) || (adx == 2 && ady == 1))) return false;
                int legX, legY;
                if (adx == 2)
                {
                    legX = fx + (targetX > fx ? 1 : -1);
                    legY = fy;
                }
                else
                {
                    legX = fx;
                    legY = fy + (targetY > fy ? 1 : -1);
                }
                return GetPieceAt(legX, legY) == null;

            case ChessPiece.PieceType.Ju:
                if (fx != targetX && fy != targetY) return false;
                if (fx == targetX)
                {
                    int step = (targetY > fy) ? 1 : -1;
                    for (int y = fy + step; y != targetY; y += step)
                        if (GetPieceAt(fx, y) != null) return false;
                }
                else
                {
                    int step = (targetX > fx) ? 1 : -1;
                    for (int x = fx + step; x != targetX; x += step)
                        if (GetPieceAt(x, fy) != null) return false;
                }
                return true;

            case ChessPiece.PieceType.Pao:
                if (fx != targetX && fy != targetY) return false;

                int obstacleCount = 0;
                if (fx == targetX)
                {
                    int step = (targetY > fy) ? 1 : -1;
                    for (int y = fy + step; y != targetY; y += step)
                        if (GetPieceAt(fx, y) != null) obstacleCount++;
                }
                else
                {
                    int step = (targetX > fx) ? 1 : -1;
                    for (int x = fx + step; x != targetX; x += step)
                        if (GetPieceAt(x, fy) != null) obstacleCount++;
                }

                ChessPiece target = GetPieceAt(targetX, targetY);
                if (target != null)
                    return obstacleCount == 1;
                else
                    return obstacleCount == 0;

            case ChessPiece.PieceType.Zu:
                if (piece.PlayerSide == ChessPiece.Side.Red)
                {
                    if (fy >= 5) return dx == 0 && dy == -1;
                    return (dx == 0 && dy == -1) || (adx == 1 && dy == 0);
                }
                else
                {
                    if (fy <= 4) return dx == 0 && dy == 1;
                    return (dx == 0 && dy == 1) || (adx == 1 && dy == 0);
                }

            default:
                return false;
        }
    }

    // 悔棋功能
    void OnUndo()
    {
        if (moveHistory.Count == 0)
        {
            Debug.Log("没有可以悔的棋！");
            return;
        }

        MoveRecord record = moveHistory.Pop();

        ChessPiece movedPiece = null;
        foreach (var p in allPieces)
        {
            if (p != null && p.GridX == record.ToX && p.GridY == record.ToY &&
                p.Type == record.PieceType && p.PlayerSide == record.PieceSide)
            {
                movedPiece = p;
                break;
            }
        }

        if (movedPiece == null)
        {
            Debug.LogError("找不到要悔棋的棋子！");
            return;
        }

        movedPiece.GridX = record.FromX;
        movedPiece.GridY = record.FromY;
        movedPiece.UpdatePosition(true);

        if (record.HasEatenPiece)
        {
            ChessPiece revivedPiece = CreatePiece(record.EatenType, record.EatenSide, record.EatenX, record.EatenY);
            allPieces.Add(revivedPiece);
        }

        CurrentTurn = record.TurnBeforeMove;
        UpdateTurnText();

        // ========== 删除棋谱记录 ==========
        // record.PieceSide 是被悔棋的那一方（谁走的这步棋）
        ChessRecord?.RemoveLastMove(record.PieceSide);
        // =================================

        if (gameOver)
        {
            gameOver = false;
            GameOverPanel.SetActive(false);
        }

        HideJiangJunHint();
        Debug.Log($"悔棋：{record.PieceSide} {record.PieceType} 回到 ({record.FromX},{record.FromY})");
        UpdateUndoButton();
    }

    private GameObject GetPiecePrefab(ChessPiece.PieceType type, ChessPiece.Side side)
    {
        if (side == ChessPiece.Side.Red)
        {
            switch (type)
            {
                case ChessPiece.PieceType.Jiang: return redJiangPrefab;
                case ChessPiece.PieceType.Shi: return redShiPrefab;
                case ChessPiece.PieceType.Xiang: return redXiangPrefab;
                case ChessPiece.PieceType.Ma: return redMaPrefab;
                case ChessPiece.PieceType.Ju: return redJuPrefab;
                case ChessPiece.PieceType.Pao: return redPaoPrefab;
                case ChessPiece.PieceType.Zu: return redZuPrefab;
            }
        }
        else
        {
            switch (type)
            {
                case ChessPiece.PieceType.Jiang: return blackJiangPrefab;
                case ChessPiece.PieceType.Shi: return blackShiPrefab;
                case ChessPiece.PieceType.Xiang: return blackXiangPrefab;
                case ChessPiece.PieceType.Ma: return blackMaPrefab;
                case ChessPiece.PieceType.Ju: return blackJuPrefab;
                case ChessPiece.PieceType.Pao: return blackPaoPrefab;
                case ChessPiece.PieceType.Zu: return blackZuPrefab;
            }
        }
        return null;
    }

    ChessPiece CreatePiece(ChessPiece.PieceType type, ChessPiece.Side side, int x, int y)
    {
        GameObject prefab = GetPiecePrefab(type, side);
        if (prefab == null)
        {
            Debug.LogError($"找不到预制体：{side} {type}");
            return null;
        }

        // 找父物体（棋子容器）
        Transform parent = GameObject.Find("PiecesContainer")?.transform;
        if (parent == null) parent = transform;

        GameObject newObj = Instantiate(prefab, parent);
        newObj.name = $"Revived_{side}_{type}_{x}_{y}";

        ChessPiece newPiece = newObj.GetComponent<ChessPiece>();
        newPiece.Type = type;
        newPiece.PlayerSide = side;
        newPiece.GridX = x;
        newPiece.GridY = y;
        newPiece.UpdatePosition(false);

        Button btn = newPiece.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            ChessPiece p = newPiece;
            btn.onClick.AddListener(() => OnPieceClicked(p));
        }

        return newPiece;
    }

    void UpdateUndoButton()
    {
        if (UndoButton != null)
        {
            UndoButton.interactable = (moveHistory.Count > 0) && !gameOver;
        }
    }

    public void GameOver(ChessPiece.Side winner, bool isCheckmate = false, bool isStalemate = false)
    {
        if (gameOver) return;  // 阻止重复结束
        gameOver = true;

        if (isStalemate)
        {
            ResultText.text = "困毙！和棋！";
            Debug.Log("游戏结束：困毙和棋");
        }
        else if (isCheckmate)
        {
            string winnerText = winner == ChessPiece.Side.Red ? "红方" : "黑方";
            ResultText.text = $"{winnerText}将死对方获胜！";
            Debug.Log($"游戏结束：{winnerText}将死对方获胜！");
        }
        else
        {
            string winnerText = winner == ChessPiece.Side.Red ? "红方" : "黑方";
            ResultText.text = $"{winnerText}获胜！";
            Debug.Log($"游戏结束：{winnerText}获胜！");
        }

        GameOverPanel.SetActive(true);
        UpdateUndoButton();

        // ========== 显示收藏按钮 ==========
        ShowSaveGameButton(true);
        // ===================================
    }

    void SwitchTurn()
    {
        CurrentTurn = (CurrentTurn == ChessPiece.Side.Red) ? ChessPiece.Side.Black : ChessPiece.Side.Red;
        UpdateTurnText();

        if (!gameOver && enableAI && CurrentTurn == aiSide && !IsReplayMode)
        {
            if (!isAIPlaying)
            {
                isAIPlaying = true;
                StartCoroutine(InvokeAIMove());
            }
        }
    }

    IEnumerator InvokeAIMove()
    {
        yield return null; // 等待一帧
        if (ChessAI.Instance != null && CurrentTurn == aiSide && !gameOver)
            ChessAI.Instance.MakeAIMove();
        isAIPlaying = false;
    }

    public void UpdateTurnText()
    {
        if (TurnText != null)
        {
            TurnText.color = new Color(1f, 0.78f, 0.22f, 1f);

            if (CurrentTurn == ChessPiece.Side.Red)
            {
                TurnText.text = "红方回合";
            }
            else
            {
                TurnText.text = "黑方回合";
            }
        }
    }

    public void RestartGame()
    {
        ChessRecord?.ClearRecords();

        // 重置收藏标记
        isStalemateGame = false;

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // 规则验证
    public bool IsValidMove(ChessPiece piece, int tx, int ty)
    {
        int fx = piece.GridX, fy = piece.GridY;
        int dx = tx - fx, dy = ty - fy;
        int adx = Mathf.Abs(dx), ady = Mathf.Abs(dy);

        if (fx == tx && fy == ty) return false;
        if (tx < 0 || tx > 8 || ty < 0 || ty > 9) return false;

        switch (piece.Type)
        {
            case ChessPiece.PieceType.Jiang:
                return IsValidJiang(piece, fx, fy, tx, ty, adx, ady);
            case ChessPiece.PieceType.Shi:
                return IsValidShi(fx, fy, tx, ty, adx, ady, piece.PlayerSide);
            case ChessPiece.PieceType.Xiang:
                return IsValidXiang(piece, fx, fy, tx, ty, adx, ady);
            case ChessPiece.PieceType.Ma:
                return IsValidMa(fx, fy, tx, ty, adx, ady);
            case ChessPiece.PieceType.Ju:
                return IsValidJu(fx, fy, tx, ty);
            case ChessPiece.PieceType.Pao:
                return IsValidPao(fx, fy, tx, ty);
            case ChessPiece.PieceType.Zu:
                return IsValidZu(piece, fx, fy, tx, ty, dy, adx);
            default:
                return false;
        }
    }

    bool IsValidJiang(ChessPiece piece, int fx, int fy, int tx, int ty, int adx, int ady)
    {
        if (!((adx == 1 && ady == 0) || (adx == 0 && ady == 1))) return false;
        if (tx < 3 || tx > 5) return false;
        if (piece.PlayerSide == ChessPiece.Side.Red && ty < 7) return false;
        if (piece.PlayerSide == ChessPiece.Side.Black && ty > 2) return false;
        return true;
    }

    bool IsValidShi(int fx, int fy, int tx, int ty, int adx, int ady, ChessPiece.Side side)
    {
        if (adx != 1 || ady != 1) return false;
        if (tx < 3 || tx > 5) return false;
        if (side == ChessPiece.Side.Red && ty < 7) return false;
        if (side == ChessPiece.Side.Black && ty > 2) return false;
        return true;
    }

    bool IsValidXiang(ChessPiece piece, int fx, int fy, int tx, int ty, int adx, int ady)
    {
        if (adx != 2 || ady != 2) return false;
        if (piece.PlayerSide == ChessPiece.Side.Red && ty < 5) return false;
        if (piece.PlayerSide == ChessPiece.Side.Black && ty > 4) return false;

        int eyeX = (fx + tx) / 2;
        int eyeY = (fy + ty) / 2;
        if (GetPieceAt(eyeX, eyeY) != null) return false;

        return true;
    }

    bool IsValidMa(int fx, int fy, int tx, int ty, int adx, int ady)
    {
        if (!((adx == 1 && ady == 2) || (adx == 2 && ady == 1))) return false;

        int legX, legY;
        if (adx == 2)
        {
            legX = fx + (tx > fx ? 1 : -1);
            legY = fy;
        }
        else
        {
            legX = fx;
            legY = fy + (ty > fy ? 1 : -1);
        }
        if (GetPieceAt(legX, legY) != null) return false;

        return true;
    }

    bool IsValidJu(int fx, int fy, int tx, int ty)
    {
        if (fx != tx && fy != ty) return false;

        if (fx == tx)
        {
            int step = (ty > fy) ? 1 : -1;
            for (int y = fy + step; y != ty; y += step)
                if (GetPieceAt(fx, y) != null) return false;
        }
        else
        {
            int step = (tx > fx) ? 1 : -1;
            for (int x = fx + step; x != tx; x += step)
                if (GetPieceAt(x, fy) != null) return false;
        }
        return true;
    }

    bool IsValidPao(int fx, int fy, int tx, int ty)
    {
        if (fx != tx && fy != ty) return false;

        ChessPiece target = GetPieceAt(tx, ty);
        int count = 0;

        if (fx == tx)
        {
            int step = (ty > fy) ? 1 : -1;
            for (int y = fy + step; y != ty; y += step)
                if (GetPieceAt(fx, y) != null) count++;
        }
        else
        {
            int step = (tx > fx) ? 1 : -1;
            for (int x = fx + step; x != tx; x += step)
                if (GetPieceAt(x, fy) != null) count++;
        }

        if (target == null) return count == 0;
        return count == 1;
    }

    bool IsValidZu(ChessPiece piece, int fx, int fy, int tx, int ty, int dy, int adx)
    {
        int dx = tx - fx;

        if (piece.PlayerSide == ChessPiece.Side.Red)
        {
            if (dy > 0) return false;
            if (fy >= 5)
            {
                return (dx == 0 && dy == -1);
            }
            else
            {
                return (dx == 0 && dy == -1) || (adx == 1 && dy == 0);
            }
        }
        else
        {
            if (dy < 0) return false;
            if (fy <= 4)
            {
                return (dx == 0 && dy == 1);
            }
            else
            {
                return (dx == 0 && dy == 1) || (adx == 1 && dy == 0);
            }
        }
    }

    public ChessPiece GetPieceAt(int x, int y)
    {
        foreach (var piece in allPieces)
        {
            if (piece != null && piece.GridX == x && piece.GridY == y)
                return piece;
        }
        return null;
    }


    // ========== 收藏功能方法 ==========

    /// <summary>
    /// 控制收藏按钮的显示/隐藏
    /// </summary>
    void ShowSaveGameButton(bool show)
    {
        if (SaveGameButton != null)
        {
            SaveGameButton.gameObject.SetActive(show);
        }
    }

    /// <summary>
    /// 点击"收藏本局"按钮
    /// </summary>
    void OnSaveGameButtonClicked()
    {
        if (SaveNameInputPanel != null)
        {
            SaveNameInputPanel.SetActive(true);

            // 自动生成默认名称
            if (SaveNameInput != null)
            {
                string defaultName = $"棋局_{System.DateTime.Now:MMdd_HHmm}";
                SaveNameInput.text = defaultName;
                SaveNameInput.ActivateInputField();
            }
        }
    }

    /// <summary>
    /// 确认保存
    /// </summary>
    void OnConfirmSave()
    {
        string gameName = SaveNameInput != null ? SaveNameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(gameName))
        {
            gameName = $"棋局_{System.DateTime.Now:MMdd_HHmm}";
        }

        SavedGame savedGame = new SavedGame();
        savedGame.GameName = gameName;
        savedGame.SaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        savedGame.TotalMoves = moveHistory.Count;

        // 记录胜负结果
        if (isStalemateGame)
        {
            savedGame.Winner = "Stalemate"; // 和棋
        }
        else
        {
            savedGame.Winner = gameWinner == ChessPiece.Side.Red ? "Red" : "Black";
        }

        // 导出棋谱记录
        int moveIndex = 0;
        var historyList = new List<MoveRecord>(moveHistory);
        historyList.Reverse(); // Stack是倒序的，需要反转

        foreach (var record in historyList)
        {
            MoveData data = new MoveData
            {
                MoveIndex = moveIndex++,
                Side = record.PieceSide.ToString(),
                PieceType = record.PieceType.ToString(),
                FromX = record.FromX,
                FromY = record.FromY,
                ToX = record.ToX,
                ToY = record.ToY,
                HasEaten = record.HasEatenPiece,
                EatenPieceType = record.HasEatenPiece ? record.EatenType.ToString() : "",
                EatenSide = record.HasEatenPiece ? record.EatenSide.ToString() : "",
                Notation = NotationConverter.ToNotation(
                    record.PieceType, record.PieceSide,
                    record.FromX, record.FromY, record.ToX, record.ToY,
                    record.HasEatenPiece
                )
            };
            savedGame.Moves.Add(data);
        }

        // 保存到本地（JSON）
        if (GameSaveManager.Instance == null)
        {
            Debug.LogError("GameSaveManager.Instance 为 null，无法保存 JSON 棋局！");
        }
        else
        {
            GameSaveManager.Instance.SaveGame(savedGame);
        }

        // 导出 TXT 棋谱
        ExportToTxt(savedGame, gameName);

        // 关闭弹窗
        if (SaveNameInputPanel != null)
            SaveNameInputPanel.SetActive(false);

        // 隐藏收藏按钮（已收藏）
        ShowSaveGameButton(false);

        Debug.Log($"棋局已收藏：{gameName}");
    }

    /// <summary>
    /// 将棋局导出为格式化的 TXT 文件
    /// </summary>
    private void ExportToTxt(SavedGame savedGame, string gameName)
    {
        try
        {
            string txtFolder = Path.Combine(Application.persistentDataPath, "SavedGamesTxt");
            if (!Directory.Exists(txtFolder))
                Directory.CreateDirectory(txtFolder);

            string safeName = string.Concat(gameName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrEmpty(safeName)) safeName = "棋局";
            string fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string txtPath = Path.Combine(txtFolder, fileName);

            using (StreamWriter writer = new StreamWriter(txtPath, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine($"棋局名称：{gameName}");
                writer.WriteLine($"棋局日期：{savedGame.SaveTime}");

                string winnerText = savedGame.Winner switch
                {
                    "Red" => "红方",
                    "Black" => "黑方",
                    "Stalemate" => "和棋",
                    _ => "未知"
                };
                writer.WriteLine($"胜利方：{winnerText}");
                writer.WriteLine($"步数：{savedGame.TotalMoves}");
                writer.WriteLine();

                int halfMove = 0;
                for (int i = 0; i < savedGame.Moves.Count; i++)
                {
                    MoveData move = savedGame.Moves[i];
                    if (move.Side == "Red")
                    {
                        int round = halfMove + 1;
                        string redMove = move.Notation;
                        string blackMove = "";

                        if (i + 1 < savedGame.Moves.Count && savedGame.Moves[i + 1].Side == "Black")
                        {
                            blackMove = savedGame.Moves[i + 1].Notation;
                            i++; // 跳过下一个黑方走法
                        }
                        writer.WriteLine($"{round}. {redMove,-8}    {blackMove}");
                        halfMove++;
                    }
                    else
                    {
                        // 单独黑方走法（理论上极少发生，但保留兼容）
                        int round = halfMove + 1;
                        writer.WriteLine($"{round}. {"",-8}    {move.Notation}");
                        halfMove++;
                    }
                }
            }

            Debug.Log($"棋谱 TXT 已导出: {txtPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"导出 TXT 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 取消保存
    /// </summary>
    void OnCancelSave()
    {
        if (SaveNameInputPanel != null)
            SaveNameInputPanel.SetActive(false);
    }

    // ==============================

    // ========== 供 ReplayManager 调用的公共方法 ==========

    /// <summary>
    /// 获取所有棋子（包括null）
    /// </summary>
    public List<ChessPiece> GetAllPieces()
    {
        allPieces.RemoveAll(item => item == null);
        return new List<ChessPiece>(allPieces);
    }

    /// <summary>
    /// 清除所有棋子
    /// </summary>
    public void ClearAllPieces()
    {
        allPieces.Clear();
    }

    /// <summary>
    /// 添加棋子
    /// </summary>
    public void AddPiece(ChessPiece piece)
    {
        if (piece != null && !allPieces.Contains(piece))
        {
            allPieces.Add(piece);
        }
    }

    /// <summary>
    /// 移除棋子
    /// </summary>
    public void RemovePiece(ChessPiece piece)
    {
        if (piece != null)
        {
            allPieces.Remove(piece);
        }
    }

    /// <summary>
    /// 更新回合显示（公共方法）
    /// </summary>
    public void UpdateTurnTextPublic()
    {
        UpdateTurnText();
    }

    // ==============================
}
