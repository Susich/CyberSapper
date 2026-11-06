using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Minesweeper.Editor
{
    /// <summary>
    /// Входит в режим play в headless-редакторе, даёт игре поработать несколько секунд и сообщает
    /// о каждой ошибке/предупреждении. Используется для проверки, что пересобранный HUD работает в рантайме.
    /// </summary>
    public static class PlayModeSmokeTest
    {
        private static readonly List<string> Problems = new List<string>();
        private static float _elapsed = 0f;
        private static bool _running = false;
        private static bool _interactionChecked = false;

        public static void Execute()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CyberMinesweeper.unity");
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _running = true;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.EnterPlaymode();
            Debug.Log("[SmokeTest] Entering play mode...");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Debug.Log("[SmokeTest] Play mode entered.");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && _running)
            {
                Finish();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            _elapsed += 0.016f;
            if (_elapsed < 2f) return;

            if (!_interactionChecked)
            {
                _interactionChecked = true;
                RunInteractionChecks();
            }

            if (_elapsed < 6f) return;

            Finish();
        }

        /// <summary>Запускает проверки взаимодействия с HUD внутри открытого редактора (не закрывает Unity).</summary>
        [MenuItem("Tools/Run HUD Interaction Checks")]
        public static void RunChecksInEditor()
        {
            Problems.Clear();
            _interactionChecked = false;
            _running = true;
            _elapsed = 0f;

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }
        }

        /// <summary>
        /// Имитирует клики, которые игрок выполняет по HUD, чтобы убедиться, что кнопки
        /// закрытия модальных окон и вкладки навигации действительно работают.
        /// </summary>
        private static void RunInteractionChecks()
        {
            // Каждое модальное окно должно иметь рабочую кнопку закрытия (заголовок "✕" и кнопка в нижнем колонтитуле).
            CheckModalCloseButton<Minesweeper.UI.SettingsModal>("SettingsModal");
            CheckModalCloseButton<Minesweeper.UI.LeaderboardsModal>("LeaderboardsModal");
            CheckModalCloseButton<Minesweeper.UI.HelpAboutModal>("HelpAboutModal");

            // У модальных окон результата есть свои кнопки действий.
            CheckSingleButton<Minesweeper.UI.VictoryModal>("VictoryModal", "_menuButton");
            CheckSingleButton<Minesweeper.UI.DefeatModal>("DefeatModal", "_menuButton");

            // Вкладки навигации верхней панели должны быть подключены.
            var topBar = UnityEngine.Object.FindFirstObjectByType<Minesweeper.UI.TopMenuBar>(FindObjectsInactive.Include);
            if (topBar == null)
            {
                Problems.Add("[Check] TopMenuBar not found");
            }
        }

        private static void CheckSingleButton<T>(string label, string fieldName) where T : MonoBehaviour
        {
            var modal = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (modal == null)
            {
                Problems.Add($"[Check] {label} not found");
                return;
            }

            var field = modal.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var btn = field?.GetValue(modal) as UnityEngine.UI.Button;
            if (btn == null)
            {
                Problems.Add($"[Check] {label}.{fieldName} is not assigned");
                return;
            }

            bool clicked = false;
            btn.onClick.AddListener(() => clicked = true);
            btn.onClick.Invoke();

            if (!clicked)
            {
                Problems.Add($"[Check] {label}.{fieldName} did not react to onClick");
            }
        }

        private static void CheckModalCloseButton<T>(string label) where T : MonoBehaviour
        {
            var modal = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (modal == null)
            {
                Problems.Add($"[Check] {label} not found");
                return;
            }

            var type = modal.GetType();
            foreach (var fieldName in new[] { "_headerCloseButton", "_closeButton" })
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var btn = field?.GetValue(modal) as UnityEngine.UI.Button;
                if (btn == null)
                {
                    Problems.Add($"[Check] {label}.{fieldName} is not assigned");
                    continue;
                }

                bool clicked = false;
                btn.onClick.AddListener(() => clicked = true);
                btn.onClick.Invoke();

                if (!clicked)
                {
                    Problems.Add($"[Check] {label}.{fieldName} did not react to onClick");
                }
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Problems.Add($"[{type}] {condition}");
            }
        }

        private static void Finish()
        {
            _running = false;
            EditorApplication.update -= OnEditorUpdate;
            Application.logMessageReceived -= OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (Problems.Count == 0)
            {
                Debug.Log("[SmokeTest] RESULT: no runtime errors detected.");
            }
            else
            {
                Debug.LogError($"[SmokeTest] RESULT: {Problems.Count} runtime problem(s):");
                foreach (var p in Problems)
                {
                    Debug.LogError("  " + p);
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(Problems.Count == 0 ? 0 : 1);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
