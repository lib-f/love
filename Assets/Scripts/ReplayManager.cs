using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class ReplayManager : MonoBehaviour
{
    public static ReplayManager Instance;

    [Header("UI引用")]
    public GameObject ReplayControlPanel;
    public GameObject SavedGamesPanel;
    public Button PrevButton;
    public Button NextButton;
    public Button PlayPauseButton;
    public Button SpeedButton;
    public Button ExitButton;
    public TextMeshProUGUI StepInfoText;
    public TextMeshProUGUI PlayPauseText;
    public TextMeshProUGUI ResultText;

    [Header("游戏引用")]
    public GameManager gameManager;
    public ChessRecord chessRecord;

    private List<MoveRecord> replayMoves = new List<MoveRecord>();
    private int currentStep = -1;
    private bool isPlaying = false;
    private float playSpeed = 1f;
    private float[] speeds = {0.5f, 1f, 2f, 4f };
    private int speedIndex = 0;
    private Coroutine playCoroutine;
    private string gameResult;
    private bool skipEffects = false;  // 跳过特效和音效

    // 初始棋子布局备份
    private List<PieceState> initialPieces = new List<PieceState>();

    [Header("棋子预制体（请手动绑定）")]
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

    [System.Serializable]
    class PieceState
    {
        public ChessPiece.PieceType Type;
        public ChessPiece.Side Side;
        public int X, Y;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        PrevButton?.onClick.AddListener(OnPrevStep);
        NextButton?.onClick.AddListener(OnNextStep);
        PlayPauseButton?.onClick.AddListener(OnPlayPause);
        SpeedButton?.onClick.AddListener(OnChangeSpeed);
        ExitButton?.onClick.AddListener(OnExitReplay);

        // 确保控制面板初始隐藏
        if (ReplayControlPanel != null)
            ReplayControlPanel.SetActive(false);
    }

    /// <summary>
    /// 备份初始棋子状态（必须在游戏开始时调用一次）
    /// </summary>
    public void BackupInitialState()
    {
        initialPieces.Clear();
        var pieces = gameManager?.GetAllPieces();
        if (pieces == null)
        {
            Debug.LogError("gameManager.GetAllPieces() 返回 null");
            return;
        }

        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            initialPieces.Add(new PieceState
            {
                Type = piece.Type,
                Side = piece.PlayerSide,
                X = piece.GridX,
                Y = piece.GridY
            });
        }
        Debug.Log($"[ReplayManager] 备份了 {initialPieces.Count} 个棋子的初始状态");
    }

    /// <summary>
    /// 开始回放
    /// </summary>
    public void StartReplay(List<MoveRecord> moves, string winner)
    {
        chessRecord.autoScrollEnabled = false;
        Debug.Log($"[ReplayManager] 开始回放，共 {moves?.Count ?? 0} 步");

        if (moves == null || moves.Count == 0)
        {
            Debug.LogError("回放数据为空！");
            return;
        }

        replayMoves = new List<MoveRecord>(moves);
        currentStep = -1;
        isPlaying = false;
        gameResult = winner;

        // 进入回放模式
        if (gameManager != null)
            gameManager.IsReplayMode = true;

        // 重置棋盘
        ResetBoardToInitial();

        // 清空棋谱显示
        chessRecord?.ClearRecords();

        // 显示控制面板
        if (ReplayControlPanel != null)
            ReplayControlPanel.SetActive(true);

        // 显示结果
        if (ResultText != null)
        {
            string resultStr = winner switch
            {
                "Red" => "红方胜",
                "Black" => "黑方胜",
                "Stalemate" => "困毙和棋",
                _ => ""
            };
            ResultText.text = resultStr;
        }

        UpdateUI();
        Debug.Log("[ReplayManager] 回放准备完成，等待操作");
    }

    /// <summary>
    /// 重置棋盘到初始状态
    /// </summary>
     // 修改 ResetBoardToInitial，使用预制体而不是 FindTemplate
    void ResetBoardToInitial()
    {
        Debug.Log("[ReplayManager] 重置棋盘...");

        // 清除所有棋子
        var pieces = gameManager?.GetAllPieces();
        if (pieces != null)
        {
            foreach (var piece in pieces)
            {
                if (piece != null) Destroy(piece.gameObject);
            }
        }
        gameManager?.ClearAllPieces();

        // 重新创建初始布局
        int successCount = 0;
        foreach (var state in initialPieces)
        {
            GameObject prefab = GetPrefab(state.Type, state.Side);
            if (prefab == null)
            {
                Debug.LogError($"找不到预制体: {state.Side} {state.Type}");
                continue;
            }

            // 使用棋盘作为父物体（PiecesContainer 的父级是 Board，但建议直接使用 PiecesContainer）
            Transform parent = GameObject.Find("PiecesContainer")?.transform;
            if (parent == null) parent = gameManager.transform; // 备用

            GameObject newObj = Instantiate(prefab, parent);
            Image img = newObj.GetComponent<Image>();
            if (img != null) img.enabled = true;

            ChessPiece newPiece = newObj.GetComponent<ChessPiece>();
            newPiece.Type = state.Type;
            newPiece.PlayerSide = state.Side;
            newPiece.GridX = state.X;
            newPiece.GridY = state.Y;
            newPiece.UpdatePosition(false);

            Button btn = newPiece.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                ChessPiece p = newPiece;
                btn.onClick.AddListener(() => gameManager?.OnPieceClicked(p));
            }

            gameManager?.AddPiece(newPiece);
            successCount++;
        }

        Debug.Log($"[ReplayManager] 重置完成，创建了 {successCount} 个棋子");

        // 重置回合
        if (gameManager != null)
        {
            gameManager.CurrentTurn = ChessPiece.Side.Red;
            gameManager.UpdateTurnTextPublic();
        }

        // 清空棋谱显示
        chessRecord?.ClearRecords();
    }


    // 获取对应棋子预制体
    GameObject GetPrefab(ChessPiece.PieceType type, ChessPiece.Side side)
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

    /// <summary>
    /// 执行到指定步数
    /// </summary>
    void ExecuteToStep(int targetStep)
    {
        if (targetStep < -1 || targetStep >= replayMoves.Count)
        {
            Debug.Log($"目标步数 {targetStep} 超出范围 [-1, {replayMoves.Count - 1}]");
            return;
        }

        bool isRewind = (targetStep < currentStep);
        bool oldSkipEffects = skipEffects;

        if (isRewind)
        {
            // 回退：重置时跳过所有特效/音效
            skipEffects = true;
            Debug.Log($"回退: {currentStep} -> {targetStep}，重置棋盘");
            ResetBoardToInitial();
            currentStep = -1;
        }
        else
        {
            // 前进：正常播放特效
            skipEffects = false;
        }

        // 逐步执行到目标步数
        while (currentStep < targetStep)
        {
            currentStep++;
            ExecuteMove(replayMoves[currentStep]);
        }

        // 恢复原来的标志（避免影响下次）
        skipEffects = oldSkipEffects;

        UpdateUI();
    }

    /// <summary>
    /// 执行单步移动
    /// </summary>
    /// <summary>
    /// 执行单步移动
    /// </summary>
    void ExecuteMove(MoveRecord move)
    {
        ChessPiece piece = gameManager?.GetPieceAt(move.FromX, move.FromY);
        if (piece == null)
        {
            Debug.LogError($"回放错误：找不到棋子 at ({move.FromX},{move.FromY})，第 {currentStep} 步");
            return;
        }

        // 获取移动前后的世界坐标（用于拖尾特效）
        Vector2 startPos = GameManager.GridToAnchoredPosition(move.FromX, move.FromY);
        Vector2 endPos = GameManager.GridToAnchoredPosition(move.ToX, move.ToY);
        // 播放走子拖尾效果
        if (!skipEffects)
            EffectManager.Instance?.PlayMoveTrail(startPos, endPos);
        // 处理吃子
        if (move.HasEatenPiece)
        {
            ChessPiece target = gameManager?.GetPieceAt(move.ToX, move.ToY);
            if (target != null)
            {
                if (!skipEffects)
                {
                    // 播放吃子特效（在目标位置）
                    RectTransform targetRT = target.GetComponent<RectTransform>();
                    if (targetRT != null)
                        EffectManager.Instance?.PlayEatEffect(targetRT.anchoredPosition);
                    // 播放吃子音效
                    AudioManager.Instance?.PlayEatSound();
                }

                gameManager?.RemovePiece(target);
                Destroy(target.gameObject);
            }
        }
        else
        {
            // 播放走子音效
            if (!skipEffects)
                AudioManager.Instance?.PlayMoveSound();
        }

        // 移动棋子（无动画）
        piece.GridX = move.ToX;
        piece.GridY = move.ToY;
        piece.UpdatePosition(false);

        // 添加记谱
        string notation = NotationConverter.ToNotation(piece, move.FromX, move.FromY, move.ToX, move.ToY, move.HasEatenPiece);
        chessRecord?.AddMove(piece.PlayerSide, notation);

        Debug.Log($"[Replay] 第 {currentStep} 步: {move.PieceSide} {move.PieceType} ({move.FromX},{move.FromY}) -> ({move.ToX},{move.ToY})");
    }

    // ========== 按钮回调 ==========

    void OnPrevStep()
    {
        if (isPlaying) StopPlay();
        ExecuteToStep(currentStep - 1);
    }

    void OnNextStep()
    {
        if (isPlaying) StopPlay();
        ExecuteToStep(currentStep + 1);
    }

    void OnPlayPause()
    {
        if (isPlaying)
            StopPlay();
        else
            StartPlay();
    }

    void StartPlay()
    {
        isPlaying = true;
        if (PlayPauseText != null) PlayPauseText.text = "暂停";
        playCoroutine = StartCoroutine(AutoPlay());
        Debug.Log("[ReplayManager] 开始自动播放");
    }

    void StopPlay()
    {
        isPlaying = false;
        if (PlayPauseText != null) PlayPauseText.text = "播放";
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }

    IEnumerator AutoPlay()
    {
        while (currentStep < replayMoves.Count - 1 && isPlaying)
        {
            yield return new WaitForSeconds(0.8f / playSpeed);
            ExecuteToStep(currentStep + 1);
        }

        if (currentStep >= replayMoves.Count - 1)
        {
            Debug.Log("[ReplayManager] 播放完毕");
            StopPlay();
        }
    }

    void OnChangeSpeed()
    {
        speedIndex = (speedIndex + 1) % speeds.Length;
        playSpeed = speeds[speedIndex];

        var speedText = SpeedButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (speedText != null) speedText.text = $"{playSpeed}x";

        Debug.Log($"[ReplayManager] 播放速度: {playSpeed}x");
    }

    public void OnExitReplay()
    {
        Debug.Log("[ReplayManager] 退出回放");
        StopPlay();
        MainMenuManager.OpenSavedGamesOnStart = false;
        SceneManager.LoadScene(SavedGamesUI.MainMenuSceneName);
    }

    void UpdateUI()
    {
        if (StepInfoText != null)
        {
            StepInfoText.text = $"第 {currentStep + 1} 步 / 共 {replayMoves.Count} 步";
        }

        if (PrevButton != null) PrevButton.interactable = currentStep > -1;
        if (NextButton != null) NextButton.interactable = currentStep < replayMoves.Count - 1;
    }
}
