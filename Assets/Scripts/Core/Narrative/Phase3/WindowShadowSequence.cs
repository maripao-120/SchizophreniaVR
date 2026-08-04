using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WindowShadowSequence : MonoBehaviour
{
    public enum SequenceState
    {
        Idle,
        Waiting,
        Moving,
        Completed
    }

    [SerializeField]
    private WindowShadowView shadowView;

    [SerializeField]
    private LinearShadowMovement shadowMovement;

    [SerializeField, Min(0f)]
    private float initialDelay = 4f;

    [SerializeField]
    private bool debugLogs;

    private Coroutine sequenceCoroutine;

    public SequenceState CurrentState { get; private set; } = SequenceState.Idle;

    public bool HasActivated { get; private set; }

    private void Awake()
    {
        shadowView?.ResetView();
        shadowMovement?.ResetPosition();
        CurrentState = SequenceState.Idle;
    }

    private void OnDisable()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = null;
        shadowView?.Hide();
    }

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
    }

    public void BeginSequence()
    {
        if (HasActivated)
            return;

        if (shadowView == null || shadowMovement == null)
        {
            Debug.LogError("WindowShadowSequence has missing references.", this);
            return;
        }

        HasActivated = true;
        sequenceCoroutine = StartCoroutine(PlaySequence());
        Log("Sequence activated.");
    }

    [ContextMenu("Play Shadow Sequence For Testing")]
    public void PlayForTesting()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode before running the isolated shadow test.", this);
            return;
        }

        BeginSequence();
    }

    private IEnumerator PlaySequence()
    {
        CurrentState = SequenceState.Waiting;
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        shadowMovement.ResetPosition();
        shadowView.Show();
        CurrentState = SequenceState.Moving;

        if (!shadowMovement.PlayMovement())
        {
            shadowView.Hide();
            CurrentState = SequenceState.Completed;
            sequenceCoroutine = null;
            yield break;
        }

        while (shadowMovement.IsMoving)
            yield return null;

        shadowView.Hide();
        CurrentState = SequenceState.Completed;
        sequenceCoroutine = null;
        Log("Sequence completed.");
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"WindowShadowSequence: {message}", this);
    }
}
