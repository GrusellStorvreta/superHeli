using UnityEngine;

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

        private enum MenuPhase { Main, LevelSelect }
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

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.03f, 0.05f, 0.09f, 0.92f);
        private static readonly Color Grey      = new Color(0.4f, 0.4f, 0.4f, 0.9f);

        // Called by MissionManager after mission success
        public void ShowMenu()
        {
            _menuPhase = MenuPhase.Main;
            EnterMenu();
        }

        void Start()
        {
            EnterMenu();
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
            _buttonLockedTex= MakeTex(new Color(0.06f, 0.06f, 0.08f, 0.85f));

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
            float panelH = _menuPhase == MenuPhase.Main ? 300f : 340f;
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
            else
                DrawLevelSelect(bx, py, btnW, btnH);
        }

        void DrawMainMenu(float bx, float py, float btnW, float btnH)
        {
            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), "FREE FLIGHT", _buttonStyle))
                StartGame(GameSettings.Mode.FreeFlight);

            if (GUI.Button(new Rect(bx, py + 222f, btnW, btnH), "MISSION MODE", _buttonStyle))
                _menuPhase = MenuPhase.LevelSelect;
        }

        void DrawLevelSelect(float bx, float py, float btnW, float btnH)
        {
            int unlocked = GameSettings.UnlockedLevels;

            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), "LEVEL 1", _buttonStyle))
                StartGame(GameSettings.Mode.Mission, 1);

            bool level2Unlocked = unlocked >= 2;
            string level2Label  = level2Unlocked ? "LEVEL 2" : "LEVEL 2  \U0001F512";
            if (GUI.Button(new Rect(bx, py + 222f, btnW, btnH), level2Label,
                           level2Unlocked ? _buttonStyle : _buttonLockedStyle))
            {
                if (level2Unlocked) StartGame(GameSettings.Mode.Mission, 2);
            }

            if (GUI.Button(new Rect(bx, py + 296f, btnW, 36f), "← BACK", _backStyle))
                _menuPhase = MenuPhase.Main;
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
            if (_buttonLockedTex != null) Destroy(_buttonLockedTex);
        }
    }
}
