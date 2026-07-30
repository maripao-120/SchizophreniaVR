using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class XRLocomotionDiagnostics : MonoBehaviour
{
    [SerializeField]
    private XROrigin xrOrigin;

    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private XRBodyTransformer bodyTransformer;

    [SerializeField]
    private LocomotionMediator locomotionMediator;

    [SerializeField]
    private DynamicMoveProvider moveProvider;

    [SerializeField]
    private Transform mainCamera;

    [SerializeField]
    private Transform cameraOffset;

    [SerializeField]
    private bool enableDiagnostics;

    [SerializeField, Min(0.05f)]
    private float logInterval = 0.25f;

    private float nextLogTime;
    private bool hasPreviousSample;
    private Vector3 previousOriginPosition;
    private Vector3 previousCameraPosition;
    private Vector3 previousCameraOffsetPosition;
    private Vector3 previousCapsuleCenter;

    public void ConfigureReferences(
        XROrigin origin,
        CharacterController controller,
        XRBodyTransformer transformer,
        LocomotionMediator mediator,
        DynamicMoveProvider dynamicMoveProvider,
        Transform cameraTransform,
        Transform cameraOffsetTransform)
    {
        xrOrigin = origin;
        characterController = controller;
        bodyTransformer = transformer;
        locomotionMediator = mediator;
        moveProvider = dynamicMoveProvider;
        mainCamera = cameraTransform;
        cameraOffset = cameraOffsetTransform;
    }

    public bool HasExpectedReferences(
        XROrigin origin,
        CharacterController controller,
        XRBodyTransformer transformer,
        LocomotionMediator mediator,
        DynamicMoveProvider dynamicMoveProvider,
        Transform cameraTransform,
        Transform cameraOffsetTransform)
    {
        return xrOrigin == origin &&
               characterController == controller &&
               bodyTransformer == transformer &&
               locomotionMediator == mediator &&
               moveProvider == dynamicMoveProvider &&
               mainCamera == cameraTransform &&
               cameraOffset == cameraOffsetTransform;
    }

    private void Awake()
    {
        if (!ValidateReferences())
            enableDiagnostics = false;
    }

    private void OnEnable()
    {
        ResetSampling();
    }

    private void OnValidate()
    {
        logInterval = Mathf.Max(0.05f, logInterval);
    }

    private void Update()
    {
        if (!enableDiagnostics || Time.unscaledTime < nextLogTime)
            return;

        nextLogTime = Time.unscaledTime + logInterval;
        LogSample();
    }

    private void LogSample()
    {
        Vector2 leftMove = moveProvider.leftHandMoveInput.ReadValue();
        Vector2 rightMove = moveProvider.rightHandMoveInput.ReadValue();
        Vector3 originPosition = xrOrigin.Origin.transform.position;
        Vector3 cameraPosition = mainCamera.position;
        Vector3 cameraOffsetPosition = cameraOffset.position;
        Vector3 capsuleCenter = characterController.transform.TransformPoint(characterController.center);
        Vector3 horizontalSeparation = Vector3.ProjectOnPlane(cameraPosition - capsuleCenter, Vector3.up);

        Vector3 originDelta = hasPreviousSample ? originPosition - previousOriginPosition : Vector3.zero;
        Vector3 cameraDelta = hasPreviousSample ? cameraPosition - previousCameraPosition : Vector3.zero;
        Vector3 cameraOffsetDelta = hasPreviousSample ? cameraOffsetPosition - previousCameraOffsetPosition : Vector3.zero;
        Vector3 capsuleDelta = hasPreviousSample ? capsuleCenter - previousCapsuleCenter : Vector3.zero;

        IConstrainedXRBodyManipulator manipulator = bodyTransformer.constrainedBodyManipulator;
        string manipulatorName = manipulator != null ? manipulator.GetType().FullName : "null";

        Debug.Log(
            "XRLocomotionDiagnostics | " +
            $"leftMove={leftMove} rightMove={rightMove} " +
            $"providerActive={moveProvider.isActiveAndEnabled} locomotionState={moveProvider.locomotionState} " +
            $"mediatorActive={locomotionMediator.isActiveAndEnabled} " +
            $"bodyTransformerActive={bodyTransformer.isActiveAndEnabled} " +
            $"useCharacterController={bodyTransformer.useCharacterControllerIfExists} " +
            $"constrainedManipulator={manipulatorName} " +
            $"origin={originPosition} originDelta={originDelta} " +
            $"camera={cameraPosition} cameraDelta={cameraDelta} " +
            $"cameraOffset={cameraOffsetPosition} cameraOffsetDelta={cameraOffsetDelta} " +
            $"capsuleCenter={capsuleCenter} capsuleDelta={capsuleDelta} " +
            $"horizontalSeparation={horizontalSeparation.magnitude:F4} " +
            $"controllerEnabled={characterController.enabled} grounded={characterController.isGrounded} " +
            $"height={characterController.height:F4} center={characterController.center} " +
            $"collisionFlags={characterController.collisionFlags}",
            this);

        previousOriginPosition = originPosition;
        previousCameraPosition = cameraPosition;
        previousCameraOffsetPosition = cameraOffsetPosition;
        previousCapsuleCenter = capsuleCenter;
        hasPreviousSample = true;
    }

    private bool ValidateReferences()
    {
        bool valid = xrOrigin != null &&
                     characterController != null &&
                     bodyTransformer != null &&
                     locomotionMediator != null &&
                     moveProvider != null &&
                     mainCamera != null &&
                     cameraOffset != null;

        if (!valid)
        {
            Debug.LogError(
                "XRLocomotionDiagnostics: faltan referencias. Ejecuta " +
                "Tools > Tesis VR > Configure Player Collision.",
                this);
        }

        return valid;
    }

    private void ResetSampling()
    {
        nextLogTime = 0f;
        hasPreviousSample = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (characterController == null)
            return;

        Transform controllerTransform = characterController.transform;
        Vector3 center = controllerTransform.TransformPoint(characterController.center);
        Vector3 scale = controllerTransform.lossyScale;
        float radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = characterController.height * Mathf.Abs(scale.y);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 up = controllerTransform.up;
        Vector3 top = center + up * halfSegment;
        Vector3 bottom = center - up * halfSegment;
        Vector3 right = controllerTransform.right * radius;
        Vector3 forward = controllerTransform.forward * radius;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);
        Gizmos.DrawLine(top + right, bottom + right);
        Gizmos.DrawLine(top - right, bottom - right);
        Gizmos.DrawLine(top + forward, bottom + forward);
        Gizmos.DrawLine(top - forward, bottom - forward);
        Gizmos.DrawSphere(center, Mathf.Max(0.01f, radius * 0.08f));

        if (mainCamera == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(mainCamera.position, Mathf.Max(0.015f, radius * 0.1f));
        Gizmos.DrawLine(mainCamera.position, center);
    }
}
