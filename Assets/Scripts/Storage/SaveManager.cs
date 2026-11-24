using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Minesweeper.Storage
{
    public class SaveManager
    {
        private static readonly string SettingsFileName = "settings.json";
        private static readonly string HighScoresFileName = "highscores.json";
        private static readonly string SavedSessionFileName = "saved_session.json";

        private readonly string _saveDirectory;

        public SaveManager(string directory = null)
        {
            _saveDirectory = directory ?? Application.persistentDataPath;
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }

        private string GetPath(string fileName) => Path.Combine(_saveDirectory, fileName);

        public GameSettingsData LoadSettings()
        {
            string path = GetPath(SettingsFileName);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<GameSettingsData>(json);
                    if (data != null) return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to load settings: {ex.Message}");
            }
            return new GameSettingsData();
        }

        public void SaveSettings(GameSettingsData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetPath(SettingsFileName), json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save settings: {ex.Message}");
            }
        }

        public HighScoresData LoadHighScores()
        {
            string path = GetPath(HighScoresFileName);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<HighScoresData>(json);
                    if (data != null)
                    {
                        // Пустые таблицы - это результат кнопки «Сбросить рекорды»,
                        // а не отсутствие файла. Не подменяем их стартовыми записями.
                        if (data.BeginnerScores == null) data.BeginnerScores = new List<HighScoreEntry>();
                        if (data.IntermediateScores == null) data.IntermediateScores = new List<HighScoreEntry>();
                        if (data.ExpertScores == null) data.ExpertScores = new List<HighScoreEntry>();
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to load high scores: {ex.Message}");
            }

            var defaultScores = HighScoresData.CreateDefault();
            SaveHighScores(defaultScores);
            return defaultScores;
        }

        public void SaveHighScores(HighScoresData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetPath(HighScoresFileName), json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save high scores: {ex.Message}");
            }
        }

        public SavedGameState LoadSession()
        {
            string path = GetPath(SavedSessionFileName);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var state = JsonUtility.FromJson<SavedGameState>(json);
                    return state;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to load saved session: {ex.Message}");
            }
            return null;
        }

        public void SaveSession(SavedGameState state)
        {
            try
            {
                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(GetPath(SavedSessionFileName), json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save session: {ex.Message}");
            }
        }

        public void ClearSession()
        {
            try
            {
                string path = GetPath(SavedSessionFileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to clear session: {ex.Message}");
            }
        }
    }
}
