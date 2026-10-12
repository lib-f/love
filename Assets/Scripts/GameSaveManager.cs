using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance;

    private string SaveFolderPath;
    private List<SavedGame> savedGames = new List<SavedGame>();

    void Awake()
    {
        // 单例模式，防止重复创建
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);  // 跨场景保持

        SaveFolderPath = Path.Combine(Application.persistentDataPath, "SavedGames");
        if (!Directory.Exists(SaveFolderPath))
        {
            Directory.CreateDirectory(SaveFolderPath);
        }

        LoadAllSavedGames();
    }

    /// <summary>
    /// 保存新棋局
    /// </summary>
    public void SaveGame(SavedGame game)
    {
        Debug.Log($"SaveGame 被调用，游戏名：{game.GameName}");
        string json = JsonUtility.ToJson(game, true);
        string filePath = Path.Combine(SaveFolderPath, game.GameId + ".json");
        File.WriteAllText(filePath, json);

        // 更新内存列表
        savedGames.RemoveAll(g => g.GameId == game.GameId);
        savedGames.Add(game);

        // 按时间倒序排列
        savedGames.Sort((a, b) => b.SaveTime.CompareTo(a.SaveTime));

        Debug.Log($"棋局已保存: {game.GameName}");
    }

    /// <summary>
    /// 删除棋局
    /// </summary>
    public void DeleteGame(string gameId)
    {
        string filePath = Path.Combine(SaveFolderPath, gameId + ".json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        savedGames.RemoveAll(g => g.GameId == gameId);
    }

    /// <summary>
    /// 加载所有保存的棋局
    /// </summary>
    public void LoadAllSavedGames()
    {
        savedGames.Clear();
        string[] files = Directory.GetFiles(SaveFolderPath, "*.json");

        foreach (string file in files)
        {
            string json = File.ReadAllText(file);
            SavedGame game = JsonUtility.FromJson<SavedGame>(json);
            if (game != null)
            {
                savedGames.Add(game);
            }
        }

        savedGames.Sort((a, b) => b.SaveTime.CompareTo(a.SaveTime));
    }

    /// <summary>
    /// 获取所有保存的棋局
    /// </summary>
    public List<SavedGame> GetAllSavedGames()
    {
        return new List<SavedGame>(savedGames);
    }

    /// <summary>
    /// 根据ID获取棋局
    /// </summary>
    public SavedGame GetGameById(string gameId)
    {
        return savedGames.Find(g => g.GameId == gameId);
    }
}