using UnityEngine;

namespace SimCore
{
    // Displays the AI flight coach's latest comment as a HUD panel. Implements
    // ICoachOutput so a future TtsCoachOutput can replace this without any
    // change to the code that calls Say().
    public class HudCoachOutput : MonoBehaviour, ICoachOutput
    {
        public float displaySeconds = 12f;
        public float panelWidth = 560f;
        public float minPanelHeight = 90f;
        public float maxPanelHeight = 220f;
        public int fontSize = 18;

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.04f, 0.06f, 0.1f, 0.82f);

        private GUIStyle  textStyle;
        private Texture2D bgTex;
        private string    message;
        private float     hideAt;

        void BuildStyles()
        {
            bgTex = MakeTex(DarkPanel);
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap  = true,
                alignment = TextAnchor.MiddleLeft,
            };
            textStyle.normal.textColor = Amber;
        }

        public void Say(string comment)
        {
            message = comment;
            hideAt  = Time.time + displaySeconds;
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(message) || Time.time >= hideAt) return;
            if (textStyle == null || bgTex == null) BuildStyles();

            float contentWidth = panelWidth - 24f;
            float textHeight   = textStyle.CalcHeight(new GUIContent(message), contentWidth);
            float boxHeight    = Mathf.Clamp(textHeight + 16f, minPanelHeight, maxPanelHeight);

            float px = 16f;
            float py = Screen.height - boxHeight - 16f;

            GUI.DrawTexture(new Rect(px, py, panelWidth, boxHeight), bgTex);
            GUI.Label(new Rect(px + 12f, py + 8f, contentWidth, boxHeight - 16f), message, textStyle);
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
            if (bgTex != null) Destroy(bgTex);
        }
    }
}
