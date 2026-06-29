using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimCore
{
    public class MissionResultScreen : MonoBehaviour
    {
        [Header("References")]
        public MissionManager  missionManager;
        public SimulatorDriver driver;

        [Header("Timing")]
        public float niceLandingDelay = 2.5f;

        private bool   _visible;
        private bool   _isSuccess;
        private string _failReason;
        private int    _currentLevel;
        private int    _nextLevel;
        private int    _selectedIndex;

        private readonly MenuTheme _theme = new MenuTheme();

        int ButtonCount => (_isSuccess && _nextLevel > 0) ? 2 : 2;

        public void ShowSuccess(int level)
        {
            _currentLevel  = level;
            _nextLevel     = level + 1;
            _isSuccess     = true;
            _selectedIndex = 0;
            StartCoroutine(ShowAfterDelay());
        }

        public void ShowFailure(string reason, int level)
        {
            _currentLevel  = level;
            _failReason    = reason;
            _isSuccess     = false;
            _selectedIndex = 0;
            _visible       = true;
        }

        IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(niceLandingDelay);
            _visible = true;
        }

        void Update()
        {
            if (!_visible) return;

            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool navDown = MenuTheme.NavDown(kb, gp);
            bool navUp   = MenuTheme.NavUp(kb, gp);
            bool confirm = MenuTheme.Confirm(kb, gp);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % ButtonCount;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + ButtonCount) % ButtonCount;
            if (confirm) ConfirmSelected();
        }

        void ConfirmSelected()
        {
            if (_isSuccess)
            {
                if (_selectedIndex == 0) ContinueToLevel(_nextLevel);
                else                     BackToMenu();
            }
            else
            {
                if (_selectedIndex == 0) TryAgain();
                else                     BackToMenu();
            }
        }

        void ContinueToLevel(int level) { _visible = false; driver?.ResetToSpawnPoint(); missionManager?.Initialize(level); }
        void TryAgain()                 { _visible = false; driver?.ResetToSpawnPoint(); missionManager?.Initialize(_currentLevel); }
        void BackToMenu()               { _visible = false; driver?.ResetToSpawnPoint(); FindObjectOfType<MainMenuManager>()?.ShowMenu(); }

        void OnGUI()
        {
            if (!_visible) return;
            _theme.Build();

            float sw = Screen.width;
            float sh = Screen.height;

            _theme.DrawOverlay();

            float panW = 460f;
            float panH = _isSuccess ? 440f : 360f;
            float px   = (sw - panW) * 0.5f;
            float py   = (sh - panH) * 0.5f;

            _theme.DrawPanel(new Rect(px, py, panW, panH));

            // Title
            var titleStyle = _theme.TitleStyle;
            titleStyle.fontSize = 36;
            titleStyle.normal.textColor = _isSuccess ? MenuTheme.Green : MenuTheme.Red;
            _theme.DrawShadowedText(new Rect(px, py + 18f, panW, 50f),
                _isSuccess ? "MISSION COMPLETE" : "MISSION FAILED", titleStyle);

            _theme.DrawSeparator(px + 30f, py + 74f, panW - 60f);

            float btnW = 340f;
            float btnH = 56f;
            float bx   = px + (panW - btnW) * 0.5f;

            if (_isSuccess)
            {
                // Badge
                _theme.DrawShadowedText(new Rect(px, py + 76f, panW, 100f), "★", _theme.BadgeStyle, 3f, 4f);

                // Time
                var infoStyle = _theme.InfoStyle;
                infoStyle.fontSize = 20;
                infoStyle.normal.textColor = MenuTheme.Amber;
                GUI.Label(new Rect(px, py + 182f, panW, 30f),
                    $"Completed in  {missionManager?.FinalTime:F1} sec", infoStyle);

                _theme.DrawSeparator(px + 30f, py + 220f, panW - 60f);

                if (_theme.DrawButton(new Rect(bx, py + 238f, btnW, btnH), $"CONTINUE  →  LEVEL {_nextLevel}", _selectedIndex == 0))
                    ContinueToLevel(_nextLevel);
                if (_theme.DrawButton(new Rect(bx, py + 312f, btnW, btnH), "BACK TO MENU", _selectedIndex == 1))
                    BackToMenu();
            }
            else
            {
                var infoStyle = _theme.InfoStyle;
                infoStyle.fontSize         = 18;
                infoStyle.normal.textColor = new Color(1f, 0.65f, 0.65f);
                GUI.Label(new Rect(px + 20f, py + 90f, panW - 40f, 80f), _failReason, infoStyle);

                _theme.DrawSeparator(px + 30f, py + 178f, panW - 60f);

                if (_theme.DrawButton(new Rect(bx, py + 196f, btnW, btnH), "TRY AGAIN", _selectedIndex == 0))
                    TryAgain();
                if (_theme.DrawButton(new Rect(bx, py + 270f, btnW, btnH), "BACK TO MENU", _selectedIndex == 1))
                    BackToMenu();
            }
        }

        void OnDestroy() => _theme.Destroy();
    }
}
