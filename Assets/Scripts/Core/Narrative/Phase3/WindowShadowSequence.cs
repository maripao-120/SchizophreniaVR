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
    private WindowLookDetector lookDetector;

    [SerializeField]
    private AudioSource footstepsAudioSource;

    [SerializeField]
    private AudioSource guidanceAudioSource;

    [SerializeField]
    private AudioSource closingAudioSource;

    [SerializeField]
    private AudioClip footstepsClip;

    [SerializeField]
    private AudioClip guidanceClip;

    [SerializeField]
    private AudioClip closingClip;

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

    [SerializeField, Range(0f, 1f)]
    private float closingVolume = 1f;

    [SerializeField]
    private bool stopFootstepsWhenMovementEnds = true;

    [SerializeField, Range(0f, 1f)]
    private float minimumPauseProgress = 0.15f;

    [SerializeField, Range(0f, 1f)]
    private float maximumPauseProgress = 0.8f;

    [SerializeField, Min(0.01f)]
    private float lookReactionPauseDuration = 1.5f;

    [SerializeField]
    private bool debugLogs;

    private Coroutine sequenceCoroutine;
    private bool lookReactionConsumed;
    private bool closingAudioPlayed;

    public SequenceState CurrentState { get; private set; } = SequenceState.Idle;

    public bool HasActivated { get; private set; }

    private void Awake()
    {
        shadowView?.ResetView();
        shadowMovement?.ResetPosition();
        lookDetector?.ResetDetection();
        lookReactionConsumed = false;
        closingAudioPlayed = false;
        ConfigureAudioSource(footstepsAudioSource, footstepsClip, footstepsVolume);
        ConfigureAudioSource(guidanceAudioSource, guidanceClip, guidanceVolume);
        ConfigureAudioSource(closingAudioSource, closingClip, closingVolume);
        CurrentState = SequenceState.Idle;
    }

    private void OnDisable()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = null;
        lookDetector?.StopDetection();
        StopAudio(footstepsAudioSource);
        StopAudio(guidanceAudioSource);
        StopAudio(closingAudioSource);
        shadowView?.Hide();
    }

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        guidanceDelayAfterFootsteps = Mathf.Max(0f, guidanceDelayAfterFootsteps);
        shadowDelayAfterGuidance = Mathf.Max(0f, shadowDelayAfterGuidance);
        footstepsVolume = Mathf.Clamp01(footstepsVolume);
        guidanceVolume = Mathf.Clamp01(guidanceVolume);
        closingVolume = Mathf.Clamp01(closingVolume);
        minimumPauseProgress = Mathf.Clamp01(minimumPauseProgress);
        maximumPauseProgress = Mathf.Clamp01(maximumPauseProgress);
        lookReactionPauseDuration = Mathf.Max(0.01f, lookReactionPauseDuration);
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
        lookReactionConsumed = false;
        lookDetector?.ResetDetection();
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
            lookDetector?.StopDetection();
            shadowView.Hide();
            StopAudio(footstepsAudioSource);
            CurrentState = SequenceState.Completed;
            sequenceCoroutine = null;
            yield break;
        }

        bool detectionStarted = false;
        bool detectionWindowClosed = false;
        while (shadowMovement.IsMoving)
        {
            float progress = shadowMovement.NormalizedProgress;
            if (!lookReactionConsumed && !detectionWindowClosed && lookDetector != null)
            {
                if (!detectionStarted && progress >= minimumPauseProgress)
                {
                    if (progress <= maximumPauseProgress)
                        detectionStarted = lookDetector.BeginDetection();

                    if (!detectionStarted)
                        detectionWindowClosed = true;
                }

                if (detectionStarted && progress > maximumPauseProgress)
                {
                    lookDetector.StopDetection();
                    detectionWindowClosed = true;
                }
            }

            yield return null;
        }

        lookDetector?.StopDetection();
        shadowView.Hide();
        StopFootstepsBeforeClosing();
        PlayClosingAudio();
        CurrentState = SequenceState.Completed;
        sequenceCoroutine = null;
        Log("Sequence completed.");
    }

    public void HandleLookConfirmed()
    {
        if (lookReactionConsumed || shadowMovement == null || !shadowMovement.IsMoving)
            return;

        float progress = shadowMovement.NormalizedProgress;
        if (progress < minimumPauseProgress || progress > maximumPauseProgress)
            return;

        if (!shadowMovement.PauseBriefly(lookReactionPauseDuration))
            return;

        lookReactionConsumed = true;
        lookDetector?.StopDetection();
        Log($"Look reaction consumed at progress {progress:F2}.");
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

    private void PlayClosingAudio()
    {
        if (closingAudioPlayed)
            return;

        closingAudioPlayed = true;
        if (closingAudioSource == null)
        {
            Debug.LogWarning(
                "WindowShadowSequence cannot play the closing voice because its AudioSource is missing. " +
                "The sequence will still complete.",
                this);
            return;
        }

        if (closingClip == null)
        {
            Debug.LogWarning(
                "WindowShadowSequence cannot play the closing voice because its AudioClip is missing. " +
                "The sequence will still complete.",
                this);
            return;
        }

        closingAudioSource.clip = closingClip;
        closingAudioSource.volume = closingVolume;
        closingAudioSource.Play();
        Log("closing voice started.");
    }

    private void StopFootstepsBeforeClosing()
    {
        if (!stopFootstepsWhenMovementEnds)
            Log("Footsteps are being stopped to prevent overlap with the closing voice.");

        StopAudio(footstepsAudioSource);
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
