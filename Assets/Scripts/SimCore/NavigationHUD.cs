using UnityEngine;

namespace SimCore
{
    public class NavigationHUD : MonoBehaviour
    {
        [Header("Navigation")]
        public Transform homeBase;

        private MissionManager mission;
        private SimulatorDriver driver;
        private GUIStyle        labelStyle;
        private Texture2D       bgTex;
        private Texture2D       arrowTex;

        private static readonly Color Amber     = new Color(1f, 0.78f, 0.08f);
        private static readonly Color DarkPanel = new Color(0.04f, 0.06f, 0.1f, 0.82f);

        void BuildStyles()
        {
            bgTex     = MakeTex(DarkPanel);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText  = true,
            };
            labelStyle.normal.textColor = Amber;
            arrowTex = MakeArrowTexture(32, Amber);
        }

        void OnGUI()
        {
            if (mission == null) mission = FindObjectOfType<MissionManager>();
            if (driver  == null) driver  = FindObjectOfType<SimulatorDriver>();
            if (driver == null) return;
            if (labelStyle == null || bgTex == null) BuildStyles();

            Vector3? navTarget = mission?.NavigationTarget
                              ?? (homeBase != null ? homeBase.position : (Vector3?)null);
            if (navTarget == null) return;

            Vector3 heliPos = driver.LastBodyPosition;
            float   heliHdg = driver.LastBodyRotation.eulerAngles.y;
            Vector3 target  = navTarget.Value;

            Vector3 toTarget = target - heliPos;
            float   distM    = toTarget.magnitude;
            string  distStr  = distM >= 1000f
                               ? $"{distM / 1000f:F1} km"
                               : $"{distM:F0} m";

            Vector3 toFlat   = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            float   worldBrg = Mathf.Atan2(toFlat.x, toFlat.z) * Mathf.Rad2Deg;
            float   relBrg   = Mathf.DeltaAngle(heliHdg, worldBrg);
            string  brgStr   = $"{((relBrg % 360f + 360f) % 360f):F0}°";

            float sw   = Screen.width;
            float panW = 160f, panH = 80f;
            float px   = sw - panW - 16f, py = 16f;

            GUI.DrawTexture(new Rect(px, py, panW, panH), bgTex);

            float lineH = 24f;
            float lx    = px + 10f;
            GUI.Label(new Rect(lx, py + 8f,  panW - 50f, lineH), FormatRow("BRG", brgStr), labelStyle);
            GUI.Label(new Rect(lx, py + 32f, panW - 50f, lineH), FormatRow("DST", distStr), labelStyle);

            float ax    = px + panW - 44f;
            float ay    = py + (panH - 32f) * 0.5f;
            var   pivot = new Vector2(ax + 16f, ay + 16f);
            GUIUtility.RotateAroundPivot(180f - relBrg, pivot);
            GUI.DrawTexture(new Rect(ax, ay, 32f, 32f), arrowTex);
            GUI.matrix = Matrix4x4.identity;
        }

        static string FormatRow(string lbl, string val) => $"{lbl}   {val}";

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static Texture2D MakeArrowTexture(int size, Color col)
        {
            var t   = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pix = new Color[size * size];
            float cx    = size * 0.5f;
            float tipY  = 2f;
            float midY  = size * 0.55f;
            float baseY = size - 2f;
            float halfW = size * 0.25f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t2      = Mathf.Clamp01((y - tipY) / (midY - tipY));
                float hw      = t2 * halfW;
                bool inArrow  = y >= tipY && y <= midY && Mathf.Abs(x - cx) <= hw;
                bool inBody   = y > midY && y <= baseY && Mathf.Abs(x - cx) <= halfW * 0.55f;
                pix[y * size + x] = (inArrow || inBody)
                    ? new Color(col.r, col.g, col.b, 1f)
                    : Color.clear;
            }
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            if (bgTex    != null) Destroy(bgTex);
            if (arrowTex != null) Destroy(arrowTex);
        }
    }
}
