using UnityEngine;

namespace SimCore
{
    public class SimulatorHUD : MonoBehaviour
    {
        public Vector2 position = new Vector2(16, 16);
        public int fontSize = 17;
        public bool show = true;

        public SimulatorDriver driver;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
        private Texture2D bgTexture;
        private Texture2D gimbalBgTexture;
        private Texture2D dotTexture;

        private const float MsToKnots = 1.94384f;
        private const float MToFeet   = 3.28084f;
        private const float MaxTilt   = 45f;

        private const int   GimbalTexSize  = 140;
        private const int   GimbalOuterR   = 62;
        private const int   GimbalInnerR   = 7;
        private const float GimbalDispSize = 140f;
        private const int   DotTexSize     = 9;
        private const float DotDispSize    = 9f;

        void OnEnable() => BuildStyles();

        void BuildStyles()
        {
            bgTexture = MakeFillTexture(new Color(0.04f, 0.06f, 0.1f, 0.82f));

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = bgTexture;

            Color amber = new Color(1f, 0.78f, 0.08f);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText  = true
            };
            labelStyle.normal.textColor = amber;

            gimbalBgTexture = MakeGimbalTexture(GimbalTexSize, GimbalOuterR, GimbalInnerR, amber);
            dotTexture      = MakeDotTexture(DotTexSize, amber);
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

            // Flight data
            float heading    = rot.eulerAngles.y;
            float fwdSpeed   = Vector3.Dot(vel, rot * Vector3.forward);
            float airspeedKt = fwdSpeed * MsToKnots;

            float groundY = 0f;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
                groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;

            float altAglFt = (pos.y - groundY) * MToFeet;
            float altMslFt = pos.y * MToFeet;

            float vsMs  = Mathf.Abs(vel.y) > 0.05f ? vel.y : 0f; // suppress jitter when on ground
            float vsFpm = vsMs * MToFeet * 60f;

            // Attitude (normalize euler to -180..180)
            Vector3 euler = rot.eulerAngles;
            float pitch = euler.x > 180f ? -(360f - euler.x) : -euler.x; // + = nose up on display
            float roll  = euler.z > 180f ? -(360f - euler.z) :  euler.z; // + = right on display
            pitch = Mathf.Clamp(pitch, -MaxTilt, MaxTilt);
            roll  = Mathf.Clamp(roll,  -MaxTilt, MaxTilt);

            // Layout
            float lineH    = fontSize + 10f;
            float textW    = 172f;
            float padding  = 12f;
            float panelH   = Mathf.Max(lineH * 5 + 14f, GimbalDispSize + 14f);
            float panelW   = textW + GimbalDispSize + padding * 2f;
            float x = position.x;
            float y = position.y;

            GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, boxStyle);

            // Text rows
            float textY = y + (panelH - lineH * 4) * 0.5f;
            string[] rows =
            {
                FormatRow("HDG", $"{heading:000}°"),
                FormatRow("SPD", $"{Sign(airspeedKt)}{Mathf.Abs(airspeedKt):F0} kt"),
                FormatRow("V/S", $"{Sign(vsFpm)}{Mathf.Abs(vsFpm):F0} fpm"),
                FormatRow("MSL", $"{altMslFt:F0} ft"),
                FormatRow("AGL", $"{altAglFt:F0} ft"),
            };
            for (int i = 0; i < rows.Length; i++)
                GUI.Label(new Rect(x + padding, textY + i * lineH, textW, lineH), rows[i], labelStyle);

            // Gimbal
            float gx = x + textW + padding;
            float gy = y + (panelH - GimbalDispSize) * 0.5f;
            GUI.DrawTexture(new Rect(gx, gy, GimbalDispSize, GimbalDispSize), gimbalBgTexture);

            // Dot position: map tilt to display coords
            float scale = (GimbalDispSize * 0.5f * GimbalOuterR / GimbalTexSize) / MaxTilt;
            float cx = gx + GimbalDispSize * 0.5f;
            float cy = gy + GimbalDispSize * 0.5f;
            float dx = cx + roll  * scale - DotDispSize * 0.5f;
            float dy = cy - pitch * scale - DotDispSize * 0.5f; // Y inverted in screen space
            GUI.DrawTexture(new Rect(dx, dy, DotDispSize, DotDispSize), dotTexture);
        }

        // --- Texture helpers ---

        private static Texture2D MakeFillTexture(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static Texture2D MakeGimbalTexture(int size, int outerR, int innerR, Color col)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            int midR = outerR / 2; // mid-range reference ring

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float ao  = 1f - Mathf.Clamp01(Mathf.Abs(d - outerR) - 0.5f);
                float ai  = 1f - Mathf.Clamp01(Mathf.Abs(d - innerR) - 0.5f);
                float amid = (1f - Mathf.Clamp01(Mathf.Abs(d - midR) - 0.5f)) * 0.4f;

                // Crosshair: thin lines through center, fading outside outer ring
                float distH = Mathf.Abs(y - cy);
                float distV = Mathf.Abs(x - cx);
                float crossH = (1f - Mathf.Clamp01(distH - 0.5f)) * Mathf.Clamp01(outerR - d) * 0.5f;
                float crossV = (1f - Mathf.Clamp01(distV - 0.5f)) * Mathf.Clamp01(outerR - d) * 0.5f;
                float cross  = Mathf.Max(crossH, crossV);

                float a = Mathf.Max(Mathf.Max(ao, ai), Mathf.Max(amid, cross)) * col.a;
                pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
            }

            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        private static Texture2D MakeDotTexture(int size, Color col)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float r  = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = (1f - Mathf.Clamp01(d - r + 1f)) * col.a;
                pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
            }

            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        private static string FormatRow(string label, string value) => $"{label}   {value}";
        private static string Sign(float v) => v >= 0f ? "+" : "-";

        void OnDestroy()
        {
            if (bgTexture      != null) Destroy(bgTexture);
            if (gimbalBgTexture != null) Destroy(gimbalBgTexture);
            if (dotTexture      != null) Destroy(dotTexture);
        }
    }
}
