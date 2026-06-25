using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SimCore
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Cameras")]
        public Camera     menuCamera;
        public CameraLook cameraLook;

        [Header("Helicopter")]
        public SimulatorDriver driver;
        public Rigidbody       helicopterRb;

        [Header("Gameplay components (disabled during menu)")]
        public MissionManager  missionManager;
        public MissionHUD      missionHUD;
        public SimulatorHUD[]  hudComponents;

        private enum MenuPhase { Main, LevelSelect, Settings }
        private MenuPhase _menuPhase = MenuPhase.Main;
        private bool      _inMenu    = true;

        private GUIStyle  _titleStyle;
        private GUIStyle  _subtitleStyle;
        private GUIStyle  _buttonStyle;
        private GUIStyle  _buttonLockedStyle;
        private GUIStyle  _backStyle;

        private Texture2D _panelTex;
        private Texture2D _overlayTex;
        private Texture2D _buttonTex;
        private Texture2D _buttonHoverTex;
        private Texture2D _buttonLockedTex;
        private Texture2D _buttonSelectedTex;

        private int _selectedIndex = 0;

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.03f, 0.05f, 0.09f, 0.92f);
        private static readonly Color Grey      = new Color(0.4f, 0.4f, 0.4f, 0.9f);

        private int ButtonCount => _menuPhase == MenuPhase.Main ? 4
                                 : _menuPhase == MenuPhase.Settings ? 2
                                 : 4;

        // Called by MissionManager after mission success
        public void ShowMenu()
        {
            _menuPhase = MenuPhase.Main;
            EnterMenu();
        }

        void Start()
        {
            GameSettings.ApplyAudioSettings();
            EnterMenu();
        }

        void Update()
        {
            if (!_inMenu) return;
            var gp = Gamepad.current;
            if (gp == null) return;

            if (gp.dpad.down.wasPressedThisFrame || (gp.leftStick.ReadValue().y < -0.5f && gp.leftStick.down.wasPressedThisFrame))
                _selectedIndex = (_selectedIndex + 1) % ButtonCount;

            if (gp.dpad.up.wasPressedThisFrame || (gp.leftStick.ReadValue().y > 0.5f && gp.leftStick.up.wasPressedThisFrame))
                _selectedIndex = (_selectedIndex - 1 + ButtonCount) % ButtonCount;

            // Volume adjust with left/right when in Settings on sound row
            if (_menuPhase == MenuPhase.Settings && _selectedIndex == 0 && GameSettings.SoundEnabled)
            {
                if (gp.dpad.right.wasPressedThisFrame)
                { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume + 0.1f); GameSettings.ApplyAudioSettings(); }
                if (gp.dpad.left.wasPressedThisFrame)
                { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume - 0.1f); GameSettings.ApplyAudioSettings(); }
            }

            if (gp.buttonSouth.wasPressedThisFrame)
                ConfirmSelected();

            if (gp.buttonEast.wasPressedThisFrame)
                GoBack();
        }

        void ConfirmSelected()
        {
            if (_menuPhase == MenuPhase.Main)
            {
                if (_selectedIndex == 0) StartGame(GameSettings.Mode.FreeFlight);
                if (_selectedIndex == 1) { _menuPhase = MenuPhase.LevelSelect; _selectedIndex = 0; }
                if (_selectedIndex == 2) { _menuPhase = MenuPhase.Settings;    _selectedIndex = 0; }
                if (_selectedIndex == 3) Application.Quit();
            }
            else if (_menuPhase == MenuPhase.LevelSelect)
            {
                int unlocked = GameSettings.UnlockedLevels;
                if (_selectedIndex == 0) StartGame(GameSettings.Mode.Mission, 1);
                if (_selectedIndex == 1 && unlocked >= 2) StartGame(GameSettings.Mode.Mission, 2);
                if (_selectedIndex == 2 && unlocked >= 7) StartGame(GameSettings.Mode.Mission, 7);
                if (_selectedIndex == 3) GoBack();
            }
            else if (_menuPhase == MenuPhase.Settings)
            {
                if (_selectedIndex == 0) { GameSettings.SoundEnabled = !GameSettings.SoundEnabled; GameSettings.ApplyAudioSettings(); }
                if (_selectedIndex == 1) GoBack();
            }
        }

        void GoBack()
        {
            _menuPhase = MenuPhase.Main; _selectedIndex = 0;
        }

        void EnterMenu()
        {
            _inMenu = true;

            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            if (cameraLook != null)
            {
                cameraLook.enabled = false;
                foreach (var cam in cameraLook.cameras)
                    if (cam != null) cam.gameObject.SetActive(false);
            }

            if (helicopterRb != null) helicopterRb.isKinematic = true;
            if (driver       != null) driver.enabled           = false;

            foreach (var hud in hudComponents)
                if (hud != null) hud.enabled = false;

            if (missionManager != null) missionManager.enabled = false;
            if (missionHUD     != null) missionHUD.enabled     = false;
        }

        void StartGame(GameSettings.Mode mode, int level = 1)
        {
            GameSettings.CurrentMode  = mode;
            GameSettings.CurrentLevel = level;

            if (level == 7)
            {
                SceneManager.LoadScene("Level7");
                return;
            }

            _inMenu = false;

            if (menuCamera != null) menuCamera.gameObject.SetActive(false);
            if (cameraLook != null)
            {
                cameraLook.enabled = true;
                var cams = cameraLook.cameras;
                if (cams != null && cams.Length > 0 && cams[0] != null)
                    cams[0].gameObject.SetActive(true);
            }

            if (helicopterRb != null) helicopterRb.isKinematic = false;
            if (driver       != null) driver.enabled           = true;

            foreach (var hud in hudComponents)
                if (hud != null) hud.enabled = true;

            if (mode == GameSettings.Mode.Mission)
            {
                if (missionManager != null)
                {
                    missionManager.levelNumber = level;
                    missionManager.enabled     = true;
                }
                if (missionHUD != null) missionHUD.enabled = true;
            }
        }

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            _overlayTex     = MakeTex(new Color(0f, 0f, 0f, 0.45f));
            _panelTex       = MakeTex(DarkPanel);
            _buttonTex      = MakeTex(new Color(0.08f, 0.12f, 0.18f, 0.95f));
            _buttonHoverTex = MakeTex(new Color(0.15f, 0.22f, 0.32f, 0.95f));
            _buttonLockedTex    = MakeTex(new Color(0.06f, 0.06f, 0.08f, 0.85f));
            _buttonSelectedTex  = MakeTex(new Color(0.25f, 0.35f, 0.5f, 0.98f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = Amber;

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                alignment = TextAnchor.MiddleCenter,
            };
            _subtitleStyle.normal.textColor = new Color(1f, 0.78f, 0.08f, 0.55f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.background  = _buttonTex;
            _buttonStyle.hover.background   = _buttonHoverTex;
            _buttonStyle.active.background  = _buttonHoverTex;
            _buttonStyle.normal.textColor   = Amber;
            _buttonStyle.hover.textColor    = Color.white;
            _buttonStyle.active.textColor   = Color.white;
            _buttonStyle.border             = new RectOffset(4, 4, 4, 4);

            _buttonLockedStyle = new GUIStyle(_buttonStyle);
            _buttonLockedStyle.normal.background = _buttonLockedTex;
            _buttonLockedStyle.hover.background  = _buttonLockedTex;
            _buttonLockedStyle.normal.textColor  = Grey;
            _buttonLockedStyle.hover.textColor   = Grey;

            _backStyle = new GUIStyle(_buttonStyle) { fontSize = 16 };
        }

        void OnGUI()
        {
            if (!_inMenu) return;
            if (_titleStyle == null) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            GUI.DrawTexture(new Rect(0, 0, sw, sh), _overlayTex);

            float panelW = 360f;
            float panelH = _menuPhase == MenuPhase.Main ? 450f
                         : _menuPhase == MenuPhase.Settings ? 370f
                         : 420f;
            float px     = (sw - panelW) * 0.5f;
            float py     = (sh - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);

            GUI.Label(new Rect(px, py + 20f, panelW, 80f), "SUPERHELI", _titleStyle);
            GUI.Label(new Rect(px, py + 98f, panelW, 24f), "HELICOPTER SIMULATOR", _subtitleStyle);

            float btnW = 280f;
            float btnH = 58f;
            float bx   = px + (panelW - btnW) * 0.5f;

            if (_menuPhase == MenuPhase.Main)
                DrawMainMenu(bx, py, btnW, btnH);
            else if (_menuPhase == MenuPhase.LevelSelect)
                DrawLevelSelect(bx, py, btnW, btnH);
            else
                DrawSettings(bx, py, btnW, btnH);
        }

        void DrawMainMenu(float bx, float py, float btnW, float btnH)
        {
            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), "FREE FLIGHT", StyleFor(0)))
                StartGame(GameSettings.Mode.FreeFlight);

            if (GUI.Button(new Rect(bx, py + 222f, btnW, btnH), "MISSION MODE", StyleFor(1)))
                { _menuPhase = MenuPhase.LevelSelect; _selectedIndex = 0; }

            if (GUI.Button(new Rect(bx, py + 296f, btnW, btnH), "SETTINGS", StyleFor(2)))
                { _menuPhase = MenuPhase.Settings; _selectedIndex = 0; }

            if (GUI.Button(new Rect(bx, py + 370f, btnW, btnH), "QUIT", StyleFor(3)))
                Application.Quit();
        }

        void DrawSettings(float bx, float py, float btnW, float btnH)
        {
            string soundLabel = GameSettings.SoundEnabled ? "SOUND   ON" : "SOUND   OFF";
            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), soundLabel, StyleFor(0)))
            { GameSettings.SoundEnabled = !GameSettings.SoundEnabled; GameSettings.ApplyAudioSettings(); }

            if (GameSettings.SoundEnabled)
            {
                float vol  = GameSettings.SoundVolume;
                float barW = btnW - 20f;
                float barX = bx + 10f;
                float barY = py + 218f;
                GUI.DrawTexture(new Rect(barX, barY, barW, 8f), _buttonLockedTex);
                GUI.DrawTexture(new Rect(barX, barY, barW * vol, 8f), _buttonSelectedTex);
                float newVol = GUI.HorizontalSlider(new Rect(barX, barY - 6f, barW, 20f), vol, 0f, 1f);
                if (!Mathf.Approximately(newVol, vol))
                { GameSettings.SoundVolume = newVol; GameSettings.ApplyAudioSettings(); }
                GUI.Label(new Rect(bx, barY + 12f, btnW, 20f), $"VOLUME   {vol * 100f:F0}%", _subtitleStyle);
            }

            if (GUI.Button(new Rect(bx, py + 260f, btnW, btnH), "← BACK", StyleFor(1)))
                GoBack();
        }

        void DrawLevelSelect(float bx, float py, float btnW, float btnH)
        {
            int unlocked = GameSettings.UnlockedLevels;

            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), "LEVEL 1", StyleFor(0)))
                StartGame(GameSettings.Mode.Mission, 1);

            bool level2Unlocked = unlocked >= 2;
            string level2Label  = level2Unlocked ? "LEVEL 2" : "LEVEL 2  \U0001F512";
            if (GUI.Button(new Rect(bx, py + 222f, btnW, btnH), level2Label,
                           level2Unlocked ? StyleFor(1) : _buttonLockedStyle))
            {
                if (level2Unlocked) StartGame(GameSettings.Mode.Mission, 2);
            }

            bool level7Unlocked = unlocked >= 7;
            string level7Label  = level7Unlocked ? "LEVEL 7" : "LEVEL 7  \U0001F512";
            if (GUI.Button(new Rect(bx, py + 296f, btnW, btnH), level7Label,
                           level7Unlocked ? StyleFor(2) : _buttonLockedStyle))
            {
                if (level7Unlocked) StartGame(GameSettings.Mode.Mission, 7);
            }

            if (GUI.Button(new Rect(bx, py + 374f, btnW, 36f), "← BACK", StyleFor(3)))
                GoBack();
        }

        GUIStyle StyleFor(int index)
        {
            if (index != _selectedIndex) return _buttonStyle;
            var s = new GUIStyle(_buttonStyle);
            s.normal.background = _buttonSelectedTex;
            s.normal.textColor  = Color.white;
            return s;
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
            if (_panelTex        != null) Destroy(_panelTex);
            if (_overlayTex      != null) Destroy(_overlayTex);
            if (_buttonTex       != null) Destroy(_buttonTex);
            if (_buttonHoverTex  != null) Destroy(_buttonHoverTex);
            if (_buttonLockedTex    != null) Destroy(_buttonLockedTex);
            if (_buttonSelectedTex  != null) Destroy(_buttonSelectedTex);
        }
    }
}
