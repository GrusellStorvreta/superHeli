using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SimCore
{
    public class PauseManager : MonoBehaviour
    {
        private bool _paused;
        private int  _selectedIndex;

        private readonly MenuTheme _theme = new MenuTheme();

        void Update()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool toggle  = (kb != null && kb.pKey.wasPressedThisFrame)
                        || (gp != null && gp.startButton.wasPressedThisFrame);
            if (toggle) { if (_paused) Resume(); else Pause(); return; }

            if (!_paused) return;

            bool navDown = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.down.wasPressedThisFrame);
            bool navUp   = (kb != null && (kb.upArrowKey.wasPressedThisFrame   || kb.wKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.up.wasPressedThisFrame);
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                        || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            bool back    = (kb != null && kb.escapeKey.wasPressedThisFrame)
                        || (gp != null && gp.buttonEast.wasPressedThisFrame);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % 2;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + 2) % 2;
            if (confirm) ConfirmSelected();
            if (back)    Resume();
        }

        void Pause()  { _paused = true;  _selectedIndex = 0; Time.timeScale = 0f; }
        void Resume() { _paused = false; Time.timeScale = 1f; }

        void ConfirmSelected()
        {
            if (_selectedIndex == 0) Resume();
            else                     ReturnToMenu();
        }

        void ReturnToMenu()
        {
            Time.timeScale = 1f;
            _paused        = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnGUI()
        {
            if (!_paused) return;
            _theme.Build();

            float sw = Screen.width;
            float sh = Screen.height;

            _theme.DrawOverlay();

            float panW = 360f, panH = 280f;
            float px = (sw - panW) * 0.5f;
            float py = (sh - panH) * 0.5f;

            _theme.DrawPanel(new Rect(px, py, panW, panH));
            _theme.DrawShadowedText(new Rect(px, py + 18f, panW, 52f), "PAUSED", _theme.TitleStyle);
            _theme.DrawSeparator(px + 30f, py + 76f, panW - 60f);

            float btnW = 280f, btnH = 56f;
            float bx = px + (panW - btnW) * 0.5f;

            if (_theme.DrawButton(new Rect(bx, py + 100f, btnW, btnH), "RESUME",    _selectedIndex == 0)) Resume();
            if (_theme.DrawButton(new Rect(bx, py + 174f, btnW, btnH), "MAIN MENU", _selectedIndex == 1)) ReturnToMenu();
        }

        void OnDestroy()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            _theme.Destroy();
        }
    }
}
