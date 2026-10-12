using UnityEngine;
using UnityEngine.EventSystems;

public class BoardClick : MonoBehaviour, IPointerClickHandler
{
    private const int COLS = 9;
    private const int ROWS = 10;
    private const float CELL_SIZE = 100f;
    private const float MARGIN = 60f;
    private const float BOARD_WIDTH = 920f;
    private const float BOARD_HEIGHT = 1020f;

    public void OnPointerClick(PointerEventData eventData)
    {
        RectTransform rt = GetComponent<RectTransform>();
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out localPos);

        // 关键修正：棋盘锚点是Middle-Center，localPos原点在中心
        // 棋盘左上角在local坐标中是 (-BOARD_WIDTH/2, BOARD_HEIGHT/2)
        // 转换为以左上角为原点的坐标（Y向下为正，和UI一致）
        float xFromLeft = localPos.x + BOARD_WIDTH / 2f;   // 0 ~ 920
        float yFromTop = -localPos.y + BOARD_HEIGHT / 2f; // 0 ~ 1020 (Y翻转)

        // 检查是否在棋盘边距内
        if (xFromLeft < MARGIN - 50 || xFromLeft > BOARD_WIDTH - MARGIN + 50)
            return;
        if (yFromTop < MARGIN - 50 || yFromTop > BOARD_HEIGHT - MARGIN + 50)
            return;

        // 计算最近的交叉点（从左上角往下数）
        // 列：0~8（左到右）
        // 行：0~9（上到下）
        int visualX = Mathf.RoundToInt((xFromLeft - MARGIN) / CELL_SIZE);
        int visualY = Mathf.RoundToInt((yFromTop - MARGIN) / CELL_SIZE);

        visualX = Mathf.Clamp(visualX, 0, 8);
        visualY = Mathf.Clamp(visualY, 0, 9);
        Vector2Int logicalGrid = GameManager.VisualGridToLogicalGrid(visualX, visualY);

        Debug.Log($"鐐瑰嚮妫嬬洏: 灞忓箷{eventData.position} 鏈湴{localPos} 瑙嗚({visualX},{visualY}) 閫昏緫({logicalGrid.x},{logicalGrid.y})");

        if (GameManager.Instance != null && GameManager.Instance.selectedPiece != null)
        {
            GameManager.Instance.TryMoveOrEat(GameManager.Instance.selectedPiece, logicalGrid.x, logicalGrid.y);
        }
    }
}