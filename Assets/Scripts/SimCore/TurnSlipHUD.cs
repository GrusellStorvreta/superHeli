using UnityEngine;

namespace SimCore
{
    // Turn coordinator: a wedge that slides with turn rate, and a ball that
    // slides with sideslip — same motion for both so it reads at a glance.
    // Sideslip uses the same definition used to tune the weathervane effect
    // (angle between nose heading and actual travel direction) — centered
    // ball = coordinated turn.
    public class TurnSlipHUD : MonoBehaviour
    {
        public SimulatorDriver driver;
        public bool show = true;

        public float maxTurnRateDeg  = 20f; // yaw rate (deg/s) for full wedge travel
        public float maxSlipDeg      = 30f; // sideslip (deg) for full ball travel

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.04f, 0.06f, 0.1f, 0.82f);

        private const float PanelW   = 180f;
        private const float PanelH   = 110f;
        private const float TrackW   = 140f;
        private const float WedgeSize = 22f;
        private const float BallSize  = 14f;

        private GUIStyle  captionStyle;
        private Texture2D bgTex;
        private Texture2D lineTex;
        private Texture2D wedgeTex;
        private Texture2D ballTex;

        void BuildStyles()
        {
            bgTex   = MakeTex(DarkPanel);
            lineTex = MakeTex(Amber);
            wedgeTex = MakeWedgeTexture(24, Amber);
            ballTex  = MakeDotTexture(14, Amber);

            captionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            captionStyle.normal.textColor = Amber;
        }

        void OnGUI()
        {
            if (!show) return;
            if (captionStyle == null || bgTex == null) BuildStyles();
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            if (driver == null) return;

            Vector3    vel = driver.LastBodyVelocity;
            Quaternion rot = driver.LastBodyRotation;
            float      yawRateDeg = driver.LastBodyAngularVelocity.y * Mathf.Rad2Deg;

            float sideslipDeg = 0f;
            Vector3 flatFwd = rot * Vector3.forward; flatFwd.y = 0f;
            Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
            if (flatFwd.sqrMagnitude > 0.001f && flatVel.sqrMagnitude > 1f)
                sideslipDeg = Vector3.SignedAngle(flatFwd, flatVel, Vector3.up);

            float px = (Screen.width - PanelW) * 0.5f;
            float py = Screen.height - PanelH - 16f;

            GUI.DrawTexture(new Rect(px, py, PanelW, PanelH), bgTex);

            float trackX = px + (PanelW - TrackW) * 0.5f;

            // --- Turn rate: wedge slides left/right along the track ---
            float turnRowY   = py + 14f;
            float turnLineY  = turnRowY + WedgeSize * 0.5f;
            GUI.DrawTexture(new Rect(trackX, turnLineY, TrackW, 2f), lineTex);
            DrawTick(trackX + TrackW * 0.5f, turnLineY + 1f, 14f, 2f);

            float turnFrac = Mathf.Clamp(yawRateDeg / maxTurnRateDeg, -1f, 1f);
            float wedgeX = trackX + TrackW * 0.5f + turnFrac * (TrackW * 0.5f - WedgeSize * 0.5f);
            GUI.DrawTexture(new Rect(wedgeX - WedgeSize * 0.5f, turnRowY, WedgeSize, WedgeSize), wedgeTex);

            GUI.Label(new Rect(px, turnRowY + WedgeSize + 4f, PanelW, 16f), "TURN", captionStyle);

            // --- Slip: ball slides left/right along the track ---
            float slipRowY  = turnRowY + WedgeSize + 26f;
            float slipLineY = slipRowY + BallSize * 0.5f;
            GUI.DrawTexture(new Rect(trackX, slipLineY, TrackW, 2f), lineTex);
            DrawTick(trackX + TrackW * 0.5f, slipLineY + 1f, 14f, 2f);

            float slipFrac = Mathf.Clamp(sideslipDeg / maxSlipDeg, -1f, 1f);
            float ballX = trackX + TrackW * 0.5f + slipFrac * (TrackW * 0.5f - BallSize * 0.5f);
            GUI.DrawTexture(new Rect(ballX - BallSize * 0.5f, slipRowY, BallSize, BallSize), ballTex);

            GUI.Label(new Rect(px, slipRowY + BallSize + 4f, PanelW, 16f), "SLIP", captionStyle);
        }

        // centerY is where the tick straddles the track line; height/width define the mark.
        private void DrawTick(float centerX, float centerY, float height, float width) =>
            GUI.DrawTexture(new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height), lineTex);

        // --- Texture helpers ---

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static Texture2D MakeWedgeTexture(int size, Color col)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size * 0.5f;
            float tipY = 1f;
            float baseY = size - 1f;
            float halfW = size * 0.4f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t2 = Mathf.Clamp01((y - tipY) / (baseY - tipY));
                float hw = t2 * halfW;
                bool inWedge = y >= tipY && y <= baseY && Mathf.Abs(x - cx) <= hw;
                pixels[y * size + x] = inWedge ? new Color(col.r, col.g, col.b, 1f) : Color.clear;
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

        void OnDestroy()
        {
            if (bgTex    != null) Destroy(bgTex);
            if (lineTex  != null) Destroy(lineTex);
            if (wedgeTex != null) Destroy(wedgeTex);
            if (ballTex  != null) Destroy(ballTex);
        }
    }
}
