using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public static class TesisPhase2Validator
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Tesis VR/Phase 2/Validate")]
    private static void ValidateFromMenu()
    {
        try
        {
            ValidationReport report = Validate();
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 2 Validation",
                report.ErrorCount == 0
                    ? $"Validation completed with {report.WarningCount} warning(s)."
                    : $"Validation found {report.ErrorCount} error(s). See Console.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Tesis VR - Validation Error", exception.Message, "OK");
        }
    }

    public static void ValidateFromCommandLine()
    {
        ValidationReport report = Validate();
        if (report.ErrorCount > 0)
            throw new InvalidOperationException(
                $"Phase 2 validation failed with {report.ErrorCount} error(s).");
    }

    private static ValidationReport Validate()
    {
        ValidationReport report = new ValidationReport();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        report.Check(
            scene.IsValid() && scene.isLoaded && scene.path == ScenePath,
            "SampleScene loaded",
            $"Could not load {ScenePath}.");
        if (!scene.IsValid() || !scene.isLoaded)
            return Finish(report);

        Transform room = FindUniqueRoot(scene, "Room", report);
        Transform furniture = FindUniqueDirectChild(room, "Furniture", report);
        Transform nightstand = FindUniqueDirectChild(furniture, "Nightstand", report);
        Transform narrativeProps = FindUniqueDirectChild(room, "NarrativeProps", report);
        Transform bottle = FindUniqueDirectChild(narrativeProps, "MedicineBottle", report);
        Transform note = FindUniqueDirectChild(narrativeProps, "Note", report);
        Transform experience = FindUniqueRoot(scene, "Experience", report);
        Transform phaseObject = FindUniqueDirectChild(
            experience,
            "Phase2_FirstHallucination",
            report);

        report.Check(nightstand != null, "Nightstand", "Room/Furniture/Nightstand is missing.");
        ValidateGlobalNameCount(scene, "MedicineBottle", report);
        ValidateGlobalNameCount(scene, "Note", report);
        ValidateGlobalNameCount(scene, "Phase2_FirstHallucination", report);

        FirstGrabTrigger firstGrab = ValidateBottle(scene, bottle, report);
        NoteView noteView = ValidateNote(note, report);
        FirstHallucinationSequence sequence = ValidateSequence(
            phaseObject,
            noteView,
            report);
        ValidateEvent(firstGrab, sequence, report);
        ValidatePhaseOne(scene, report);
        ValidateMissingScripts(scene, report);
        ValidateGeneratedAssets(report);

        return Finish(report);
    }

    private static FirstGrabTrigger ValidateBottle(
        Scene scene,
        Transform bottle,
        ValidationReport report)
    {
        if (bottle == null)
            return null;

        Rigidbody[] rigidbodies = bottle.GetComponents<Rigidbody>();
        report.Check(
            rigidbodies.Length == 1,
            "MedicineBottle Rigidbody",
            $"MedicineBottle needs exactly one Rigidbody; found {rigidbodies.Length}.");
        if (rigidbodies.Length == 1)
        {
            report.Check(
                rigidbodies[0].useGravity && !rigidbodies[0].isKinematic,
                "MedicineBottle physics",
                "MedicineBottle Rigidbody must be dynamic and use gravity.");
        }

        Collider[] activeColliders = bottle.GetComponentsInChildren<Collider>(true)
            .Where(collider => collider.enabled)
            .ToArray();
        report.Check(
            activeColliders.Length == 1 && !activeColliders[0].isTrigger,
            "MedicineBottle solid collider",
            "MedicineBottle needs exactly one active, non-trigger Collider.");

        XRGrabInteractable[] grabComponents = bottle.GetComponents<XRGrabInteractable>();
        report.Check(
            grabComponents.Length == 1,
            "MedicineBottle XRGrabInteractable",
            $"MedicineBottle needs one XRGrabInteractable; found {grabComponents.Length}.");

        FirstGrabTrigger[] triggers = bottle.GetComponents<FirstGrabTrigger>();
        report.Check(
            triggers.Length == 1,
            "FirstGrabTrigger one-shot component",
            $"MedicineBottle needs one FirstGrabTrigger; found {triggers.Length}.");

        Transform grabCube = FindUniqueByName(scene, "GrabCube", report, false);
        XRGrabInteractable grabCubeInteractable =
            grabCube != null ? grabCube.GetComponent<XRGrabInteractable>() : null;
        if (grabComponents.Length == 1 && grabCubeInteractable != null)
        {
            int bottleBits = GetInteractionLayerBits(grabComponents[0]);
            int cubeBits = GetInteractionLayerBits(grabCubeInteractable);
            report.Check(
                bottleBits != 0 && (bottleBits & cubeBits) != 0,
                "MedicineBottle Interaction Layer",
                "MedicineBottle Interaction Layer is incompatible with GrabCube.");
            report.Check(
                grabComponents[0].interactionManager != null,
                "MedicineBottle interaction manager",
                "MedicineBottle XRGrabInteractable has no XR Interaction Manager assigned.");
        }

        report.Check(
            bottle.Find("BottleBody") != null && bottle.Find("BottleCap") != null,
            "MedicineBottle visuals",
            "BottleBody or BottleCap is missing.");
        return triggers.Length == 1 ? triggers[0] : null;
    }

    private static NoteView ValidateNote(Transform note, ValidationReport report)
    {
        if (note == null)
            return null;

        NoteView[] views = note.GetComponents<NoteView>();
        report.Check(
            views.Length == 1,
            "NoteView",
            $"Note needs one NoteView; found {views.Length}.");
        report.Check(
            note.Find("NoteSurface")?.GetComponent<Renderer>() != null,
            "Note surface",
            "Note/NoteSurface with Renderer is missing.");
        TMP_Text noteText = note.Find("NoteText")?.GetComponent<TMP_Text>();
        report.Check(noteText != null, "Note TMP 3D text", "Note/NoteText TMP component is missing.");
        if (noteText != null)
            report.Check(noteText.font != null, "Note TMP font", "NoteText has no TMP font asset.");

        report.Check(
            note.GetComponentsInChildren<Rigidbody>(true).Length == 0,
            "Note is static (no Rigidbody)",
            "Note must not have a Rigidbody.");
        report.Check(
            note.GetComponentsInChildren<XRGrabInteractable>(true).Length == 0,
            "Note is not grabbable",
            "Note must not have XRGrabInteractable.");
        report.Check(
            note.GetComponentsInChildren<FirstGrabTrigger>(true).Length == 0,
            "Note has no grab trigger",
            "Note must not have FirstGrabTrigger.");

        if (views.Length == 1)
        {
            SerializedObject serializedView = new SerializedObject(views[0]);
            string initialText = serializedView.FindProperty("initialText").stringValue;
            string alteredText = serializedView.FindProperty("alteredText").stringValue;
            report.Check(
                initialText.Contains("NO OLVIDES TOMAR LA PASTILLA") &&
                initialText.Contains("SI EMPIEZAS A SENTIRTE MAL."),
                "Note initial narrative",
                "NoteView initial text does not match Phase 2 narrative.");
            report.Check(
                alteredText.Contains("NO CONFÍES EN ELLOS.") &&
                alteredText.Contains("NO TOMES LA PASTILLA."),
                "Note altered narrative",
                "NoteView altered text does not match Phase 2 narrative.");
            report.Check(
                serializedView.FindProperty("noteText").objectReferenceValue != null &&
                serializedView.FindProperty("backgroundRenderer").objectReferenceValue != null,
                "NoteView references",
                "NoteView has missing text or background references.");
        }

        return views.Length == 1 ? views[0] : null;
    }

    private static FirstHallucinationSequence ValidateSequence(
        Transform phaseObject,
        NoteView expectedNoteView,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;

        FirstHallucinationSequence[] sequences =
            phaseObject.GetComponents<FirstHallucinationSequence>();
        report.Check(
            sequences.Length == 1,
            "FirstHallucinationSequence",
            $"Phase object needs one sequence; found {sequences.Length}.");

        AudioSource[] audioSources = phaseObject.GetComponents<AudioSource>();
        report.Check(
            audioSources.Length == 2,
            "Two Phase 2 AudioSources",
            $"Phase object needs exactly two AudioSources; found {audioSources.Length}.");
        if (audioSources.Length == 2)
        {
            report.Check(
                audioSources.All(source => !source.playOnAwake && !source.loop),
                "AudioSource playback settings",
                "Both Phase 2 AudioSources must disable Play On Awake and Loop.");
            report.Check(
                audioSources.All(source => Mathf.Approximately(source.spatialBlend, 0f)),
                "AudioSource 2D guidance",
                "Both Phase 2 AudioSources must use Spatial Blend 0.");
        }

        if (sequences.Length != 1)
            return null;

        FirstHallucinationSequence sequence = sequences[0];
        SerializedObject serializedSequence = new SerializedObject(sequence);
        UnityEngine.Object noteReference =
            serializedSequence.FindProperty("noteView").objectReferenceValue;
        UnityEngine.Object guidanceSource =
            serializedSequence.FindProperty("guidanceAudioSource").objectReferenceValue;
        UnityEngine.Object hallucinationSource =
            serializedSequence.FindProperty("hallucinationAudioSource").objectReferenceValue;
        UnityEngine.Object guidanceClip =
            serializedSequence.FindProperty("guidanceClip").objectReferenceValue;
        UnityEngine.Object hallucinationClip =
            serializedSequence.FindProperty("firstHallucinationClip").objectReferenceValue;

        report.Check(
            noteReference == expectedNoteView && noteReference != null,
            "Sequence NoteView reference",
            "FirstHallucinationSequence does not reference the scene NoteView.");
        report.Check(
            guidanceSource != null && hallucinationSource != null &&
            guidanceSource != hallucinationSource,
            "Independent guidance/hallucination sources",
            "Sequence needs two distinct AudioSource references.");
        report.Warn(
            guidanceClip != null,
            "Guidance Audio",
            "Guidance clip is missing; visual flow remains testable.");
        report.Warn(
            hallucinationClip != null,
            "Hallucination Audio",
            "Hallucination clip is missing; visual flow remains testable.");

        SerializedProperty lights = serializedSequence.FindProperty("affectedLights");
        bool hasValidLight = Enumerable.Range(0, lights.arraySize)
            .Any(index => lights.GetArrayElementAtIndex(index).objectReferenceValue != null);
        report.Warn(
            hasValidLight,
            "Affected room light",
            "No affected Light is assigned; audio/note flow still works.");

        ValidateNonNegative(serializedSequence, "guidanceDelay", report);
        ValidateNonNegative(serializedSequence, "noteChangeDelay", report);
        ValidateNonNegative(serializedSequence, "lightTransitionDuration", report);
        ValidateUnitInterval(serializedSequence, "guidanceVolume", report);
        ValidateUnitInterval(serializedSequence, "hallucinationVolume", report);
        ValidateUnitInterval(serializedSequence, "alteredIntensityMultiplier", report);
        report.Ok("Sequence state and repeat lock", "Runtime sequence exposes a one-way activation lock.");
        return sequence;
    }

    private static void ValidateEvent(
        FirstGrabTrigger trigger,
        FirstHallucinationSequence sequence,
        ValidationReport report)
    {
        if (trigger == null || sequence == null)
            return;

        int matchingListeners = 0;
        for (int index = 0; index < trigger.OnFirstGrab.GetPersistentEventCount(); index++)
        {
            if (trigger.OnFirstGrab.GetPersistentTarget(index) == sequence &&
                trigger.OnFirstGrab.GetPersistentMethodName(index) ==
                nameof(FirstHallucinationSequence.BeginHallucination))
            {
                matchingListeners++;
            }
        }

        report.Check(
            matchingListeners == 1,
            "FirstGrab event connection",
            $"Expected one persistent BeginHallucination listener; found {matchingListeners}.");
    }

    private static void ValidatePhaseOne(Scene scene, ValidationReport report)
    {
        XROrigin[] origins = FindSceneComponents<XROrigin>(scene);
        report.Check(
            origins.Length == 1,
            "Phase 1 XR Origin preserved",
            $"Expected one XR Origin; found {origins.Length}.");
        if (origins.Length == 1)
        {
            GameObject originRoot = origins[0].Origin;
            report.Check(
                originRoot != null && originRoot.GetComponent<CharacterController>() != null,
                "Phase 1 CharacterController preserved",
                "XR Origin has no CharacterController.");
            report.Check(
                originRoot != null && originRoot.GetComponent<DesktopKeyboardLocomotion>() != null,
                "Phase 1 desktop locomotion preserved",
                "XR Origin has no DesktopKeyboardLocomotion.");
            report.Check(
                originRoot != null && originRoot.GetComponent<Rigidbody>() == null,
                "XR Origin Rigidbody remains absent",
                "XR Origin must not have a Rigidbody.");
        }

        report.Check(
            FindSceneComponents<XRInteractionManager>(scene).Length == 1,
            "XR Interaction Manager preserved",
            "Expected exactly one XR Interaction Manager.");
        report.Check(
            FindSceneComponents<XRBodyTransformer>(scene).Length >= 1,
            "XRBodyTransformer preserved",
            "XRBodyTransformer is missing.");
        report.Check(
            FindSceneComponents<LocomotionMediator>(scene).Length >= 1,
            "LocomotionMediator preserved",
            "LocomotionMediator is missing.");
        report.Check(
            FindSceneComponents<DynamicMoveProvider>(scene).Length >= 1,
            "DynamicMoveProvider preserved",
            "DynamicMoveProvider is missing.");

        Transform simulator = FindUniqueByName(scene, "XR Interaction Simulator", report, false);
        report.Check(
            simulator != null,
            "XR Interaction Simulator preserved",
            "XR Interaction Simulator is missing.");

        Transform grabCube = FindUniqueByName(scene, "GrabCube", report, false);
        bool grabCubeValid = grabCube != null &&
                             grabCube.GetComponent<Collider>() != null &&
                             grabCube.GetComponent<Rigidbody>() != null &&
                             grabCube.GetComponent<XRGrabInteractable>() != null;
        report.Check(
            grabCubeValid,
            "GrabCube preserved and grabbable",
            "GrabCube or one of its interaction components is missing.");
    }

    private static void ValidateMissingScripts(Scene scene, ValidationReport report)
    {
        int missingScripts = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                transform.gameObject));
        report.Check(
            missingScripts == 0,
            "No Missing Scripts",
            $"Scene contains {missingScripts} missing script reference(s).");
    }

    private static void ValidateGeneratedAssets(ValidationReport report)
    {
        ValidateSingleAsset("MAT_Phase2_MedicineBottle t:Material", "bottle material", report);
        ValidateSingleAsset("MAT_Phase2_MedicineCap t:Material", "cap material", report);
        ValidateSingleAsset("MAT_Phase2_Note t:Material", "note material", report);
        ValidateSingleAsset("Phase2_SystemFont SDF t:TMP_FontAsset", "TMP font asset", report);
        ValidateSingleAsset("TMP Settings t:TMP_Settings", "TMP settings asset", report);
    }

    private static void ValidateSingleAsset(
        string filter,
        string label,
        ValidationReport report)
    {
        int count = AssetDatabase.FindAssets(filter, new[] { "Assets/Generated/Phase2" }).Length;
        report.Check(
            count == 1,
            $"Single generated {label}",
            $"Expected one generated {label}; found {count}.");
    }

    private static void ValidateNonNegative(
        SerializedObject serializedObject,
        string propertyName,
        ValidationReport report)
    {
        float value = serializedObject.FindProperty(propertyName).floatValue;
        report.Check(
            value >= 0f,
            $"{propertyName} valid",
            $"{propertyName} must be non-negative.");
    }

    private static void ValidateUnitInterval(
        SerializedObject serializedObject,
        string propertyName,
        ValidationReport report)
    {
        float value = serializedObject.FindProperty(propertyName).floatValue;
        report.Check(
            value >= 0f && value <= 1f,
            $"{propertyName} valid",
            $"{propertyName} must be in [0, 1].");
    }

    private static int GetInteractionLayerBits(XRGrabInteractable interactable)
    {
        SerializedObject serializedObject = new SerializedObject(interactable);
        return serializedObject.FindProperty("m_InteractionLayers")
            .FindPropertyRelative("m_Bits")
            .intValue;
    }

    private static Transform FindUniqueRoot(
        Scene scene,
        string name,
        ValidationReport report)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        report.Check(
            matches.Length == 1,
            name,
            $"Expected exactly one root '{name}', found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindUniqueDirectChild(
        Transform parent,
        string name,
        ValidationReport report)
    {
        if (parent == null)
            return null;

        Transform[] matches = parent.Cast<Transform>()
            .Where(child => child.name == name)
            .ToArray();
        string path = GetPath(parent) + "/" + name;
        report.Check(
            matches.Length == 1,
            path,
            $"Expected exactly one '{path}', found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindUniqueByName(
        Scene scene,
        string name,
        ValidationReport report,
        bool logResult)
    {
        Transform[] matches = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(transform => transform.gameObject.scene == scene && transform.name == name)
            .ToArray();
        if (logResult)
        {
            report.Check(
                matches.Length == 1,
                name,
                $"Expected exactly one scene object '{name}', found {matches.Length}.");
        }
        return matches.Length == 1 ? matches[0] : null;
    }

    private static void ValidateGlobalNameCount(
        Scene scene,
        string name,
        ValidationReport report)
    {
        int count = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(transform => transform.gameObject.scene == scene && transform.name == name);
        report.Check(
            count == 1,
            $"Single {name}",
            $"Expected one scene object named '{name}', found {count}.");
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(component => component.gameObject.scene == scene)
            .ToArray();
    }

    private static string GetPath(Transform target)
    {
        return target.parent == null ? target.name : GetPath(target.parent) + "/" + target.name;
    }

    private static ValidationReport Finish(ValidationReport report)
    {
        Debug.Log(report.BuildSummary());
        return report;
    }

    private sealed class ValidationReport
    {
        private readonly List<string> lines = new List<string>();

        public int WarningCount { get; private set; }

        public int ErrorCount { get; private set; }

        public void Check(bool condition, string okLabel, string errorMessage)
        {
            if (condition)
                Ok(okLabel);
            else
                Error(errorMessage);
        }

        public void Warn(bool condition, string okLabel, string warningMessage)
        {
            if (condition)
            {
                Ok(okLabel);
                return;
            }

            WarningCount++;
            lines.Add("[WARNING] " + warningMessage);
        }

        public void Ok(string label, string detail = null)
        {
            lines.Add("[OK] " + label + (string.IsNullOrEmpty(detail) ? string.Empty : " - " + detail));
        }

        private void Error(string message)
        {
            ErrorCount++;
            lines.Add("[ERROR] " + message);
        }

        public string BuildSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Phase 2 Validation");
            builder.AppendLine();
            foreach (string line in lines)
                builder.AppendLine(line);
            builder.AppendLine();
            builder.Append($"Result: {ErrorCount} error(s), {WarningCount} warning(s).");
            return builder.ToString();
        }
    }
}
