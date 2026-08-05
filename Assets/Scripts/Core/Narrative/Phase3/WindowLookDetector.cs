using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class WindowLookDetector : MonoBehaviour
{
    [SerializeField]
    private Transform cameraTransform;

    [SerializeField]
    private Transform lookTarget;

    [SerializeField, Range(0.1f, 180f)]
    private float maximumLookAngle = 25f;

    [SerializeField, Min(0f)]
    private float requiredLookDuration = 0.5f;

    [SerializeField]
    private UnityEvent onLookConfirmed = new UnityEvent();

    [SerializeField]
    private bool debugLogs;

    private float currentLookDuration;
    private bool detectionActive;
    private bool hasConfirmed;
    private bool missingReferenceWarningShown;

    public UnityEvent OnLookConfirmed => onLookConfirmed;

    public bool IsDetectionActive => detectionActive;

    public bool HasConfirmed => hasConfirmed;

    private void Awake()
    {
        ResetDetection();
    }

    private void OnDisable()
    {
        detectionActive = false;
        currentLookDuration = 0f;
    }

    private void OnValidate()
    {
        maximumLookAngle = Mathf.Clamp(maximumLookAngle, 0.1f, 180f);
        requiredLookDuration = Mathf.Max(0f, requiredLookDuration);
    }

    private void Update()
    {
        if (!detectionActive || hasConfirmed)
            return;

        if (cameraTransform == null || lookTarget == null)
        {
            WarnAboutMissingReferences();
            StopDetection();
            return;
        }

        Vector3 directionToTarget = lookTarget.position - cameraTransform.position;
        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            currentLookDuration = 0f;
            return;
        }

        float angle = Vector3.Angle(cameraTransform.forward, directionToTarget.normalized);
        if (angle > maximumLookAngle)
        {
            currentLookDuration = 0f;
            return;
        }

        currentLookDuration += Time.deltaTime;
        if (currentLookDuration >= requiredLookDuration)
            ConfirmLook();
    }

    public bool BeginDetection()
    {
        if (hasConfirmed || detectionActive)
            return false;

        if (cameraTransform == null || lookTarget == null)
        {
            WarnAboutMissingReferences();
            StopDetection();
            return false;
        }

        currentLookDuration = 0f;
        detectionActive = true;
        enabled = true;
        Log("Detection started.");
        return true;
    }

    public void StopDetection()
    {
        detectionActive = false;
        currentLookDuration = 0f;
        enabled = false;
    }

    public void ResetDetection()
    {
        detectionActive = false;
        hasConfirmed = false;
        missingReferenceWarningShown = false;
        currentLookDuration = 0f;
        enabled = false;
    }

    private void ConfirmLook()
    {
        if (hasConfirmed)
            return;

        hasConfirmed = true;
        detectionActive = false;
        currentLookDuration = 0f;
        enabled = false;
        Log("Look confirmed.");
        OnLookConfirmed.Invoke();
    }

    private void WarnAboutMissingReferences()
    {
        if (missingReferenceWarningShown)
            return;

        missingReferenceWarningShown = true;
        Debug.LogWarning(
            "WindowLookDetector cannot run because Camera Transform or Look Target is missing.",
            this);
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"WindowLookDetector: {message}", this);
    }
}
