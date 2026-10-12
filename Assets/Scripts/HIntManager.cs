using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance;

    [Header("提示预制体")]
    public GameObject dotHintPrefab;      // 圆点提示
    public GameObject focusHintPrefab;    // 聚焦标记

    [Header("父容器")]
    public RectTransform hintsContainer;

    private List<GameObject> activeHints = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void ShowHints(ChessPiece piece)
    {
        ClearHints();

        for (int x = 0; x <= 8; x++)
        {
            for (int y = 0; y <= 9; y++)
            {
                if (piece.GridX == x && piece.GridY == y) continue;

                ChessPiece target = GameManager.Instance.GetPieceAt(x, y);

                // 跳过己方棋子位置（不能吃自己人）
                if (target != null && target.PlayerSide == piece.PlayerSide) continue;

                // 检查是否可走（包括吃子）
                if (!GameManager.Instance.IsValidMove(piece, x, y)) continue;

                // 判断用哪种提示样式
                bool isLineMove = IsLineMovement(piece.Type);

                Vector2 pos = GridToAnchoredPos(x, y);
                GameObject hint = null;

                if (isLineMove)
                {
                    // 直线走法：显示圆点
                    hint = Instantiate(dotHintPrefab, hintsContainer);
                    RectTransform rt = hint.GetComponent<RectTransform>();
                    rt.anchoredPosition = pos;
                }
                else
                {
                    // 跳跃走法：显示聚焦标记
                    hint = Instantiate(focusHintPrefab, hintsContainer);
                    RectTransform rt = hint.GetComponent<RectTransform>();
                    rt.anchoredPosition = pos;
                }

                if (hint != null)
                {
                    activeHints.Add(hint);
                }
            }
        }
    }

    // 判断是否是直线移动棋子（显示圆点）
    bool IsLineMovement(ChessPiece.PieceType type)
    {
        return type == ChessPiece.PieceType.Ju ||
               type == ChessPiece.PieceType.Pao ||
               type == ChessPiece.PieceType.Zu;
    }

    Vector2 GridToAnchoredPos(int x, int y)
    {
        return GameManager.GridToAnchoredPosition(x, y);
    }

    public void ClearHints()
    {
        foreach (var hint in activeHints)
        {
            if (hint != null) Destroy(hint);
        }
        activeHints.Clear();
    }
}