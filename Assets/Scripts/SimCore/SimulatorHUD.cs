using UnityEngine;

namespace SimCore
{
    // IMGUI-based HUD: avoids UnityEngine.UI dependency by drawing directly in OnGUI.
    [ExecuteAlways]
    public class SimulatorHUD : MonoBehaviour
    {
        public Vector2 margin = new Vector2(10, 10);
        public int fontSize = 14;
        public Color fontColor = Color.white;
        public bool show = true;

        private SimulatorDriver driver;
        private string[] labels = new string[] { "Cyclic X:", "Cyclic Y:", "Left Pedal:", "Right Pedal:", "Collective:" };
        private GUIStyle style;

        void OnEnable()
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.normal.textColor = fontColor;
        }

        void OnValidate()
        {
            if (style != null)
            {
                style.fontSize = fontSize;
                style.normal.textColor = fontColor;
            }
        }

        void OnGUI()
        {
            if (!show) return;

            // Lazily initialize style if null
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontSize = fontSize;
                style.normal.textColor = fontColor;
            }

            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            if (driver == null) return;

            var ctrl = driver.LastBufferedControl;
            float cyclicX = (float)ctrl.cyclic_x;
            float cyclicY = (float)ctrl.cyclic_y;
            float collective = (float)ctrl.collective;
            float left = driver.LastBufferedLeftTrigger;
            float right = driver.LastBufferedRightTrigger;

            string[] lines = new string[5];
            lines[0] = string.Format("{0} {1:F2}", labels[0], cyclicX);
            lines[1] = string.Format("{0} {1:F2}", labels[1], cyclicY);
            lines[2] = string.Format("{0} {1:F2}", labels[2], left);
            lines[3] = string.Format("{0} {1:F2}", labels[3], right);
            lines[4] = string.Format("{0} {1:F2}", labels[4], collective);

            float x = margin.x;
            float y = margin.y;
            for (int i = 0; i < lines.Length; i++)
            {
                Rect r = new Rect(x, y, 400f, fontSize + 6);
                GUI.Label(r, lines[i], style);
                y += fontSize + 4;
            }
        }
    }
}
