using System.Collections.Generic;
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

        private List<(int num, string name)> _levelList;

        private int LevelSelectButtonCount => (_levelList?.Count ?? 3) + 1; // levels + back
        private int ButtonCount => _menuPhase == MenuPhase.Main     ? 4
                                 : _menuPhase == MenuPhase.Settings ? 2
                                 : LevelSelectButtonCount;

        public void ShowMenu()
        {
            _menuPhase = MenuPhase.Main;
            EnterMenu();
        }

        void Start()
        {
            GameSettings.ApplyAudioSettings();
            BuildLevelList();
            EnterMenu();
        }

        void BuildLevelList()
        {
            _levelList = new List<(int, string)>
            {
                (1, Loc.Get("menu.level", 1)),
                (2, Loc.Get("menu.level", 2)),
            };

            if (missionManager != null && missionManager.courses != null)
                foreach (var c in missionManager.courses)
                    if (c != null)
                        _levelList.Add((c.levelNumber, c.courseName.ToUpper()));

            _levelList.Add((7, Loc.Get("menu.level", 7)));
            _levelList.Sort((a, b) => a.num.CompareTo(b.num));
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (!_inMenu) return;
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            bool navDown = MenuTheme.NavDown(kb, gp);
            bool navUp   = MenuTheme.NavUp(kb, gp);
            bool confirm = MenuTheme.Confirm(kb, gp);
            bool back    = MenuTheme.Back(kb, gp);

            if (navDown) _selectedIndex = (_selectedIndex + 1) % ButtonCount;
            if (navUp)   _selectedIndex = (_selectedIndex - 1 + ButtonCount) % ButtonCount;

            if (_menuPhase == MenuPhase.Settings && _selectedIndex == 0 && GameSettings.SoundEnabled)
            {
                if (MenuTheme.NavRight(kb, gp)) { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume + 0.1f); GameSettings.ApplyAudioSettings(); }
                if (MenuTheme.NavLeft(kb, gp))  { GameSettings.SoundVolume = Mathf.Clamp01(GameSettings.SoundVolume - 0.1f); GameSettings.ApplyAudioSettings(); }
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
                if (_selectedIndex == 3) QuitGame();
            }
            else if (_menuPhase == MenuPhase.LevelSelect)
            {
                int u    = GameSettings.UnlockedLevels;
                int back = _levelList.Count;
                if (_selectedIndex == back) { GoBack(); return; }
                var lvl = _levelList[_selectedIndex];
                if (u >= lvl.num) StartGame(GameSettings.Mode.Mission, lvl.num);
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
            float gap  = 68f;
            float panH = _menuPhase == MenuPhase.Main     ? 460f
                       : _menuPhase == MenuPhase.Settings ? 340f
                       : 140f + gap * LevelSelectButtonCount + 30f;
            float px = (sw - panW) * 0.5f;
            float py = (sh - panH) * 0.5f;

            _theme.DrawPanel(new Rect(px, py, panW, panH));

            _theme.DrawShadowedText(new Rect(px, py + 18f, panW, 72f), Loc.Get("menu.title"), _theme.TitleStyle);
            GUI.Label(new Rect(px, py + 94f, panW, 22f), Loc.Get("menu.subtitle"), _theme.SubtitleStyle);
            _theme.DrawSeparator(px + 30f, py + 122f, panW - 60f);

            float btnW = 300f;
            float btnH = 56f;
            float bx   = px + (panW - btnW) * 0.5f;
            float by   = py + 140f;

            if (_menuPhase == MenuPhase.Main)
            {
                if (_theme.DrawButton(new Rect(bx, by,           btnW, btnH), Loc.Get("menu.free_flight"), _selectedIndex == 0)) StartGame(GameSettings.Mode.FreeFlight);
                if (_theme.DrawButton(new Rect(bx, by + gap,     btnW, btnH), Loc.Get("menu.missions"),    _selectedIndex == 1)) { _menuPhase = MenuPhase.LevelSelect; _selectedIndex = 0; }
                if (_theme.DrawButton(new Rect(bx, by + gap * 2, btnW, btnH), Loc.Get("menu.settings"),    _selectedIndex == 2)) { _menuPhase = MenuPhase.Settings;    _selectedIndex = 0; }
                if (_theme.DrawButton(new Rect(bx, by + gap * 3, btnW, btnH), Loc.Get("menu.quit"),        _selectedIndex == 3)) QuitGame();
            }
            else if (_menuPhase == MenuPhase.LevelSelect)
            {
                int u = GameSettings.UnlockedLevels;
                for (int i = 0; i < _levelList.Count; i++)
                {
                    var lvl    = _levelList[i];
                    bool locked = u < lvl.num;
                    string lbl  = locked ? $"{lvl.name}  \U0001F512" : lvl.name;
                    if (_theme.DrawButton(new Rect(bx, by + gap * i, btnW, btnH), lbl, _selectedIndex == i, locked))
                        if (!locked) StartGame(GameSettings.Mode.Mission, lvl.num);
                }
                if (_theme.DrawButton(new Rect(bx, by + gap * _levelList.Count, btnW, btnH), Loc.Get("menu.back"), _selectedIndex == _levelList.Count))
                    GoBack();
            }
            else // Settings
            {
                string soundLabel = GameSettings.SoundEnabled ? Loc.Get("menu.sound_on") : Loc.Get("menu.sound_off");
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
                    GUI.Label(new Rect(bx, barY + 10f, btnW, 20f), Loc.Get("menu.volume", (int)(vol * 100f)), _theme.SubtitleStyle);
                }

                if (_theme.DrawButton(new Rect(bx, by + gap * 2, btnW, btnH), Loc.Get("menu.back"), _selectedIndex == 1)) GoBack();
            }
        }

        void OnDestroy() => _theme.Destroy();
    }
}
