using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIManager : MonoBehaviour
{
    public enum Difficulty { Easy, Normal, Hard }
    [Header("AI配置")]
    public Difficulty curDifficulty = Difficulty.Normal;
    public int aiType = 2;       // AI棋子类型，默认黑棋
    public int playerType = 1;   // 玩家棋子类型，默认白棋
    [Header("难度参数(Hard模式生效)")]
    public int hardMinDepth = 3; // 困难模式最小搜索深度
    public int hardMaxDepth = 6; // 困难模式最大搜索深度

    // 评分权重：强化公式化棋型的分值，突出五子棋经典公式优先级
    private const int SCORE_WIN = 100000;    // 连五（胜利）
    private const int SCORE_LIVE4 = 15000;   // 活四（无阻挡，必赢）
    private const int SCORE_RUSH4 = 8000;    // 冲四（一侧挡，一侧空）
    private const int SCORE_LIVE3 = 5000;    // 活三（无阻挡，可发展活四）
    private const int SCORE_SLEEP3 = 1000;   // 眠三（一侧挡）
    private const int SCORE_LIVE2 = 500;     // 活二（无阻挡）
    private const int SCORE_SLEEP2 = 100;    // 眠二（一侧挡）
    private const int SCORE_TWO_LIVE3 = 20000; // 两活三（核心公式，优先级高于单活四）
    private const int SCORE_LIVE3_RUSH4 = 18000; // 活三+冲四（公式组合）
    private const int SCORE_DEFAULT = 1;     // 普通点位
    private const int SCORE_CENTER_BONUS = 50; // 中心奖励（15x15棋盘7,7）
    private const int SCORE_THREAT_MULTIPLIER = 2; // 对手威胁评分放大倍数

    private BoardManager _boardMgr;
    private int _boardSize;
    private Vector2Int _boardCenter; // 棋盘中心点

    void Awake()
    {
        _boardMgr = GetComponent<BoardManager>();
        _boardSize = _boardMgr.boardSize;
        _boardCenter = new Vector2Int(_boardSize / 2, _boardSize / 2);
    }

    public Vector2Int GetBestMove()
    {
        switch (curDifficulty)
        {
            case Difficulty.Easy:
                return GetEasyMove();      // 简单：优先自身赢+优先堵玩家活三
            case Difficulty.Normal:
                return GetNormalMove();    // 正常：全局最优胜利位置
            case Difficulty.Hard:
                return GetHardMove();      // 困难：公式化下棋+精准防反+主动造威胁
            default:
                return GetNormalMove();
        }
    }

    #region 不同难度的决策逻辑（核心修改区）
    /// <summary>
    /// 简单难度：优先自身赢 → 优先堵玩家活三/活四/冲四 → 占中心区域
    /// </summary>
    private Vector2Int GetEasyMove()
    {
        List<Vector2Int> emptyPos = GetAllEmptyPos();
        if (emptyPos.Count == 0) return new Vector2Int(-1, -1);

        // 1. 空棋盘优先占中心
        if (emptyPos.Count == _boardSize * _boardSize)
        {
            return _boardCenter;
        }

        // === 优先级1：自身赢（连五/活四）===
        Vector2Int aiWin = GetBlockPos(aiType, SCORE_WIN);
        if (aiWin.x != -1) return aiWin;
        Vector2Int aiLive4 = GetBlockPos(aiType, SCORE_LIVE4);
        if (aiLive4.x != -1) return aiLive4;

        // === 优先级2：优先堵玩家活三（核心需求）===
        Vector2Int blockPlayerLive3 = GetBlockPos(playerType, SCORE_LIVE3);
        if (blockPlayerLive3.x != -1) return blockPlayerLive3;

        // === 优先级3：堵玩家能赢的棋型（活四/冲四/连五）===
        Vector2Int blockPlayerWin = GetBlockPos(playerType, SCORE_WIN);
        if (blockPlayerWin.x != -1) return blockPlayerWin;
        Vector2Int blockPlayerLive4 = GetBlockPos(playerType, SCORE_LIVE4);
        if (blockPlayerLive4.x != -1) return blockPlayerLive4;
        Vector2Int blockPlayerRush4 = GetBlockPos(playerType, SCORE_RUSH4);
        if (blockPlayerRush4.x != -1) return blockPlayerRush4;

        // === 优先级4：自身冲四 ===
        Vector2Int aiRush4 = GetBlockPos(aiType, SCORE_RUSH4);
        if (aiRush4.x != -1) return aiRush4;

        // 无威胁时优先占中心区域
        List<Vector2Int> centerAreaPos = new List<Vector2Int>();
        foreach (var pos in emptyPos)
        {
            if (Mathf.Abs(pos.x - _boardCenter.x) <= 4 && Mathf.Abs(pos.y - _boardCenter.y) <= 4)
            {
                centerAreaPos.Add(pos);
            }
        }
        if (centerAreaPos.Count > 0)
        {
            int randomIdx = UnityEngine.Random.Range(0, centerAreaPos.Count);
            return centerAreaPos[randomIdx];
        }
        int globalRandomIdx = UnityEngine.Random.Range(0, emptyPos.Count);
        return emptyPos[globalRandomIdx];
    }

    /// <summary>
    /// 正常难度：全局最优 → 综合自身发展+对手威胁+全局布局，选胜率最高的位置
    /// </summary>
    private Vector2Int GetNormalMove()
    {
        List<Vector2Int> emptyPos = GetAllEmptyPos();
        if (emptyPos.Count == 0) return new Vector2Int(-1, -1);

        int maxGlobalScore = int.MinValue;
        Vector2Int bestGlobalPos = new Vector2Int(-1, -1);

        foreach (var pos in emptyPos)
        {
            // 1. 模拟落子：先判断是否能直接赢（优先级最高）
            int aiWinScore = SimulateCalcScore(pos.x, pos.y, aiType);
            if (aiWinScore >= SCORE_WIN) return pos;

            // 2. 模拟对手落子：堵对手必赢点（优先级第二）
            int playerWinScore = SimulateCalcScore(pos.x, pos.y, playerType) * SCORE_THREAT_MULTIPLIER;
            if (playerWinScore >= SCORE_WIN) return pos;

            // 3. 计算全局得分：综合「自身发展+对手威胁+中心权重+后续潜力」
            // 自身发展得分（权重2）：活三/冲四/活二的长期价值
            int aiDevelopScore = CalcGlobalDevelopScore(pos, aiType) * 2;
            // 对手威胁得分（权重3）：优先堵对手高威胁
            int playerThreatScore = CalcGlobalDevelopScore(pos, playerType) * 3 * SCORE_THREAT_MULTIPLIER;
            // 中心奖励：强化全局布局的中心倾向
            int centerBonus = GetCenterBonus(pos.x, pos.y);
            // 后续潜力分：该点位能衍生的公式化棋型（活三、冲四等）
            int potentialScore = CalcPotentialScore(pos, aiType);

            // 全局总分 = 自身发展 + 对手威胁 + 中心奖励 + 后续潜力
            int totalGlobalScore = aiDevelopScore + playerThreatScore + centerBonus + potentialScore;

            // 更新全局最优位置
            if (totalGlobalScore > maxGlobalScore)
            {
                maxGlobalScore = totalGlobalScore;
                bestGlobalPos = pos;
            }
        }

        // 兜底：防止无得分时选中心
        if (bestGlobalPos.x == -1 && emptyPos.Count > 0)
        {
            bestGlobalPos = _boardCenter;
        }

        return bestGlobalPos;
    }

    /// <summary>
    /// 最高难度：公式化下棋（两活三/活三冲四为基操）+ 精准防反 + 活四造威胁
    /// </summary>
    private Vector2Int GetHardMove()
    {
        List<Vector2Int> emptyPos = GetAllEmptyPos();
        if (emptyPos.Count == 0) return new Vector2Int(-1, -1);

        int maxScore = int.MinValue;
        Vector2Int bestPos = new Vector2Int(-1, -1);
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        // 动态搜索深度：棋盘越满，搜索越深（精准度越高）
        int emptyCount = emptyPos.Count;
        float fillRatio = 1 - (float)emptyCount / (_boardSize * _boardSize);
        int dynamicDepth;
        if (fillRatio < 0.3f)
            dynamicDepth = hardMinDepth;
        else if (fillRatio < 0.7f)
            dynamicDepth = Mathf.RoundToInt((hardMinDepth + hardMaxDepth) * 0.75f);
        else
            dynamicDepth = hardMaxDepth;
        dynamicDepth = Mathf.Clamp(dynamicDepth, hardMinDepth, hardMaxDepth);

        foreach (var pos in emptyPos)
        {
            // === 第一步：检测五子棋核心公式（优先级最高） ===
            // 1. 两活三（核心公式，优先级高于单活四）
            if (CheckTwoLive3(pos, aiType))
            {
                _boardMgr.PlaceChess(pos.x, pos.y, aiType);
                int twoLive3Score = SCORE_TWO_LIVE3;
                _boardMgr.ForceResetPos(pos.x, pos.y);
                if (twoLive3Score > maxScore)
                {
                    maxScore = twoLive3Score;
                    bestPos = pos;
                    continue; // 两活三直接优先选
                }
            }

            // 2. 活三+冲四（公式组合，优先级第二）
            if (CheckLive3Rush4(pos, aiType))
            {
                _boardMgr.PlaceChess(pos.x, pos.y, aiType);
                int live3Rush4Score = SCORE_LIVE3_RUSH4;
                _boardMgr.ForceResetPos(pos.x, pos.y);
                if (live3Rush4Score > maxScore)
                {
                    maxScore = live3Rush4Score;
                    bestPos = pos;
                    continue;
                }
            }

            // === 第二步：Minimax+α-β剪枝搜索（全局深度评估） ===
            // 模拟AI落子
            _boardMgr.PlaceChess(pos.x, pos.y, aiType);
            // Minimax：AI是Max节点，玩家是Min节点
            int score = Minimax(pos.x, pos.y, dynamicDepth - 1, false, alpha, beta);
            _boardMgr.ForceResetPos(pos.x, pos.y);

            // === 第三步：特殊逻辑：活四先造威胁（恶心玩家） ===
            // 若当前点位能形成活四，且玩家无即时赢面 → 优先选活四（让玩家必须堵，同时造其他威胁）
            int aiLive4Score = SimulateCalcScore(pos.x, pos.y, aiType);
            if (aiLive4Score >= SCORE_LIVE4 && !IsPlayerHasWinThreat())
            {
                score += 5000; // 给活四点额外加分，优先选
            }

            // === 第四步：玩家有赢面时优先堵 ===
            int playerThreatScore = SimulateCalcScore(pos.x, pos.y, playerType);
            if (playerThreatScore >= SCORE_RUSH4) // 玩家有冲四/活三/活四等赢面
            {
                score += 8000; // 堵点额外加分，优先选
            }

            // 叠加中心奖励
            score += GetCenterBonus(pos.x, pos.y);

            // 更新最优位置
            if (score > maxScore)
            {
                maxScore = score;
                bestPos = pos;
            }
            alpha = Math.Max(alpha, score);
            if (beta <= alpha) break; // α-β剪枝，提升性能
        }

        // 兜底：选中心
        if (bestPos.x == -1 && emptyPos.Count > 0)
        {
            bestPos = _boardCenter;
        }

        return bestPos;
    }
    #endregion

    #region 新增：五子棋公式检测+全局评分辅助方法
    /// <summary>
    /// 检测两活三：落子后形成两个独立的活三（五子棋核心公式）
    /// </summary>
    private bool CheckTwoLive3(Vector2Int pos, int chessType)
    {
        _boardMgr.PlaceChess(pos.x, pos.y, chessType);
        int live3Count = 0;
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

        // 检测四个方向的活三数量
        for (int i = 0; i < 4; i++)
        {
            int dirScore = CalcDirScore(pos.x, pos.y, dirs[i, 0], dirs[i, 1], chessType);
            if (dirScore >= SCORE_LIVE3)
            {
                live3Count++;
            }
        }

        _boardMgr.ForceResetPos(pos.x, pos.y);
        return live3Count >= 2; // 两个及以上活三
    }

    /// <summary>
    /// 检测活三+冲四：落子后同时有活三和冲四（公式组合）
    /// </summary>
    private bool CheckLive3Rush4(Vector2Int pos, int chessType)
    {
        _boardMgr.PlaceChess(pos.x, pos.y, chessType);
        bool hasLive3 = false;
        bool hasRush4 = false;
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

        for (int i = 0; i < 4; i++)
        {
            int dirScore = CalcDirScore(pos.x, pos.y, dirs[i, 0], dirs[i, 1], chessType);
            if (dirScore >= SCORE_LIVE3) hasLive3 = true;
            if (dirScore >= SCORE_RUSH4) hasRush4 = true;
        }

        _boardMgr.ForceResetPos(pos.x, pos.y);
        return hasLive3 && hasRush4;
    }

    /// <summary>
    /// 计算全局发展得分（正常难度用）：评估点位的长期发展价值
    /// </summary>
    private int CalcGlobalDevelopScore(Vector2Int pos, int chessType)
    {
        _boardMgr.PlaceChess(pos.x, pos.y, chessType);
        int totalScore = 0;
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

        for (int i = 0; i < 4; i++)
        {
            totalScore += CalcDirScore(pos.x, pos.y, dirs[i, 0], dirs[i, 1], chessType);
        }

        _boardMgr.ForceResetPos(pos.x, pos.y);
        return totalScore == 0 ? SCORE_DEFAULT : totalScore;
    }

    /// <summary>
    /// 计算后续潜力得分（正常难度用）：评估点位衍生公式化棋型的潜力
    /// </summary>
    private int CalcPotentialScore(Vector2Int pos, int chessType)
    {
        int potential = 0;
        // 若落子后能形成活三，加潜力分
        if (SimulateCalcScore(pos.x, pos.y, chessType) >= SCORE_LIVE3)
        {
            potential += 1000;
        }
        // 若落子后能形成冲四，加潜力分
        if (SimulateCalcScore(pos.x, pos.y, chessType) >= SCORE_RUSH4)
        {
            potential += 2000;
        }
        return potential;
    }

    /// <summary>
    /// 检测玩家是否有赢面威胁（困难难度用）
    /// </summary>
    private bool IsPlayerHasWinThreat()
    {
        // 玩家有活四/冲四/连五 → 有赢面
        Vector2Int playerWin = GetBlockPos(playerType, SCORE_WIN);
        Vector2Int playerLive4 = GetBlockPos(playerType, SCORE_LIVE4);
        Vector2Int playerRush4 = GetBlockPos(playerType, SCORE_RUSH4);
        return playerWin.x != -1 || playerLive4.x != -1 || playerRush4.x != -1;
    }
    #endregion

    #region 算法核心逻辑（评分/Minimax）
    /// <summary>
    /// 模拟落子计算得分（含威胁放大）
    /// </summary>
    private int SimulateCalcScore(int x, int y, int chessType)
    {
        if (!_boardMgr.IsInBoard(x, y)) return 0;

        _boardMgr.PlaceChess(x, y, chessType);
        int score = CalcPointScore(x, y, chessType, chessType == aiType);
        _boardMgr.ForceResetPos(x, y);

        return score;
    }

    /// <summary>
    /// 计算点位总分（含AI/玩家权重）
    /// </summary>
    private int CalcPointScore(int x, int y, int chessType, bool isAI)
    {
        if (!_boardMgr.IsInBoard(x, y)) return 0;

        int totalScore = 0;
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

        for (int i = 0; i < 4; i++)
        {
            totalScore += CalcDirScore(x, y, dirs[i, 0], dirs[i, 1], chessType);
        }

        // 基础分兜底 + AI/玩家权重区分
        totalScore = totalScore == 0 ? SCORE_DEFAULT : totalScore;
        return isAI ? totalScore : -totalScore * SCORE_THREAT_MULTIPLIER;
    }

    /// <summary>
    /// 计算单方向得分（棋型判断核心）
    /// </summary>
    private int CalcDirScore(int x, int y, int dx, int dy, int chessType)
    {
        int count = 1;
        int emptyLeft = 0;
        int emptyRight = 0;
        bool blockLeft = false;
        bool blockRight = false;

        // 右/下统计
        int nx = x + dx;
        int ny = y + dy;
        while (_boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) == chessType)
        {
            count++;
            nx += dx;
            ny += dy;
        }
        while (_boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) == 0)
        {
            emptyRight++;
            nx += dx;
            ny += dy;
        }
        blockRight = _boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) != 0;

        // 左/上统计
        nx = x - dx;
        ny = y - dy;
        while (_boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) == chessType)
        {
            count++;
            nx -= dx;
            ny -= dy;
        }
        while (_boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) == 0)
        {
            emptyLeft++;
            nx -= dx;
            ny -= dy;
        }
        blockLeft = _boardMgr.IsInBoard(nx, ny) && _boardMgr.GetChessTpye(nx, ny) != 0;

        int totalEmpty = emptyLeft + emptyRight;
        bool isBlocked = blockLeft && blockRight;

        return GetScoreByShape(count, totalEmpty, isBlocked, blockLeft, blockRight);
    }

    /// <summary>
    /// Minimax算法（带α-β剪枝）：深度评估最优解
    /// </summary>
    private int Minimax(int x, int y, int depth, bool isMax, int alpha, int beta)
    {
        // 终止条件：深度为0 或 已分胜负
        bool isWin = _boardMgr.CheckWin(x, y, isMax ? aiType : playerType);
        if (depth == 0 || isWin)
        {
            int baseScore = CalcPointScore(x, y, isMax ? aiType : playerType, isMax);
            // 胜利得分放大（AI赢分更高，玩家赢扣分更多）
            return isWin ? (isMax ? SCORE_WIN * 10 : -SCORE_WIN * 20) : baseScore;
        }

        List<Vector2Int> emptyPos = GetAllEmptyPos();
        if (emptyPos.Count == 0) return 0;

        if (isMax)
        {
            // AI落子（Max节点）：最大化自身得分
            int maxEval = int.MinValue;
            foreach (var pos in emptyPos)
            {
                _boardMgr.PlaceChess(pos.x, pos.y, aiType);
                int eval = Minimax(pos.x, pos.y, depth - 1, false, alpha, beta);
                _boardMgr.ForceResetPos(pos.x, pos.y);

                maxEval = Math.Max(maxEval, eval);
                alpha = Math.Max(alpha, eval);
                if (beta <= alpha) break; // 剪枝
            }
            return maxEval;
        }
        else
        {
            // 玩家落子（Min节点）：最小化AI得分
            int minEval = int.MaxValue;
            foreach (var pos in emptyPos)
            {
                _boardMgr.PlaceChess(pos.x, pos.y, playerType);
                int eval = Minimax(pos.x, pos.y, depth - 1, true, alpha, beta);
                _boardMgr.ForceResetPos(pos.x, pos.y);

                minEval = Math.Min(minEval, eval);
                beta = Math.Min(beta, eval);
                if (beta <= alpha) break; // 剪枝
            }
            return minEval;
        }
    }

    /// <summary>
    /// 按棋型计算得分（核心判断）
    /// </summary>
    private int GetScoreByShape(int count, int totalEmpty, bool isBlocked, bool blockLeft, bool blockRight)
    {
        if (count >= 5) return SCORE_WIN;          // 连五

        switch (count)
        {
            case 4:
                if (totalEmpty >= 2 && !isBlocked) return SCORE_LIVE4;    // 活四
                if ((blockLeft || blockRight) && totalEmpty >= 1) return SCORE_RUSH4; // 冲四
                break;
            case 3:
                if (totalEmpty >= 2 && !isBlocked) return SCORE_LIVE3;    // 活三
                if ((blockLeft || blockRight) && totalEmpty == 1) return SCORE_SLEEP3; // 眠三
                break;
            case 2:
                if (totalEmpty >= 2 && !isBlocked) return SCORE_LIVE2;    // 活二
                if ((blockLeft || blockRight) && totalEmpty == 1) return SCORE_SLEEP2; // 眠二
                break;
        }

        return SCORE_DEFAULT; // 普通点位
    }
    #endregion

    #region 工具方法
    /// <summary>
    /// 获取需要封堵的点位（按得分阈值）
    /// </summary>
    private Vector2Int GetBlockPos(int targetType, int scoreThreshold)
    {
        List<Vector2Int> emptyPos = GetAllEmptyPos();
        int maxScore = 0;
        Vector2Int bestBlockPos = new Vector2Int(-1, -1);
        foreach (var pos in emptyPos)
        {
            int score = SimulateCalcScore(pos.x, pos.y, targetType);
            if (score >= scoreThreshold && score > maxScore)
            {
                maxScore = score;
                bestBlockPos = pos;
            }
        }
        return bestBlockPos;
    }

    /// <summary>
    /// 中心区域奖励分
    /// </summary>
    private int GetCenterBonus(int x, int y)
    {
        int centerRadius = 2;
        if (Mathf.Abs(x - _boardCenter.x) <= centerRadius && Mathf.Abs(y - _boardCenter.y) <= centerRadius)
        {
            return SCORE_CENTER_BONUS;
        }
        return 0;
    }

    /// <summary>
    /// 获取所有空点位
    /// </summary>
    private List<Vector2Int> GetAllEmptyPos()
    {
        List<Vector2Int> emptyPos = new List<Vector2Int>();
        for (int x = 0; x < _boardSize; x++)
        {
            for (int y = 0; y < _boardSize; y++)
            {
                if (_boardMgr.IsValidMove(x, y))
                {
                    emptyPos.Add(new Vector2Int(x, y));
                }
            }
        }
        return emptyPos;
    }

    /// <summary>
    /// 设置难度（同步调整搜索深度）
    /// </summary>
    public void SetDifficulty(Difficulty diff)
    {
        curDifficulty = diff;
        switch (diff)
        {
            case Difficulty.Hard:
                hardMinDepth = 4;  // 困难模式最小深度
                hardMaxDepth = 6;
                break;
            case Difficulty.Normal:
                hardMinDepth = 3;
                hardMaxDepth = 5;
                break;
            case Difficulty.Easy:
                hardMinDepth = 2;
                hardMaxDepth = 3;
                break;
        }
    }
    #endregion
}