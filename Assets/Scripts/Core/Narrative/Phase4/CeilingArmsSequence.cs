using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CeilingArmsSequence : MonoBehaviour
{
    public enum SequenceState
    {
        Idle,
        Waiting,
        WaitingForLook,
        ArmsVisible,
        Completed
    }

    [SerializeField]
    private CeilingArmsView armsView;

    [SerializeField]
    private SimpleArmMovement armMovement;

    [SerializeField]
    private WindowLookDetector lookDetector;

    [SerializeField]
    private AudioSource roofImpactsAudioSource;

    [SerializeField]
    private AudioSource ceilingGuidanceAudioSource;

    [SerializeField]
    private AudioClip roofImpactsClip;

    [SerializeField]
    private AudioClip ceilingGuidanceClip;

    [SerializeField, Min(0f)]
    private float phaseStartDelay = 3f;

    [SerializeField, Min(0f)]
    private float guidanceDelayAfterImpacts = 1f;

    [SerializeField, Min(0.01f)]
    private float maximumLookWait = 10f;

    [SerializeField, Min(0f)]
    private float armsVisibleDuration = 4f;

    [SerializeField, Range(0f, 1f)]
    private float roofImpactsVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float ceilingGuidanceVolume = 1f;

    [SerializeField]
    private bool debugLogs;

    private Coroutine sequenceCoroutine;
    private bool lookConfirmed;

    public SequenceState CurrentState { get; private set; } = SequenceState.Idle;

    public bool HasActivated { get; private set; }

    private void Awake()
    {
        armsView?.ResetView();
        armMovement?.ResetMovement();
        lookDetector?.ResetDetection();
        ConfigureAudioSource(roofImpactsAudioSource, roofImpactsClip, roofImpactsVolume);
        ConfigureAudioSource(ceilingGuidanceAudioSource, ceilingGuidanceClip, ceilingGuidanceVolume);
        CurrentState = SequenceState.Idle;
    }

    private void OnDisable()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = null;
        lookDetector?.StopDetection();
        armMovement?.ResetMovement();
        armsView?.Hide();
        StopAudio(roofImpactsAudioSource);
        StopAudio(ceilingGuidanceAudioSource);
    }

    private void OnValidate()
    {
        phaseStartDelay = Mathf.Max(0f, phaseStartDelay);
        guidanceDelayAfterImpacts = Mathf.Max(0f, guidanceDelayAfterImpacts);
        maximumLookWait = Mathf.Max(0.01f, maximumLookWait);
        armsVisibleDuration = Mathf.Max(0f, armsVisibleDuration);
        roofImpactsVolume = Mathf.Clamp01(roofImpactsVolume);
        ceilingGuidanceVolume = Mathf.Clamp01(ceilingGuidanceVolume);
    }

    public void BeginSequence()
    {
        if (HasActivated)
            return;

        if (armsView == null || armMovement == null)
        {
            Debug.LogError("CeilingArmsSequence has missing arms references.", this);
            return;
        }

        HasActivated = true;
        lookConfirmed = false;
        armsView.ResetView();
        armMovement.ResetMovement();
        lookDetector?.ResetDetection();
        sequenceCoroutine = StartCoroutine(PlaySequence());
        Log("Sequence activated.");
    }

    [ContextMenu("Play Ceiling Arms Sequence For Testing")]
    public void PlayForTesting()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode before running the isolated ceiling arms test.", this);
            return;
        }

        BeginSequence();
    }

    public void HandleLookConfirmed()
    {
        if (CurrentState != SequenceState.WaitingForLook || lookConfirmed)
            return;

        lookConfirmed = true;
        lookDetector?.StopDetection();
        Log("Ceiling look confirmed.");
    }

    private IEnumerator PlaySequence()
    {
        CurrentState = SequenceState.Waiting;
        if (phaseStartDelay > 0f)
            yield return new WaitForSeconds(phaseStartDelay);

        PlayAudio(roofImpactsAudioSource, roofImpactsClip, roofImpactsVolume, "roof impacts");

        if (guidanceDelayAfterImpacts > 0f)
            yield return new WaitForSeconds(guidanceDelayAfterImpacts);

        PlayAudio(
            ceilingGuidanceAudioSource,
            ceilingGuidanceClip,
            ceilingGuidanceVolume,
            "ceiling guidance");

        lookDetector?.ResetDetection();
        bool detectionStarted = lookDetector != null && lookDetector.BeginDetection();
        if (!detectionStarted)
        {
            Debug.LogWarning(
                "CeilingArmsSequence could not start ceiling look detection. " +
                "The sequence will continue after the look timeout.",
                this);
        }

        CurrentState = SequenceState.WaitingForLook;
        float elapsedLookWait = 0f;
        while (!lookConfirmed && elapsedLookWait < maximumLookWait)
        {
            elapsedLookWait += Time.deltaTime;
            yield return null;
        }

        lookDetector?.StopDetection();
        CurrentState = SequenceState.ArmsVisible;
        armsView.Show();

        if (armMovement.PlayMovement())
        {
            while (armMovement.IsMoving)
                yield return null;
        }
        else
        {
            Debug.LogWarning(
                "CeilingArmsSequence could not start the arm movement. " +
                "The visible phase will continue.",
                this);
        }

        if (armsVisibleDuration > 0f)
            yield return new WaitForSeconds(armsVisibleDuration);

        armsView.Hide();
        lookDetector?.StopDetection();
        StopAudio(roofImpactsAudioSource);
        CurrentState = SequenceState.Completed;
        sequenceCoroutine = null;
        Log(lookConfirmed ? "Sequence completed after ceiling look." : "Sequence completed after look timeout.");
    }

    private void PlayAudio(AudioSource source, AudioClip clip, float volume, string cueName)
    {
        if (source == null || clip == null)
        {
            Debug.LogWarning(
                $"CeilingArmsSequence cannot play {cueName}; its AudioSource or AudioClip is missing. " +
                "The visual sequence will continue.",
                this);
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
            Debug.Log($"CeilingArmsSequence: {message}", this);
    }
}
