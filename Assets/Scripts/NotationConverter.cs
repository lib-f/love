using UnityEngine;

public static class NotationConverter
{
    // 红方数字（中文大写）
    static readonly string[] redNumbers = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    // 黑方数字（阿拉伯数字）
    static readonly string[] blackNumbers = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    // 获取棋子名称
    static string GetPieceName(ChessPiece.PieceType type, ChessPiece.Side side)
    {
        switch (type)
        {
            case ChessPiece.PieceType.Jiang:
                return side == ChessPiece.Side.Red ? "帅" : "将";
            case ChessPiece.PieceType.Shi:
                return side == ChessPiece.Side.Red ? "仕" : "士";
            case ChessPiece.PieceType.Xiang:
                return side == ChessPiece.Side.Red ? "相" : "象";
            case ChessPiece.PieceType.Ma:
                return "马";
            case ChessPiece.PieceType.Ju:
                return "车";
            case ChessPiece.PieceType.Pao:
                return "炮";
            case ChessPiece.PieceType.Zu:
                return side == ChessPiece.Side.Red ? "兵" : "卒";
            default:
                return "?";
        }
    }

    // 转换为标准棋谱
    public static string ToNotation(ChessPiece piece, int fromX, int fromY, int toX, int toY, bool hasEaten)
    {
        return ToNotation(piece.Type, piece.PlayerSide, fromX, fromY, toX, toY, hasEaten);
    }

    // 根据棋子类型、阵营和坐标生成走法记录
    public static string ToNotation(ChessPiece.PieceType type, ChessPiece.Side side, int fromX, int fromY, int toX, int toY, bool hasEaten)
    {
        string pieceName = GetPieceName(type, side);
        string result = pieceName;

        int dx = toX - fromX;
        int dy = toY - fromY;

        // 获取列号：红方从右到左为一至九，黑方从左到右为1至9
        int colNumber = GetColumnNumber(side, fromX);

        if (side == ChessPiece.Side.Red)
            result += redNumbers[colNumber - 1];
        else
            result += blackNumbers[colNumber - 1];

        // 判断移动方向
        if (dx == 0)
        {
            if (side == ChessPiece.Side.Red)
                result += dy < 0 ? "进" : "退";
            else
                result += dy > 0 ? "进" : "退";

            int steps = Mathf.Abs(dy);
            if (side == ChessPiece.Side.Red)
                result += redNumbers[steps - 1];
            else
                result += blackNumbers[steps - 1];
        }
        else if (dy == 0)
        {
            result += "平";
            int targetCol = GetColumnNumber(side, toX);
            if (side == ChessPiece.Side.Red)
                result += redNumbers[targetCol - 1];
            else
                result += blackNumbers[targetCol - 1];
        }
        else
        {
            if (side == ChessPiece.Side.Red)
                result += dy < 0 ? "进" : "退";
            else
                result += dy > 0 ? "进" : "退";

            int targetCol = GetColumnNumber(side, toX);
            if (side == ChessPiece.Side.Red)
                result += redNumbers[targetCol - 1];
            else
                result += blackNumbers[targetCol - 1];
        }

        return result;
    }

    // 获取列号：红方从右到左为一至九，黑方从左到右为1至9
    static int GetColumnNumber(ChessPiece.Side side, int gridX)
    {
        if (side == ChessPiece.Side.Red)
        {
            return 9 - gridX;
        }

        return gridX + 1;
    }
}
