using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimCore
{
    // Adapter MonoBehaviour that exposes simple input properties using the new Unity Input System.
    // Place this on a GameObject to make Heli input accessible to other scripts.
    public class HeliInput : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        private HeliControls controls;
#endif

        public Vector2 move { get; private set; }
        public float collective { get; private set; }
        public float yaw { get; private set; }

        void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            controls = new HeliControls();
#endif
        }

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            controls.Enable();

            controls.Player.Move.performed += ctx => move = ctx.ReadValue<Vector2>();
            controls.Player.Move.canceled  += ctx => move = Vector2.zero;

            controls.Player.Collective.performed += ctx => collective = ctx.ReadValue<float>();
            controls.Player.Collective.canceled  += ctx => collective = 0f;

            controls.Player.Yaw.performed += ctx => yaw = ctx.ReadValue<float>();
            controls.Player.Yaw.canceled  += ctx => yaw = 0f;
#endif
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            controls.Disable();
#endif
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // Also sample Gamepad & Keyboard directly as fallback for producer/diagnostics
            var gp = Gamepad.current;
            if (gp != null)
            {
                move = gp.leftStick.ReadValue();
                collective = gp.rightStick.ReadValue().y * 0.5f + 0.5f; // map [-1,1] -> [0,1]
                yaw = gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();
            }
            else
            {
                // if no gamepad, ensure values from actions remain
            }
#endif
        }
    }
}
