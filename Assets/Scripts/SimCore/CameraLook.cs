using UnityEngine;
using UnityEngine.InputSystem;

public class CameraLook : MonoBehaviour
{
    public float keyboardLookSpeed = 120f;
    public float gamepadLookSpeed = 180f;
    public float stickDeadzone = 0.15f;

    public Transform tiltTransform;

    public Camera[] cameras;
    private int activeCameraIndex = 0;

    private float yaw = 0f;
    private float pitch = 10f;

    void Start()
    {
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null)
                cameras[i].gameObject.SetActive(i == activeCameraIndex);
    }

    void Update()
    {
        float x = 0f;
        float y = 0f;

        ReadKeyboardInput(ref x, ref y);
        ReadGamepadInput(ref x, ref y);

        if (SwitchCameraPressed())
            CycleCamera();

        yaw += x * GetCurrentSpeed(x, y) * Time.deltaTime;
        pitch -= y * GetCurrentSpeed(x, y) * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, -30f, 50f);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Transform pitchTarget = tiltTransform != null ? tiltTransform : transform;
        pitchTarget.localRotation = Quaternion.Euler(pitch, tiltTransform != null ? 0f : yaw, 0f);
    }

    void ReadKeyboardInput(ref float x, ref float y)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Vänster / höger
        if ( keyboard.jKey.isPressed)
            x = -1f;
        else if ( keyboard.lKey.isPressed)
            x = 1f;

        // Upp / ner
        if (keyboard.iKey.isPressed )
            y = 1f;
        else if (keyboard.kKey.isPressed)
            y = -1f;
    }

    void ReadGamepadInput(ref float x, ref float y)
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return;

        bool lookMode = gamepad.rightStickButton.isPressed;
        if (!lookMode) return;

        Vector2 look = gamepad.rightStick.ReadValue();

        if (Mathf.Abs(look.x) < stickDeadzone) look.x = 0f;
        if (Mathf.Abs(look.y) < stickDeadzone) look.y = 0f;

       
    if (Mathf.Abs(look.x) > 0f)
    {
      x = look.x;
    }

    if (Mathf.Abs(look.y) > 0f)
    {
        y = look.y;
    }

    }

    float GetCurrentSpeed(float x, float y)
    {
        var gamepad = Gamepad.current;

        if (gamepad != null && gamepad.rightStickButton.isPressed && (Mathf.Abs(x) > 0f || Mathf.Abs(y) > 0f))
            return gamepadLookSpeed;

        return keyboardLookSpeed;
    }

    bool SwitchCameraPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame) return true;

        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) return true;

        return false;
    }

    void CycleCamera()
    {
        if (cameras == null || cameras.Length < 2) return;

        cameras[activeCameraIndex]?.gameObject.SetActive(false);
        activeCameraIndex = (activeCameraIndex + 1) % cameras.Length;
        cameras[activeCameraIndex]?.gameObject.SetActive(true);
    }
}