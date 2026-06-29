using UnityEngine;

namespace SimCore
{
    // Shared visual theme for all menus.
    // Instantiate once per menu, call Build() inside OnGUI (lazy init),
    // call Destroy() inside MonoBehaviour.OnDestroy().
    public class MenuTheme
    {
        // --- Colors (shared across menus) ---
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

        private bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;

            _overlay     = Tex(new Color(0f,    0f,    0f,    0.55f));
            _panel       = Tex(new Color(0.07f, 0.10f, 0.16f, 0.97f));
            _panelShadow = Tex(new Color(0f,    0f,    0f,    0.60f));
            _panelBorder = Tex(new Color(0.22f, 0.30f, 0.44f, 0.90f));
            _btnNormal   = Tex(new Color(0.11f, 0.16f, 0.25f, 0.98f));
            _btnHover    = Tex(new Color(0.18f, 0.26f, 0.38f, 0.98f));
            _btnSelected = Tex(new Color(0.20f, 0.30f, 0.46f, 1.00f));
            _btnLocked   = Tex(new Color(0.06f, 0.07f, 0.09f, 0.90f));
            _edgeLight   = Tex(new Color(0.50f, 0.62f, 0.78f, 0.45f));
            _edgeDark    = Tex(new Color(0.01f, 0.01f, 0.02f, 0.70f));
            _accentBar   = Tex(new Color(1f,    0.78f, 0.08f, 0.92f));

            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            TitleStyle.normal.textColor = Amber;

            SubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleCenter,
            };
            SubtitleStyle.normal.textColor = AmberDim;

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
            ButtonStyle.border             = new RectOffset(4, 4, 4, 4);

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
        // Drawing helpers
        // -------------------------------------------------------

        public void DrawOverlay()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlay);
        }

        // Panel with drop shadow, border, and top highlight line.
        public void DrawPanel(Rect r)
        {
            GUI.DrawTexture(new Rect(r.x + 7, r.y + 9,      r.width,     r.height),     _panelShadow);
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1,      r.width + 2, r.height + 2), _panelBorder);
            GUI.DrawTexture(r, _panel);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 1,      r.width - 6, 1),            _edgeLight);
        }

        // Button with 3D bevel edges and amber accent bar when selected.
        // Returns true if clicked (never returns true when locked).
        public bool DrawButton(Rect r, string label, bool selected, bool locked = false)
        {
            // Drop shadow behind button
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 4, r.width, r.height), _edgeDark);

            GUIStyle style = locked    ? ButtonLockedStyle
                           : selected  ? ButtonSelectedStyle
                           : ButtonStyle;

            bool clicked = GUI.Button(r, label, style);

            // Bevel edges drawn on top of button body
            GUI.DrawTexture(new Rect(r.x,                r.y,                r.width - 1, 1), _edgeLight); // top
            GUI.DrawTexture(new Rect(r.x,                r.y,                1, r.height - 1), _edgeLight); // left
            GUI.DrawTexture(new Rect(r.x,                r.y + r.height - 1, r.width, 1),      _edgeDark);  // bottom
            GUI.DrawTexture(new Rect(r.x + r.width - 1, r.y,                1, r.height),      _edgeDark);  // right

            // Amber accent bar = controller cursor indicator
            if (selected && !locked)
                GUI.DrawTexture(new Rect(r.x, r.y + 5, 4, r.height - 10), _accentBar);

            return clicked && !locked;
        }

        // Label with drop shadow (allocates a temp style — only use for titles, not hot paths).
        public void DrawShadowedText(Rect r, string text, GUIStyle style, float ox = 2f, float oy = 3f)
        {
            var sh = new GUIStyle(style);
            sh.normal.textColor = new Color(0f, 0f, 0f, 0.72f);
            GUI.Label(new Rect(r.x + ox, r.y + oy, r.width, r.height), text, sh);
            GUI.Label(r, text, style);
        }

        // Horizontal volume/progress bar.
        public void DrawVolumeBar(Rect r, float fill)
        {
            GUI.DrawTexture(r, _edgeDark);
            if (fill > 0f)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), _accentBar);
        }

        // Thin separator line (dark + light = engraved look).
        public void DrawSeparator(float x, float y, float width)
        {
            GUI.DrawTexture(new Rect(x, y,     width, 1), _edgeDark);
            GUI.DrawTexture(new Rect(x, y + 1, width, 1), _edgeLight);
        }

        public void Destroy()
        {
            Object.Destroy(_overlay);
            Object.Destroy(_panel);
            Object.Destroy(_panelShadow);
            Object.Destroy(_panelBorder);
            Object.Destroy(_btnNormal);
            Object.Destroy(_btnHover);
            Object.Destroy(_btnSelected);
            Object.Destroy(_btnLocked);
            Object.Destroy(_edgeLight);
            Object.Destroy(_edgeDark);
            Object.Destroy(_accentBar);
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
