using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SimpleArmMovement : MonoBehaviour
{
    [SerializeField]
    private Transform leftForearm;

    [SerializeField]
    private Transform rightForearm;

    [SerializeField, Min(0.01f)]
    private float movementDuration = 2f;

    [SerializeField, Range(0f, 45f)]
    private float movementAngle = 15f;

    [SerializeField]
    private bool debugLogs;

    private Coroutine movementCoroutine;
    private Quaternion leftInitialRotation;
    private Quaternion rightInitialRotation;
    private bool initialPoseCached;

    public bool IsMoving { get; private set; }

    public bool HasCompleted { get; private set; }

    private void Awake()
    {
        CacheInitialPose();
        ResetMovement();
    }

    private void OnDisable()
    {
        ResetMovement();
    }

    private void OnValidate()
    {
        movementDuration = Mathf.Max(0.01f, movementDuration);
        movementAngle = Mathf.Clamp(movementAngle, 0f, 45f);
    }

    public bool PlayMovement()
    {
        if (IsMoving)
            return false;

        if (leftForearm == null || rightForearm == null)
        {
            Debug.LogError("SimpleArmMovement has missing forearm references.", this);
            return false;
        }

        if (!initialPoseCached)
            CacheInitialPose();

        RestoreInitialPose();
        HasCompleted = false;
        movementCoroutine = StartCoroutine(MoveForearms());
        return true;
    }

    public void ResetMovement()
    {
        if (movementCoroutine != null)
            StopCoroutine(movementCoroutine);

        movementCoroutine = null;
        IsMoving = false;
        HasCompleted = false;
        if (initialPoseCached)
            RestoreInitialPose();
    }

    private IEnumerator MoveForearms()
    {
        IsMoving = true;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, movementDuration);
        Quaternion leftTarget = leftInitialRotation * Quaternion.Euler(0f, 0f, movementAngle);
        Quaternion rightTarget = rightInitialRotation * Quaternion.Euler(0f, 0f, -movementAngle);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            leftForearm.localRotation = Quaternion.Slerp(leftInitialRotation, leftTarget, progress);
            rightForearm.localRotation = Quaternion.Slerp(rightInitialRotation, rightTarget, progress);
            yield return null;
        }

        leftForearm.localRotation = leftTarget;
        rightForearm.localRotation = rightTarget;
        IsMoving = false;
        HasCompleted = true;
        movementCoroutine = null;
        Log("Movement completed.");
    }

    private void CacheInitialPose()
    {
        if (leftForearm == null || rightForearm == null)
            return;

        leftInitialRotation = leftForearm.localRotation;
        rightInitialRotation = rightForearm.localRotation;
        initialPoseCached = true;
    }

    private void RestoreInitialPose()
    {
        if (leftForearm != null)
            leftForearm.localRotation = leftInitialRotation;
        if (rightForearm != null)
            rightForearm.localRotation = rightInitialRotation;
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"SimpleArmMovement: {message}", this);
    }
}
