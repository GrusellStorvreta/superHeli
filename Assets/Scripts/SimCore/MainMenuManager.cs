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

        private bool        _inMenu = true;
        private GUIStyle    _titleStyle;
        private GUIStyle    _buttonStyle;
        private GUIStyle    _subtitleStyle;
        private Texture2D   _panelTex;
        private Texture2D   _overlayTex;
        private Texture2D   _buttonTex;
        private Texture2D   _buttonHoverTex;

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.03f, 0.05f, 0.09f, 0.92f);

        void Start()
        {
            EnterMenu();
        }

        void EnterMenu()
        {
            _inMenu = true;

            // Activate menu camera, deactivate gameplay cameras
            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            if (cameraLook != null)
            {
                cameraLook.enabled = false;
                foreach (var cam in cameraLook.cameras)
                    if (cam != null) cam.gameObject.SetActive(false);
            }

            // Freeze helicopter — no physics, no input
            if (helicopterRb != null) helicopterRb.isKinematic = true;
            if (driver       != null) driver.enabled           = false;

            // Hide gameplay HUD
            foreach (var hud in hudComponents)
                if (hud != null) hud.enabled = false;

            if (missionManager != null) missionManager.enabled = false;
            if (missionHUD     != null) missionHUD.enabled     = false;
        }

        void StartGame(GameSettings.Mode mode)
        {
            GameSettings.CurrentMode = mode;
            _inMenu = false;

            // Deactivate menu camera, restore gameplay cameras
            if (menuCamera != null) menuCamera.gameObject.SetActive(false);
            if (cameraLook != null)
            {
                cameraLook.enabled = true;
                // Restore whichever camera CameraLook considers active (index 0 at start)
                var cams = cameraLook.cameras;
                if (cams != null && cams.Length > 0 && cams[0] != null)
                    cams[0].gameObject.SetActive(true);
            }

            // Unfreeze helicopter
            if (helicopterRb != null) helicopterRb.isKinematic = false;
            if (driver       != null) driver.enabled           = true;

            // Show gameplay HUD
            foreach (var hud in hudComponents)
                if (hud != null) hud.enabled = true;

            // Mission mode only
            if (mode == GameSettings.Mode.Mission)
            {
                if (missionManager != null) missionManager.enabled = true;
                if (missionHUD     != null) missionHUD.enabled     = true;
            }
        }

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            _overlayTex     = MakeTex(new Color(0f, 0f, 0f, 0.45f));
            _panelTex       = MakeTex(DarkPanel);
            _buttonTex      = MakeTex(new Color(0.08f, 0.12f, 0.18f, 0.95f));
            _buttonHoverTex = MakeTex(new Color(0.15f, 0.22f, 0.32f, 0.95f));

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
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            _subtitleStyle.normal.textColor = new Color(1f, 0.78f, 0.08f, 0.55f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.background   = _buttonTex;
            _buttonStyle.hover.background    = _buttonHoverTex;
            _buttonStyle.active.background   = _buttonHoverTex;
            _buttonStyle.normal.textColor    = Amber;
            _buttonStyle.hover.textColor     = Color.white;
            _buttonStyle.active.textColor    = Color.white;
            _buttonStyle.border              = new RectOffset(4, 4, 4, 4);
        }

        void OnGUI()
        {
            if (!_inMenu) return;
            if (_titleStyle == null) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Subtle full-screen dark tint
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _overlayTex);

            // Centered panel
            float panelW = 360f;
            float panelH = 300f;
            float px     = (sw - panelW) * 0.5f;
            float py     = (sh - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);

            // Title
            GUI.Label(new Rect(px, py + 20f, panelW, 80f), "SUPERHELI", _titleStyle);

            // Subtitle / tagline
            GUI.Label(new Rect(px, py + 98f, panelW, 24f), "HELICOPTER SIMULATOR", _subtitleStyle);

            // Buttons
            float btnW = 280f;
            float btnH = 58f;
            float bx   = px + (panelW - btnW) * 0.5f;

            if (GUI.Button(new Rect(bx, py + 148f, btnW, btnH), "FREE FLIGHT", _buttonStyle))
                StartGame(GameSettings.Mode.FreeFlight);

            if (GUI.Button(new Rect(bx, py + 222f, btnW, btnH), "MISSION MODE", _buttonStyle))
                StartGame(GameSettings.Mode.Mission);
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
            if (_panelTex       != null) Destroy(_panelTex);
            if (_overlayTex     != null) Destroy(_overlayTex);
            if (_buttonTex      != null) Destroy(_buttonTex);
            if (_buttonHoverTex != null) Destroy(_buttonHoverTex);
        }
    }
}
