using UnityEngine;

namespace SimCore
{
    // Shared visual theme for all menus.
    // Call Build() inside OnGUI (lazy init). Call Destroy() in OnDestroy().
    public class MenuTheme
    {
        // --- Colors ---
        public static readonly Color Amber     = new Color(1f,    0.78f, 0.08f, 1f);
        public static readonly Color AmberDim  = new Color(1f,    0.78f, 0.08f, 0.50f);
        public static readonly Color White     = Color.white;
        public static readonly Color GreyText  = new Color(0.35f, 0.35f, 0.38f, 0.90f);
        public static readonly Color Green     = new Color(0.25f, 0.95f, 0.35f, 1f);
        public static readonly Color Red       = new Color(0.95f, 0.25f, 0.20f, 1f);
        public static readonly Color Gold      = new Color(1f,    0.84f, 0.10f, 1f);

        // --- Styles ---
        public GUIStyle TitleStyle;
        public GUIStyle SubtitleStyle;
        public GUIStyle ButtonStyle;
        public GUIStyle ButtonSelectedStyle;
        public GUIStyle ButtonLockedStyle;
        public GUIStyle InfoStyle;
        public GUIStyle BadgeStyle;

        // --- Textures ---
        private Texture2D _overlay;
        private Texture2D _panel, _panelShadow, _panelBorder;
        private Texture2D _btnNormal, _btnHover, _btnSelected, _btnLocked;
        private Texture2D _edgeLight, _edgeDark, _accentBar;

        // Panel draw style (for 9-slice rounded corners)
        private GUIStyle _panelStyle;
        private GUIStyle _panelBorderStyle;
        private GUIStyle _panelShadowStyle;
        private GUIStyle _btnNormalStyle, _btnHoverStyle, _btnSelectedStyle, _btnLockedStyle;

        private bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;

            const int R = 10; // corner radius

            _overlay          = Flat(new Color(0f,    0f,    0f,    0.55f));
            _panel            = Rounded(new Color(0.07f, 0.10f, 0.16f, 0.97f), R);
            _panelShadow      = Rounded(new Color(0f,    0f,    0f,    0.55f), R);
            _panelBorder      = Rounded(new Color(0.22f, 0.30f, 0.44f, 0.92f), R);
            _btnNormal        = Rounded(new Color(0.11f, 0.16f, 0.25f, 0.98f), 6);
            _btnHover         = Rounded(new Color(0.18f, 0.26f, 0.38f, 0.98f), 6);
            _btnSelected      = Rounded(new Color(0.20f, 0.30f, 0.46f, 1.00f), 6);
            _btnLocked        = Rounded(new Color(0.06f, 0.07f, 0.09f, 0.90f), 6);
            _edgeLight        = Flat(new Color(0.50f, 0.62f, 0.78f, 0.45f));
            _edgeDark         = Flat(new Color(0.01f, 0.01f, 0.02f, 0.70f));
            _accentBar        = Flat(new Color(1f,    0.78f, 0.08f, 0.92f));

            _panelStyle       = RoundedStyle(_panel,       R);
            _panelBorderStyle = RoundedStyle(_panelBorder, R);
            _panelShadowStyle = RoundedStyle(_panelShadow, R);
            _btnNormalStyle   = RoundedStyle(_btnNormal,   6);
            _btnHoverStyle    = RoundedStyle(_btnHover,    6);
            _btnSelectedStyle = RoundedStyle(_btnSelected, 6);
            _btnLockedStyle   = RoundedStyle(_btnLocked,   6);

            // TitleStyle — all states identical so hover never changes appearance
            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            TitleStyle.normal.textColor  = Amber;
            TitleStyle.hover.textColor   = Amber;
            TitleStyle.active.textColor  = Amber;
            TitleStyle.focused.textColor = Amber;

            SubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleCenter,
            };
            SubtitleStyle.normal.textColor  = AmberDim;
            SubtitleStyle.hover.textColor   = AmberDim;

            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            ButtonStyle.normal.background  = _btnNormal;
            ButtonStyle.hover.background   = _btnHover;
            ButtonStyle.active.background  = _btnHover;
            ButtonStyle.normal.textColor   = Amber;
            ButtonStyle.hover.textColor    = White;
            ButtonStyle.active.textColor   = White;
            ButtonStyle.border             = new RectOffset(6, 6, 6, 6);

            ButtonSelectedStyle = new GUIStyle(ButtonStyle);
            ButtonSelectedStyle.normal.background = _btnSelected;
            ButtonSelectedStyle.normal.textColor  = White;

            ButtonLockedStyle = new GUIStyle(ButtonStyle);
            ButtonLockedStyle.normal.background = _btnLocked;
            ButtonLockedStyle.hover.background  = _btnLocked;
            ButtonLockedStyle.normal.textColor  = GreyText;
            ButtonLockedStyle.hover.textColor   = GreyText;

            InfoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
            };
            InfoStyle.normal.textColor = Amber;

            BadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 80,
                alignment = TextAnchor.MiddleCenter,
            };
            BadgeStyle.normal.textColor = Gold;
        }

        // -------------------------------------------------------
        // Input helpers — call from Update, not OnGUI
        // -------------------------------------------------------

        public static bool NavDown(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
            || (gp != null && (gp.dpad.down.wasPressedThisFrame
                || (gp.leftStick.ReadValue().y < -0.5f && gp.leftStick.down.wasPressedThisFrame)));

        public static bool NavUp(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
            || (gp != null && (gp.dpad.up.wasPressedThisFrame
                || (gp.leftStick.ReadValue().y > 0.5f && gp.leftStick.up.wasPressedThisFrame)));

        public static bool NavLeft(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && kb.leftArrowKey.wasPressedThisFrame)
            || (gp != null && (gp.dpad.left.wasPressedThisFrame
                || gp.leftStick.ReadValue().x < -0.5f));

        public static bool NavRight(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && kb.rightArrowKey.wasPressedThisFrame)
            || (gp != null && (gp.dpad.right.wasPressedThisFrame
                || gp.leftStick.ReadValue().x > 0.5f));

        public static bool Confirm(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            || (gp != null && gp.buttonSouth.wasPressedThisFrame);

        public static bool Back(UnityEngine.InputSystem.Keyboard kb, UnityEngine.InputSystem.Gamepad gp)
            => (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (gp != null && gp.buttonEast.wasPressedThisFrame);

        // -------------------------------------------------------
        // Drawing helpers
        // -------------------------------------------------------

        public void DrawOverlay()
            => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlay);

        public void DrawPanel(Rect r)
        {
            GUI.Box(new Rect(r.x + 7, r.y + 9, r.width, r.height), GUIContent.none, _panelShadowStyle);
            GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), GUIContent.none, _panelBorderStyle);
            GUI.Box(r, GUIContent.none, _panelStyle);
            GUI.DrawTexture(new Rect(r.x + 12, r.y + 2, r.width - 24, 1), _edgeLight);
        }

        public bool DrawButton(Rect r, string label, bool selected, bool locked = false)
        {
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 4, r.width, r.height), _edgeDark);

            GUIStyle style = locked   ? ButtonLockedStyle
                           : selected ? ButtonSelectedStyle
                           : ButtonStyle;

            bool clicked = GUI.Button(r, label, style);

            GUI.DrawTexture(new Rect(r.x + 1, r.y,                r.width - 2, 1), _edgeLight);
            GUI.DrawTexture(new Rect(r.x,     r.y + 1,            1, r.height - 2), _edgeLight);
            GUI.DrawTexture(new Rect(r.x + 1, r.y + r.height - 1, r.width - 2, 1), _edgeDark);
            GUI.DrawTexture(new Rect(r.x + r.width - 1, r.y + 1,  1, r.height - 2), _edgeDark);

            if (selected && !locked)
                GUI.DrawTexture(new Rect(r.x, r.y + 6, 4, r.height - 12), _accentBar);

            return clicked && !locked;
        }

        public void DrawShadowedText(Rect r, string text, GUIStyle style, float ox = 2f, float oy = 3f)
        {
            var shadow = new GUIStyle(style);
            shadow.normal.textColor  = new Color(0f, 0f, 0f, 0.72f);
            shadow.hover.textColor   = new Color(0f, 0f, 0f, 0.72f);
            shadow.focused.textColor = new Color(0f, 0f, 0f, 0.72f);
            GUI.Label(new Rect(r.x + ox, r.y + oy, r.width, r.height), text, shadow);
            GUI.Label(r, text, style);
        }

        public void DrawVolumeBar(Rect r, float fill)
        {
            GUI.DrawTexture(r, _edgeDark);
            if (fill > 0f)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), _accentBar);
        }

        public void DrawSeparator(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y,     width, 1), _edgeDark);
            GUI.DrawTexture(new Rect(x, y + 1, width, 1), _edgeLight);
        }

        public void Destroy()
        {
            Object.Destroy(_overlay);
            Object.Destroy(_panel);      Object.Destroy(_panelShadow); Object.Destroy(_panelBorder);
            Object.Destroy(_btnNormal);  Object.Destroy(_btnHover);    Object.Destroy(_btnSelected); Object.Destroy(_btnLocked);
            Object.Destroy(_edgeLight);  Object.Destroy(_edgeDark);    Object.Destroy(_accentBar);
        }

        // -------------------------------------------------------
        // Texture factories
        // -------------------------------------------------------

        static Texture2D Flat(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // Generates a rounded-rectangle texture for use with 9-slice (border = radius on all sides).
        static Texture2D Rounded(Color fill, int radius)
        {
            int size = radius * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Min(x, size - 1 - x);
                float cy = Mathf.Min(y, size - 1 - y);
                if (cx < radius && cy < radius)
                {
                    float dx   = radius - cx - 0.5f;
                    float dy   = radius - cy - 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist);
                    t.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * alpha));
                }
                else
                {
                    t.SetPixel(x, y, fill);
                }
            }
            t.Apply();
            return t;
        }

        static GUIStyle RoundedStyle(Texture2D tex, int radius)
        {
            var s = new GUIStyle();
            s.normal.background = tex;
            s.border            = new RectOffset(radius, radius, radius, radius);
            return s;
        }
    }
}
