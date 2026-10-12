using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class SavedGamesUI : MonoBehaviour
{
    public static SavedGamesUI Instance;

    [Header("UI引用")]
    public GameObject SavedGamesPanel;          // 收藏列表面板
    public Transform Content;                    // ScrollView的Content
    public GameObject SavedGameItemPrefab;      // 列表项预制体
    public Button CloseButton;                   // 关闭按钮
    public TextMeshProUGUI EmptyHintText;        // 空列表提示
    public Button ReturnMenuButton;     // 新增：返回主菜单按钮

    [Header("其他引用")]
    public ReplayManager replayManager;

    // 去掉 public 的 saveManager 字段，改用属性获取
    private GameSaveManager SaveManager
    {
        get { return GameSaveManager.Instance; }
    }
    [Header("场景名称")]
    public static string MainMenuSceneName = "MainMenu";

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        CloseButton?.onClick.AddListener(OnClosePanel);
        ReturnMenuButton?.onClick.AddListener(OnReturnToMenu);

        if (SavedGamesPanel != null)
            SavedGamesPanel.SetActive(false);
    }
    /// <summary>
    /// 关闭面板（回到游戏）
    /// </summary>
    void OnClosePanel()
    {
        //SavedGamesPanel?.SetActive(false);
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void OnReturnToMenu()
    {
        MainMenuManager.OpenSavedGamesOnStart = false;
        SavedGamesPanel?.SetActive(false);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    /// <summary>
    /// 显示收藏列表
    /// </summary>
    public void ShowSavedGames()
    {
        if (SavedGamesPanel == null)
        {
            Debug.LogError("SavedGamesPanel 未绑定！");
            return;
        }

        SavedGamesPanel.SetActive(true);
        RefreshList();
    }

    /// <summary>
    /// 刷新列表
    /// </summary>
    void RefreshList()
    {
        // 清除旧列表
        for (int i = Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Content.GetChild(i).gameObject);
        }

        List<SavedGame> games = SaveManager?.GetAllSavedGames();

        // 空列表提示
        if (EmptyHintText != null)
        {
            EmptyHintText.gameObject.SetActive(games == null || games.Count == 0);
        }

        if (games == null) return;

        foreach (var game in games)
        {
            GameObject item = Instantiate(SavedGameItemPrefab, Content);

            // 设置显示内容
            item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>()?.SetText(game.GameName);
            item.transform.Find("TimeText")?.GetComponent<TextMeshProUGUI>()?.SetText(game.SaveTime);

            string resultStr = game.Winner switch
            {
                "Red" => "红方胜",
                "Black" => "黑方胜",
                "Stalemate" => "和棋",
                _ => "未知"
            };
            item.transform.Find("ResultText")?.GetComponent<TextMeshProUGUI>()?.SetText(resultStr);
            item.transform.Find("MovesText")?.GetComponent<TextMeshProUGUI>()?.SetText($"{game.TotalMoves}步");

            // 绑定按钮事件
            string gameId = game.GameId; // 捕获到局部变量

            Button playBtn = item.transform.Find("PlayButton")?.GetComponent<Button>();
            if (playBtn != null)
            {
                playBtn.onClick.AddListener(() => OnPlayGame(gameId));
            }

            Button deleteBtn = item.transform.Find("DeleteButton")?.GetComponent<Button>();
            if (deleteBtn != null)
            {
                deleteBtn.onClick.AddListener(() => OnDeleteGame(gameId));
            }
        }

        // 调整Content高度
        float height = games.Count * 100f + 20f;
        RectTransform rt = Content.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
    }

    /// <summary>
    /// 点击回放按钮
    /// </summary>
    void OnPlayGame(string gameId)
    {
        SavedGame game = SaveManager?.GetGameById(gameId);
        Debug.Log("播放棋局："+gameId);
        if (game == null) return;

        // 关闭列表
        SavedGamesPanel?.SetActive(false);

        // 转换数据并开始回放
        List<MoveRecord> records = ConvertToMoveRecords(game.Moves);
        replayManager?.StartReplay(records, game.Winner);
    }

    /// <summary>
    /// 点击删除按钮
    /// </summary>
    void OnDeleteGame(string gameId)
    {
        SaveManager?.DeleteGame(gameId);
        RefreshList();
    }

    /// <summary>
    /// 将MoveData转换为MoveRecord
    /// </summary>
    List<MoveRecord> ConvertToMoveRecords(List<MoveData> moves)
    {
        List<MoveRecord> records = new List<MoveRecord>();

        foreach (var move in moves)
        {
            MoveRecord record = new MoveRecord();
            record.PieceSide = move.Side == "Red" ? ChessPiece.Side.Red : ChessPiece.Side.Black;
            record.PieceType = (ChessPiece.PieceType)System.Enum.Parse(typeof(ChessPiece.PieceType), move.PieceType);
            record.FromX = move.FromX;
            record.FromY = move.FromY;
            record.ToX = move.ToX;
            record.ToY = move.ToY;
            record.HasEatenPiece = move.HasEaten;

            if (move.HasEaten)
            {
                record.EatenType = (ChessPiece.PieceType)System.Enum.Parse(typeof(ChessPiece.PieceType), move.EatenPieceType);
                record.EatenSide = move.EatenSide == "Red" ? ChessPiece.Side.Red : ChessPiece.Side.Black;
            }

            records.Add(record);
        }

        return records;
    }
}
