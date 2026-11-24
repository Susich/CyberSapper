using UnityEngine;

namespace Minesweeper.UI
{
    /// <summary>
    /// Не даёт уменьшить окно приложения ниже минимального размера.
    /// HUD свёрстан под 1920x1080, поэтому в слишком маленьком окне он сжимается
    /// до нечитаемого состояния. Windows не предоставляет настройку минимального
    /// размера окна в Unity, поэтому размер возвращается в пределах допустимого.
    /// </summary>
    public class WindowSizeGuard : MonoBehaviour
    {
        private const int MinWidth = 1280;
        private const int MinHeight = 720;
        private const float CheckInterval = 0.25f;

        private float _nextCheck;

        private void Update()
        {
            // В редакторе размер задаёт Game view; менять его из кода не нужно.
            if (Application.isEditor) return;

            // В полноэкранном режиме размер окна не ограничивается.
            if (Screen.fullScreen) return;

            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;

            int width = Screen.width;
            int height = Screen.height;
            if (width >= MinWidth && height >= MinHeight) return;

            Screen.SetResolution(
                Mathf.Max(width, MinWidth),
                Mathf.Max(height, MinHeight),
                FullScreenMode.Windowed);
        }
    }
}
