using UnityEngine;

namespace SimCore
{
    public class SimulatorHUD : MonoBehaviour
    {
        public Vector2 position = new Vector2(16, 16);
        public int fontSize = 17;
        public bool show = true;

        private SimulatorDriver driver;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
        private Texture2D bgTexture;

        private const float MsToKnots = 1.94384f;
        private const float MToFeet   = 3.28084f;

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.1f, 0.82f));
            bgTexture.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = bgTexture;

            Color amber = new Color(1f, 0.78f, 0.08f);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize   = fontSize,
                fontStyle  = FontStyle.Bold,
                alignment  = TextAnchor.MiddleLeft,
                richText   = true
            };
            labelStyle.normal.textColor = amber;
        }

        void OnGUI()
        {
            if (!show) return;
            if (labelStyle == null || bgTexture == null) BuildStyles();
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            if (driver == null) return;

            Vector3 vel = driver.LastBodyVelocity;
            Vector3 pos = driver.LastBodyPosition;
            Quaternion rot = driver.LastBodyRotation;

            float heading    = rot.eulerAngles.y;
            float fwdSpeed   = Vector3.Dot(vel, rot * Vector3.forward);
            float airspeedKt = fwdSpeed * MsToKnots;
            float vsFpm      = vel.y * MToFeet * 60f;
            float altFt      = pos.y * MToFeet;

            string[] rows = new string[]
            {
                FormatRow("HDG", $"{heading:000}°"),
                FormatRow("SPD", $"{Sign(airspeedKt)}{Mathf.Abs(airspeedKt):F0} kt"),
                FormatRow("V/S", $"{Sign(vsFpm)}{Mathf.Abs(vsFpm):F0} fpm"),
                FormatRow("ALT", $"{altFt:F0} ft"),
            };

            float lineH  = fontSize + 10f;
            float panelW = 172f;
            float panelH = lineH * rows.Length + 14f;
            float x = position.x;
            float y = position.y;

            GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, boxStyle);

            for (int i = 0; i < rows.Length; i++)
                GUI.Label(new Rect(x + 12f, y + 7f + i * lineH, panelW - 16f, lineH), rows[i], labelStyle);
        }

        private static string FormatRow(string label, string value) =>
            $"<b>{label}</b>   {value}";

        private static string Sign(float v) => v >= 0f ? "+" : "-";

        void OnDestroy()
        {
            if (bgTexture != null) Destroy(bgTexture);
        }
    }
}
