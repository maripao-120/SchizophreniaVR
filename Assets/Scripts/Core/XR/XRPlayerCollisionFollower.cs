using Unity.XR.CoreUtils;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class XRPlayerCollisionFollower : MonoBehaviour
{
    [SerializeField]
    private XROrigin xrOrigin;

    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform xrCamera;

    public void ConfigureReferences(
        XROrigin origin,
        CharacterController controller,
        Transform cameraTransform)
    {
        xrOrigin = origin;
        characterController = controller;
        xrCamera = cameraTransform;
    }

    private void Awake()
    {
        Debug.LogWarning(
            "XRPlayerCollisionFollower está obsoleto y permanece desactivado: " +
            "Full Body Translate representa tracking y no debe convertirse en locomoción física.",
            this);
        enabled = false;
    }

    private void OnValidate()
    {
        if (enabled)
            enabled = false;
    }

    public bool HasExpectedReferences(
        XROrigin origin,
        CharacterController controller,
        Transform cameraTransform)
    {
        return xrOrigin == origin &&
               characterController == controller &&
               xrCamera == cameraTransform;
    }
}
