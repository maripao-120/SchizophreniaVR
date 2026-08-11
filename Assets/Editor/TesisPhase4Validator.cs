using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TesisPhase4Validator
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RoofImpactsClipPath =
        "Assets/Audio/Phase4/Phase4_RoofImpacts.mp3";
    private const string CeilingGuidanceClipPath =
        "Assets/Audio/Phase4/Phase4_CeilingGuidance.mp3";
    private const string ArmsMaterialPath =
        "Assets/Generated/Phase4/Materials/MAT_Phase4_Arms.mat";
    private const float MaximumTargetDistanceFromCeilingCenter = 0.75f;
    private const float MaximumAudioDistanceFromTarget = 1f;
    private const float MinimumCameraClearance = 0.35f;

    [MenuItem("Tools/Tesis VR/Phase 4/Validate")]
    private static void ValidateFromMenu()
    {
        try
        {
            ValidationReport report = Validate();
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 4 Validation",
                report.ErrorCount == 0
                    ? $"Validation completed with {report.WarningCount} warning(s)."
                    : $"Validation found {report.ErrorCount} error(s). See Console.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 4 Validation Error",
                exception.Message,
                "OK");
        }
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
        Transform structure = FindUniqueDirectChild(room, "Structure", report);
        Transform ceiling = FindUniqueDirectChild(structure, "Ceiling", report);
        Transform lookTarget = FindUniqueDirectChild(ceiling, "CeilingLookTarget", report);
        Transform environment = FindUniqueDirectChild(room, "Phase4Environment", report);
        Transform arms = FindUniqueDirectChild(environment, "CeilingArms", report);
        Transform leftArm = FindUniqueDirectChild(arms, "LeftArm", report);
        Transform leftUpper = FindUniqueDirectChild(leftArm, "UpperArm", report);
        Transform leftForearm = FindUniqueDirectChild(leftArm, "Forearm", report);
        Transform rightArm = FindUniqueDirectChild(arms, "RightArm", report);
        Transform rightUpper = FindUniqueDirectChild(rightArm, "UpperArm", report);
        Transform rightForearm = FindUniqueDirectChild(rightArm, "Forearm", report);
        Transform impactsObject = FindUniqueDirectChild(environment, "RoofImpactsAudio", report);

        Transform experience = FindUniqueRoot(scene, "Experience", report);
        Transform phaseObject = FindUniqueDirectChild(experience, "Phase4_CeilingArms", report);
        Transform guidanceObject = FindUniqueDirectChild(
            phaseObject,
            "CeilingGuidanceAudio",
            report);

        Camera mainCamera = ValidateMainCamera(scene, report);
        ValidateCeiling(ceiling, lookTarget, report);

        AudioClip impactsClip = ValidateAudioAsset(
            RoofImpactsClipPath,
            "RoofImpacts",
            true,
            report);
        AudioClip guidanceClip = ValidateAudioAsset(
            CeilingGuidanceClipPath,
            "CeilingGuidance",
            false,
            report);
        Material armsMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArmsMaterialPath);
        report.Check(
            armsMaterial != null,
            $"Phase 4 arms material exists at {ArmsMaterialPath}",
            $"Missing Phase 4 arms material at {ArmsMaterialPath}.");
        ValidateArmsMaterial(armsMaterial, report);

        Renderer[] expectedRenderers =
        {
            ValidateArmPart(leftUpper, armsMaterial, report),
            ValidateArmPart(leftForearm, armsMaterial, report),
            ValidateArmPart(rightUpper, armsMaterial, report),
            ValidateArmPart(rightForearm, armsMaterial, report)
        };
        ValidateArmsHierarchy(arms, mainCamera, report);

        AudioSource impactsSource = ValidateImpactsAudio(
            impactsObject,
            lookTarget,
            impactsClip,
            report);
        AudioSource guidanceSource = ValidateGuidanceAudio(
            guidanceObject,
            guidanceClip,
            report);

        CeilingArmsView view = ValidateView(phaseObject, expectedRenderers, report);
        SimpleArmMovement movement = ValidateMovement(
            phaseObject,
            leftForearm,
            rightForearm,
            report);
        WindowLookDetector detector = ValidateDetector(
            phaseObject,
            mainCamera,
            lookTarget,
            report);
        CeilingArmsSequence sequence = ValidateSequence(
            phaseObject,
            view,
            movement,
            detector,
            impactsSource,
            guidanceSource,
            impactsClip,
            guidanceClip,
            report);
        ValidateLookConnection(detector, sequence, report);
        ValidatePhaseThreeIntegration(scene, sequence, report);
        ValidatePreviousPhases(scene, report);
        ValidateGlobalNameCounts(scene, report);
        ValidateNoMissingScripts(scene, report);
        ValidateAudioListeners(scene, report);

        return Finish(report);
    }

    private static void ValidateCeiling(
        Transform ceiling,
        Transform lookTarget,
        ValidationReport report)
    {
        report.Check(ceiling != null, "Ceiling exists", "Room/Structure/Ceiling is missing.");
        if (ceiling == null || lookTarget == null)
            return;

        Renderer ceilingRenderer = ceiling.GetComponent<Renderer>();
        report.Check(
            ceilingRenderer != null,
            "Ceiling has geometry for derived placement",
            "Ceiling needs a Renderer so Phase 4 placement can be verified.");
        report.Check(
            lookTarget.GetComponents<Renderer>().Length == 0,
            "CeilingLookTarget has no Renderer",
            "CeilingLookTarget must be an empty visual target.");
        report.Check(
            lookTarget.GetComponents<Rigidbody>().Length == 0,
            "CeilingLookTarget has no Rigidbody",
            "CeilingLookTarget must not have a Rigidbody.");
        report.Check(
            lookTarget.GetComponents<Collider>().Length == 0,
            "CeilingLookTarget has no Collider",
            "CeilingLookTarget must not have a solid Collider.");
        report.Check(
            Vector3.Distance(lookTarget.position, ceiling.position) <=
            MaximumTargetDistanceFromCeilingCenter,
            "CeilingLookTarget is near the Ceiling center",
            "CeilingLookTarget is too far from the Ceiling center.");
        report.Check(
            Vector3.Dot(lookTarget.position - ceiling.position, ceiling.TransformDirection(Vector3.down)) > 0f,
            "CeilingLookTarget is on the room-facing side of Ceiling",
            "CeilingLookTarget must be below the Ceiling surface.");
    }

    private static AudioClip ValidateAudioAsset(
        string path,
        string label,
        bool recommendMono,
        ValidationReport report)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        report.Check(
            clip != null,
            $"{label} AudioClip exists at {path}",
            $"Missing or invalid AudioClip at {path}.");
        if (clip == null)
            return null;

        report.Ok($"{label}: {clip.length:F2} s, {clip.channels} channel(s)");
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        report.Check(
            importer != null,
            $"{label} uses AudioImporter",
            $"Could not inspect AudioImporter for {path}.");
        if (recommendMono && importer != null)
        {
            report.Warn(
                clip.channels == 1 || importer.forceToMono,
                "RoofImpacts is mono or Force To Mono is enabled",
                "RoofImpacts is stereo with Force To Mono disabled; 3D positioning works, " +
                "but mono is recommended for clearer localization. Import settings were not changed.");
        }
        return clip;
    }

    private static Renderer ValidateArmPart(
        Transform part,
        Material expectedMaterial,
        ValidationReport report)
    {
        if (part == null)
            return null;

        Renderer[] renderers = part.GetComponents<Renderer>();
        report.Check(
            renderers.Length == 1,
            $"{GetPath(part)} has one Renderer",
            $"{GetPath(part)} needs one Renderer; found {renderers.Length}.");
        if (renderers.Length != 1)
            return null;

        Renderer renderer = renderers[0];
        report.Check(
            renderer.sharedMaterial == expectedMaterial && expectedMaterial != null,
            $"{GetPath(part)} uses the Phase 4 arms material",
            $"{GetPath(part)} has the wrong or missing material.");
        report.Check(
            !renderer.enabled,
            $"{GetPath(part)} starts hidden",
            $"{GetPath(part)} Renderer must be disabled before Play Mode.");
        return renderer;
    }

    private static void ValidateArmsMaterial(Material material, ValidationReport report)
    {
        if (material == null)
            return;

        Color color = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.color;
        report.Check(
            color.maxColorComponent > 0.2f,
            "Phase 4 arms material is not black",
            "Phase 4 arms material is too dark for the neutral placeholder direction.");
        report.Check(
            color.a >= 0.99f,
            "Phase 4 arms material is opaque",
            "Phase 4 arms material must be opaque.");
        if (material.HasProperty("_Surface"))
        {
            report.Check(
                Mathf.Approximately(material.GetFloat("_Surface"), 0f),
                "Phase 4 arms material uses an opaque URP surface",
                "Phase 4 arms material Surface Type must be Opaque.");
        }
        if (material.HasProperty("_EmissionColor"))
        {
            report.Check(
                material.GetColor("_EmissionColor").maxColorComponent <= 0.001f,
                "Phase 4 arms material has no emission",
                "Phase 4 arms material must not use emission.");
        }
    }

    private static void ValidateArmsHierarchy(
        Transform arms,
        Camera mainCamera,
        ValidationReport report)
    {
        if (arms == null)
            return;

        Transform[] hierarchy = arms.GetComponentsInChildren<Transform>(true);
        report.Check(
            hierarchy.SelectMany(item => item.GetComponents<Rigidbody>()).Count() == 0,
            "Ceiling arms have no Rigidbody",
            "Ceiling arms must not use Rigidbody physics.");
        report.Check(
            hierarchy.SelectMany(item => item.GetComponents<Collider>()).Count() == 0,
            "Ceiling arms have no Collider",
            "Ceiling arms must not have solid Colliders.");
        report.Check(
            hierarchy.SelectMany(item => item.GetComponents<XRGrabInteractable>()).Count() == 0,
            "Ceiling arms are not grabbable",
            "Ceiling arms must not use XRGrabInteractable.");
        report.Check(
            hierarchy.SelectMany(item => item.GetComponents<Animator>()).Count() == 0,
            "Ceiling arms do not use Animator",
            "Phase 4 placeholder movement must not use Animator.");

        Renderer[] renderers = arms.GetComponentsInChildren<Renderer>(true);
        report.Check(
            renderers.Length == 4,
            "CeilingArms has four placeholder Renderers",
            $"Expected four arm Renderers; found {renderers.Length}.");
        if (mainCamera != null && renderers.Length > 0)
        {
            float closestDistance = renderers.Min(renderer =>
                Vector3.Distance(renderer.bounds.ClosestPoint(mainCamera.transform.position),
                    mainCamera.transform.position));
            report.Warn(
                closestDistance >= MinimumCameraClearance,
                "Ceiling arms do not intersect the initial Main Camera position",
                "Ceiling arms are close to the initial Main Camera position; verify clearance in XR Simulator.");
        }
    }

    private static AudioSource ValidateImpactsAudio(
        Transform audioObject,
        Transform lookTarget,
        AudioClip expectedClip,
        ValidationReport report)
    {
        AudioSource source = ValidateSingleAudioSource(audioObject, "RoofImpactsAudio", report);
        if (source == null)
            return null;

        report.Check(source.clip == expectedClip && expectedClip != null,
            "RoofImpacts clip is assigned", "RoofImpactsAudio has the wrong or missing clip.");
        report.Check(!source.playOnAwake, "RoofImpacts Play On Awake is disabled",
            "RoofImpactsAudio must not Play On Awake.");
        report.Check(!source.loop, "RoofImpacts Loop is disabled",
            "RoofImpactsAudio must not loop.");
        report.Check(Mathf.Approximately(source.spatialBlend, 1f), "RoofImpacts uses 3D audio",
            "RoofImpactsAudio Spatial Blend must be 1.");
        report.Check(Mathf.Approximately(source.dopplerLevel, 0f), "RoofImpacts Doppler is disabled",
            "RoofImpactsAudio Doppler Level must be 0.");
        report.Check(source.volume >= 0f && source.volume <= 1f, "RoofImpacts volume is valid",
            "RoofImpactsAudio volume must be between 0 and 1.");
        if (lookTarget != null)
        {
            report.Check(
                Vector3.Distance(audioObject.position, lookTarget.position) <= MaximumAudioDistanceFromTarget,
                "RoofImpactsAudio is near CeilingLookTarget",
                "RoofImpactsAudio is too far from CeilingLookTarget.");
        }
        return source;
    }

    private static AudioSource ValidateGuidanceAudio(
        Transform audioObject,
        AudioClip expectedClip,
        ValidationReport report)
    {
        AudioSource source = ValidateSingleAudioSource(audioObject, "CeilingGuidanceAudio", report);
        if (source == null)
            return null;

        report.Check(source.clip == expectedClip && expectedClip != null,
            "CeilingGuidance clip is assigned", "CeilingGuidanceAudio has the wrong or missing clip.");
        report.Check(!source.playOnAwake, "CeilingGuidance Play On Awake is disabled",
            "CeilingGuidanceAudio must not Play On Awake.");
        report.Check(!source.loop, "CeilingGuidance Loop is disabled",
            "CeilingGuidanceAudio must not loop.");
        report.Check(Mathf.Approximately(source.spatialBlend, 0f), "CeilingGuidance uses clear 2D audio",
            "CeilingGuidanceAudio Spatial Blend must be 0.");
        report.Check(source.volume >= 0f && source.volume <= 1f, "CeilingGuidance volume is valid",
            "CeilingGuidanceAudio volume must be between 0 and 1.");
        return source;
    }

    private static AudioSource ValidateSingleAudioSource(
        Transform audioObject,
        string label,
        ValidationReport report)
    {
        if (audioObject == null)
            return null;
        AudioSource[] sources = audioObject.GetComponents<AudioSource>();
        report.Check(
            sources.Length == 1,
            $"{label} has one AudioSource",
            $"{label} needs one AudioSource; found {sources.Length}.");
        return sources.Length == 1 ? sources[0] : null;
    }

    private static CeilingArmsView ValidateView(
        Transform phaseObject,
        Renderer[] expectedRenderers,
        ValidationReport report)
    {
        CeilingArmsView view = FindSingleComponent<CeilingArmsView>(phaseObject, report);
        if (view == null)
            return null;

        SerializedObject serializedView = new SerializedObject(view);
        SerializedProperty renderers = serializedView.FindProperty("controlledRenderers");
        bool referencesMatch = renderers.arraySize == expectedRenderers.Length;
        for (int index = 0; referencesMatch && index < expectedRenderers.Length; index++)
        {
            referencesMatch = expectedRenderers[index] != null &&
                renderers.GetArrayElementAtIndex(index).objectReferenceValue == expectedRenderers[index];
        }
        report.Check(
            referencesMatch,
            "CeilingArmsView controls all four arm Renderers",
            "CeilingArmsView Renderer references are incomplete or incorrect.");
        return view;
    }

    private static SimpleArmMovement ValidateMovement(
        Transform phaseObject,
        Transform expectedLeftForearm,
        Transform expectedRightForearm,
        ValidationReport report)
    {
        SimpleArmMovement movement = FindSingleComponent<SimpleArmMovement>(phaseObject, report);
        if (movement == null)
            return null;

        SerializedObject serializedMovement = new SerializedObject(movement);
        report.Check(
            serializedMovement.FindProperty("leftForearm").objectReferenceValue == expectedLeftForearm &&
            serializedMovement.FindProperty("rightForearm").objectReferenceValue == expectedRightForearm &&
            expectedLeftForearm != null && expectedRightForearm != null,
            "SimpleArmMovement references both Forearms",
            "SimpleArmMovement has incorrect Forearm references.");
        float duration = serializedMovement.FindProperty("movementDuration").floatValue;
        float angle = serializedMovement.FindProperty("movementAngle").floatValue;
        report.Check(duration > 0f, "Arm movement duration is positive",
            "SimpleArmMovement movementDuration must be positive.");
        report.Check(angle > 0f && angle <= 45f, "Arm movement angle is subtle",
            "SimpleArmMovement movementAngle must be between 0 and 45 degrees.");
        report.Ok("SimpleArmMovement uses a coroutine without permanent Update");
        return movement;
    }

    private static WindowLookDetector ValidateDetector(
        Transform phaseObject,
        Camera expectedCamera,
        Transform expectedTarget,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;
        WindowLookDetector[] detectors = phaseObject.GetComponents<WindowLookDetector>();
        report.Check(
            detectors.Length == 1,
            "Phase4_CeilingArms has one reused WindowLookDetector",
            $"Phase4_CeilingArms needs one WindowLookDetector; found {detectors.Length}.");
        if (detectors.Length != 1)
            return null;

        SerializedObject serializedDetector = new SerializedObject(detectors[0]);
        report.Check(
            serializedDetector.FindProperty("cameraTransform").objectReferenceValue ==
                (expectedCamera != null ? expectedCamera.transform : null) &&
            serializedDetector.FindProperty("lookTarget").objectReferenceValue == expectedTarget &&
            expectedCamera != null && expectedTarget != null,
            "Ceiling detector references Main Camera and CeilingLookTarget",
            "Phase 4 WindowLookDetector references are incorrect.");
        float angle = serializedDetector.FindProperty("maximumLookAngle").floatValue;
        float duration = serializedDetector.FindProperty("requiredLookDuration").floatValue;
        report.Check(angle > 0f && angle <= 180f, "Ceiling look angle is valid",
            "maximumLookAngle must be between 0 and 180 degrees.");
        report.Check(duration > 0f, "Ceiling look duration is positive",
            "requiredLookDuration must be positive so looking up is not accepted in one frame.");
        return detectors[0];
    }

    private static CeilingArmsSequence ValidateSequence(
        Transform phaseObject,
        CeilingArmsView expectedView,
        SimpleArmMovement expectedMovement,
        WindowLookDetector expectedDetector,
        AudioSource expectedImpactsSource,
        AudioSource expectedGuidanceSource,
        AudioClip expectedImpactsClip,
        AudioClip expectedGuidanceClip,
        ValidationReport report)
    {
        CeilingArmsSequence sequence = FindSingleComponent<CeilingArmsSequence>(phaseObject, report);
        if (sequence == null)
            return null;

        SerializedObject serializedSequence = new SerializedObject(sequence);
        report.Check(
            serializedSequence.FindProperty("armsView").objectReferenceValue == expectedView &&
            serializedSequence.FindProperty("armMovement").objectReferenceValue == expectedMovement &&
            serializedSequence.FindProperty("lookDetector").objectReferenceValue == expectedDetector &&
            expectedView != null && expectedMovement != null && expectedDetector != null,
            "CeilingArmsSequence visual references are correct",
            "CeilingArmsSequence has incorrect visual or detector references.");
        report.Check(
            serializedSequence.FindProperty("roofImpactsAudioSource").objectReferenceValue == expectedImpactsSource &&
            serializedSequence.FindProperty("ceilingGuidanceAudioSource").objectReferenceValue == expectedGuidanceSource &&
            expectedImpactsSource != null && expectedGuidanceSource != null,
            "CeilingArmsSequence AudioSource references are correct",
            "CeilingArmsSequence has incorrect AudioSource references.");
        report.Check(
            serializedSequence.FindProperty("roofImpactsClip").objectReferenceValue == expectedImpactsClip &&
            serializedSequence.FindProperty("ceilingGuidanceClip").objectReferenceValue == expectedGuidanceClip &&
            expectedImpactsClip != null && expectedGuidanceClip != null,
            "CeilingArmsSequence AudioClip references are correct",
            "CeilingArmsSequence has incorrect AudioClip references.");

        float phaseDelay = serializedSequence.FindProperty("phaseStartDelay").floatValue;
        float guidanceDelay = serializedSequence.FindProperty("guidanceDelayAfterImpacts").floatValue;
        float lookWait = serializedSequence.FindProperty("maximumLookWait").floatValue;
        float visibleDuration = serializedSequence.FindProperty("armsVisibleDuration").floatValue;
        float impactsVolume = serializedSequence.FindProperty("roofImpactsVolume").floatValue;
        float guidanceVolume = serializedSequence.FindProperty("ceilingGuidanceVolume").floatValue;
        report.Check(phaseDelay >= 0f, "Phase 4 start delay is non-negative",
            "phaseStartDelay must not be negative.");
        report.Check(guidanceDelay >= 0f, "Impacts-to-guidance delay is non-negative",
            "guidanceDelayAfterImpacts must not be negative.");
        report.Check(lookWait > 0f, "maximumLookWait is positive",
            "maximumLookWait must be positive to avoid an indefinite wait.");
        report.Check(visibleDuration >= 0f, "Arms visible duration is non-negative",
            "armsVisibleDuration must not be negative.");
        report.Check(
            impactsVolume >= 0f && impactsVolume <= 1f &&
            guidanceVolume >= 0f && guidanceVolume <= 1f,
            "Phase 4 sequence volumes are valid",
            "Phase 4 audio volumes must be between 0 and 1.");
        report.Ok("CeilingArmsSequence has a one-way activation guard");
        report.Ok("CeilingArmsSequence continues after maximumLookWait timeout");
        report.Check(
            phaseObject.GetComponents<AudioSource>().Length == 0,
            "Phase4_CeilingArms keeps audio on dedicated objects",
            "Phase4_CeilingArms must not have a direct AudioSource.");
        return sequence;
    }

    private static void ValidateLookConnection(
        WindowLookDetector detector,
        CeilingArmsSequence sequence,
        ValidationReport report)
    {
        if (detector == null || sequence == null || detector.OnLookConfirmed == null)
            return;

        int matches = CountPersistentListeners(
            detector.OnLookConfirmed,
            sequence,
            nameof(CeilingArmsSequence.HandleLookConfirmed));
        report.Check(
            matches == 1,
            "Ceiling look confirmation connects once to CeilingArmsSequence",
            $"Expected one Phase 4 HandleLookConfirmed listener; found {matches}.");
    }

    private static void ValidatePhaseThreeIntegration(
        Scene scene,
        CeilingArmsSequence phaseFour,
        ValidationReport report)
    {
        WindowShadowSequence[] phaseThreeSequences = FindSceneComponents<WindowShadowSequence>(scene);
        report.Check(
            phaseThreeSequences.Length == 1,
            "Single WindowShadowSequence preserved",
            $"Expected one WindowShadowSequence; found {phaseThreeSequences.Length}.");
        if (phaseThreeSequences.Length != 1 || phaseFour == null)
            return;

        WindowShadowSequence phaseThree = phaseThreeSequences[0];
        report.Check(
            phaseThree.OnNarrativeCompleted != null,
            "Phase 3 exposes narrative completion after ClosingVoice",
            "WindowShadowSequence narrative completion event is missing.");
        if (phaseThree.OnNarrativeCompleted != null)
        {
            int matches = CountPersistentListeners(
                phaseThree.OnNarrativeCompleted,
                phaseFour,
                nameof(CeilingArmsSequence.BeginSequence));
            report.Check(
                matches == 1,
                "Phase 3 narrative completion connects once to Phase 4",
                $"Expected one Phase 3 -> Phase 4 listener; found {matches}.");
        }

        SerializedObject serializedPhaseThree = new SerializedObject(phaseThree);
        AudioSource closingSource =
            serializedPhaseThree.FindProperty("closingAudioSource").objectReferenceValue as AudioSource;
        AudioClip closingClip =
            serializedPhaseThree.FindProperty("closingClip").objectReferenceValue as AudioClip;
        report.Check(
            closingSource != null && closingClip != null && closingSource.clip == closingClip,
            "Phase 3 ClosingVoice remains assigned",
            "Phase 3 ClosingVoice source or clip is missing.");
        SerializedObject serializedPhaseFour = new SerializedObject(phaseFour);
        AudioClip phaseFourGuidance =
            serializedPhaseFour.FindProperty("ceilingGuidanceClip").objectReferenceValue as AudioClip;
        report.Warn(
            closingClip != null && closingClip != phaseFourGuidance,
            "Phase 3 ClosingVoice and Phase 4 Guidance use distinct clips",
            "Phase 3 ClosingVoice and Phase 4 CeilingGuidance reference the same AudioClip. " +
            "They will not overlap, but the same recording will play twice; verify that this is intentional.");
        report.Ok("Phase 4 waits for the Phase 3 narrative completion event, preventing audio overlap");
    }

    private static int CountPersistentListeners(
        UnityEngine.Events.UnityEvent unityEvent,
        UnityEngine.Object target,
        string methodName)
    {
        int matches = 0;
        for (int index = 0; index < unityEvent.GetPersistentEventCount(); index++)
        {
            if (unityEvent.GetPersistentTarget(index) == target &&
                unityEvent.GetPersistentMethodName(index) == methodName)
            {
                matches++;
            }
        }
        return matches;
    }

    private static void ValidatePreviousPhases(Scene scene, ValidationReport report)
    {
        XROrigin[] origins = FindSceneComponents<XROrigin>(scene);
        report.Check(
            origins.Length == 1 && origins[0].Origin != null &&
            origins[0].Origin.GetComponent<CharacterController>() != null,
            "Phase 1 XR Origin and CharacterController preserved",
            "XR Origin or CharacterController is missing.");
        report.Check(
            FindSceneComponents<DesktopKeyboardLocomotion>(scene).Length == 1,
            "Phase 1 WASD locomotion preserved",
            "Expected one DesktopKeyboardLocomotion.");

        Transform bottle = FindUniqueByName(scene, "MedicineBottle", report);
        report.Check(
            bottle != null && bottle.GetComponents<Rigidbody>().Length == 1 &&
            bottle.GetComponents<XRGrabInteractable>().Length == 1 &&
            bottle.GetComponents<FirstGrabTrigger>().Length == 1,
            "Phase 2 MedicineBottle interaction preserved",
            "MedicineBottle Phase 2 interaction is incomplete.");
        Transform note = FindUniqueByName(scene, "Note", report);
        report.Check(
            note != null && note.GetComponents<NoteView>().Length == 1,
            "Phase 2 NoteView preserved",
            "Note or NoteView is missing.");
        report.Check(
            FindSceneComponents<FirstHallucinationSequence>(scene).Length == 1,
            "Phase 2 FirstHallucinationSequence preserved",
            "Expected one FirstHallucinationSequence.");

        Transform phaseThreeObject = FindUniqueByName(scene, "Phase3_WindowShadow", report);
        report.Check(
            phaseThreeObject != null && phaseThreeObject.GetComponents<WindowLookDetector>().Length == 1,
            "Phase 3 WindowLookDetector preserved",
            "Phase 3 WindowLookDetector is missing or duplicated.");
        report.Check(
            FindSceneComponents<LinearShadowMovement>(scene).Length == 1,
            "Phase 3 shadow movement preserved",
            "Expected one LinearShadowMovement.");
        report.Check(
            FindSceneComponents<WindowLookDetector>(scene).Length == 2,
            "One look detector exists for each of Phase 3 and Phase 4",
            "Expected exactly two WindowLookDetector components in SampleScene.");

        int grabCubeCount = FindSceneTransforms(scene).Count(transform => transform.name == "GrabCube");
        report.Check(
            grabCubeCount == 0,
            "GrabCube was not reintroduced",
            $"Found {grabCubeCount} GrabCube object(s).");
    }

    private static Camera ValidateMainCamera(Scene scene, ValidationReport report)
    {
        Camera[] cameras = FindSceneComponents<Camera>(scene)
            .Where(camera => camera.CompareTag("MainCamera"))
            .ToArray();
        report.Check(
            cameras.Length == 1,
            "Single tagged Main Camera preserved",
            $"Expected one tagged Main Camera; found {cameras.Length}.");
        return cameras.Length == 1 ? cameras[0] : null;
    }

    private static void ValidateGlobalNameCounts(Scene scene, ValidationReport report)
    {
        string[] names =
        {
            "CeilingLookTarget",
            "Phase4Environment",
            "CeilingArms",
            "LeftArm",
            "RightArm",
            "RoofImpactsAudio",
            "Phase4_CeilingArms",
            "CeilingGuidanceAudio"
        };
        foreach (string name in names)
        {
            int count = FindSceneTransforms(scene).Count(transform => transform.name == name);
            report.Check(count == 1, $"Single {name}", $"Expected one {name}; found {count}.");
        }

        int upperArmCount = FindSceneTransforms(scene).Count(transform => transform.name == "UpperArm");
        int forearmCount = FindSceneTransforms(scene).Count(transform => transform.name == "Forearm");
        report.Check(upperArmCount == 2, "Two UpperArm placeholders", $"Expected two UpperArm objects; found {upperArmCount}.");
        report.Check(forearmCount == 2, "Two Forearm placeholders", $"Expected two Forearm objects; found {forearmCount}.");
    }

    private static void ValidateNoMissingScripts(Scene scene, ValidationReport report)
    {
        int count = FindSceneTransforms(scene)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        report.Check(count == 0, "No Missing Scripts", $"Scene contains {count} missing script reference(s).");
    }

    private static void ValidateAudioListeners(Scene scene, ValidationReport report)
    {
        AudioListener[] listeners = FindSceneComponents<AudioListener>(scene);
        report.Check(
            listeners.Length == 1,
            "Scene retains exactly one AudioListener",
            $"Expected one AudioListener; found {listeners.Length}.");
    }

    private static T FindSingleComponent<T>(Transform target, ValidationReport report)
        where T : Component
    {
        if (target == null)
            return null;
        T[] components = target.GetComponents<T>();
        report.Check(
            components.Length == 1,
            $"Single {typeof(T).Name}",
            $"{GetPath(target)} needs one {typeof(T).Name}; found {components.Length}.");
        return components.Length == 1 ? components[0] : null;
    }

    private static Transform FindUniqueRoot(Scene scene, string name, ValidationReport report)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        report.Check(matches.Length == 1, name, $"Expected one root named {name}; found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindUniqueDirectChild(
        Transform parent,
        string name,
        ValidationReport report)
    {
        if (parent == null)
            return null;
        Transform[] matches = parent.Cast<Transform>().Where(child => child.name == name).ToArray();
        report.Check(
            matches.Length == 1,
            GetPath(parent) + "/" + name,
            $"Expected one {GetPath(parent)}/{name}; found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindUniqueByName(Scene scene, string name, ValidationReport report)
    {
        Transform[] matches = FindSceneTransforms(scene).Where(transform => transform.name == name).ToArray();
        report.Check(matches.Length == 1, $"Single {name}", $"Expected one {name}; found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static IEnumerable<Transform> FindSceneTransforms(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true));
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
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

        public void Ok(string label)
        {
            lines.Add("[OK] " + label);
        }

        private void Error(string message)
        {
            ErrorCount++;
            lines.Add("[ERROR] " + message);
        }

        public string BuildSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Phase 4 Validation");
            builder.AppendLine();
            foreach (string line in lines)
                builder.AppendLine(line);
            builder.AppendLine();
            builder.Append($"Result: {ErrorCount} error(s), {WarningCount} warning(s).");
            return builder.ToString();
        }
    }
}
