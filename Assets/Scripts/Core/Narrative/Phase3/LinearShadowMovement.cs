using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LinearShadowMovement : MonoBehaviour
{
    [SerializeField]
    private Transform movingTransform;

    [SerializeField]
    private Transform startPoint;

    [SerializeField]
    private Transform endPoint;

    [SerializeField, Min(0.01f)]
    private float movementDuration = 8f;

    [SerializeField]
    private AnimationCurve movementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField]
    private bool debugLogs;

    private Coroutine movementCoroutine;
    private float pauseTimeRemaining;

    public event Action MovementCompleted;

    public bool IsMoving { get; private set; }

    public bool HasCompleted { get; private set; }

    public bool IsPaused { get; private set; }

    public bool HasPaused { get; private set; }

    public float NormalizedProgress { get; private set; }

    private void OnDisable()
    {
        if (movementCoroutine != null)
            StopCoroutine(movementCoroutine);

        movementCoroutine = null;
        IsMoving = false;
        IsPaused = false;
        pauseTimeRemaining = 0f;
    }

    private void OnValidate()
    {
        movementDuration = Mathf.Max(0.01f, movementDuration);
    }

    public void ResetPosition()
    {
        if (movementCoroutine != null)
            StopCoroutine(movementCoroutine);

        movementCoroutine = null;
        IsMoving = false;
        HasCompleted = false;
        IsPaused = false;
        HasPaused = false;
        NormalizedProgress = 0f;
        pauseTimeRemaining = 0f;

        if (movingTransform != null && startPoint != null)
            movingTransform.position = startPoint.position;
    }

    public bool PlayMovement()
    {
        if (IsMoving)
            return false;

        if (movingTransform == null || startPoint == null || endPoint == null)
        {
            Debug.LogError("LinearShadowMovement has missing Transform references.", this);
            return false;
        }

        HasCompleted = false;
        IsPaused = false;
        HasPaused = false;
        NormalizedProgress = 0f;
        pauseTimeRemaining = 0f;
        movementCoroutine = StartCoroutine(MoveBetweenPoints());
        return true;
    }

    public bool PauseBriefly(float duration)
    {
        if (!IsMoving || HasPaused || duration <= 0f)
            return false;

        HasPaused = true;
        IsPaused = true;
        pauseTimeRemaining = duration;

        if (debugLogs)
            Debug.Log($"LinearShadowMovement: paused for {duration:F2} s.", this);

        return true;
    }

    private IEnumerator MoveBetweenPoints()
    {
        IsMoving = true;
        Vector3 startPosition = startPoint.position;
        Vector3 endPosition = endPoint.position;
        movingTransform.position = startPosition;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, movementDuration);
        while (elapsed < duration)
        {
            if (pauseTimeRemaining > 0f)
            {
                pauseTimeRemaining = Mathf.Max(0f, pauseTimeRemaining - Time.deltaTime);
                IsPaused = pauseTimeRemaining > 0f;
                yield return null;
                continue;
            }

            IsPaused = false;
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float progress = movementCurve != null
                ? movementCurve.Evaluate(normalizedTime)
                : normalizedTime;
            NormalizedProgress = Mathf.Clamp01(progress);
            movingTransform.position = Vector3.LerpUnclamped(startPosition, endPosition, progress);
            yield return null;
        }

        movingTransform.position = endPosition;
        NormalizedProgress = 1f;
        IsMoving = false;
        IsPaused = false;
        pauseTimeRemaining = 0f;
        HasCompleted = true;
        movementCoroutine = null;
        MovementCompleted?.Invoke();

        if (debugLogs)
            Debug.Log("LinearShadowMovement: traversal completed.", this);
    }
}
