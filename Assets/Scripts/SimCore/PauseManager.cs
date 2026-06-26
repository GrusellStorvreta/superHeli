using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SimCore
{
    public class PauseManager : MonoBehaviour
    {
        private bool _paused;
        private int  _selectedIndex;

        private GUIStyle  _titleStyle;
        private GUIStyle  _buttonStyle;
        private GUIStyle  _buttonSelectedStyle;
        private Texture2D _overlayTex;
        private Texture2D _panelTex;
        private Texture2D _buttonTex;
        private Texture2D _buttonSelectedTex;

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.03f, 0.05f, 0.09f, 0.95f);

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            _overlayTex      = MakeTex(new Color(0f, 0f, 0f, 0.6f));
            _panelTex        = MakeTex(DarkPanel);
            _buttonTex       = MakeTex(new Color(0.08f, 0.12f, 0.18f, 0.95f));
            _buttonSelectedTex = MakeTex(new Color(0.25f, 0.35f, 0.5f, 0.98f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = Amber;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.background  = _buttonTex;
            _buttonStyle.hover.background   = _buttonTex;
            _buttonStyle.normal.textColor   = Amber;
            _buttonStyle.hover.textColor    = Color.white;
            _buttonStyle.border             = new RectOffset(4, 4, 4, 4);

            _buttonSelectedStyle = new GUIStyle(_buttonStyle);
            _buttonSelectedStyle.normal.background = _buttonSelectedTex;
            _buttonSelectedStyle.normal.textColor  = Color.white;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool togglePressed = (kb != null && kb.pKey.wasPressedThisFrame)
                              || (gp != null && gp.startButton.wasPressedThisFrame);

            if (togglePressed)
            {
                if (_paused) Resume();
                else         Pause();
                return;
            }

            if (!_paused) return;

            bool navDown = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.down.wasPressedThisFrame);

            bool navUp = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
                      || (gp != null && gp.dpad.up.wasPressedThisFrame);

            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                        || (gp != null && gp.buttonSouth.wasPressedThisFrame);

            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame)
                     || (gp != null && gp.buttonEast.wasPressedThisFrame);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % 2;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + 2) % 2;
            if (confirm) ConfirmSelected();
            if (back)    Resume();
        }

        void Pause()
        {
            _paused        = true;
            _selectedIndex = 0;
            Time.timeScale = 0f;
        }

        void Resume()
        {
            _paused        = false;
            Time.timeScale = 1f;
        }

        void ConfirmSelected()
        {
            if (_selectedIndex == 0) Resume();
            if (_selectedIndex == 1) ReturnToMenu();
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
            if (_titleStyle == null) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            GUI.DrawTexture(new Rect(0, 0, sw, sh), _overlayTex);

            float panelW = 340f, panelH = 260f;
            float px = (sw - panelW) * 0.5f;
            float py = (sh - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);
            GUI.Label(new Rect(px, py + 20f, panelW, 60f), "PAUSED", _titleStyle);

            float btnW = 260f, btnH = 58f;
            float bx = px + (panelW - btnW) * 0.5f;

            if (GUI.Button(new Rect(bx, py + 110f, btnW, btnH), "RESUME",
                           _selectedIndex == 0 ? _buttonSelectedStyle : _buttonStyle))
                Resume();

            if (GUI.Button(new Rect(bx, py + 185f, btnW, btnH), "MAIN MENU",
                           _selectedIndex == 1 ? _buttonSelectedStyle : _buttonStyle))
                ReturnToMenu();
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            if (_overlayTex       != null) Destroy(_overlayTex);
            if (_panelTex         != null) Destroy(_panelTex);
            if (_buttonTex        != null) Destroy(_buttonTex);
            if (_buttonSelectedTex!= null) Destroy(_buttonSelectedTex);
        }
    }
}
