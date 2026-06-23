using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盘数据管理器
/// 管理棋盘状态、落子/撤销、胜负判断等核心数据逻辑
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("棋盘配置")]
    public int boardSize = 15; // 棋盘大小（标准五子棋15x15）

    private int[,] _board; // 棋盘数据：0=空，1=黑棋，2=白棋
    private List<Vector2Int> _moveHistory; // 落子历史记录

    private void Awake()
    {
        _board = new int[boardSize, boardSize];
        _moveHistory = new List<Vector2Int>();
    }

    /// <summary>
    /// 检查坐标是否在棋盘内
    /// </summary>
    public bool IsInBoard(int x, int y)
    {
        return x >= 0 && x < boardSize && y >= 0 && y < boardSize;
    }

    /// <summary>
    /// 检查是否为有效落子（空位置+在棋盘内）
    /// </summary>
    public bool IsValidMove(int x, int y)
    {
        return IsInBoard(x, y) && _board[x, y] == 0;
    }

    /// <summary>
    /// 执行落子（会记录到历史）
    /// </summary>
    /// <returns>落子是否成功</returns>
    public bool PlaceChess(int x, int y, int chessType)
    {
        if (!IsValidMove(x, y)) return false;

        _board[x, y] = chessType;
        _moveHistory.Add(new Vector2Int(x, y));
        return true;
    }

    /// <summary>
    /// 手动重置指定位置（用于AI模拟落子，不修改历史）
    /// </summary>
    public void ForceResetPos(int x, int y)
    {
        if (IsInBoard(x, y))
        {
            _board[x, y] = 0;
        }
    }

    /// <summary>
    /// 撤销上一步落子
    /// </summary>
    /// <returns>撤销是否成功</returns>
    public bool UndoMove()
    {
        if (_moveHistory.Count == 0) return false;

        Vector2Int lastMove = _moveHistory[_moveHistory.Count - 1];
        if (IsInBoard(lastMove.x, lastMove.y))
        {
            _board[lastMove.x, lastMove.y] = 0;
            _moveHistory.RemoveAt(_moveHistory.Count - 1);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 判断是否获胜（连五）
    /// </summary>
    public bool CheckWin(int x, int y, int chessType)
    {
        if (!IsInBoard(x, y) || _board[x, y] != chessType) return false;

        // 四个方向：水平、垂直、正斜线、反斜线
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };
        for (int i = 0; i < 4; i++)
        {
            int count = 1;
            int dx = dirs[i, 0];
            int dy = dirs[i, 1];

            // 正向遍历
            int nx = x + dx;
            int ny = y + dy;
            while (IsInBoard(nx, ny) && _board[nx, ny] == chessType)
            {
                count++;
                nx += dx;
                ny += dy;
            }

            // 反向遍历
            nx = x - dx;
            ny = y - dy;
            while (IsInBoard(nx, ny) && _board[nx, ny] == chessType)
            {
                count++;
                nx -= dx;
                ny -= dy;
            }

            if (count >= 5) return true; // 连五获胜
        }
        return false;
    }

    /// <summary>
    /// 重置棋盘为初始状态
    /// </summary>
    public void ResetBoard()
    {
        System.Array.Clear(_board, 0, _board.Length);
        _moveHistory.Clear();
    }

    /// <summary>
    /// 获取指定位置的棋子类型
    /// </summary>
    public int GetChessTpye(int x, int y)
    {
        if (!IsInBoard(x, y)) return -1;
        return _board[x, y];
    }

    /// <summary>
    /// 判断是否为空棋盘
    /// </summary>
    public bool IsBoardEmpty()
    {
        return _moveHistory.Count == 0;
    }
}