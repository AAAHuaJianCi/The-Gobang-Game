using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 棋盘输入处理
/// 处理鼠标点击、坐标转换为棋盘落子坐标
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BoardInput : MonoBehaviour, IPointerClickHandler
{
    [Header("棋盘配置")]
    public int boardSize = 15; // 棋盘尺寸

    [HideInInspector] public float cellSize; // 每个格子的尺寸

    private RectTransform _boardRect;

    void Awake()
    {
        _boardRect = GetComponent<RectTransform>();
        // 计算格子尺寸（15x15棋盘有14个间隔）
        cellSize = _boardRect.rect.width / (boardSize - 1);
    }
    void Start()
    {
        // 启动后重新计算一次，确保尺寸读取正确
        cellSize = _boardRect.rect.width / (boardSize - 1);
    }

    /// <summary>
    /// 处理棋盘点击事件
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 校验：游戏中 + 玩家回合（非AI回合）
        if (GameManager.Instance == null ||
            GameManager.Instance.curState != GameManager.GameState.Playing ||
            (GameManager.Instance.curMode == GameManager.GameMode.PVE &&
             GameManager.Instance.curPlayer == GameManager.Instance.GetComponent<AIManager>().aiType))
        {
            return;
        }

        // 屏幕坐标转换为棋盘本地坐标
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _boardRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPos))
        {
            return;
        }

        // 本地坐标转棋盘格子索引
        float halfWidth = _boardRect.rect.width / 2f;
        float halfHeight = _boardRect.rect.height / 2f;
        int x = Mathf.RoundToInt((localPos.x + halfWidth) / cellSize);
        // 若棋盘背景底部为y=0、顶部为y=14，用这行
        int y = Mathf.RoundToInt((localPos.y + halfHeight) / cellSize);
        // 若棋盘背景顶部为y=0、底部为y=14，用这行（保留负号）
        // int y = Mathf.RoundToInt((-localPos.y + halfHeight) / cellSize);

        // 落子（校验坐标范围）
        if (x >= 0 && x < boardSize && y >= 0 && y < boardSize)
        {
            GameManager.Instance.PlayerPlaceChess(x, y);
        }
    }
}