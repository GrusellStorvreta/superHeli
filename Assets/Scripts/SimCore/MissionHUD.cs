using UnityEngine;

namespace SimCore
{
    public class MissionHUD : MonoBehaviour
    {
        private MissionManager mission;

        private GUIStyle taskStyle;
        private GUIStyle timerStyle;
        private GUIStyle resultStyle;
        private GUIStyle subStyle;

        private Texture2D panelTex;
        private Texture2D barBgTex;
        private Texture2D barGreenTex;
        private Texture2D barRedTex;

        private static readonly Color Amber    = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DimAmber = new Color(1f, 0.78f, 0.08f, 0.55f);
        private static readonly Color Green    = new Color(0.25f, 0.95f, 0.35f);
        private static readonly Color Red      = new Color(0.95f, 0.25f, 0.2f);
        private static readonly Color Panel    = new Color(0.04f, 0.06f, 0.1f, 0.84f);

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            panelTex    = MakeTex(Panel);
            barBgTex    = MakeTex(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            barGreenTex = MakeTex(Green);
            barRedTex   = MakeTex(Red);

            taskStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            taskStyle.normal.textColor = Amber;

            timerStyle = new GUIStyle(taskStyle) { fontSize = 22, alignment = TextAnchor.MiddleRight };

            resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            subStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (mission == null) mission = FindObjectOfType<MissionManager>();
            if (mission == null) return;
            if (taskStyle == null) BuildStyles();

            var phase = mission.CurrentPhase;

            if (phase == MissionManager.Phase.Success)  { DrawResult(true);  return; }
            if (phase == MissionManager.Phase.Failed)   { DrawResult(false); return; }
            if (phase == MissionManager.Phase.Running)  { DrawRunning();             }
        }

        void DrawRunning()
        {
            float sw = Screen.width;

            // --- Task + timer bar ---
            float barW = 520f;
            float barH = 52f;
            float barX = (sw - barW) * 0.5f;
            float barY = 18f;

            GUI.DrawTexture(new Rect(barX, barY, barW, barH), panelTex);

            // Task instruction (left-ish part)
            float timerW = 90f;
            GUI.Label(new Rect(barX + 12f, barY, barW - timerW - 20f, barH),
                      mission.CurrentInstruction, taskStyle);

            // Countdown timer (right part, amber → red when < 10s)
            float t = mission.TimeRemaining;
            timerStyle.normal.textColor = t < 10f ? Red : Amber;
            GUI.Label(new Rect(barX + barW - timerW - 10f, barY, timerW, barH),
                      $"{t:F1} s", timerStyle);

            // --- Hover progress bar (only during hover task) ---
            if (!mission.IsHoverTask) return;

            float pbW = barW;
            float pbH = 14f;
            float pbX = barX;
            float pbY = barY + barH + 6f;

            GUI.DrawTexture(new Rect(pbX, pbY, pbW, pbH), barBgTex);

            Texture2D fill = mission.HoverInZone ? barGreenTex : barRedTex;
            float fillW = pbW * mission.HoverProgress;
            if (fillW > 2f)
                GUI.DrawTexture(new Rect(pbX, pbY, fillW, pbH), fill);

            // Zone status label
            subStyle.fontSize = 15;
            subStyle.normal.textColor = mission.HoverInZone ? Green : DimAmber;
            string zoneLabel = mission.HoverInZone ? "IN ZONE" : "OUT OF ZONE — hold position";
            GUI.Label(new Rect(pbX, pbY + pbH + 2f, pbW, 22f), zoneLabel, subStyle);
            subStyle.fontSize = 28;
        }

        void DrawResult(bool success)
        {
            // Semi-transparent full-screen tint
            Color tint = success
                ? new Color(0f, 0.05f, 0f, 0.55f)
                : new Color(0.1f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                            success ? barGreenTex : barRedTex,
                            ScaleMode.StretchToFill, true,
                            0f, tint, 0f, 0f);

            resultStyle.normal.textColor = success ? Green : Red;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                      mission.CurrentInstruction, resultStyle);

            if (success)
            {
                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.5f + 50f, Screen.width, 50f),
                          $"Time: {mission.FinalTime:F1} sec", subStyle);
            }
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
            if (panelTex    != null) Destroy(panelTex);
            if (barBgTex    != null) Destroy(barBgTex);
            if (barGreenTex != null) Destroy(barGreenTex);
            if (barRedTex   != null) Destroy(barRedTex);
        }
    }
}
