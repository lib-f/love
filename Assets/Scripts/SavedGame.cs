using System;
using System.Collections.Generic;

[Serializable]
public class SavedGame
{
    public string GameId;           // 唯一ID
    public string GameName;         // 用户自定义名称
    public string SaveTime;         // 保存时间
    public List<MoveData> Moves;    // 所有走法记录
    public string Winner;           // 获胜方 ("Red"/"Black"/"Stalemate")
    public int TotalMoves;          // 总步数

    public SavedGame()
    {
        GameId = Guid.NewGuid().ToString("N").Substring(0, 8);
        SaveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        Moves = new List<MoveData>();
    }
}

[Serializable]
public class MoveData
{
    public int MoveIndex;           // 第几步
    public string Side;             // "Red" 或 "Black"
    public string PieceType;        // 棋子类型
    public int FromX, FromY;        // 起始位置
    public int ToX, ToY;            // 目标位置
    public bool HasEaten;           // 是否吃子
    public string EatenPieceType;   // 被吃棋子类型（如果有）
    public string EatenSide;        // 被吃棋子方
    public string Notation;         // 记谱字符串
}