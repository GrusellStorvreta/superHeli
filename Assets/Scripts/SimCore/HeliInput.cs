using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimCore
{
    // Adapter MonoBehaviour that exposes simple input properties using the new Unity Input System.
    // Loads a HeliControls.inputactions asset placed in Assets/Resources if present; otherwise falls back to Gamepad/Keyboard sampling.
    public class HeliInput : MonoBehaviour
    {
        public Vector2 move { get; private set; }
        public float collective { get; private set; }
        public float yaw { get; private set; }

#if ENABLE_INPUT_SYSTEM
        private InputActionAsset _actionsAsset;
        private InputAction _moveAction;
        private InputAction _collectiveAction;
        private InputAction _yawAction;

        void Awake()
        {
            // Try to load InputActions asset stored in Resources/HeliControls.inputactions
            var ta = Resources.Load<TextAsset>("HeliControls");
            if (ta != null)
            {
                try
                {
                    _actionsAsset = InputActionAsset.FromJson(ta.text);
                    var map = _actionsAsset.FindActionMap("Player");
                    if (map != null)
                    {
                        _moveAction = map.FindAction("Move");
                        _collectiveAction = map.FindAction("Collective");
                        _yawAction = map.FindAction("Yaw");

                        _moveAction?.Enable();
                        _collectiveAction?.Enable();
                        _yawAction?.Enable();

                        Debug.Log("HeliInput: Loaded InputActionAsset from Resources/HeliControls.inputactions");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("HeliInput: Failed to load InputActionAsset: " + ex.Message);
                }
            }
        }

        void OnDestroy()
        {
            _moveAction?.Disable();
            _collectiveAction?.Disable();
            _yawAction?.Disable();
        }
#endif

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // If InputActionAsset is available, read from actions
            if (_actionsAsset != null && _moveAction != null)
            {
                move = _moveAction.ReadValue<Vector2>();
                collective = _collectiveAction != null ? _collectiveAction.ReadValue<float>() : 0f;
                yaw = _yawAction != null ? _yawAction.ReadValue<float>() : 0f;
                return;
            }

            // Fallback: prefer Gamepad.current
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
                // collective mapped to A/Z (hold)
                collective = (kb.aKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f);
                yaw = (kb.mKey.isPressed ? 1f : 0f) - (kb.nKey.isPressed ? 1f : 0f);
                return;
            }
#else
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
#endif
        }
    }
}
