using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class FirstHallucinationSequence : MonoBehaviour
{
    public enum PhaseState
    {
        Exploration,
        GuidancePlaying,
        WaitingForBottle,
        HallucinationPlaying,
        Completed
    }

    [Header("References")]
    [SerializeField]
    private NoteView noteView;

    [SerializeField]
    private AudioSource guidanceAudioSource;

    [SerializeField]
    private AudioSource hallucinationAudioSource;

    [SerializeField]
    private AudioClip guidanceClip;

    [SerializeField]
    private AudioClip firstHallucinationClip;

    [SerializeField]
    private Light[] affectedLights = System.Array.Empty<Light>();

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float guidanceDelay = 10f;

    [SerializeField, Min(0f)]
    private float noteChangeDelay = 0.75f;

    [SerializeField, Min(0f)]
    private float lightTransitionDuration = 1.5f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)]
    private float guidanceVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float hallucinationVolume = 0.9f;

    [Header("Lighting")]
    [SerializeField, Range(0f, 1f)]
    private float alteredIntensityMultiplier = 0.82f;

    [SerializeField]
    private bool tintAffectedLights = true;

    [SerializeField]
    private Color alteredLightColor = new Color(0.92f, 0.78f, 0.72f, 1f);

    [Header("Events")]
    [SerializeField]
    private UnityEvent onCompleted = new UnityEvent();

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs;

    private float[] initialLightIntensities;
    private Color[] initialLightColors;
    private Coroutine guidanceCoroutine;
    private Coroutine hallucinationCoroutine;
    private bool hallucinationStarted;
    private bool lightingCaptured;
    private bool completionEventInvoked;

    public PhaseState CurrentState { get; private set; } = PhaseState.Exploration;

    public bool HasStartedHallucination => hallucinationStarted;

    public UnityEvent OnCompleted => onCompleted;

    private void Awake()
    {
        noteView?.ShowInitialState();
        StopAndConfigureAudioSources();
        CaptureInitialLighting();
        CurrentState = PhaseState.Exploration;
        completionEventInvoked = false;
    }

    private void Start()
    {
        if (noteView == null)
            Debug.LogError("FirstHallucinationSequence has no NoteView assigned.", this);

        guidanceCoroutine = StartCoroutine(WaitForGuidance());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        guidanceCoroutine = null;
        hallucinationCoroutine = null;

        if (guidanceAudioSource != null)
            guidanceAudioSource.Stop();
        if (hallucinationAudioSource != null)
            hallucinationAudioSource.Stop();

        RestoreInitialLighting();
    }

    private void OnValidate()
    {
        guidanceDelay = Mathf.Max(0f, guidanceDelay);
        noteChangeDelay = Mathf.Max(0f, noteChangeDelay);
        lightTransitionDuration = Mathf.Max(0f, lightTransitionDuration);
        guidanceVolume = Mathf.Clamp01(guidanceVolume);
        hallucinationVolume = Mathf.Clamp01(hallucinationVolume);
        alteredIntensityMultiplier = Mathf.Clamp01(alteredIntensityMultiplier);
    }

    /// <summary>Starts the Phase 2 alteration once and cancels independent guidance.</summary>
    public void BeginHallucination()
    {
        if (hallucinationStarted)
            return;

        hallucinationStarted = true;

        if (guidanceCoroutine != null)
        {
            StopCoroutine(guidanceCoroutine);
            guidanceCoroutine = null;
        }

        if (guidanceAudioSource != null)
            guidanceAudioSource.Stop();

        if (hallucinationAudioSource != null)
            hallucinationAudioSource.Stop();

        CurrentState = PhaseState.HallucinationPlaying;
        hallucinationCoroutine = StartCoroutine(PlayHallucination());
        Log("First grab received; guidance cancelled and hallucination started.");
    }

    private IEnumerator WaitForGuidance()
    {
        if (guidanceDelay > 0f)
            yield return new WaitForSeconds(guidanceDelay);

        if (hallucinationStarted)
            yield break;

        if (guidanceAudioSource == null || guidanceClip == null)
        {
            Debug.LogWarning(
                "FirstHallucinationSequence cannot play guidance because its source or clip is missing.",
                this);
            CurrentState = PhaseState.WaitingForBottle;
            guidanceCoroutine = null;
            yield break;
        }

        guidanceAudioSource.clip = guidanceClip;
        guidanceAudioSource.volume = guidanceVolume;
        guidanceAudioSource.Play();
        CurrentState = PhaseState.GuidancePlaying;
        Log("Guidance started.");

        // Observe AudioSource state instead of coupling the flow to today's clip duration.
        while (!hallucinationStarted && guidanceAudioSource.isPlaying)
            yield return null;

        if (!hallucinationStarted)
            CurrentState = PhaseState.WaitingForBottle;

        guidanceCoroutine = null;
    }

    private IEnumerator PlayHallucination()
    {
        if (hallucinationAudioSource != null && firstHallucinationClip != null)
        {
            hallucinationAudioSource.clip = firstHallucinationClip;
            hallucinationAudioSource.volume = hallucinationVolume;
            hallucinationAudioSource.Play();
        }
        else
        {
            Debug.LogWarning(
                "FirstHallucinationSequence cannot play the hallucination audio; visual flow continues.",
                this);
        }

        float totalDuration = Mathf.Max(noteChangeDelay, lightTransitionDuration);
        float elapsed = 0f;
        bool noteAltered = false;

        if (noteChangeDelay <= 0f)
        {
            noteView?.ShowAlteredState();
            noteAltered = true;
        }

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            if (!noteAltered && elapsed >= noteChangeDelay)
            {
                noteView?.ShowAlteredState();
                noteAltered = true;
            }

            ApplyLightingTransition(lightTransitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / lightTransitionDuration));
            yield return null;
        }

        if (!noteAltered)
            noteView?.ShowAlteredState();
        ApplyLightingTransition(1f);

        CurrentState = PhaseState.Completed;
        Log("Phase 2 visual flow completed.");

        while (hallucinationAudioSource != null && hallucinationAudioSource.isPlaying)
            yield return null;

        hallucinationCoroutine = null;
        if (!completionEventInvoked)
        {
            completionEventInvoked = true;
            OnCompleted.Invoke();
            Log("Phase 2 completion event invoked.");
        }
    }

    private void StopAndConfigureAudioSources()
    {
        ConfigureAudioSource(guidanceAudioSource, guidanceClip, guidanceVolume);
        ConfigureAudioSource(hallucinationAudioSource, firstHallucinationClip, hallucinationVolume);
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

    private void CaptureInitialLighting()
    {
        int count = affectedLights?.Length ?? 0;
        initialLightIntensities = new float[count];
        initialLightColors = new Color[count];

        for (int index = 0; index < count; index++)
        {
            Light affectedLight = affectedLights[index];
            if (affectedLight == null)
                continue;

            initialLightIntensities[index] = affectedLight.intensity;
            initialLightColors[index] = affectedLight.color;
        }

        lightingCaptured = true;
    }

    private void ApplyLightingTransition(float progress)
    {
        if (!lightingCaptured)
            return;

        for (int index = 0; index < affectedLights.Length; index++)
        {
            Light affectedLight = affectedLights[index];
            if (affectedLight == null)
                continue;

            float alteredIntensity = initialLightIntensities[index] * alteredIntensityMultiplier;
            affectedLight.intensity = Mathf.Lerp(
                initialLightIntensities[index],
                alteredIntensity,
                progress);

            if (tintAffectedLights)
                affectedLight.color = Color.Lerp(initialLightColors[index], alteredLightColor, progress);
        }
    }

    private void RestoreInitialLighting()
    {
        if (!lightingCaptured || affectedLights == null)
            return;

        for (int index = 0; index < affectedLights.Length; index++)
        {
            Light affectedLight = affectedLights[index];
            if (affectedLight == null)
                continue;

            affectedLight.intensity = initialLightIntensities[index];
            affectedLight.color = initialLightColors[index];
        }
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"FirstHallucinationSequence: {message}", this);
    }
}
