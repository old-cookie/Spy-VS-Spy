using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A simple free-look camera for debugging and scene exploration.
/// </summary>
public class FreeCamera : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float sensitivity = 2f;

    private float rotationX;
    private float rotationY;

    private InputSystem_Actions inputActions;
    private InputAction arrowAction;
    private InputAction pickAction;
    private InputAction useAction;

    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }

        arrowAction = inputActions.MiniGame.Arrow;
        pickAction = inputActions.Player.Pick;
        useAction = inputActions.Player.Use;

        arrowAction.Enable();
        pickAction.Enable();
        useAction.Enable();

        // Initialize rotation from the camera's current orientation
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.y;
        rotationY = -euler.x;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        arrowAction?.Disable();
        pickAction?.Disable();
        useAction?.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        // Mouse look
        rotationX += Input.GetAxis("Mouse X") * sensitivity;
        rotationY += Input.GetAxis("Mouse Y") * sensitivity;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up) * Quaternion.AngleAxis(rotationY, Vector3.left);
    }

    private void HandleMovement()
    {
        // Use Mini Game/Arrow for planar movement (arrow keys / dpad)
        Vector2 move = arrowAction.ReadValue<Vector2>();
        float forward = Mathf.Clamp(move.y, -1f, 1f);
        float right = Mathf.Clamp(move.x, -1f, 1f);

        // Use Player/Use (Q) and Player/Pick (E) for vertical motion
        float up = 0f;
        if (pickAction.IsPressed())
        {
            up += 1f;
        }
        if (useAction.IsPressed())
        {
            up -= 1f;
        }

        Vector3 delta = (transform.forward * forward + transform.right * right + transform.up * up) * moveSpeed * Time.deltaTime;
        transform.position += delta;
    }
}
