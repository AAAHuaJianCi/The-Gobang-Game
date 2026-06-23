using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器
/// 统一管理UI面板、按钮事件、棋子生成等
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } // 单例模式

    [Header("面板引用")]
    public GameObject mainPanel; // 主菜单面板
    public GameObject gamePanel; // 游戏面板
    public GameObject resultPanel; // 结果面板
    public GameObject settingPanel; // 设置面板
    public GameObject difficultyPanel; // 难度选择面板

    [Header("游戏UI元素")]
    public Text turnTipText; // 当前回合提示文本
    public Text resultText;  // 结果文本
    public Transform chessParent; // 棋子父物体（棋盘RectTransform）
    public GameObject blackChessPrefab; // 黑棋预制体
    public GameObject whiteChessPrefab; // 白棋预制体

    [Header("输入配置")]
    public BoardInput boardInput;

    private List<GameObject> _chessList = new List<GameObject>(); // 生成的棋子列表
    private Dictionary<string, GameObject> _panelDict; // 面板字典

    private void Awake()
    {
        // 单例模式正确实现
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化面板字典
        InitPanelDict();
        // 校验引用
        ValidateReferences();
    }

    void Start()
    {
        ShowMainPanel(); // 初始显示主菜单
    }

    #region 面板管理
    private void InitPanelDict()
    {
        _panelDict = new Dictionary<string, GameObject>
        {
            { "Main", mainPanel },
            { "Game", gamePanel },
            { "Result", resultPanel },
            { "Setting", settingPanel }
        };
    }

    public void ShowMainPanel()
    {
        ShowPanel("Main");
        ClearAllChess();
    }

    public void ShowGamePanel()
    {
        ShowPanel("Game");
    }

    /// <summary>
    /// 显示指定面板（隐藏其他）
    /// </summary>
    private void ShowPanel(string panelKey)
    {
        foreach (var panel in _panelDict.Values)
        {
            if (panel != null) panel.SetActive(false);
        }

        if (_panelDict.ContainsKey(panelKey) && _panelDict[panelKey] != null)
        {
            _panelDict[panelKey].SetActive(true);
        }
        else
        {
            Debug.LogError($"未找到面板Key：{panelKey} 或面板为空！");
        }
    }
    #endregion

    #region 按钮事件
    /// <summary>
    /// 开始双人对战
    /// </summary>
    public void OnClickStartPVP()
    {
        ShowGamePanel();
        GameManager.Instance.StartGame(GameManager.GameMode.PVP);
    }

    /// <summary>
    /// 人机对战（打开难度选择）
    /// </summary>
    public void OnClickPVE()
    {
        difficultyPanel.SetActive(true);
    }

    /// <summary>
    /// 选择简单难度
    /// </summary>
    public void OnSelectEasy()
    {
        GameManager.Instance.SetAIDifficulty(AIManager.Difficulty.Easy);
        difficultyPanel.SetActive(false);
        ShowGamePanel();
        GameManager.Instance.StartGame(GameManager.GameMode.PVE);
    }

    /// <summary>
    /// 选择普通难度
    /// </summary>
    public void OnSelectNormal()
    {
        GameManager.Instance.SetAIDifficulty(AIManager.Difficulty.Normal);
        difficultyPanel.SetActive(false);
        ShowGamePanel();
        GameManager.Instance.StartGame(GameManager.GameMode.PVE);
    }

    /// <summary>
    /// 选择困难难度
    /// </summary>
    public void OnSelectHard()
    {
        GameManager.Instance.SetAIDifficulty(AIManager.Difficulty.Hard);
        difficultyPanel.SetActive(false);
        ShowGamePanel();
        GameManager.Instance.StartGame(GameManager.GameMode.PVE);
    }

    /// <summary>
    /// 撤销按钮
    /// </summary>
    public void OnClickUndo()
    {
        GameManager.Instance?.UndoLastMove();
    }

    /// <summary>
    /// 重启按钮
    /// </summary>
    public void OnClickRestart()
    {
        GameManager.Instance?.RestartGame();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void OnClickBackMenu()
    {
        GameManager.Instance?.BackToMainMenu();
    }

    /// <summary>
    /// 打开设置
    /// </summary>
    public void OnClickOpenSetting()
    {
        settingPanel.SetActive(true);
    }

    /// <summary>
    /// 关闭设置
    /// </summary>
    public void OnClickCloseSetting()
    {
        settingPanel.SetActive(false);
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 选择AI难度（通用接口）
    /// </summary>
    public void OnSelectAIDifficulty(int diffIdx)
    {
        AIManager.Difficulty diff = (AIManager.Difficulty)diffIdx;
        GameManager.Instance.SetAIDifficulty(diff);
    }
    #endregion

    #region 棋子生成/销毁
    /// <summary>
    /// 在指定坐标生成棋子UI
    /// </summary>
    /// <summary>
    /// 在指定棋盘坐标生成棋子UI（与BoardInput坐标逻辑完全一致）
    /// </summary>
    public void SpawnChess(int x, int y, int chessType)
    {
        // 空值校验
        if (chessParent == null || boardInput == null ||
            (chessType != GameManager.Instance.playerBlack && chessType != GameManager.Instance.playerWhite))
        {
            Debug.LogError("生成棋子失败：参数无效或依赖缺失");
            return;
        }

        // 直接复用BoardInput的cellSize和棋盘尺寸，保证两边计算基准完全一致
        float cellSize = boardInput.cellSize;
        int boardSize = boardInput.boardSize;

        // 获取棋盘父物体的RectTransform
        RectTransform boardRect = chessParent.GetComponent<RectTransform>();
        if (boardRect == null) return;

        // 棋盘半宽/半高（以中心为原点）
        float halfWidth = boardRect.rect.width / 2f;
        float halfHeight = boardRect.rect.height / 2f;

        // 坐标计算：和BoardInput点击转坐标的逻辑完全镜像
        float posX = x * cellSize - halfWidth;
        // Y轴：和BoardInput计算逻辑统一，若上下颠倒则加负号
        float posY = y * cellSize - halfHeight;

        // 生成棋子
        GameObject chessPrefab = chessType == GameManager.Instance.playerBlack ? blackChessPrefab : whiteChessPrefab;
        GameObject chess = Instantiate(chessPrefab, chessParent);
        RectTransform chessRect = chess.GetComponent<RectTransform>();

        if (chessRect != null)
        {
            // 强制设置锚点和中心点，确保棋子中心精准对齐交叉点
            chessRect.anchorMin = new Vector2(0.5f, 0.5f);
            chessRect.anchorMax = new Vector2(0.5f, 0.5f);
            chessRect.pivot = new Vector2(0.5f, 0.5f);
            chessRect.anchoredPosition = new Vector2(posX, posY);
            // 强制设置棋子大小（默认是格子的80%，可自行调整比例）
            chessRect.sizeDelta = new Vector2(cellSize * 0.8f, cellSize * 0.8f);
        }
        _chessList.Add(chess);
    }

    /// <summary>
    /// 移除最后生成的棋子
    /// </summary>
    public void RemoveLastChess()
    {
        if (_chessList.Count == 0) return;

        GameObject lastChess = _chessList[_chessList.Count - 1];
        _chessList.RemoveAt(_chessList.Count - 1);
        Destroy(lastChess);
    }

    /// <summary>
    /// 清空所有棋子
    /// </summary>
    public void ClearAllChess()
    {
        foreach (GameObject chess in _chessList)
        {
            Destroy(chess);
        }
        _chessList.Clear();
    }
    #endregion

    #region UI更新
    /// <summary>
    /// 刷新游戏UI（清空棋子+更新回合+显示游戏面板）
    /// </summary>
    public void RefreshGameUI()
    {
        ClearAllChess();
        RefreshTurnText();
        ShowPanel("Game");
    }

    /// <summary>
    /// 刷新回合提示文本
    /// </summary>
    public void RefreshTurnText()
    {
        if (turnTipText == null || GameManager.Instance == null) return;

        int curPlayer = GameManager.Instance.curPlayer;
        turnTipText.text = curPlayer == GameManager.Instance.playerBlack
            ? "当前回合：黑棋"
            : "当前回合：白棋";
    }

    /// <summary>
    /// 显示结果面板
    /// </summary>
    public void ShowResultPanel(int winner)
    {
        if (resultText == null || GameManager.Instance == null) return;

        resultText.text = winner == GameManager.Instance.playerBlack
            ? "黑棋获胜！"
            : "白棋获胜！";

        ShowPanel("Result");
    }
    #endregion

    #region 工具方法
    /// <summary>
    /// 校验关键引用（避免空引用错误）
    /// </summary>
    private void ValidateReferences()
    {
        if (chessParent == null)
        {
            Debug.LogError("UIManager：chessParent 未赋值！请在Inspector中指定！");
        }
        if (blackChessPrefab == null || whiteChessPrefab == null)
        {
            Debug.LogError("UIManager：棋子预制体未赋值！请在Inspector中指定！");
        }
        if (boardInput == null)
        {
            Debug.LogWarning("UIManager：boardInput 未赋值！请在Inspector中指定！");
        }
    }
    #endregion
}