using UnityEngine;
using UnityEngine.UI;

namespace SimCore
{
    // Simple on-screen HUD that displays cyclic x/y, left/right trigger and collective in the lower-left.
    // Creates a Canvas at runtime if none exists and updates five text fields each frame.
    [ExecuteAlways]
    public class SimulatorHUD : MonoBehaviour
    {
        public Vector2 margin = new Vector2(10, 10);
        public int fontSize = 14;
        public Color fontColor = Color.white;

        private SimulatorDriver driver;
        private Canvas hudCanvas;
        private Text[] lines;
        private string[] labels = new string[] { "Cyclic X:", "Cyclic Y:", "Left Pedal:", "Right Pedal:", "Collective:" };

        void Awake()
        {
            // Try to find an existing canvas first
            hudCanvas = FindObjectOfType<Canvas>();
            if (hudCanvas == null)
            {
                var go = new GameObject("SimulatorHUD_Canvas");
                hudCanvas = go.AddComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.AddComponent<CanvasScaler>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            CreateLines();
        }

        void CreateLines()
        {
            // Create a parent container to hold texts
            var parent = new GameObject("SimulatorHUD_Panel");
            parent.transform.SetParent(hudCanvas.transform, false);
            var rt = parent.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(margin.x, margin.y);

            lines = new Text[labels.Length];
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            for (int i = 0; i < labels.Length; i++)
            {
                var lgo = new GameObject("HUD_Line_" + i);
                lgo.transform.SetParent(parent.transform, false);
                var txt = lgo.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = fontSize;
                txt.color = fontColor;
                txt.alignment = TextAnchor.LowerLeft;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;

                var lrt = lgo.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0f, 0f);
                lrt.pivot = new Vector2(0f, 0f);
                lrt.anchoredPosition = new Vector2(0f, i * (fontSize + 4));
                lrt.sizeDelta = new Vector2(400f, fontSize + 6);

                txt.text = labels[i] + " 0.00";
                lines[i] = txt;
            }
        }

        void Update()
        {
            if (driver == null)
                driver = FindObjectOfType<SimulatorDriver>();

            if (driver == null)
                return;

            var ctrl = driver.LastBufferedControl; // ControlInput

            float cyclicX = ctrl.cyclic_x;
            float cyclicY = ctrl.cyclic_y;
            float collective = ctrl.collective;
            float left = driver.LastBufferedLeftTrigger;
            float right = driver.LastBufferedRightTrigger;

            // Update strings
            lines[0].text = string.Format("{0} {1:F2}", labels[0], cyclicX);
            lines[1].text = string.Format("{0} {1:F2}", labels[1], cyclicY);
            lines[2].text = string.Format("{0} {1:F2}", labels[2], left);
            lines[3].text = string.Format("{0} {1:F2}", labels[3], right);
            lines[4].text = string.Format("{0} {1:F2}", labels[4], collective);
        }
    }
}
