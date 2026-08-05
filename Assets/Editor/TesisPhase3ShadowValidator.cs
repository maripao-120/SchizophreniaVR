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

public static class TesisPhase3ShadowValidator
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ShadowMaterialPath =
        "Assets/Generated/Phase3/Materials/MAT_Phase3_Shadow.mat";
    private const string FootstepsClipPath =
        "Assets/Audio/Phase3/Phase3_ShadowFootsteps.mp3";
    private const string GuidanceClipPath =
        "Assets/Audio/Phase3/Phase3_WindowGuidance.mp3";
    private const float MaximumFunctionalAudioDistance = 2.5f;
    private const float MaximumFunctionalLookTargetDistance = 0.25f;

    [MenuItem("Tools/Tesis VR/Phase 3/Validate Shadow")]
    private static void ValidateFromMenu()
    {
        try
        {
            ValidationReport report = Validate();
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3D Look Detection Validation",
                report.ErrorCount == 0
                    ? $"Validation completed with {report.WarningCount} warning(s)."
                    : $"Validation found {report.ErrorCount} error(s). See Console.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3D Look Detection Validation Error",
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
        Transform floor = FindUniqueDirectChild(structure, "Floor", report);
        Transform architecture = FindUniqueDirectChild(room, "Architecture", report);
        Transform window = FindUniqueDirectChild(architecture, "Window_01", report);
        Transform frame = FindUniqueDirectChild(window, "Frame", report);
        Transform glass = FindUniqueDirectChild(window, "Glass", report);
        Transform opening = FindUniqueDirectChild(window, "ViewOpening", report);
        Transform lookTarget = FindUniqueDirectChild(window, "WindowLookTarget", report);
        Transform wall = FindUniqueDirectChild(structure, "Wall_East", report);
        Camera mainCamera = ValidateMainCamera(scene, report);

        Transform environment = FindUniqueDirectChild(room, "Phase3Environment", report);
        Transform path = FindUniqueDirectChild(environment, "ShadowPath", report);
        Transform start = FindUniqueDirectChild(path, "ShadowStart", report);
        Transform end = FindUniqueDirectChild(path, "ShadowEnd", report);
        Transform figure = FindUniqueDirectChild(environment, "ShadowFigure", report);
        Transform capsule = FindUniqueDirectChild(figure, "ShadowCapsule", report);
        Transform footstepsObject = FindUniqueDirectChild(
            environment,
            "WindowFootstepsAudio",
            report);

        Transform experience = FindUniqueRoot(scene, "Experience", report);
        Transform phaseObject = FindUniqueDirectChild(experience, "Phase3_WindowShadow", report);
        Transform guidanceObject = FindUniqueDirectChild(
            phaseObject,
            "WindowGuidanceAudio",
            report);
        WindowLookDetector lookDetector = ValidateLookDetector(
            mainCamera,
            lookTarget,
            phaseObject,
            report);

        AudioClip footstepsClip = ValidateAudioAsset(
            FootstepsClipPath,
            "footsteps",
            true,
            report);
        AudioClip guidanceClip = ValidateAudioAsset(
            GuidanceClipPath,
            "window guidance",
            false,
            report);

        ValidatePhase3A(window, frame, glass, opening, wall, report);
        ValidateLookTarget(window, opening, lookTarget, report);
        ValidateFigure(room, floor, window, capsule, figure, start, end, report);
        LinearShadowMovement movement = ValidateMovement(
            room,
            floor,
            window,
            opening,
            start,
            end,
            figure,
            phaseObject,
            report);
        WindowShadowView view = ValidateView(capsule, phaseObject, report);
        AudioSource footstepsSource = ValidateFootstepsAudio(
            scene,
            room,
            floor,
            window,
            opening,
            footstepsObject,
            footstepsClip,
            report);
        AudioSource guidanceSource = ValidateGuidanceAudio(
            guidanceObject,
            guidanceClip,
            report);
        WindowShadowSequence sequence = ValidateSequence(
            view,
            movement,
            lookDetector,
            footstepsSource,
            guidanceSource,
            footstepsClip,
            guidanceClip,
            phaseObject,
            report);
        ValidateLookConnection(lookDetector, sequence, report);
        ValidatePhaseTwoConnection(scene, sequence, report);
        ValidatePreviousPhases(scene, report);
        ValidateAudioListeners(scene, report);
        ValidateNoMissingScripts(scene, report);
        ValidateGlobalNameCounts(scene, report);

        return Finish(report);
    }

    private static void ValidatePhase3A(
        Transform window,
        Transform frame,
        Transform glass,
        Transform opening,
        Transform wall,
        ValidationReport report)
    {
        if (window == null)
            return;

        report.Check(
            window.GetComponents<Renderer>().Length == 0 && window.GetComponents<Collider>().Length == 0,
            "Window_01 remains a non-blocking container",
            "Window_01 root regained a Renderer or Collider.");
        report.Check(
            frame != null && frame.Cast<Transform>().Count() == 4,
            "Phase 3A four-piece Frame preserved",
            "Frame must contain exactly four pieces.");
        report.Check(
            frame != null && frame.Cast<Transform>().All(piece =>
                piece.GetComponent<Renderer>() != null &&
                piece.GetComponent<BoxCollider>() is { enabled: true, isTrigger: false }),
            "Phase 3A frame renderers and colliders preserved",
            "Every frame piece needs a Renderer and solid BoxCollider.");

        Renderer glassRenderer = glass != null ? glass.GetComponent<Renderer>() : null;
        report.Check(
            glassRenderer != null && !glassRenderer.enabled && glass.GetComponents<Collider>().Length == 0,
            "Glass remains invisible and non-colliding",
            "Glass must keep its disabled Renderer and no Collider.");
        report.Check(
            opening != null && opening.GetComponents<Renderer>().Length == 0 &&
            opening.GetComponents<Collider>().Length == 0,
            "ViewOpening remains an empty reference",
            "ViewOpening must remain non-rendering and non-colliding.");
        report.Check(
            wall != null && wall.GetComponents<Renderer>().Length == 0 &&
            wall.GetComponents<Collider>().Length == 0 && wall.childCount == 4,
            "Wall_East remains segmented",
            "Wall_East root must remain a four-segment non-rendering container.");

        if (opening != null && wall != null)
        {
            Collider[] blockers = window.GetComponentsInChildren<Collider>(true)
                .Concat(wall.GetComponentsInChildren<Collider>(true))
                .Where(collider => collider.enabled && !collider.isTrigger &&
                                   collider.bounds.Contains(opening.position))
                .ToArray();
            report.Check(
                blockers.Length == 0,
                "Window opening remains unblocked",
                "A solid collider covers the center of ViewOpening.");
        }
    }

    private static void ValidateLookTarget(
        Transform window,
        Transform opening,
        Transform lookTarget,
        ValidationReport report)
    {
        if (lookTarget == null)
            return;

        report.Check(
            lookTarget.parent == window,
            "WindowLookTarget is a direct child of Window_01",
            "WindowLookTarget must remain under Window_01.");
        if (opening != null)
        {
            report.Check(
                Vector3.Distance(lookTarget.position, opening.position) <=
                MaximumFunctionalLookTargetDistance,
                "WindowLookTarget is centered near ViewOpening",
                "WindowLookTarget is too far from ViewOpening.");
        }
        report.Check(
            lookTarget.GetComponents<Renderer>().Length == 0 &&
            lookTarget.GetComponents<MeshFilter>().Length == 0,
            "WindowLookTarget is an empty visual target",
            "WindowLookTarget must not have a Renderer or MeshFilter.");
        report.Check(
            lookTarget.GetComponents<Rigidbody>().Length == 0,
            "WindowLookTarget has no Rigidbody",
            "WindowLookTarget must not have a Rigidbody.");
        report.Check(
            lookTarget.GetComponents<Collider>()
                .All(collider => !collider.enabled || collider.isTrigger),
            "WindowLookTarget has no active solid collider",
            "WindowLookTarget must not have an active solid collider.");
    }

    private static WindowLookDetector ValidateLookDetector(
        Camera mainCamera,
        Transform lookTarget,
        Transform phaseObject,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;

        WindowLookDetector[] detectors = phaseObject.GetComponents<WindowLookDetector>();
        int sceneDetectorCount =
            FindSceneComponents<WindowLookDetector>(phaseObject.gameObject.scene).Length;
        report.Check(
            sceneDetectorCount == 1,
            "SampleScene has exactly one WindowLookDetector",
            $"Expected one WindowLookDetector in SampleScene; found {sceneDetectorCount}.");
        report.Check(
            detectors.Length == 1,
            "Single WindowLookDetector",
            $"Phase3_WindowShadow needs one WindowLookDetector; found {detectors.Length}.");
        if (detectors.Length != 1)
            return null;

        WindowLookDetector detector = detectors[0];
        SerializedObject serializedDetector = new SerializedObject(detector);
        Transform cameraReference =
            serializedDetector.FindProperty("cameraTransform").objectReferenceValue as Transform;
        Transform targetReference =
            serializedDetector.FindProperty("lookTarget").objectReferenceValue as Transform;
        float maximumAngle = serializedDetector.FindProperty("maximumLookAngle").floatValue;
        float requiredDuration =
            serializedDetector.FindProperty("requiredLookDuration").floatValue;

        report.Check(
            mainCamera != null && cameraReference == mainCamera.transform,
            "WindowLookDetector references XR Main Camera",
            "WindowLookDetector Camera Transform must reference the XROrigin Main Camera.");
        report.Check(
            lookTarget != null && targetReference == lookTarget,
            "WindowLookDetector references WindowLookTarget",
            "WindowLookDetector has the wrong or missing Look Target.");
        report.Check(
            maximumAngle > 0f && maximumAngle <= 180f,
            $"Maximum look angle is valid ({maximumAngle:F1} degrees)",
            "maximumLookAngle must be greater than 0 and no more than 180 degrees.");
        report.Warn(
            maximumAngle >= 20f && maximumAngle <= 30f,
            "Maximum look angle is in the recommended 20-30 degree range",
            $"maximumLookAngle is {maximumAngle:F1}; recommended range is 20-30 degrees.");
        report.Check(
            requiredDuration >= 0f,
            $"Required look duration is valid ({requiredDuration:F2} s)",
            "requiredLookDuration must not be negative.");
        report.Warn(
            requiredDuration >= 0.4f && requiredDuration <= 0.7f,
            "Required look duration is in the recommended 0.4-0.7 s range",
            $"requiredLookDuration is {requiredDuration:F2} s; recommended range is 0.4-0.7 s.");
        report.Ok("WindowLookDetector uses camera orientation without eye-tracking APIs");
        return detector;
    }

    private static void ValidateLookConnection(
        WindowLookDetector detector,
        WindowShadowSequence sequence,
        ValidationReport report)
    {
        if (detector == null || sequence == null || detector.OnLookConfirmed == null)
        {
            report.Check(false, string.Empty, "Window look confirmation event is unavailable.");
            return;
        }

        int matches = 0;
        for (int index = 0; index < detector.OnLookConfirmed.GetPersistentEventCount(); index++)
        {
            if (detector.OnLookConfirmed.GetPersistentTarget(index) == sequence &&
                detector.OnLookConfirmed.GetPersistentMethodName(index) ==
                nameof(WindowShadowSequence.HandleLookConfirmed))
            {
                matches++;
            }
        }
        report.Check(
            matches == 1,
            "Window look confirmation connects once to WindowShadowSequence",
            $"Expected one HandleLookConfirmed listener; found {matches}.");
    }

    private static void ValidateFigure(
        Transform room,
        Transform floor,
        Transform window,
        Transform capsule,
        Transform figure,
        Transform start,
        Transform end,
        ValidationReport report)
    {
        if (capsule == null || figure == null)
            return;

        Renderer[] renderers = capsule.GetComponents<Renderer>();
        Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        int shadowMaterialCount = AssetDatabase.FindAssets(
            "MAT_Phase3_Shadow t:Material",
            new[] { "Assets/Generated/Phase3" }).Length;
        report.Check(
            shadowMaterialCount == 1,
            "Single generated shadow material",
            $"Expected one MAT_Phase3_Shadow material; found {shadowMaterialCount}.");
        report.Check(
            renderers.Length == 1,
            "ShadowCapsule has one Renderer",
            $"ShadowCapsule needs one Renderer; found {renderers.Length}.");
        report.Check(
            capsule.GetComponents<MeshFilter>().Length == 1,
            "ShadowCapsule has one MeshFilter",
            "ShadowCapsule needs exactly one MeshFilter.");
        if (renderers.Length == 1)
        {
            report.Check(
                !renderers[0].enabled,
                "ShadowCapsule starts hidden",
                "ShadowCapsule Renderer must be disabled before Play Mode.");
            report.Check(
                expectedMaterial != null && renderers[0].sharedMaterial == expectedMaterial,
                "Shadow material assigned only to ShadowCapsule",
                "ShadowCapsule does not use MAT_Phase3_Shadow.");
            if (expectedMaterial != null)
            {
                Color color = expectedMaterial.HasProperty("_BaseColor")
                    ? expectedMaterial.GetColor("_BaseColor")
                    : expectedMaterial.color;
                report.Check(
                    Mathf.Max(color.r, color.g, color.b) <= 0.12f,
                    "Shadow material is suitably dark",
                    "MAT_Phase3_Shadow is too bright for a silhouette.");
                report.Check(
                    !expectedMaterial.IsKeywordEnabled("_EMISSION"),
                    "Shadow material has no emission",
                    "MAT_Phase3_Shadow must not enable emission.");
                if (expectedMaterial.HasProperty("_Surface"))
                {
                    report.Check(
                        Mathf.Approximately(expectedMaterial.GetFloat("_Surface"), 0f),
                        "Shadow material is opaque",
                        "MAT_Phase3_Shadow must use opaque surface type.");
                }
            }
        }

        report.Check(
            figure.GetComponentsInChildren<Rigidbody>(true).Length == 0,
            "ShadowFigure has no Rigidbody",
            "ShadowFigure hierarchy must not contain a Rigidbody.");
        report.Check(
            figure.GetComponentsInChildren<XRGrabInteractable>(true).Length == 0,
            "ShadowFigure is not grabbable",
            "ShadowFigure hierarchy must not contain XRGrabInteractable.");
        report.Check(
            figure.GetComponentsInChildren<Collider>(true)
                .All(collider => !collider.enabled || collider.isTrigger),
            "ShadowFigure has no active solid collider",
            "ShadowFigure must not collide with the player.");
        report.Check(
            figure.GetComponentsInChildren<Animator>(true).Length == 0,
            "ShadowFigure has no Animator",
            "Phase 3B must not add animation.");
        report.Check(
            figure.GetComponentsInChildren<Component>(true)
                .All(component => component == null || component.GetType().Name != "NavMeshAgent"),
            "ShadowFigure has no NavMeshAgent",
            "Phase 3B must not use NavMesh.");

        if (renderers.Length == 1)
        {
            float height = renderers[0].bounds.size.y;
            report.Check(
                height >= 1.7f && height <= 1.9f,
                $"ShadowCapsule height is human-scale ({height:F2} m)",
                $"ShadowCapsule height must be 1.7-1.9 m; found {height:F2} m.");
        }

        if (room != null && floor != null && window != null && start != null && end != null)
        {
            Vector3 exterior = GetExteriorDirection(room, floor, window);
            bool allOutside = Vector3.Dot(start.position - window.position, exterior) > 0.2f &&
                              Vector3.Dot(end.position - window.position, exterior) > 0.2f &&
                              Vector3.Dot(figure.position - window.position, exterior) > 0.2f;
            report.Check(allOutside, "Shadow path and figure are outside the room", "Shadow objects are not on the exterior side of Window_01.");
        }

        report.Check(
            figure.GetComponents<AudioSource>().Length == 0,
            "ShadowFigure has no AudioSource",
            "Phase 3B must not add audio.");
    }

    private static LinearShadowMovement ValidateMovement(
        Transform room,
        Transform floor,
        Transform window,
        Transform opening,
        Transform start,
        Transform end,
        Transform figure,
        Transform phaseObject,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;

        LinearShadowMovement[] movements = phaseObject.GetComponents<LinearShadowMovement>();
        report.Check(
            movements.Length == 1,
            "Single LinearShadowMovement",
            $"Phase3_WindowShadow needs one LinearShadowMovement; found {movements.Length}.");
        if (movements.Length != 1)
            return null;

        SerializedObject serializedMovement = new SerializedObject(movements[0]);
        Transform movingReference = serializedMovement.FindProperty("movingTransform").objectReferenceValue as Transform;
        Transform startReference = serializedMovement.FindProperty("startPoint").objectReferenceValue as Transform;
        Transform endReference = serializedMovement.FindProperty("endPoint").objectReferenceValue as Transform;
        float duration = serializedMovement.FindProperty("movementDuration").floatValue;
        report.Check(
            movingReference == figure && startReference == start && endReference == end,
            "LinearShadowMovement references are correct",
            "LinearShadowMovement has incorrect moving/start/end references.");
        report.Check(duration > 0f, $"Movement duration is positive ({duration:F2} s)", "Movement duration must be positive.");
        report.Check(
            typeof(LinearShadowMovement).GetProperty(nameof(LinearShadowMovement.NormalizedProgress)) != null,
            "LinearShadowMovement exposes normalized progress",
            "LinearShadowMovement must expose NormalizedProgress.");
        report.Check(
            typeof(LinearShadowMovement).GetMethod(
                nameof(LinearShadowMovement.PauseBriefly),
                new[] { typeof(float) }) != null,
            "LinearShadowMovement exposes one temporary pause API",
            "LinearShadowMovement must expose PauseBriefly(float).");
        report.Check(
            phaseObject.GetComponents<Rigidbody>().Length == 0,
            "Phase3_WindowShadow has no Rigidbody",
            "LinearShadowMovement controller must not use a Rigidbody.");
        report.Check(
            phaseObject.GetComponents<Component>()
                .All(component => component == null || component.GetType().Name != "NavMeshAgent"),
            "Phase3_WindowShadow has no NavMeshAgent",
            "LinearShadowMovement controller must not use NavMesh.");

        if (opening != null && start != null && end != null)
        {
            Vector3 path = end.position - start.position;
            Vector3 exterior = room != null && floor != null && window != null
                ? GetExteriorDirection(room, floor, window)
                : Vector3.zero;
            report.Check(
                exterior.sqrMagnitude > 0.0001f,
                "Exterior direction can be derived from Room and Window_01",
                "Could not derive the exterior direction for path validation.");
            GetOpeningHorizontal(opening, exterior, out Vector3 expectedRight, out float openingWidth);
            report.Check(
                path.sqrMagnitude > 0.01f,
                "ShadowStart and ShadowEnd are different",
                "ShadowStart and ShadowEnd must not overlap.");
            if (path.sqrMagnitude > 0.01f)
            {
                report.Check(
                    Vector3.Dot(path.normalized, expectedRight) > 0.95f && Mathf.Abs(path.y) < 0.05f,
                    "Shadow path is horizontal and left-to-right",
                    "Shadow path is not aligned left-to-right with ViewOpening.");
                report.Check(
                    path.magnitude > openingWidth,
                    "Shadow path extends beyond both visible edges",
                    "Shadow path must be wider than ViewOpening.");
            }
        }

        return movements[0];
    }

    private static WindowShadowView ValidateView(
        Transform capsule,
        Transform phaseObject,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;

        WindowShadowView[] views = phaseObject.GetComponents<WindowShadowView>();
        report.Check(
            views.Length == 1,
            "Single WindowShadowView",
            $"Phase3_WindowShadow needs one WindowShadowView; found {views.Length}.");
        if (views.Length != 1)
            return null;

        SerializedObject serializedView = new SerializedObject(views[0]);
        SerializedProperty renderers = serializedView.FindProperty("controlledRenderers");
        Renderer expectedRenderer = capsule != null ? capsule.GetComponent<Renderer>() : null;
        report.Check(
            renderers.arraySize == 1 &&
            renderers.GetArrayElementAtIndex(0).objectReferenceValue == expectedRenderer &&
            expectedRenderer != null,
            "WindowShadowView controls ShadowCapsule Renderer",
            "WindowShadowView must reference only the ShadowCapsule Renderer.");
        return views[0];
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

        report.Ok($"{label} clip: {clip.length:F2} s, {clip.channels} channel(s)");
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        report.Check(
            importer != null,
            $"{label} uses an AudioImporter",
            $"Could not inspect AudioImporter settings for {path}.");
        if (recommendMono && importer != null)
        {
            report.Warn(
                clip.channels == 1 || importer.forceToMono,
                "Footsteps clip is mono or Force To Mono is enabled",
                "Footsteps clip is stereo with Force To Mono disabled; 3D positioning works, " +
                "but a mono source is recommended for clearer localization.");
        }

        return clip;
    }

    private static AudioSource ValidateFootstepsAudio(
        Scene scene,
        Transform room,
        Transform floor,
        Transform window,
        Transform opening,
        Transform audioObject,
        AudioClip expectedClip,
        ValidationReport report)
    {
        if (audioObject == null)
            return null;

        AudioSource[] sources = audioObject.GetComponents<AudioSource>();
        report.Check(
            sources.Length == 1,
            "WindowFootstepsAudio has one AudioSource",
            $"WindowFootstepsAudio needs one AudioSource; found {sources.Length}.");
        if (sources.Length != 1)
            return null;

        AudioSource source = sources[0];
        report.Check(source.clip == expectedClip && expectedClip != null,
            "Footsteps clip is assigned", "WindowFootstepsAudio has the wrong or missing clip.");
        report.Check(!source.playOnAwake, "Footsteps Play On Awake is disabled",
            "WindowFootstepsAudio must not Play On Awake.");
        report.Check(!source.loop, "Footsteps Loop is disabled",
            "WindowFootstepsAudio must not loop.");
        report.Check(Mathf.Approximately(source.spatialBlend, 1f), "Footsteps use 3D spatial audio",
            "WindowFootstepsAudio Spatial Blend must be 1 (3D).");
        report.Check(Mathf.Approximately(source.dopplerLevel, 0f), "Footsteps Doppler is disabled",
            "WindowFootstepsAudio Doppler Level must be 0.");
        report.Check(source.rolloffMode == AudioRolloffMode.Logarithmic,
            "Footsteps use logarithmic rolloff", "WindowFootstepsAudio must use logarithmic rolloff.");
        report.Check(source.minDistance > 0f && source.maxDistance > source.minDistance,
            "Footsteps distance range is valid", "WindowFootstepsAudio Min/Max Distance is invalid.");
        report.Check(source.volume >= 0f && source.volume <= 1f, "Footsteps volume is valid",
            "WindowFootstepsAudio volume must be between 0 and 1.");

        if (opening != null)
        {
            report.Check(
                Vector3.Distance(audioObject.position, opening.position) <= MaximumFunctionalAudioDistance,
                "WindowFootstepsAudio is near ViewOpening",
                "WindowFootstepsAudio is too far from ViewOpening.");
        }
        if (room != null && floor != null && window != null && opening != null)
        {
            Vector3 exterior = GetExteriorDirection(room, floor, window);
            report.Check(
                Vector3.Dot(audioObject.position - opening.position, exterior) >= -0.05f,
                "WindowFootstepsAudio is on the exterior window side",
                "WindowFootstepsAudio is inside the room instead of near the exterior window plane.");
        }

        Camera[] cameras = FindSceneComponents<Camera>(scene);
        report.Check(
            cameras.All(camera => !audioObject.IsChildOf(camera.transform)),
            "WindowFootstepsAudio is not parented to a camera",
            "WindowFootstepsAudio must not be a child of Main Camera.");
        return source;
    }

    private static AudioSource ValidateGuidanceAudio(
        Transform audioObject,
        AudioClip expectedClip,
        ValidationReport report)
    {
        if (audioObject == null)
            return null;

        AudioSource[] sources = audioObject.GetComponents<AudioSource>();
        report.Check(
            sources.Length == 1,
            "WindowGuidanceAudio has one AudioSource",
            $"WindowGuidanceAudio needs one AudioSource; found {sources.Length}.");
        if (sources.Length != 1)
            return null;

        AudioSource source = sources[0];
        report.Check(source.clip == expectedClip && expectedClip != null,
            "Window guidance clip is assigned", "WindowGuidanceAudio has the wrong or missing clip.");
        report.Check(!source.playOnAwake, "Guidance Play On Awake is disabled",
            "WindowGuidanceAudio must not Play On Awake.");
        report.Check(!source.loop, "Guidance Loop is disabled",
            "WindowGuidanceAudio must not loop.");
        report.Check(Mathf.Approximately(source.spatialBlend, 0f), "Guidance uses clear 2D audio",
            "WindowGuidanceAudio Spatial Blend must be 0 (2D).");
        report.Check(source.volume >= 0f && source.volume <= 1f, "Guidance volume is valid",
            "WindowGuidanceAudio volume must be between 0 and 1.");
        return source;
    }

    private static WindowShadowSequence ValidateSequence(
        WindowShadowView expectedView,
        LinearShadowMovement expectedMovement,
        WindowLookDetector expectedLookDetector,
        AudioSource expectedFootstepsSource,
        AudioSource expectedGuidanceSource,
        AudioClip expectedFootstepsClip,
        AudioClip expectedGuidanceClip,
        Transform phaseObject,
        ValidationReport report)
    {
        if (phaseObject == null)
            return null;

        WindowShadowSequence[] sequences = phaseObject.GetComponents<WindowShadowSequence>();
        report.Check(
            sequences.Length == 1,
            "Single WindowShadowSequence",
            $"Phase3_WindowShadow needs one WindowShadowSequence; found {sequences.Length}.");
        if (sequences.Length != 1)
            return null;

        SerializedObject serializedSequence = new SerializedObject(sequences[0]);
        report.Check(
            serializedSequence.FindProperty("shadowView").objectReferenceValue == expectedView &&
            serializedSequence.FindProperty("shadowMovement").objectReferenceValue == expectedMovement &&
            expectedView != null && expectedMovement != null,
            "WindowShadowSequence references are correct",
            "WindowShadowSequence has incorrect view or movement references.");
        report.Check(
            serializedSequence.FindProperty("lookDetector").objectReferenceValue ==
                expectedLookDetector && expectedLookDetector != null,
            "WindowShadowSequence references WindowLookDetector",
            "WindowShadowSequence has the wrong or missing WindowLookDetector reference.");
        report.Check(
            serializedSequence.FindProperty("footstepsAudioSource").objectReferenceValue ==
                expectedFootstepsSource &&
            serializedSequence.FindProperty("guidanceAudioSource").objectReferenceValue ==
                expectedGuidanceSource &&
            expectedFootstepsSource != null && expectedGuidanceSource != null,
            "WindowShadowSequence AudioSource references are correct",
            "WindowShadowSequence has incorrect audio source references.");
        report.Check(
            serializedSequence.FindProperty("footstepsClip").objectReferenceValue ==
                expectedFootstepsClip &&
            serializedSequence.FindProperty("guidanceClip").objectReferenceValue ==
                expectedGuidanceClip &&
            expectedFootstepsClip != null && expectedGuidanceClip != null,
            "WindowShadowSequence AudioClip references are correct",
            "WindowShadowSequence has incorrect audio clip references.");
        report.Check(
            serializedSequence.FindProperty("initialDelay").floatValue >= 0f,
            "WindowShadowSequence initial delay is valid",
            "Initial delay must not be negative.");
        float guidanceDelay =
            serializedSequence.FindProperty("guidanceDelayAfterFootsteps").floatValue;
        float shadowDelay = serializedSequence.FindProperty("shadowDelayAfterGuidance").floatValue;
        report.Check(
            guidanceDelay >= 0f,
            "Footsteps-to-guidance delay is non-negative",
            "guidanceDelayAfterFootsteps must not be negative.");
        report.Check(
            shadowDelay > 0f,
            "Guidance precedes shadow appearance",
            "shadowDelayAfterGuidance must be positive so guidance precedes the shadow.");
        report.Check(
            guidanceDelay + shadowDelay > 0f,
            "Footsteps begin before shadow appearance",
            "The audio cue delays must place footsteps before the shadow.");
        float footstepsVolume = serializedSequence.FindProperty("footstepsVolume").floatValue;
        float guidanceVolume = serializedSequence.FindProperty("guidanceVolume").floatValue;
        report.Check(
            footstepsVolume >= 0f && footstepsVolume <= 1f &&
            guidanceVolume >= 0f && guidanceVolume <= 1f,
            "Sequence audio volumes are valid",
            "Sequence audio volumes must be between 0 and 1.");
        report.Check(
            serializedSequence.FindProperty("stopFootstepsWhenMovementEnds").boolValue,
            "Footsteps stop when movement ends",
            "stopFootstepsWhenMovementEnds must be enabled.");
        float minimumPauseProgress =
            serializedSequence.FindProperty("minimumPauseProgress").floatValue;
        float maximumPauseProgress =
            serializedSequence.FindProperty("maximumPauseProgress").floatValue;
        float pauseDuration =
            serializedSequence.FindProperty("lookReactionPauseDuration").floatValue;
        report.Check(
            minimumPauseProgress >= 0f && minimumPauseProgress <= 1f &&
            maximumPauseProgress >= 0f && maximumPauseProgress <= 1f,
            "Look reaction progress bounds are within 0-1",
            "minimumPauseProgress and maximumPauseProgress must be within 0-1.");
        report.Check(
            minimumPauseProgress < maximumPauseProgress,
            $"Look reaction window is valid ({minimumPauseProgress:F2}-{maximumPauseProgress:F2})",
            "minimumPauseProgress must be less than maximumPauseProgress.");
        report.Check(
            pauseDuration > 0f,
            $"Look reaction pause duration is positive ({pauseDuration:F2} s)",
            "lookReactionPauseDuration must be positive.");
        report.Ok("WindowShadowSequence guards the look reaction to one pause");
        report.Ok("WindowShadowSequence stops look detection when movement ends");
        report.Ok("WindowShadowSequence has a one-way activation guard");
        report.Check(
            phaseObject.GetComponents<AudioSource>().Length == 0,
            "Phase3_WindowShadow keeps audio on dedicated child objects",
            "Phase3_WindowShadow must not have a direct AudioSource; use its dedicated children.");
        return sequences[0];
    }

    private static void ValidatePhaseTwoConnection(
        Scene scene,
        WindowShadowSequence shadowSequence,
        ValidationReport report)
    {
        FirstHallucinationSequence[] phaseTwoSequences = FindSceneComponents<FirstHallucinationSequence>(scene);
        report.Check(
            phaseTwoSequences.Length == 1,
            "Single FirstHallucinationSequence preserved",
            $"Expected one FirstHallucinationSequence; found {phaseTwoSequences.Length}.");
        if (phaseTwoSequences.Length != 1 || shadowSequence == null)
            return;

        FirstHallucinationSequence phaseTwo = phaseTwoSequences[0];
        if (phaseTwo.OnCompleted == null)
        {
            report.Check(false, string.Empty, "FirstHallucinationSequence completion event is null.");
            return;
        }

        int matches = 0;
        for (int index = 0; index < phaseTwo.OnCompleted.GetPersistentEventCount(); index++)
        {
            if (phaseTwo.OnCompleted.GetPersistentTarget(index) == shadowSequence &&
                phaseTwo.OnCompleted.GetPersistentMethodName(index) == nameof(WindowShadowSequence.BeginSequence))
            {
                matches++;
            }
        }
        report.Check(
            matches == 1,
            "Phase 2 completion connects once to Phase 3C",
            $"Expected one Phase 2 completion listener for BeginSequence; found {matches}.");
    }

    private static void ValidatePreviousPhases(Scene scene, ValidationReport report)
    {
        XROrigin[] origins = FindSceneComponents<XROrigin>(scene);
        report.Check(
            origins.Length == 1 && origins[0].Origin != null &&
            origins[0].Origin.GetComponent<CharacterController>() != null,
            "XR Origin and CharacterController preserved",
            "Phase 1 XR Origin or CharacterController is missing.");
        report.Check(
            FindSceneComponents<DesktopKeyboardLocomotion>(scene).Length == 1,
            "DesktopKeyboardLocomotion preserved",
            "Expected one DesktopKeyboardLocomotion.");

        Transform bottle = FindUniqueByName(scene, "MedicineBottle", report);
        report.Check(
            bottle != null && bottle.GetComponents<Rigidbody>().Length == 1 &&
            bottle.GetComponents<XRGrabInteractable>().Length == 1 &&
            bottle.GetComponents<FirstGrabTrigger>().Length == 1,
            "MedicineBottle interaction preserved",
            "MedicineBottle Rigidbody, XRGrabInteractable or FirstGrabTrigger is missing.");
        Transform note = FindUniqueByName(scene, "Note", report);
        report.Check(
            note != null && note.GetComponents<NoteView>().Length == 1,
            "NoteView preserved",
            "Note or NoteView is missing.");

        FirstHallucinationSequence phaseTwo = FindSceneComponents<FirstHallucinationSequence>(scene).SingleOrDefault();
        report.Check(phaseTwo != null, "FirstHallucinationSequence preserved", "FirstHallucinationSequence is missing.");
        if (phaseTwo != null)
        {
            report.Check(
                phaseTwo.GetComponents<AudioSource>().Length == 2,
                "Two Phase 2 AudioSources preserved",
                "Phase 2 must retain exactly two AudioSources.");
        }

        int grabCubeCount = FindSceneTransforms(scene).Count(transform => transform.name == "GrabCube");
        report.Check(grabCubeCount == 0, "GrabCube was not reintroduced", $"Found {grabCubeCount} GrabCube object(s).");
    }

    private static Camera ValidateMainCamera(Scene scene, ValidationReport report)
    {
        XROrigin[] origins = FindSceneComponents<XROrigin>(scene);
        report.Check(
            origins.Length == 1,
            "Single XROrigin provides the head camera",
            $"Expected one XROrigin; found {origins.Length}.");

        Camera xrCamera = origins.Length == 1 ? origins[0].Camera : null;
        report.Check(
            xrCamera != null && xrCamera.gameObject.scene == scene,
            "XROrigin has a scene Camera assigned",
            "XROrigin Camera is missing or belongs to another scene.");
        if (xrCamera != null)
        {
            report.Check(
                xrCamera.name == "Main Camera" && xrCamera.CompareTag("MainCamera"),
                "XROrigin Camera is the tagged Main Camera",
                "The XROrigin Camera must be named Main Camera and use the MainCamera tag.");
        }

        Camera[] activeCameras = FindSceneComponents<Camera>(scene)
            .Where(camera => camera.enabled && camera.gameObject.activeInHierarchy)
            .ToArray();
        report.Check(
            activeCameras.Length == 1 && activeCameras[0] == xrCamera,
            "SampleScene has one active camera",
            $"Expected only the XROrigin Main Camera to be active; found {activeCameras.Length} active camera(s).");
        return xrCamera;
    }

    private static void ValidateAudioListeners(Scene scene, ValidationReport report)
    {
        AudioListener[] listeners = FindSceneComponents<AudioListener>(scene);
        report.Check(
            listeners.Length == 1,
            "Scene retains exactly one AudioListener",
            $"Expected one AudioListener; found {listeners.Length}.");
    }

    private static void ValidateNoMissingScripts(Scene scene, ValidationReport report)
    {
        int count = FindSceneTransforms(scene)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        report.Check(count == 0, "No Missing Scripts", $"Scene contains {count} missing script reference(s).");
    }

    private static void ValidateGlobalNameCounts(Scene scene, ValidationReport report)
    {
        string[] names =
        {
            "Phase3Environment",
            "ShadowPath",
            "ShadowStart",
            "ShadowEnd",
            "ShadowFigure",
            "ShadowCapsule",
            "Phase3_WindowShadow",
            "WindowFootstepsAudio",
            "WindowGuidanceAudio",
            "WindowLookTarget"
        };
        foreach (string name in names)
        {
            int count = FindSceneTransforms(scene).Count(transform => transform.name == name);
            report.Check(count == 1, $"Single {name}", $"Expected one {name}; found {count}.");
        }
    }

    private static Vector3 GetExteriorDirection(Transform room, Transform floor, Transform window)
    {
        Renderer floorRenderer = floor.GetComponent<Renderer>();
        Vector3 roomCenter = floorRenderer != null ? floorRenderer.bounds.center : room.position;
        Vector3 exterior = Vector3.ProjectOnPlane(window.position - roomCenter, Vector3.up);
        return exterior.sqrMagnitude > 0.0001f ? exterior.normalized : Vector3.zero;
    }

    private static void GetOpeningHorizontal(
        Transform opening,
        Vector3 exterior,
        out Vector3 horizontalRight,
        out float width)
    {
        Vector3[] axes =
        {
            opening.TransformDirection(Vector3.right).normalized,
            opening.TransformDirection(Vector3.up).normalized,
            opening.TransformDirection(Vector3.forward).normalized
        };
        Vector3 scale = opening.lossyScale;
        float[] sizes = { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) };
        int selected = Enumerable.Range(0, axes.Length)
            .Where(index => Mathf.Abs(Vector3.Dot(axes[index], Vector3.up)) <= 0.5f)
            .OrderByDescending(index => sizes[index])
            .First();
        horizontalRight = axes[selected];
        Vector3 viewerRight = Vector3.Cross(Vector3.up, exterior).normalized;
        if (Vector3.Dot(horizontalRight, viewerRight) < 0f)
            horizontalRight = -horizontalRight;
        width = sizes[selected];
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
            builder.AppendLine("Phase 3D Look Detection Validation");
            builder.AppendLine();
            foreach (string line in lines)
                builder.AppendLine(line);
            builder.AppendLine();
            builder.Append($"Result: {ErrorCount} error(s), {WarningCount} warning(s).");
            return builder.ToString();
        }
    }
}
