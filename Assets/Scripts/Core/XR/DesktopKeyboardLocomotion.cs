using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Tesis VR/Desktop Keyboard Locomotion (Editor Only)")]
public sealed class DesktopKeyboardLocomotion : MonoBehaviour
{
    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform mainCamera;

    [SerializeField, Min(0f)]
    private float moveSpeed = 1.5f;

    [SerializeField]
    private float gravity = -9.81f;

    [SerializeField]
    private bool enableKeyboardLocomotion = true;

    [SerializeField]
    private bool debugLogs;

    private float verticalVelocity;
    private float nextDebugLogTime;

    public void ConfigureReferences(
        CharacterController controller,
        Transform cameraTransform,
        bool enableLocomotion)
    {
        characterController = controller;
        mainCamera = cameraTransform;
        enableKeyboardLocomotion = enableLocomotion;
    }

    public bool HasExpectedReferences(
        CharacterController controller,
        Transform cameraTransform)
    {
        return characterController == controller && mainCamera == cameraTransform;
    }

    private void Awake()
    {
        if (characterController == null || mainCamera == null)
        {
            Debug.LogError(
                "DesktopKeyboardLocomotion: faltan referencias. Ejecuta " +
                "Tools > Tesis VR > Configure Player Collision.",
                this);
            enabled = false;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        gravity = Mathf.Min(0f, gravity);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!enableKeyboardLocomotion || !characterController.enabled)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 input = new Vector2(
            ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed),
            ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed));
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(mainCamera.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 requestedMotion = right * input.x + forward * input.y;

        CollisionFlags movementFlags =
            characterController.Move(requestedMotion * moveSpeed * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        CollisionFlags gravityFlags =
            characterController.Move(Vector3.up * (verticalVelocity * Time.deltaTime));

        LogMovement(input, requestedMotion, movementFlags | gravityFlags);
#endif
    }

    private static float ReadAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }

    private void LogMovement(
        Vector2 input,
        Vector3 requestedMotion,
        CollisionFlags collisionFlags)
    {
        if (!debugLogs || Time.unscaledTime < nextDebugLogTime)
            return;

        nextDebugLogTime = Time.unscaledTime + 0.5f;
        Debug.Log(
            $"DesktopKeyboardLocomotion: input={input}, requested={requestedMotion}, " +
            $"collisionFlags={collisionFlags}, origin={transform.position}",
            this);
    }
}
