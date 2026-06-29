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

        private GUIStyle  _titleStyle;
        private GUIStyle  _badgeStyle;
        private GUIStyle  _infoStyle;
        private GUIStyle  _buttonStyle;
        private Texture2D _overlayTex;
        private Texture2D _panelTex;
        private Texture2D _buttonTex;
        private Texture2D _buttonHoverTex;
        private Texture2D _buttonSelectedTex;

        static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        static readonly Color DarkPanel = new Color(0.03f, 0.05f, 0.09f, 0.95f);
        static readonly Color Gold      = new Color(1f, 0.84f, 0.1f);
        static readonly Color Green     = new Color(0.25f, 0.95f, 0.35f);
        static readonly Color Red       = new Color(0.95f, 0.25f, 0.2f);

        int ButtonCount => (!_isSuccess || _nextLevel > 0) ? 2 : 1;

        public void ShowSuccess(int level)
        {
            _currentLevel  = level;
            _nextLevel     = NextLevel(level);
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

        static int NextLevel(int level) => level + 1;

        void Update()
        {
            if (!_visible) return;

            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool navDown = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.down.wasPressedThisFrame);
            bool navUp   = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.up.wasPressedThisFrame);
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                        || (gp != null && gp.buttonSouth.wasPressedThisFrame);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % ButtonCount;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + ButtonCount) % ButtonCount;
            if (confirm) ConfirmSelected();
        }

        void ConfirmSelected()
        {
            if (_isSuccess)
            {
                if (_selectedIndex == 0 && _nextLevel > 0) ContinueToLevel(_nextLevel);
                else BackToMenu();
            }
            else
            {
                if (_selectedIndex == 0) TryAgain();
                else BackToMenu();
            }
        }

        void ContinueToLevel(int level)
        {
            _visible = false;
            driver?.ResetToSpawnPoint();
            missionManager?.Initialize(level);
        }

        void TryAgain()
        {
            _visible = false;
            driver?.ResetToSpawnPoint();
            missionManager?.Initialize(_currentLevel);
        }

        void BackToMenu()
        {
            _visible = false;
            driver?.ResetToSpawnPoint();
            FindObjectOfType<MainMenuManager>()?.ShowMenu();
        }

        void OnGUI()
        {
            if (!_visible) return;
            if (_titleStyle == null) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            GUI.DrawTexture(new Rect(0, 0, sw, sh), _overlayTex);

            float panW = 440f;
            float panH = _isSuccess ? 430f : 360f;
            float px   = (sw - panW) * 0.5f;
            float py   = (sh - panH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panW, panH), _panelTex);

            _titleStyle.normal.textColor = _isSuccess ? Green : Red;
            GUI.Label(new Rect(px, py + 20f, panW, 50f),
                      _isSuccess ? "MISSION COMPLETE" : "MISSION FAILED", _titleStyle);

            float btnW = 320f;
            float btnH = 58f;
            float bx   = px + (panW - btnW) * 0.5f;

            if (_isSuccess)
            {
                _badgeStyle.normal.textColor = Gold;
                GUI.Label(new Rect(px, py + 65f, panW, 100f), "★", _badgeStyle);

                _infoStyle.normal.textColor = Amber;
                GUI.Label(new Rect(px, py + 175f, panW, 30f),
                          $"Time: {missionManager?.FinalTime:F1} sec", _infoStyle);

                if (_nextLevel > 0)
                {
                    if (GUI.Button(new Rect(bx, py + 230f, btnW, btnH),
                                   $"Continue to Level {_nextLevel}", StyleFor(0)))
                        ContinueToLevel(_nextLevel);
                    if (GUI.Button(new Rect(bx, py + 305f, btnW, btnH), "Back to Menu", StyleFor(1)))
                        BackToMenu();
                }
                else
                {
                    if (GUI.Button(new Rect(bx, py + 310f, btnW, btnH), "Back to Menu", StyleFor(0)))
                        BackToMenu();
                }
            }
            else
            {
                _infoStyle.normal.textColor = new Color(1f, 0.6f, 0.6f);
                GUI.Label(new Rect(px + 20f, py + 90f, panW - 40f, 70f), _failReason, _infoStyle);

                if (GUI.Button(new Rect(bx, py + 205f, btnW, btnH), "Try Again", StyleFor(0)))
                    TryAgain();
                if (GUI.Button(new Rect(bx, py + 280f, btnW, btnH), "Back to Menu", StyleFor(1)))
                    BackToMenu();
            }
        }

        GUIStyle StyleFor(int index)
        {
            if (index != _selectedIndex) return _buttonStyle;
            var s = new GUIStyle(_buttonStyle);
            s.normal.background = _buttonSelectedTex;
            s.normal.textColor  = Color.white;
            return s;
        }

        void BuildStyles()
        {
            _overlayTex      = MakeTex(new Color(0f, 0f, 0f, 0.6f));
            _panelTex        = MakeTex(DarkPanel);
            _buttonTex       = MakeTex(new Color(0.08f, 0.12f, 0.18f, 0.95f));
            _buttonHoverTex  = MakeTex(new Color(0.15f, 0.22f, 0.32f, 0.95f));
            _buttonSelectedTex = MakeTex(new Color(0.25f, 0.35f, 0.5f, 0.98f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 80,
                alignment = TextAnchor.MiddleCenter,
            };

            _infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.background = _buttonTex;
            _buttonStyle.hover.background  = _buttonHoverTex;
            _buttonStyle.normal.textColor  = Amber;
            _buttonStyle.hover.textColor   = Color.white;
            _buttonStyle.border            = new RectOffset(4, 4, 4, 4);
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
            if (_overlayTex       != null) Destroy(_overlayTex);
            if (_panelTex         != null) Destroy(_panelTex);
            if (_buttonTex        != null) Destroy(_buttonTex);
            if (_buttonHoverTex   != null) Destroy(_buttonHoverTex);
            if (_buttonSelectedTex != null) Destroy(_buttonSelectedTex);
        }
    }
}
