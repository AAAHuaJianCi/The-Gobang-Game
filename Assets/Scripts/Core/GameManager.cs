using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏管理器
/// 管理游戏流程、状态、模式切换、AI调用等核心逻辑
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }// 单例模式

    [Header("玩家配置")]
    public int playerBlack = 1;   // 黑棋玩家（默认人类）
    public int playerWhite = 2;   // 白棋玩家（AI默认）

    // 游戏状态枚举
    public enum GameState { Ready, Playing, End }

    // 游戏模式枚举
    public enum GameMode { PVP, PVE }

    public GameState curState { get; private set; } // 当前游戏状态
    public GameMode curMode { get; private set; }   // 当前游戏模式
    public int curPlayer { get; private set; }      // 当前落子玩家
    public int Winner { get; private set; }         // 获胜者

    private BoardManager _boardMgr;
    private AIManager _aiMgr;
    private Coroutine _aiMoveCoroutine; // AI落子协程

    private void Awake()
    {
        // 单例模式正确实现
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 获取组件引用
        _boardMgr = GetComponent<BoardManager>();
        _aiMgr = GetComponent<AIManager>();
    }

    void Start()
    {
        curState = GameState.Ready; // 初始状态为准备
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame(GameMode mode)
    {
        // 停止未完成的AI协程
        if (_aiMoveCoroutine != null)
        {
            StopCoroutine(_aiMoveCoroutine);
            _aiMoveCoroutine = null;
        }

        curMode = mode;
        curState = GameState.Playing;
        curPlayer = playerBlack; // 黑棋先落子
        Winner = 0;

        // 重置棋盘
        _boardMgr.ResetBoard();
        UIManager.Instance.RefreshGameUI();
    }

    /// <summary>
    /// 玩家落子（对外接口）
    /// </summary>
    public void PlayerPlaceChess(int x, int y)
    {
        // 校验：游戏中 + 玩家回合（非AI回合）
        if (curState != GameState.Playing || (curMode == GameMode.PVE && curPlayer == _aiMgr.aiType))
        {
            return;
        }
        ExecutePlaceChess(x, y, curPlayer);
    }

    /// <summary>
    /// 执行落子逻辑（内部）
    /// </summary>
    private void ExecutePlaceChess(int x, int y, int chessType)
    {
        if (!_boardMgr.PlaceChess(x, y, chessType)) return;

        // 更新UI
        UIManager.Instance.SpawnChess(x, y, chessType);

        // 检查获胜
        if (_boardMgr.CheckWin(x, y, chessType))
        {
            EndGame(chessType);
            return;
        }

        // 切换回合
        SwitchTurn();
    }

    /// <summary>
    /// 切换回合
    /// </summary>
    private void SwitchTurn()
    {
        curPlayer = curPlayer == playerBlack ? playerWhite : playerBlack;
        UIManager.Instance.RefreshTurnText();

        // PVE模式下，AI回合自动落子
        if (curState == GameState.Playing && curMode == GameMode.PVE && curPlayer == _aiMgr.aiType)
        {
            if (_aiMoveCoroutine != null)
            {
                StopCoroutine(_aiMoveCoroutine);
            }
            _aiMoveCoroutine = StartCoroutine(AiMoveCoroutine());
        }
    }

    private IEnumerator AiMoveCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (curState != GameState.Playing) yield break;

        Vector2Int aiPos = _aiMgr.GetBestMove();
        // 兜底：空棋盘强制下中心
        if (_boardMgr.IsBoardEmpty())
        {
            aiPos = new Vector2Int(_boardMgr.boardSize / 2, _boardMgr.boardSize / 2);
        }
        if (aiPos.x >= 0 && aiPos.y >= 0)
        {
            ExecutePlaceChess(aiPos.x, aiPos.y, curPlayer);
        }
    }

    /// <summary>
    /// 结束游戏
    /// </summary>
    private void EndGame(int winPlayer)
    {
        curState = GameState.End;
        Winner = winPlayer;
        UIManager.Instance.ShowResultPanel(winPlayer);

        // 停止AI协程
        if (_aiMoveCoroutine != null)
        {
            StopCoroutine(_aiMoveCoroutine);
            _aiMoveCoroutine = null;
        }
    }

    /// <summary>
    /// 撤销上一步
    /// </summary>
    public void UndoLastMove()
    {
        if (curState != GameState.Playing) return;

        if (_boardMgr.UndoMove())
        {
            UIManager.Instance.RemoveLastChess();
            SwitchTurn();

            // 撤销后停止AI协程
            if (_aiMoveCoroutine != null)
            {
                StopCoroutine(_aiMoveCoroutine);
                _aiMoveCoroutine = null;
            }
        }
    }

    /// <summary>
    /// 重启游戏
    /// </summary>
    public void RestartGame()
    {
        StartGame(curMode);
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void BackToMainMenu()
    {
        curState = GameState.Ready;
        _boardMgr.ResetBoard();
        UIManager.Instance.ShowMainPanel();

        // 停止AI协程
        if (_aiMoveCoroutine != null)
        {
            StopCoroutine(_aiMoveCoroutine);
            _aiMoveCoroutine = null;
        }
    }

    /// <summary>
    /// 设置AI难度（对外接口）
    /// </summary>
    public void SetAIDifficulty(AIManager.Difficulty diff)
    {
        if (_aiMgr != null)
        {
            _aiMgr.SetDifficulty(diff);
        }
    }
}