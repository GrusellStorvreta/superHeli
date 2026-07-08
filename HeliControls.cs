using UnityEngine;
using UnityEngine.InputSystem;

public class HeliInput : MonoBehaviour
{
    private HeliControls controls;

    public Vector2 move;
    public float collective;
    public float yaw;

    private void Awake()
    {
        controls = new HeliControls();
    }

    private void OnEnable()
    {
        controls.Enable();

        controls.Player.Move.performed += ctx => move = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled  += ctx => move = Vector2.zero;

        controls.Player.Collective.performed += ctx => collective = ctx.ReadValue<float>();
        controls.Player.Collective.canceled  += ctx => collective = 0;

        controls.Player.Yaw.performed += ctx => yaw = ctx.ReadValue<float>();
        controls.Player.Yaw.canceled  += ctx => yaw = 0;
    }

    private void OnDisable()
    {
        controls.Disable();
    }
}
