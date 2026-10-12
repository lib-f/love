using UnityEngine;
using DG.Tweening;

public class ChessPiece : MonoBehaviour
{
    public enum PieceType { Jiang, Shi, Xiang, Ma, Ju, Pao, Zu }
    public enum Side { Red, Black }

    public PieceType Type;
    public Side PlayerSide;
    public int GridX;
    public int GridY;
    private bool isSelected = false;

    void Start()
    {
        UpdatePosition(false);
    }

    public void UpdatePosition(bool animate = true)
    {
        Vector2 targetPos = GameManager.GridToAnchoredPosition(GridX, GridY);

        RectTransform rt = GetComponent<RectTransform>();

        if (animate)
        {
            // DOTween平滑移动，0.3秒，缓出
            rt.DOAnchorPos(targetPos, 0.3f).SetEase(Ease.OutQuad);
        }
        else
        {
            rt.anchoredPosition = targetPos;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RectTransform rt = GetComponent<RectTransform>();

        if (selected)
        {
            rt.DOScale(new Vector3(1.15f, 1.15f, 1f), 0.15f);
            // 显示走位提示
            HintManager.Instance?.ShowHints(this);
        }
        else
        {
            rt.DOScale(Vector3.one, 0.15f);
            // 清除走位提示
            HintManager.Instance?.ClearHints();
        }
    }

    public bool IsSelected => isSelected;
}