using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Minesweeper.Core;
using Minesweeper.Audio;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Minesweeper.UI
{
    public class TopMenuBar : MonoBehaviour
    {
        [Header("Main Navigation Tabs")]
        [SerializeField] private Button _btnNewGame;
        [SerializeField] private Button _btnRecords;
        [SerializeField] private Button _btnSettings;
        [SerializeField] private Button _btnHelp;

        [Header("Tab Highlight Visuals")]
        [SerializeField] private Image _gameTabBg;
        [SerializeField] private Image _recordsTabBg;
        [SerializeField] private Image _settingsTabBg;
        [SerializeField] private Image _helpTabBg;

        [Header("Right Controls")]
        [SerializeField] private Button _btnDifficultyPill;
        [SerializeField] private TextMeshProUGUI _difficultyPillText;
        [SerializeField] private Button _btnSoundToggle;
        [SerializeField] private Image _soundIconImage;
        [SerializeField] private Button _btnFullscreen;
        // Устаревшая сериализованная ссылка: шестерёнка дублировала вкладку «Параметры» и
        // удаляется во время выполнения, чтобы старые сцены её тоже не показывали.
        [SerializeField, HideInInspector] private Button _btnSettingsGear;

        [Header("Sound Toggle Icons")]
        public Sprite SoundOnSprite;
        public Sprite SoundOffSprite;

        [Header("Tab Sprites")]
        public Sprite ActiveTabSprite;
        public Sprite InactiveTabSprite;

        [Header("Modals References")]
        [SerializeField] private SettingsModal _settingsModal;
        [SerializeField] private LeaderboardsModal _leaderboardsModal;
        [SerializeField] private HelpAboutModal _helpAboutModal;

        // --- Выпадающие меню (SRS UI-011) ---
        private const float MenuItemHeight = 32f;
        private const float MenuPadding = 6f;
        private const float MenuItemSpacing = 2f;

        private static readonly Color ColorMenuBg = new Color(10f / 255f, 14f / 255f, 23f / 255f, 0.98f);
        private static readonly Color ColorMenuText = new Color(224f / 255f, 253f / 255f, 1f);
        private static readonly Color ColorMenuMuted = new Color(148f / 255f, 163f / 255f, 184f / 255f);
        private static readonly Color ColorMenuAccent = new Color(0f, 242f / 255f, 254f / 255f);

        private GameObject _menuBlocker;
        private RectTransform _gameMenu;
        private RectTransform _difficultyMenu;
        private int _activeTabIndex = 0;

        private bool IsAnyMenuOpen =>
            (_gameMenu != null && _gameMenu.gameObject.activeSelf) ||
            (_difficultyMenu != null && _difficultyMenu.gameObject.activeSelf);

        private void Awake()
        {
            NormalizeCanvasScaling();
            EnlargeIconButton(_btnSoundToggle);
            EnlargeIconButton(_btnFullscreen);

            if (_btnNewGame != null) _btnNewGame.onClick.AddListener(OnNewGameClicked);
            if (_btnRecords != null) _btnRecords.onClick.AddListener(OnRecordsClicked);
            if (_btnSettings != null) _btnSettings.onClick.AddListener(OnSettingsClicked);
            if (_btnHelp != null) _btnHelp.onClick.AddListener(OnHelpClicked);

            if (_btnDifficultyPill != null) _btnDifficultyPill.onClick.AddListener(OnDifficultyPillClicked);
            if (_btnSoundToggle != null) _btnSoundToggle.onClick.AddListener(OnSoundToggleClicked);
            if (_btnFullscreen != null) _btnFullscreen.onClick.AddListener(OnFullscreenClicked);

            // Настройки доступны только через вкладку «Параметры»; дублирующая
            // кнопка-шестерёнка в правом блоке удаляется из интерфейса.
            if (_btnSettingsGear != null)
            {
                _btnSettingsGear.gameObject.SetActive(false);
                Destroy(_btnSettingsGear.gameObject);
                _btnSettingsGear = null;
            }
        }

        /// <summary>
        /// HUD свёрстан под фиксированный холст 1920x1080. При режиме по умолчанию
        /// MatchWidthOrHeight окно ниже 16:9 сжимает логический холст меньше 1080 px:
        /// тактическая панель центрируется, поэтому её заголовок уходит под верхнюю панель
        /// (обрезается панель таймера), а нижние скобки выталкиваются за край окна.
        /// Expand всегда сохраняет полную высоту 1080 px и лишь добавляет лишнее место
        /// по более широкой оси.
        /// </summary>
        private void NormalizeCanvasScaling()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// <summary>
        /// Кнопки звука и полноэкранного режима были 34x34 в дизайн-сетке, что выглядит как
        /// слишком маленькая область нажатия рядом с таблеткой сложности. Увеличиваются и
        /// кнопка, и её значок (44x44 / 22x22 в дизайн-сетке).
        /// </summary>
        private static void EnlargeIconButton(Button button)
        {
            if (button == null) return;

            const float designScale = 1.2f;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(44f * designScale, 44f * designScale);

            var icon = button.transform.Find("Icon") as RectTransform;
            if (icon != null)
            {
                icon.sizeDelta = new Vector2(22f * designScale, 22f * designScale);
            }
        }

        private void Start()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnGameInitialized += UpdateDifficultyPill;
                if (GameController.Instance.CurrentDifficulty != null)
                {
                    UpdateDifficultyPill(GameController.Instance.CurrentDifficulty);
                }
            }
            UpdateSoundIcon();
            SetActiveTabHighlight(0);
        }

        private void OnDestroy()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnGameInitialized -= UpdateDifficultyPill;
            }
        }

        private void Update()
        {
            // Обработка клавиши Escape (HI-001)
            if (CheckEscapePressed())
            {
                if (IsAnyMenuOpen)
                {
                    CloseMenus();
                    return;
                }
                OnEscapePressed();
            }

            // Issue #2: выбранная вкладка не должна оставаться подсвеченной после закрытия её модального окна.
            if (_activeTabIndex != 0 && !IsAnyModalOpen())
            {
                SetActiveTabHighlight(0);
            }
        }

        private bool CheckEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif
            try
            {
                return Input.GetKeyDown(KeyCode.Escape);
            }
            catch
            {
                return false;
            }
        }

        private void OnEscapePressed()
        {
            if (_settingsModal != null && _settingsModal.IsOpen)
            {
                _settingsModal.Hide();
                SetActiveTabHighlight(0);
                return;
            }
            if (_leaderboardsModal != null && _leaderboardsModal.IsOpen)
            {
                _leaderboardsModal.Hide();
                SetActiveTabHighlight(0);
                return;
            }
            if (_helpAboutModal != null && _helpAboutModal.IsOpen)
            {
                _helpAboutModal.Hide();
                SetActiveTabHighlight(0);
                return;
            }

            // Переключение паузы или настроек
            if (GameController.Instance != null)
            {
                GameController.Instance.TogglePause();
            }
        }

        public void UpdateDifficultyPill(GameDifficulty diff)
        {
            if (_difficultyPillText == null || diff == null) return;
            // Названия уровней соответствуют SRS FR-011 / Dizdok 4: «Новичок», «Любитель», «Профессионал».
            string levelName = diff.Level == DifficultyLevel.Beginner ? "Новичок (9×9)" :
                               diff.Level == DifficultyLevel.Intermediate ? "Любитель (16×16)" :
                               diff.Level == DifficultyLevel.Expert ? "Профессионал (30×16)" :
                               $"Особый ({diff.Width}×{diff.Height})";
            _difficultyPillText.text = $"{levelName} <color=#929db2>• {diff.Mines} мин</color>";
        }

        /// <summary>Вкладка «Игра» - открывает системное меню (SRS UI-011, FR-028).</summary>
        public void OnNewGameClicked()
        {
            CloseAllModals();
            SetActiveTabHighlight(0);

            if (_gameMenu != null && _gameMenu.gameObject.activeSelf)
            {
                CloseMenus();
                return;
            }

            CloseMenus();
            OpenGameMenu();
        }

        public void OnRecordsClicked()
        {
            CloseMenus();
            CloseAllModals();
            SetActiveTabHighlight(1);
            if (_leaderboardsModal != null)
            {
                var diff = GameController.Instance != null && GameController.Instance.CurrentDifficulty != null
                    ? GameController.Instance.CurrentDifficulty.Level
                    : DifficultyLevel.Beginner;
                _leaderboardsModal.Show(diff);
            }
        }

        public void OnSettingsClicked()
        {
            CloseMenus();
            CloseAllModals();
            SetActiveTabHighlight(2);
            if (_settingsModal != null)
            {
                _settingsModal.Show();
            }
        }

        public void OnHelpClicked()
        {
            CloseMenus();
            CloseAllModals();
            SetActiveTabHighlight(3);
            if (_helpAboutModal != null)
            {
                _helpAboutModal.Show(false);
            }
        }

        /// <summary>«О программе» - второй таб окна справки.</summary>
        public void OnAboutClicked()
        {
            CloseMenus();
            CloseAllModals();
            SetActiveTabHighlight(3);
            if (_helpAboutModal != null)
            {
                _helpAboutModal.Show(true);
            }
        }

        /// <summary>«Выход» - пункт системного меню «Игра» (SRS UI-011).</summary>
        public void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ==================================================================
        // СИСТЕМНЫЕ МЕНЮ (SRS UI-011)
        // ==================================================================

        private void OpenGameMenu()
        {
            if (_gameMenu == null)
            {
                _gameMenu = CreateDropdown("GameMenu", (RectTransform)_btnNewGame.transform, 210f, 4);
                AddMenuItem(_gameMenu, "Новая игра", OnNewGameAction);
                AddMenuItem(_gameMenu, "Сложность", OnSettingsClicked);
                AddMenuItem(_gameMenu, "Таблица рекордов", OnRecordsClicked);
                AddMenuItem(_gameMenu, "Выход", OnExitClicked, false, ColorMenuMuted);
            }

            ShowMenu(_gameMenu);
        }

        private void OnDifficultyPillClicked()
        {
            if (_difficultyMenu != null && _difficultyMenu.gameObject.activeSelf)
            {
                CloseMenus();
                return;
            }

            CloseAllModals();
            CloseMenus();
            OpenDifficultyMenu();
        }

        private void OpenDifficultyMenu()
        {
            // Пересобираем список при каждом открытии, чтобы маркер текущего уровня был актуален.
            if (_difficultyMenu != null)
            {
                _difficultyMenu.gameObject.SetActive(false);
                Destroy(_difficultyMenu.gameObject);
                _difficultyMenu = null;
            }

            var current = GameController.Instance != null && GameController.Instance.CurrentDifficulty != null
                ? GameController.Instance.CurrentDifficulty.Level
                : DifficultyLevel.Beginner;

            _difficultyMenu = CreateDropdown("DifficultyMenu", (RectTransform)_btnDifficultyPill.transform, 300f, 4);
            AddMenuItem(_difficultyMenu, "Новичок — 9×9 • 10 мин",
                () => StartDifficulty(DifficultyLevel.Beginner), current == DifficultyLevel.Beginner);
            AddMenuItem(_difficultyMenu, "Любитель — 16×16 • 40 мин",
                () => StartDifficulty(DifficultyLevel.Intermediate), current == DifficultyLevel.Intermediate);
            AddMenuItem(_difficultyMenu, "Профессионал — 30×16 • 99 мин",
                () => StartDifficulty(DifficultyLevel.Expert), current == DifficultyLevel.Expert);
            AddMenuItem(_difficultyMenu, "Особые параметры…",
                OnCustomDifficultyClicked, current == DifficultyLevel.Custom);

            ShowMenu(_difficultyMenu);
        }

        private void OnNewGameAction()
        {
            CloseAllModals();
            SetActiveTabHighlight(0);
            if (GameController.Instance != null)
            {
                GameController.Instance.RestartGame();
            }
        }

        private void StartDifficulty(DifficultyLevel level)
        {
            CloseAllModals();
            SetActiveTabHighlight(0);
            if (GameController.Instance != null)
            {
                GameController.Instance.StartNewGame(level);
            }
        }

        private void OnCustomDifficultyClicked()
        {
            CloseAllModals();
            SetActiveTabHighlight(2);
            if (_settingsModal != null)
            {
                _settingsModal.ShowCustom();
            }
        }

        private void ShowMenu(RectTransform menu)
        {
            EnsureMenuBlocker();
            if (_menuBlocker != null) _menuBlocker.SetActive(true);
            if (menu != null) menu.gameObject.SetActive(true);
        }

        private void CloseMenus()
        {
            if (_gameMenu != null) _gameMenu.gameObject.SetActive(false);
            if (_difficultyMenu != null) _difficultyMenu.gameObject.SetActive(false);
            if (_menuBlocker != null) _menuBlocker.SetActive(false);
        }

        /// <summary>
        /// Прозрачный полноэкранный слой, закрывающий выпадающее меню, когда игрок щёлкает
        /// в любом месте вне верхней панели. Он расположен чуть ниже панели навигации в
        /// иерархии холста, поэтому сама панель остаётся кликабельной.
        /// </summary>
        private void EnsureMenuBlocker()
        {
            if (_menuBlocker != null || transform.parent == null) return;

            _menuBlocker = new GameObject("TopMenuBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            _menuBlocker.transform.SetParent(transform.parent, false);

            var rt = (RectTransform)_menuBlocker.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = _menuBlocker.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            var btn = _menuBlocker.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(CloseMenus);

            _menuBlocker.transform.SetSiblingIndex(transform.GetSiblingIndex());
            _menuBlocker.SetActive(false);
        }

        private RectTransform CreateDropdown(string name, RectTransform anchorRect, float width, int itemCount)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width,
                MenuPadding * 2f + itemCount * MenuItemHeight + Mathf.Max(0, itemCount - 1) * MenuItemSpacing);

            // Выравниваем левый край по элементу управления и вешаем панель прямо под панелью.
            if (anchorRect != null)
            {
                var anchorCorners = new Vector3[4];
                anchorRect.GetWorldCorners(anchorCorners);

                var barCorners = new Vector3[4];
                ((RectTransform)transform).GetWorldCorners(barCorners);

                rt.position = new Vector3(anchorCorners[0].x, barCorners[0].y - 2f, anchorCorners[0].z);
            }

            var bg = go.GetComponent<Image>();
            bg.color = ColorMenuBg;
            bg.raycastTarget = true;

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)MenuPadding, (int)MenuPadding, (int)MenuPadding, (int)MenuPadding);
            vlg.spacing = MenuItemSpacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Тонкая акцентная линия голубого цвета по верхнему краю выпадающего меню.
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent.transform.SetParent(go.transform, false);
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 1f);
            accentRt.anchorMax = new Vector2(1f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.sizeDelta = new Vector2(0f, 2f);
            accentRt.anchoredPosition = Vector2.zero;
            accent.GetComponent<LayoutElement>().ignoreLayout = true;
            var accentImg = accent.GetComponent<Image>();
            accentImg.color = ColorMenuAccent;
            accentImg.raycastTarget = false;

            go.SetActive(false);
            return rt;
        }

        private void AddMenuItem(RectTransform parent, string label, System.Action action, bool selected = false, Color? color = null)
        {
            var go = new GameObject("MenuItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, MenuItemHeight);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = MenuItemHeight;
            le.preferredHeight = MenuItemHeight;
            le.flexibleWidth = 1f;

            // Изображение тонируется ColorBlock кнопки; нормальное состояние полностью прозрачно.
            var img = go.GetComponent<Image>();
            img.color = Color.white;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(ColorMenuAccent.r, ColorMenuAccent.g, ColorMenuAccent.b, 0.16f);
            colors.pressedColor = new Color(ColorMenuAccent.r, ColorMenuAccent.g, ColorMenuAccent.b, 0.28f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                CloseMenus();
                action?.Invoke();
            });

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.font = GetMenuFont();
            tmp.text = (selected ? "• " : "   ") + label;
            tmp.fontSize = 13f;
            tmp.color = color ?? (selected ? ColorMenuAccent : ColorMenuText);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.characterSpacing = 1f;
            tmp.raycastTarget = false;
        }

        private TMP_FontAsset GetMenuFont()
        {
            if (_difficultyPillText != null && _difficultyPillText.font != null)
                return _difficultyPillText.font;

            if (_btnNewGame != null)
            {
                var tabLabel = _btnNewGame.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tabLabel != null) return tabLabel.font;
            }

            return null;
        }

        private bool IsAnyModalOpen()
        {
            return (_settingsModal != null && _settingsModal.IsOpen) ||
                   (_leaderboardsModal != null && _leaderboardsModal.IsOpen) ||
                   (_helpAboutModal != null && _helpAboutModal.IsOpen);
        }

        private void OnSoundToggleClicked()
        {
            CloseMenus();
            if (GameController.Instance != null)
            {
                bool newMute = !GameController.Instance.Settings.IsMuted;
                GameController.Instance.Settings.IsMuted = newMute;
                GameController.Instance.SaveMgr.SaveSettings(GameController.Instance.Settings);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetMute(newMute);
                }
                UpdateSoundIcon();
            }
        }

        private void UpdateSoundIcon()
        {
            bool isMuted = GameController.Instance != null && GameController.Instance.Settings.IsMuted;
            if (_soundIconImage != null)
            {
                _soundIconImage.sprite = isMuted ? SoundOffSprite : SoundOnSprite;
                _soundIconImage.color = isMuted ? new Color(244f / 255f, 63f / 255f, 94f / 255f) : new Color(0f, 242f / 255f, 254f / 255f);
            }
        }

        private void OnFullscreenClicked()
        {
            CloseMenus();
            Screen.fullScreen = !Screen.fullScreen;
        }

        public void SetActiveTabHighlight(int tabIndex)
        {
            _activeTabIndex = tabIndex;
            ApplyTabVisual(_gameTabBg, tabIndex == 0);
            ApplyTabVisual(_recordsTabBg, tabIndex == 1);
            ApplyTabVisual(_settingsTabBg, tabIndex == 2);
            ApplyTabVisual(_helpTabBg, tabIndex == 3);
        }

        private void ApplyTabVisual(Image bg, bool isActive)
        {
            if (bg == null) return;

            if (isActive && ActiveTabSprite != null)
            {
                bg.sprite = ActiveTabSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else if (!isActive && InactiveTabSprite != null)
            {
                bg.sprite = InactiveTabSprite;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                bg.color = isActive ? new Color(0f, 242f / 255f, 254f / 255f, 0.16f) : new Color(1f, 1f, 1f, 0f);
            }
        }

        private void CloseAllModals()
        {
            if (_settingsModal != null && _settingsModal.IsOpen) _settingsModal.Hide();
            if (_leaderboardsModal != null && _leaderboardsModal.IsOpen) _leaderboardsModal.Hide();
            if (_helpAboutModal != null && _helpAboutModal.IsOpen) _helpAboutModal.Hide();
        }
    }
}
