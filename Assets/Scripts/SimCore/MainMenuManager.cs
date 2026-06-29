using UnityEngine;
using UnityEngine.InputSystem;

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
        private MenuPhase _menuPhase    = MenuPhase.Main;
        private bool      _inMenu       = true;
        private int       _selectedIndex;

        private readonly MenuTheme _theme = new MenuTheme();

        private int ButtonCount => _menuPhase == MenuPhase.Main       ? 4
                                 : _menuPhase == MenuPhase.Settings   ? 2
                                 : 4;

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
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool navDown = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
                        || (gp != null && (gp.dpad.down.wasPressedThisFrame ||
                            (gp.leftStick.ReadValue().y < -0.5f && gp.leftStick.down.wasPressedThisFrame)));
            bool navUp   = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
                        || (gp != null && (gp.dpad.up.wasPressedThisFrame ||
                            (gp.leftStick.ReadValue().y > 0.5f && gp.leftStick.up.wasPressedThisFrame)));
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                        || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            bool back    = (kb != null && kb.escapeKey.wasPressedThisFrame)
                        || (gp != null && gp.buttonEast.wasPressedThisFrame);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % ButtonCount;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + ButtonCount) % ButtonCount;

            if (_menuPhase == MenuPhase.Settings && _selectedIndex == 0 && GameSettings.SoundEnabled)
            {
                bool volUp   = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (gp != null && gp.dpad.right.wasPressedThisFrame);
                bool volDown = (kb != null && kb.leftArrowKey.wasPressedThisFrame)  || (gp != null && gp.dpad.left.wasPressedThisFrame);
                if (volUp)   { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume + 0.1f); GameSettings.ApplyAudioSettings(); }
                if (volDown) { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume - 0.1f); GameSettings.ApplyAudioSettings(); }
            }

            if (confirm) ConfirmSelected();
            if (back)    GoBack();
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
                int u = GameSettings.UnlockedLevels;
                if (_selectedIndex == 0)           StartGame(GameSettings.Mode.Mission, 1);
                if (_selectedIndex == 1 && u >= 2) StartGame(GameSettings.Mode.Mission, 2);
                if (_selectedIndex == 2 && u >= 7) StartGame(GameSettings.Mode.Mission, 7);
                if (_selectedIndex == 3)           GoBack();
            }
            else if (_menuPhase == MenuPhase.Settings)
            {
                if (_selectedIndex == 0) { GameSettings.SoundEnabled = !GameSettings.SoundEnabled; GameSettings.ApplyAudioSettings(); }
                if (_selectedIndex == 1) GoBack();
            }
        }

        void GoBack() { _menuPhase = MenuPhase.Main; _selectedIndex = 0; }

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
                    missionManager.enabled = true;
                    missionManager.Initialize(level);
                }
                if (missionHUD != null) missionHUD.enabled = true;
            }
        }

        // -------------------------------------------------------

        void OnGUI()
        {
            if (!_inMenu) return;
            _theme.Build();

            float sw = Screen.width;
            float sh = Screen.height;

            _theme.DrawOverlay();

            float panW = 380f;
            float panH = _menuPhase == MenuPhase.Main     ? 460f
                       : _menuPhase == MenuPhase.Settings ? 340f
                       : 430f;
            float px = (sw - panW) * 0.5f;
            float py = (sh - panH) * 0.5f;

            _theme.DrawPanel(new Rect(px, py, panW, panH));

            _theme.DrawShadowedText(new Rect(px, py + 18f, panW, 72f), "SUPERHELI", _theme.TitleStyle);
            GUI.Label(new Rect(px, py + 94f, panW, 22f), "HELICOPTER SIMULATOR", _theme.SubtitleStyle);
            _theme.DrawSeparator(px + 30f, py + 122f, panW - 60f);

            float btnW = 300f;
            float btnH = 56f;
            float bx   = px + (panW - btnW) * 0.5f;
            float by   = py + 140f;
            float gap  = 68f;

            if (_menuPhase == MenuPhase.Main)
            {
                if (_theme.DrawButton(new Rect(bx, by,           btnW, btnH), "FREE FLIGHT",  _selectedIndex == 0)) StartGame(GameSettings.Mode.FreeFlight);
                if (_theme.DrawButton(new Rect(bx, by + gap,     btnW, btnH), "MISSION MODE", _selectedIndex == 1)) { _menuPhase = MenuPhase.LevelSelect; _selectedIndex = 0; }
                if (_theme.DrawButton(new Rect(bx, by + gap * 2, btnW, btnH), "SETTINGS",     _selectedIndex == 2)) { _menuPhase = MenuPhase.Settings;    _selectedIndex = 0; }
                if (_theme.DrawButton(new Rect(bx, by + gap * 3, btnW, btnH), "QUIT",         _selectedIndex == 3)) Application.Quit();
            }
            else if (_menuPhase == MenuPhase.LevelSelect)
            {
                int u = GameSettings.UnlockedLevels;
                if (_theme.DrawButton(new Rect(bx, by,           btnW, btnH), "LEVEL 1",                    _selectedIndex == 0))         StartGame(GameSettings.Mode.Mission, 1);
                if (_theme.DrawButton(new Rect(bx, by + gap,     btnW, btnH), u >= 2 ? "LEVEL 2" : "LEVEL 2  \U0001F512", _selectedIndex == 1, u < 2)) StartGame(GameSettings.Mode.Mission, 2);
                if (_theme.DrawButton(new Rect(bx, by + gap * 2, btnW, btnH), u >= 7 ? "LEVEL 7" : "LEVEL 7  \U0001F512", _selectedIndex == 2, u < 7)) StartGame(GameSettings.Mode.Mission, 7);
                if (_theme.DrawButton(new Rect(bx, by + gap * 3, btnW, btnH), "← BACK",                    _selectedIndex == 3))         GoBack();
            }
            else // Settings
            {
                string soundLabel = GameSettings.SoundEnabled ? "SOUND   ON" : "SOUND   OFF";
                if (_theme.DrawButton(new Rect(bx, by, btnW, btnH), soundLabel, _selectedIndex == 0))
                { GameSettings.SoundEnabled = !GameSettings.SoundEnabled; GameSettings.ApplyAudioSettings(); }

                if (GameSettings.SoundEnabled)
                {
                    float vol  = GameSettings.SoundVolume;
                    float barW = btnW - 20f;
                    float barX = bx + 10f;
                    float barY = by + btnH + 14f;
                    _theme.DrawVolumeBar(new Rect(barX, barY, barW, 6f), vol);
                    float newVol = GUI.HorizontalSlider(new Rect(barX, barY - 7f, barW, 20f), vol, 0f, 1f);
                    if (!Mathf.Approximately(newVol, vol)) { GameSettings.SoundVolume = newVol; GameSettings.ApplyAudioSettings(); }
                    GUI.Label(new Rect(bx, barY + 10f, btnW, 20f), $"VOLUME   {vol * 100f:F0}%", _theme.SubtitleStyle);
                }

                if (_theme.DrawButton(new Rect(bx, by + gap * 2, btnW, btnH), "← BACK", _selectedIndex == 1)) GoBack();
            }
        }

        void OnDestroy() => _theme.Destroy();
    }
}
