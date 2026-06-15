using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimCore
{
    // Adapter MonoBehaviour that exposes simple input properties using the new Unity Input System.
    // If the Input System package is not present, falls back to legacy Input API.
    public class HeliInput : MonoBehaviour
    {
        public Vector2 move { get; private set; }
        public float collective { get; private set; }
        public float yaw { get; private set; }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // Prefer Gamepad.current
            var gp = Gamepad.current;
            if (gp != null)
            {
                move = gp.leftStick.ReadValue();
                collective = gp.rightStick.ReadValue().y * 0.5f + 0.5f; // map [-1,1] -> [0,1]
                yaw = gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue(); // triggers 0..1 so diff in [-1,1]
                return;
            }

            // Fallback to keyboard using Input System
            var kb = Keyboard.current;
            if (kb != null)
            {
                move = new Vector2((kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f),
                                   (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f));
                // collective mapped to A/Z
                collective = (kb.aKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f);
                yaw = (kb.mKey.isPressed ? 1f : 0f) - (kb.nKey.isPressed ? 1f : 0f);
                return;
            }
#endif

            // Legacy fallback (when Input System not enabled)
            try
            {
                float leftX = Input.GetAxis("Horizontal");
                float leftY = Input.GetAxis("Vertical");
                move = new Vector2(leftX, leftY);
                collective = (Input.GetAxis("Axis 5") + 1f) * 0.5f; // Right stick Y -> Axis 5
                // Combined triggers Axis 3 assumed
                float combined = Input.GetAxis("Axis 3");
                yaw = combined; // pass through
            }
            catch (System.Exception)
            {
                // ignore
            }
        }
    }
}
