using UnityEngine;

namespace SimCore
{
    // Shows the currently assigned Lesson-mode task at the top of the screen,
    // same bar style/placement as MissionHUD's instruction bar, so the player
    // reads it the same way: "everything up there is what I'm meant to do."
    // The coach's feedback comment is a separate concern, shown via
    // ICoachOutput/HudCoachOutput at the bottom.
    public class LessonHUD : MonoBehaviour
    {
        private FlightTaskCoachAI coach;

        private GUIStyle  taskStyle;
        private GUIStyle  timerStyle;
        private Texture2D panelTex;

        private static readonly Color Amber = new Color(1f, 0.78f, 0.08f);
        private static readonly Color Red   = new Color(0.95f, 0.25f, 0.2f);
        private static readonly Color Panel = new Color(0.04f, 0.06f, 0.1f, 0.84f);

        void BuildStyles()
        {
            panelTex = MakeTex(Panel);

            taskStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            taskStyle.normal.textColor = Amber;

            timerStyle = new GUIStyle(taskStyle) { alignment = TextAnchor.MiddleRight };
        }

        void OnGUI()
        {
            if (coach == null) coach = FindObjectOfType<FlightTaskCoachAI>();
            if (coach == null || !coach.enabled) return;
            if (taskStyle == null) BuildStyles();

            float sw = Screen.width;
            float barW = 520f;
            float barH = 52f;
            float barX = (sw - barW) * 0.5f;
            float barY = 18f;

            GUI.DrawTexture(new Rect(barX, barY, barW, barH), panelTex);

            float timerW = 90f;
            GUI.Label(new Rect(barX + 12f, barY, barW - timerW - 20f, barH),
                      coach.CurrentTaskText, taskStyle);

            float t = coach.TimeRemaining;
            timerStyle.normal.textColor = t < 10f ? Red : Amber;
            GUI.Label(new Rect(barX + barW - timerW - 10f, barY, timerW, barH),
                      $"{t:F1} s", timerStyle);
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
            if (panelTex != null) Destroy(panelTex);
        }
    }
}
