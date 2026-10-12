using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class ChessAI : MonoBehaviour
{
    public static ChessAI Instance;

    [Header("AI难度设置")]
    [Tooltip("搜索深度：2简单，3中等，4困难")]
    public int searchDepth = 3;
    [Tooltip("AI思考延迟（秒）")]
    public float thinkDelay = 0.5f;
    [Tooltip("是否使用Alpha-Beta剪枝（强烈推荐开启）")]
    public bool useAlphaBeta = true;

    [Header("Ollama设置")]
    [Tooltip("启用后优先请求本地 Ollama 选择走法；失败时回退到内置 Minimax。")]
    public bool useOllamaAI = false;
    public string ollamaEndpoint = "";
    public string ollamaModel = "qwen3.5:9b";
    public int ollamaTimeoutSeconds = 3;
    public bool fallbackToMinimaxWhenOllamaFails = true;
    [Tooltip("只把排序后的前N个候选走法发给Ollama，减少本地大模型响应时间。0表示发送全部。")]
    public int maxOllamaMovesToSend = 24;

    [Header("性能设置")]
    [Tooltip("内置AI每次思考最多评估的第一层走法数。0表示不限制。")]
    public int maxRootMovesToEvaluate = 28;
    [Tooltip("内置AI递归搜索每层最多展开的走法数。0表示不限制。")]
    public int maxBranchMovesToEvaluate = 20;
    [Tooltip("内置AI搜索时间上限，超时就使用当前最好走法，避免卡住主线程。")]
    public int minimaxTimeBudgetMilliseconds = 1200;

    public ChessPiece.Side mySide = ChessPiece.Side.Black;

    [Header("棋子基础分值")]
    public int jiangValue = 10000;
    public int juValue = 900;
    public int maValue = 400;
    public int paoValue = 450;
    public int xiangValue = 200;
    public int shiValue = 200;
    public int zuValue = 100;

    // 棋子分值字典
    private Dictionary<ChessPiece.PieceType, int> pieceValues;
    private bool isThinking = false;
    private Stopwatch searchTimer;
    private int searchTimeBudgetMs;

    // 位置加分表（兵/卒过河，马中心等）
    private int[,] zuPositionBonusRed = new int[10, 9];   // 红方兵位置分
    private int[,] zuPositionBonusBlack = new int[10, 9]; // 黑方卒位置分
    private int[,] maPositionBonus = new int[10, 9];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitPieceValues();
        InitPositionBonus();
    }

    void InitPieceValues()
    {
        pieceValues = new Dictionary<ChessPiece.PieceType, int>
        {
            { ChessPiece.PieceType.Jiang, jiangValue },
            { ChessPiece.PieceType.Ju, juValue },
            { ChessPiece.PieceType.Ma, maValue },
            { ChessPiece.PieceType.Pao, paoValue },
            { ChessPiece.PieceType.Xiang, xiangValue },
            { ChessPiece.PieceType.Shi, shiValue },
            { ChessPiece.PieceType.Zu, zuValue }
        };
    }

    void InitPositionBonus()
    {
        // 兵/卒：过河后越靠近敌方九宫分越高
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                // 红方兵：y < 5 为过河，距离红方底线越远分越高
                if (y < 5)
                    zuPositionBonusRed[y, x] = (5 - y) * 10;
                else
                    zuPositionBonusRed[y, x] = 0;

                // 黑方卒：y > 4 为过河，距离黑方底线越远分越高（镜像）
                if (y > 4)
                    zuPositionBonusBlack[y, x] = (y - 4) * 10;
                else
                    zuPositionBonusBlack[y, x] = 0;

                // 马：中心位置（x=3~5, y=4~6）加分
                int centerBonus = 0;
                if (Mathf.Abs(x - 4) <= 2 && Mathf.Abs(y - 4.5f) <= 2.5f)
                    centerBonus = 15;
                maPositionBonus[y, x] = centerBonus;
            }
        }
    }

    /// <summary>
    /// AI 走棋入口（由GameManager调用）
    /// </summary>
    public void MakeAIMove()
    {
        if (isThinking)
        {
            Debug.LogWarning("AI正在思考，忽略重复调用。 ");
            return;
        }

        StartCoroutine(ThinkAndMove());
    }

    IEnumerator ThinkAndMove()
    {
        isThinking = true;
        yield return new WaitForSeconds(thinkDelay);

        // 获取当前回合，检查是否是该 AI 的回合
        ChessPiece.Side currentTurn = GameManager.Instance.CurrentTurn;
        if (currentTurn != mySide)
        {
            Debug.LogWarning($"AI被调用时不是AI回合（当前回合: {currentTurn}, AI方: {mySide}），跳过");
            isThinking = false;
            yield break;
        }

        // 获取所有合法走法
        List<MoveCandidate> moves = GetAllLegalMoves(mySide);
        if (moves.Count == 0)
        {
            Debug.Log("AI无合法走法，可能被将死或困毙");

            // 检查AI方是否被将军
            bool inCheck = GameManager.Instance.IsInCheck(mySide);
            if (inCheck)
            {
                // 被将死 => 对手获胜
                ChessPiece.Side winner = (mySide == ChessPiece.Side.Red) ? ChessPiece.Side.Black : ChessPiece.Side.Red;
                GameManager.Instance.GameOver(winner, true, false);
            }
            else
            {
                // 困毙 => 和棋
                GameManager.Instance.GameOver(ChessPiece.Side.Red, false, true);
            }
            isThinking = false;
            yield break;
        }

        MoveCandidate bestMove = null;
        int bestScore = int.MinValue;

        SortMovesByHeuristic(moves);

        if (useOllamaAI)
        {
            List<MoveCandidate> ollamaMoves = LimitMoves(moves, maxOllamaMovesToSend);
            yield return RequestOllamaMove(ollamaMoves, move => bestMove = move);
            if (bestMove != null)
            {
                Debug.Log($"Ollama AI走法: {bestMove.Piece.Type} ({bestMove.Piece.GridX},{bestMove.Piece.GridY}) -> ({bestMove.ToX},{bestMove.ToY})");
                GameManager.Instance.TryMoveOrEat(bestMove.Piece, bestMove.ToX, bestMove.ToY);
                isThinking = false;
                yield break;
            }

            if (!fallbackToMinimaxWhenOllamaFails)
            {
                Debug.LogWarning("Ollama未返回可用合法走法，且未开启Minimax回退。跳过本次AI走棋。");
                isThinking = false;
                yield break;
            }
        }

        int rootMoveLimit = maxRootMovesToEvaluate > 0 ? Mathf.Min(maxRootMovesToEvaluate, moves.Count) : moves.Count;
        searchTimer = Stopwatch.StartNew();
        searchTimeBudgetMs = Mathf.Max(100, minimaxTimeBudgetMilliseconds);

        // Minimax 搜索
        for (int i = 0; i < rootMoveLimit; i++)
        {
            MoveCandidate move = moves[i];
            // 注意：EvaluateBoard 的视角应改为 mySide，而不是固定 Black
            int score = EvaluateMove(move, mySide, searchDepth, int.MinValue, int.MaxValue, useAlphaBeta);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (searchTimer.ElapsedMilliseconds >= searchTimeBudgetMs)
            {
                Debug.LogWarning($"AI搜索达到时间上限 {searchTimeBudgetMs}ms，使用当前最好走法。 ");
                break;
            }

            if ((i & 3) == 3)
                yield return null;
        }

        if (bestMove != null)
        {
            Debug.Log($"AI走法: {bestMove.Piece.Type} ({bestMove.Piece.GridX},{bestMove.Piece.GridY}) -> ({bestMove.ToX},{bestMove.ToY}), 评分={bestScore}");
            GameManager.Instance.TryMoveOrEat(bestMove.Piece, bestMove.ToX, bestMove.ToY);
        }
        else
        {
            Debug.LogError("AI未找到任何走法");
        }

        isThinking = false;
    }

    /// <summary>
    /// 获取某方所有合法走法（不送将）
    /// </summary>
    List<MoveCandidate> GetAllLegalMoves(ChessPiece.Side side)
    {
        List<MoveCandidate> moves = new List<MoveCandidate>();
        var pieces = GameManager.Instance.GetAllPieces();

        foreach (var piece in pieces)
        {
            if (piece == null || piece.PlayerSide != side) continue;

            for (int tx = 0; tx <= 8; tx++)
            {
                for (int ty = 0; ty <= 9; ty++)
                {
                    if (piece.GridX == tx && piece.GridY == ty) continue;
                    ChessPiece target = GameManager.Instance.GetPieceAt(tx, ty);
                    if (target != null && target.PlayerSide == side) continue;
                    if (!GameManager.Instance.IsValidMove(piece, tx, ty)) continue;

                    // 检查是否送将（模拟走棋后自己不被将军）
                    if (!IsMoveLegal(piece, tx, ty)) continue;

                    moves.Add(new MoveCandidate
                    {
                        Piece = piece,
                        ToX = tx,
                        ToY = ty,
                        IsEat = (target != null)
                    });
                }
            }
        }
        return moves;
    }

    /// <summary>
    /// 判断走法是否合法（不送将）
    /// </summary>
    bool IsMoveLegal(ChessPiece piece, int targetX, int targetY)
    {
        int oldX = piece.GridX;
        int oldY = piece.GridY;
        ChessPiece target = GameManager.Instance.GetPieceAt(targetX, targetY);

        piece.GridX = targetX;
        piece.GridY = targetY;

        bool hadEaten = false;
        if (target != null && target.PlayerSide != piece.PlayerSide)
        {
            hadEaten = true;
            GameManager.Instance.RemovePiece(target);
        }

        bool inCheck = GameManager.Instance.IsInCheck(piece.PlayerSide);

        // 恢复
        piece.GridX = oldX;
        piece.GridY = oldY;
        if (hadEaten)
        {
            GameManager.Instance.AddPiece(target);
        }

        return !inCheck;
    }

    /// <summary>
    /// 递归评估走法（Minimax核心）
    /// </summary>
    int EvaluateMove(MoveCandidate move, ChessPiece.Side currentSide, int depth, int alpha, int beta, bool useAB)
    {
        if (IsSearchTimedOut())
            return EvaluateBoard(mySide);

        // 模拟移动
        ChessPiece piece = move.Piece;
        int oldX = piece.GridX;
        int oldY = piece.GridY;
        ChessPiece eaten = move.IsEat ? GameManager.Instance.GetPieceAt(move.ToX, move.ToY) : null;

        piece.GridX = move.ToX;
        piece.GridY = move.ToY;

        if (eaten != null && eaten.PlayerSide != piece.PlayerSide)
        {
            GameManager.Instance.RemovePiece(eaten);
        }

        int score;
        // 检查是否吃掉了将（直接胜利）
        if (eaten != null && eaten.Type == ChessPiece.PieceType.Jiang)
        {
            score = (piece.PlayerSide == currentSide) ? 99999 : -99999;
        }
        else if (depth <= 1)
        {
            // 到达叶子节点，评估当前局面（从AI视角）
            score = EvaluateBoard(mySide); // AI固定为黑方
        }
        else
        {
            // 切换棋手
            ChessPiece.Side nextSide = (currentSide == ChessPiece.Side.Red) ? ChessPiece.Side.Black : ChessPiece.Side.Red;
            List<MoveCandidate> nextMoves = GetAllLegalMoves(nextSide);
            if (nextMoves.Count == 0)
            {
                // 对方无步可走：若被将军则对方输，否则困毙和棋
                bool inCheck = GameManager.Instance.IsInCheck(nextSide);
                if (inCheck)
                    score = nextSide == mySide ? -99999 : 99999;
                else
                    score = 0; // 困毙和棋
            }
            else
            {
                SortMovesByHeuristic(nextMoves);
                int branchMoveLimit = maxBranchMovesToEvaluate > 0 ? Mathf.Min(maxBranchMovesToEvaluate, nextMoves.Count) : nextMoves.Count;

                if (nextSide == mySide)
                {
                    // AI方最大化分数
                    int maxScore = int.MinValue;
                    for (int i = 0; i < branchMoveLimit; i++)
                    {
                        if (IsSearchTimedOut())
                            break;

                        MoveCandidate nextMove = nextMoves[i];
                        int childScore = EvaluateMove(nextMove, nextSide, depth - 1, alpha, beta, useAB);
                        if (childScore > maxScore) maxScore = childScore;
                        if (useAB)
                        {
                            alpha = Mathf.Max(alpha, maxScore);
                            if (beta <= alpha) break; // β剪枝
                        }
                    }
                    score = maxScore == int.MinValue ? EvaluateBoard(mySide) : maxScore;
                }
                else
                {
                    // 对手最小化分数
                    int minScore = int.MaxValue;
                    for (int i = 0; i < branchMoveLimit; i++)
                    {
                        if (IsSearchTimedOut())
                            break;

                        MoveCandidate nextMove = nextMoves[i];
                        int childScore = EvaluateMove(nextMove, nextSide, depth - 1, alpha, beta, useAB);
                        if (childScore < minScore) minScore = childScore;
                        if (useAB)
                        {
                            beta = Mathf.Min(beta, minScore);
                            if (beta <= alpha) break; // α剪枝
                        }
                    }
                    score = minScore == int.MaxValue ? EvaluateBoard(mySide) : minScore;
                }
            }
        }

        // 恢复状态
        piece.GridX = oldX;
        piece.GridY = oldY;
        if (eaten != null)
            GameManager.Instance.AddPiece(eaten);

        return score;
    }

    bool IsSearchTimedOut()
    {
        return searchTimer != null && searchTimer.ElapsedMilliseconds >= searchTimeBudgetMs;
    }

    void SortMovesByHeuristic(List<MoveCandidate> moves)
    {
        moves.Sort((a, b) => GetMoveHeuristicScore(b).CompareTo(GetMoveHeuristicScore(a)));
    }

    List<MoveCandidate> LimitMoves(List<MoveCandidate> moves, int limit)
    {
        if (limit <= 0 || moves.Count <= limit)
            return moves;

        return moves.GetRange(0, limit);
    }

    int GetMoveHeuristicScore(MoveCandidate move)
    {
        int score = 0;
        ChessPiece target = GameManager.Instance.GetPieceAt(move.ToX, move.ToY);
        if (target != null && target.PlayerSide != move.Piece.PlayerSide)
            score += pieceValues[target.Type] - pieceValues[move.Piece.Type] / 10;

        score += GetPositionBonusAt(move.Piece, move.ToX, move.ToY);
        return score;
    }

    /// <summary>
    /// 评估当前棋盘（从 perspective 视角，正分表示 perspective 优）
    /// </summary>
    int EvaluateBoard(ChessPiece.Side perspective)
    {
        int score = 0;
        var pieces = GameManager.Instance.GetAllPieces();

        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            int value = pieceValues[piece.Type];
            int bonus = GetPositionBonus(piece);
            int total = value + bonus;

            if (piece.PlayerSide == perspective)
                score += total;
            else
                score -= total;
        }

        // 将军额外加分（鼓励威胁对方将，惩罚自己被将）
        ChessPiece.Side opponent = OppositeSide(perspective);
        if (GameManager.Instance.IsInCheck(opponent))
            score += 80;
        if (GameManager.Instance.IsInCheck(perspective))
            score -= 80;

        return score;
    }

    IEnumerator RequestOllamaMove(List<MoveCandidate> legalMoves, Action<MoveCandidate> onComplete)
    {
        onComplete?.Invoke(null);

        if (legalMoves == null || legalMoves.Count == 0 || string.IsNullOrWhiteSpace(ollamaEndpoint) || string.IsNullOrWhiteSpace(ollamaModel))
            yield break;

        string prompt = BuildOllamaPrompt(legalMoves);
        OllamaGenerateRequest requestBody = new OllamaGenerateRequest
        {
            model = ollamaModel,
            prompt = prompt,
            stream = false,
            options = new OllamaOptions
            {
                temperature = 0.1f,
                num_predict = 32,
                num_ctx = 4096
            }
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(ollamaEndpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, ollamaTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Ollama请求失败: {request.error}");
                yield break;
            }

            string responseText = request.downloadHandler.text;
            OllamaGenerateResponse response = null;
            try
            {
                response = JsonUtility.FromJson<OllamaGenerateResponse>(responseText);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Ollama响应解析失败: {e.Message}\n{responseText}");
            }

            string moveText = response != null && !string.IsNullOrWhiteSpace(response.response)
                ? response.response
                : responseText;

            MoveCandidate selectedMove = ParseOllamaMove(moveText, legalMoves);
            if (selectedMove == null)
                Debug.LogWarning($"Ollama未返回合法走法，将使用内置AI。返回内容: {moveText}");

            onComplete?.Invoke(selectedMove);
        }
    }

    string BuildOllamaPrompt(List<MoveCandidate> legalMoves)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("你是中国象棋AI。请只从给定的合法走法列表中选择一步。不要解释。");
        builder.AppendLine("坐标范围：x=0..8，y=0..9。只返回JSON，例如：{\"fromX\":0,\"fromY\":6,\"toX\":0,\"toY\":5}");
        builder.AppendLine($"你执{SideToChinese(mySide)}，当前棋盘：");

        foreach (ChessPiece piece in GameManager.Instance.GetAllPieces())
        {
            if (piece == null)
                continue;

            builder.AppendLine($"- {SideToChinese(piece.PlayerSide)}{PieceToChinese(piece.Type)} at ({piece.GridX},{piece.GridY})");
        }

        builder.AppendLine("合法走法列表：");
        for (int i = 0; i < legalMoves.Count; i++)
        {
            MoveCandidate move = legalMoves[i];
            builder.AppendLine($"{i}: {PieceToChinese(move.Piece.Type)} ({move.Piece.GridX},{move.Piece.GridY})->({move.ToX},{move.ToY})");
        }

        return builder.ToString();
    }

    MoveCandidate ParseOllamaMove(string text, List<MoveCandidate> legalMoves)
    {
        if (string.IsNullOrWhiteSpace(text) || legalMoves == null)
            return null;

        Match match = Regex.Match(text, @"fromX\D+(\d+)\D+fromY\D+(\d+)\D+toX\D+(\d+)\D+toY\D+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            match = Regex.Match(text, @"\[\s*(\d+)\s*,\s*(\d+)\s*\]\D+\[\s*(\d+)\s*,\s*(\d+)\s*\]", RegexOptions.Singleline);
        if (!match.Success)
            match = Regex.Match(text, @"\(\s*(\d+)\s*,\s*(\d+)\s*\)\D+\(\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Singleline);
        if (!match.Success)
            return null;

        int fromX = int.Parse(match.Groups[1].Value);
        int fromY = int.Parse(match.Groups[2].Value);
        int toX = int.Parse(match.Groups[3].Value);
        int toY = int.Parse(match.Groups[4].Value);

        foreach (MoveCandidate move in legalMoves)
        {
            if (move.Piece.GridX == fromX && move.Piece.GridY == fromY && move.ToX == toX && move.ToY == toY)
                return move;
        }

        return null;
    }

    ChessPiece.Side OppositeSide(ChessPiece.Side side)
    {
        return side == ChessPiece.Side.Red ? ChessPiece.Side.Black : ChessPiece.Side.Red;
    }

    string SideToChinese(ChessPiece.Side side)
    {
        return side == ChessPiece.Side.Red ? "红方" : "黑方";
    }

    string PieceToChinese(ChessPiece.PieceType type)
    {
        switch (type)
        {
            case ChessPiece.PieceType.Jiang: return "将/帅";
            case ChessPiece.PieceType.Shi: return "士/仕";
            case ChessPiece.PieceType.Xiang: return "象/相";
            case ChessPiece.PieceType.Ma: return "马";
            case ChessPiece.PieceType.Ju: return "车";
            case ChessPiece.PieceType.Pao: return "炮";
            case ChessPiece.PieceType.Zu: return "兵/卒";
            default: return type.ToString();
        }
    }

    /// <summary>
    /// 获取棋子位置加分
    /// </summary>
    int GetPositionBonus(ChessPiece piece)
    {
        return GetPositionBonusAt(piece, piece.GridX, piece.GridY);
    }

    int GetPositionBonusAt(ChessPiece piece, int x, int y)
    {
        switch (piece.Type)
        {
            case ChessPiece.PieceType.Zu:
                if (piece.PlayerSide == ChessPiece.Side.Red)
                    return zuPositionBonusRed[y, x];
                else
                    return zuPositionBonusBlack[y, x];
            case ChessPiece.PieceType.Ma:
                return maPositionBonus[y, x];
            default:
                return 0;
        }
    }
}

[Serializable]
public class OllamaGenerateRequest
{
    public string model;
    public string prompt;
    public bool stream;
    public OllamaOptions options;
}

[Serializable]
public class OllamaOptions
{
    public float temperature;
    public int num_predict;
    public int num_ctx;
}

[Serializable]
public class OllamaGenerateResponse
{
    public string response;
}

/// <summary>
/// AI候选走法数据结构
/// </summary>
public class MoveCandidate
{
    public ChessPiece Piece;
    public int ToX, ToY;
    public bool IsEat;
}
