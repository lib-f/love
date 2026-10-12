using UnityEngine;

[System.Serializable]
public class MoveRecord
{
    // 移动的棋子信息
    public ChessPiece.PieceType PieceType;
    public ChessPiece.Side PieceSide;
    public int FromX, FromY;
    public int ToX, ToY;

    // 被吃的棋子信息（如果有）
    public bool HasEatenPiece;
    public ChessPiece.PieceType EatenType;
    public ChessPiece.Side EatenSide;
    public int EatenX, EatenY;

    // 回合信息
    public ChessPiece.Side TurnBeforeMove;
}