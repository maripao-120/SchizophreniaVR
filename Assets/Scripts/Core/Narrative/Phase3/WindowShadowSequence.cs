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

    [SerializeField]
    private AudioSource footstepsAudioSource;

    [SerializeField]
    private AudioSource guidanceAudioSource;

    [SerializeField]
    private AudioClip footstepsClip;

    [SerializeField]
    private AudioClip guidanceClip;

    [SerializeField, Min(0f)]
    private float initialDelay = 4f;

    [SerializeField, Min(0f)]
    private float guidanceDelayAfterFootsteps = 0.8f;

    [SerializeField, Min(0f)]
    private float shadowDelayAfterGuidance = 1.5f;

    [SerializeField, Range(0f, 1f)]
    private float footstepsVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float guidanceVolume = 1f;

    [SerializeField]
    private bool stopFootstepsWhenMovementEnds = true;

    [SerializeField]
    private bool debugLogs;

    private Coroutine sequenceCoroutine;

    public SequenceState CurrentState { get; private set; } = SequenceState.Idle;

    public bool HasActivated { get; private set; }

    private void Awake()
    {
        shadowView?.ResetView();
        shadowMovement?.ResetPosition();
        ConfigureAudioSource(footstepsAudioSource, footstepsClip, footstepsVolume);
        ConfigureAudioSource(guidanceAudioSource, guidanceClip, guidanceVolume);
        CurrentState = SequenceState.Idle;
    }

    private void OnDisable()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = null;
        StopAudio(footstepsAudioSource);
        StopAudio(guidanceAudioSource);
        shadowView?.Hide();
    }

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        guidanceDelayAfterFootsteps = Mathf.Max(0f, guidanceDelayAfterFootsteps);
        shadowDelayAfterGuidance = Mathf.Max(0f, shadowDelayAfterGuidance);
        footstepsVolume = Mathf.Clamp01(footstepsVolume);
        guidanceVolume = Mathf.Clamp01(guidanceVolume);
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

        PlayAudio(footstepsAudioSource, footstepsClip, footstepsVolume, "footsteps");

        if (guidanceDelayAfterFootsteps > 0f)
            yield return new WaitForSeconds(guidanceDelayAfterFootsteps);

        PlayAudio(guidanceAudioSource, guidanceClip, guidanceVolume, "window guidance");

        if (shadowDelayAfterGuidance > 0f)
            yield return new WaitForSeconds(shadowDelayAfterGuidance);

        shadowMovement.ResetPosition();
        shadowView.Show();
        CurrentState = SequenceState.Moving;

        if (!shadowMovement.PlayMovement())
        {
            shadowView.Hide();
            StopAudio(footstepsAudioSource);
            CurrentState = SequenceState.Completed;
            sequenceCoroutine = null;
            yield break;
        }

        while (shadowMovement.IsMoving)
            yield return null;

        shadowView.Hide();
        if (stopFootstepsWhenMovementEnds)
            StopAudio(footstepsAudioSource);
        CurrentState = SequenceState.Completed;
        sequenceCoroutine = null;
        Log("Sequence completed.");
    }

    private void PlayAudio(AudioSource source, AudioClip clip, float volume, string cueName)
    {
        if (source == null || clip == null)
        {
            Log($"Cannot play {cueName}; its AudioSource or AudioClip is missing. Visual flow continues.");
            return;
        }

        source.clip = clip;
        source.volume = volume;
        source.Play();
        Log($"{cueName} started.");
    }

    private static void ConfigureAudioSource(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null)
            return;

        source.Stop();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = volume;
    }

    private static void StopAudio(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"WindowShadowSequence: {message}", this);
    }
}
