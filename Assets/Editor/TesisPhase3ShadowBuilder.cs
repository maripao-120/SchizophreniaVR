using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TesisPhase3ShadowBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ShadowMaterialPath =
        "Assets/Generated/Phase3/Materials/MAT_Phase3_Shadow.mat";
    private const string FootstepsClipPath =
        "Assets/Audio/Phase3/Phase3_ShadowFootsteps.mp3";
    private const string GuidanceClipPath =
        "Assets/Audio/Phase3/Phase3_WindowGuidance.mp3";
    private const string ClosingClipPath =
        "Assets/Audio/Phase3/Phase3_PostShadowVoices_Test.mp3";
    private const float ExteriorDistance = 0.65f;
    private const float MaximumFunctionalAudioDistance = 2.5f;
    private const float MaximumFunctionalLookTargetDistance = 0.25f;
    private const float PathMargin = 0.45f;
    private const float FigureHeight = 1.8f;
    private const float FigureDiameter = 0.6f;

    [MenuItem("Tools/Tesis VR/Phase 3/Build or Configure Shadow")]
    private static void BuildFromMenu()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildOrConfigure(scene);
            SaveSceneAndAssets(scene);

            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3D",
                "Window shadow look reaction configured. Run Validate Shadow next.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3D Error",
                "Shadow configuration failed. See Console for details.",
                "OK");
        }
    }

    private static void BuildOrConfigure(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException($"The loaded scene must be {ScenePath}.");

        Transform room = FindRequiredRoot(scene, "Room");
        Transform architecture = FindRequiredDirectChild(room, "Architecture");
        Transform window = FindRequiredDirectChild(architecture, "Window_01");
        Transform opening = FindRequiredDirectChild(window, "ViewOpening");
        Camera mainCamera = FindRequiredMainCamera(scene);
        Transform floor = FindRequiredDirectChild(
            FindRequiredDirectChild(room, "Structure"),
            "Floor");

        Transform lookTarget = GetOrCreateDirectChild(
            window,
            "WindowLookTarget",
            out bool lookTargetCreated);
        PositionLookTarget(lookTarget, opening, lookTargetCreated);

        CalculatePath(
            room,
            window,
            opening,
            floor,
            out Vector3 startPosition,
            out Vector3 endPosition,
            out Vector3 exteriorDirection);

        Material shadowMaterial = GetOrCreateShadowMaterial();
        Transform environment = GetOrCreateDirectChild(room, "Phase3Environment", out bool environmentCreated);
        if (environmentCreated)
            SetLocalIdentity(environment);

        Transform path = GetOrCreateDirectChild(environment, "ShadowPath", out bool pathCreated);
        if (pathCreated)
            SetLocalIdentity(path);

        Transform start = GetOrCreateDirectChild(path, "ShadowStart", out bool startCreated);
        if (startCreated)
            SetWorldPosition(start, startPosition);

        Transform end = GetOrCreateDirectChild(path, "ShadowEnd", out bool endCreated);
        if (endCreated)
            SetWorldPosition(end, endPosition);

        Transform figure = GetOrCreateDirectChild(environment, "ShadowFigure", out bool figureCreated);
        if (figureCreated)
        {
            SetWorldPosition(figure, start.position);
            figure.rotation = Quaternion.identity;
            figure.localScale = Vector3.one;
        }

        Transform capsule = GetOrCreateCapsule(figure, "ShadowCapsule", out bool capsuleCreated);
        if (capsuleCreated)
            SetLocalTransform(capsule, Vector3.zero, new Vector3(FigureDiameter, FigureHeight * 0.5f, FigureDiameter));
        ConfigureCapsule(capsule, shadowMaterial, capsuleCreated);

        AudioClip footstepsClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepsClipPath);
        Transform footstepsObject = GetOrCreateDirectChild(
            environment,
            "WindowFootstepsAudio",
            out bool footstepsObjectCreated);
        PositionFootstepsAudio(
            footstepsObject,
            opening,
            exteriorDirection,
            footstepsObjectCreated);
        AudioSource footstepsSource = GetOrAddSingleComponent<AudioSource>(
            footstepsObject.gameObject,
            out bool footstepsSourceCreated);
        ConfigureFootstepsAudio(footstepsSource, footstepsClip, footstepsSourceCreated);

        Transform experience = FindRequiredRoot(scene, "Experience");
        Transform phaseObject = GetOrCreateDirectChild(
            experience,
            "Phase3_WindowShadow",
            out bool phaseObjectCreated);
        if (phaseObjectCreated)
            SetLocalIdentity(phaseObject);

        AudioClip guidanceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GuidanceClipPath);
        Transform guidanceObject = GetOrCreateDirectChild(
            phaseObject,
            "WindowGuidanceAudio",
            out bool guidanceObjectCreated);
        if (guidanceObjectCreated)
            SetLocalIdentity(guidanceObject);
        AudioSource guidanceSource = GetOrAddSingleComponent<AudioSource>(
            guidanceObject.gameObject,
            out bool guidanceSourceCreated);
        ConfigureGuidanceAudio(guidanceSource, guidanceClip, guidanceSourceCreated);

        AudioClip closingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClosingClipPath);
        Transform closingObject = GetOrCreateDirectChild(
            phaseObject,
            "WindowClosingAudio",
            out bool closingObjectCreated);
        if (closingObjectCreated)
            SetLocalIdentity(closingObject);
        AudioSource closingSource = GetOrAddSingleComponent<AudioSource>(
            closingObject.gameObject,
            out bool closingSourceCreated);
        ConfigureClosingAudio(closingSource, closingClip, closingSourceCreated);

        WindowShadowView view = GetOrAddSingleComponent<WindowShadowView>(phaseObject.gameObject, out _);
        LinearShadowMovement movement =
            GetOrAddSingleComponent<LinearShadowMovement>(phaseObject.gameObject, out bool movementCreated);
        WindowShadowSequence sequence =
            GetOrAddSingleComponent<WindowShadowSequence>(phaseObject.gameObject, out bool sequenceCreated);
        WindowLookDetector lookDetector =
            GetOrAddSingleComponent<WindowLookDetector>(phaseObject.gameObject, out bool detectorCreated);

        ConfigureView(view, capsule.GetComponent<Renderer>());
        ConfigureMovement(movement, figure, start, end, movementCreated);
        ConfigureLookDetector(
            lookDetector,
            mainCamera.transform,
            lookTarget,
            detectorCreated);
        ConfigureSequence(
            sequence,
            view,
            movement,
            lookDetector,
            footstepsSource,
            guidanceSource,
            closingSource,
            footstepsClip,
            guidanceClip,
            closingClip,
            sequenceCreated);
        ConnectLookConfirmation(lookDetector, sequence);
        ConnectPhaseTwoCompletion(scene, sequence);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            "TesisPhase3ShadowBuilder: Phase 3D look reaction configured. " +
            $"Exterior={exteriorDirection}, path={Vector3.Distance(start.position, end.position):F2} m, " +
            $"camera={GetPath(mainCamera.transform)}, target={GetPath(lookTarget)}, " +
            "look listener=1, Phase 2 completion listener=1.");

        if (footstepsClip == null)
            Debug.LogWarning($"TesisPhase3ShadowBuilder: missing optional audio clip {FootstepsClipPath}.");
        if (guidanceClip == null)
            Debug.LogWarning($"TesisPhase3ShadowBuilder: missing optional audio clip {GuidanceClipPath}.");
        if (closingClip == null)
            Debug.LogWarning($"TesisPhase3ShadowBuilder: missing optional audio clip {ClosingClipPath}.");
    }

    private static void CalculatePath(
        Transform room,
        Transform window,
        Transform opening,
        Transform floor,
        out Vector3 startPosition,
        out Vector3 endPosition,
        out Vector3 exteriorDirection)
    {
        Vector3 roomCenter = GetRoomCenter(room, floor);
        exteriorDirection = Vector3.ProjectOnPlane(window.position - roomCenter, Vector3.up);
        if (exteriorDirection.sqrMagnitude < 0.0001f)
            throw new InvalidOperationException("Could not derive the exterior direction from Room and Window_01.");
        exteriorDirection.Normalize();

        GetOpeningHorizontal(opening, exteriorDirection, out Vector3 horizontalRight, out float openingWidth);
        float floorHeight = GetFloorHeight(floor);
        Vector3 pathCenter = opening.position + (exteriorDirection * ExteriorDistance);
        pathCenter.y = floorHeight + (FigureHeight * 0.5f);
        float halfTravel = (openingWidth * 0.5f) + PathMargin;
        startPosition = pathCenter - (horizontalRight * halfTravel);
        endPosition = pathCenter + (horizontalRight * halfTravel);
    }

    private static void GetOpeningHorizontal(
        Transform opening,
        Vector3 exteriorDirection,
        out Vector3 horizontalRight,
        out float openingWidth)
    {
        Vector3[] axes =
        {
            opening.TransformDirection(Vector3.right).normalized,
            opening.TransformDirection(Vector3.up).normalized,
            opening.TransformDirection(Vector3.forward).normalized
        };
        Vector3 scale = opening.lossyScale;
        float[] sizes = { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) };

        int horizontalIndex = -1;
        float largestHorizontalSize = 0f;
        for (int index = 0; index < axes.Length; index++)
        {
            if (Mathf.Abs(Vector3.Dot(axes[index], Vector3.up)) > 0.5f)
                continue;
            if (sizes[index] <= largestHorizontalSize)
                continue;

            horizontalIndex = index;
            largestHorizontalSize = sizes[index];
        }

        if (horizontalIndex < 0 || largestHorizontalSize <= 0.01f)
            throw new InvalidOperationException("ViewOpening has no usable horizontal dimension.");

        Vector3 viewerRight = Vector3.Cross(Vector3.up, exteriorDirection).normalized;
        horizontalRight = axes[horizontalIndex];
        if (Vector3.Dot(horizontalRight, viewerRight) < 0f)
            horizontalRight = -horizontalRight;
        openingWidth = largestHorizontalSize;
    }

    private static Vector3 GetRoomCenter(Transform room, Transform floor)
    {
        Renderer floorRenderer = floor.GetComponent<Renderer>();
        Vector3 center = floorRenderer != null ? floorRenderer.bounds.center : floor.position;
        center.y = room.position.y;
        return center;
    }

    private static float GetFloorHeight(Transform floor)
    {
        Renderer floorRenderer = floor.GetComponent<Renderer>();
        return floorRenderer != null ? floorRenderer.bounds.max.y : floor.position.y;
    }

    private static void ConfigureCapsule(Transform capsule, Material material, bool created)
    {
        MeshRenderer renderer = RequireSingleComponent<MeshRenderer>(capsule.gameObject);
        RequireSingleComponent<MeshFilter>(capsule.gameObject);
        if (created || renderer.sharedMaterial == null)
            renderer.sharedMaterial = material;
        renderer.enabled = false;

        RemoveComponents<Collider>(capsule.gameObject);
        RemoveComponents<Rigidbody>(capsule.gameObject);
        RemoveComponents<XRGrabInteractable>(capsule.gameObject);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(capsule.gameObject);
    }

    private static void ConfigureView(WindowShadowView view, Renderer renderer)
    {
        SerializedObject serializedView = new SerializedObject(view);
        SerializedProperty renderers = serializedView.FindProperty("controlledRenderers");
        renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        serializedView.ApplyModifiedProperties();
        view.ResetView();
        EditorUtility.SetDirty(view);
    }

    private static void ConfigureMovement(
        LinearShadowMovement movement,
        Transform figure,
        Transform start,
        Transform end,
        bool created)
    {
        SerializedObject serializedMovement = new SerializedObject(movement);
        serializedMovement.FindProperty("movingTransform").objectReferenceValue = figure;
        serializedMovement.FindProperty("startPoint").objectReferenceValue = start;
        serializedMovement.FindProperty("endPoint").objectReferenceValue = end;
        if (created)
        {
            serializedMovement.FindProperty("movementDuration").floatValue = 8f;
            serializedMovement.FindProperty("movementCurve").animationCurveValue =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
        serializedMovement.ApplyModifiedProperties();
        movement.ResetPosition();
        EditorUtility.SetDirty(movement);
    }

    private static void ConfigureSequence(
        WindowShadowSequence sequence,
        WindowShadowView view,
        LinearShadowMovement movement,
        WindowLookDetector lookDetector,
        AudioSource footstepsSource,
        AudioSource guidanceSource,
        AudioSource closingSource,
        AudioClip footstepsClip,
        AudioClip guidanceClip,
        AudioClip closingClip,
        bool created)
    {
        SerializedObject serializedSequence = new SerializedObject(sequence);
        serializedSequence.FindProperty("shadowView").objectReferenceValue = view;
        serializedSequence.FindProperty("shadowMovement").objectReferenceValue = movement;
        serializedSequence.FindProperty("lookDetector").objectReferenceValue = lookDetector;
        serializedSequence.FindProperty("footstepsAudioSource").objectReferenceValue = footstepsSource;
        serializedSequence.FindProperty("guidanceAudioSource").objectReferenceValue = guidanceSource;
        serializedSequence.FindProperty("closingAudioSource").objectReferenceValue = closingSource;
        serializedSequence.FindProperty("footstepsClip").objectReferenceValue = footstepsClip;
        serializedSequence.FindProperty("guidanceClip").objectReferenceValue = guidanceClip;
        serializedSequence.FindProperty("closingClip").objectReferenceValue = closingClip;
        if (created)
            serializedSequence.FindProperty("initialDelay").floatValue = 4f;

        SerializedProperty minimumProgress =
            serializedSequence.FindProperty("minimumPauseProgress");
        SerializedProperty maximumProgress =
            serializedSequence.FindProperty("maximumPauseProgress");
        SerializedProperty pauseDuration =
            serializedSequence.FindProperty("lookReactionPauseDuration");
        if (minimumProgress.floatValue < 0f || minimumProgress.floatValue >= 1f)
            minimumProgress.floatValue = 0.15f;
        if (maximumProgress.floatValue <= minimumProgress.floatValue ||
            maximumProgress.floatValue > 1f)
        {
            maximumProgress.floatValue = 0.8f;
        }
        if (pauseDuration.floatValue <= 0f)
            pauseDuration.floatValue = 1.5f;
        serializedSequence.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequence);
    }

    private static void PositionLookTarget(
        Transform lookTarget,
        Transform opening,
        bool created)
    {
        if (created ||
            Vector3.Distance(lookTarget.position, opening.position) >
            MaximumFunctionalLookTargetDistance)
        {
            SetWorldPosition(lookTarget, opening.position);
        }
    }

    private static void ConfigureLookDetector(
        WindowLookDetector detector,
        Transform cameraTransform,
        Transform lookTarget,
        bool created)
    {
        SerializedObject serializedDetector = new SerializedObject(detector);
        serializedDetector.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
        serializedDetector.FindProperty("lookTarget").objectReferenceValue = lookTarget;
        if (created)
        {
            serializedDetector.FindProperty("maximumLookAngle").floatValue = 25f;
            serializedDetector.FindProperty("requiredLookDuration").floatValue = 0.5f;
        }
        serializedDetector.ApplyModifiedProperties();
        EditorUtility.SetDirty(detector);
    }

    private static void ConnectLookConfirmation(
        WindowLookDetector detector,
        WindowShadowSequence sequence)
    {
        if (detector.OnLookConfirmed == null)
            throw new InvalidOperationException("WindowLookDetector confirmation event is unavailable.");

        int firstMatchingIndex = -1;
        for (int index = detector.OnLookConfirmed.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            bool matches = detector.OnLookConfirmed.GetPersistentTarget(index) == sequence &&
                           detector.OnLookConfirmed.GetPersistentMethodName(index) ==
                           nameof(WindowShadowSequence.HandleLookConfirmed);
            if (!matches)
                continue;

            if (firstMatchingIndex < 0)
                firstMatchingIndex = index;
            else
                UnityEventTools.RemovePersistentListener(detector.OnLookConfirmed, index);
        }

        if (firstMatchingIndex < 0)
            UnityEventTools.AddPersistentListener(
                detector.OnLookConfirmed,
                sequence.HandleLookConfirmed);

        EditorUtility.SetDirty(detector);
    }

    private static void PositionFootstepsAudio(
        Transform audioTransform,
        Transform opening,
        Vector3 exteriorDirection,
        bool created)
    {
        Vector3 targetPosition = opening.position + (exteriorDirection * ExteriorDistance);
        bool isOutsideOrOnPlane =
            Vector3.Dot(audioTransform.position - opening.position, exteriorDirection) >= -0.05f;
        bool isNearOpening =
            Vector3.Distance(audioTransform.position, opening.position) <= MaximumFunctionalAudioDistance;
        if (created || !isOutsideOrOnPlane || !isNearOpening)
            SetWorldPosition(audioTransform, targetPosition);
    }

    private static void ConfigureFootstepsAudio(
        AudioSource source,
        AudioClip clip,
        bool created)
    {
        Undo.RecordObject(source, "Configure Phase 3 footsteps audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.clip = clip;
        if (created)
        {
            source.volume = 0.8f;
            source.minDistance = 1f;
            source.maxDistance = 12f;
        }
        else if (source.minDistance <= 0f || source.maxDistance <= source.minDistance)
        {
            source.minDistance = 1f;
            source.maxDistance = 12f;
        }
        EditorUtility.SetDirty(source);
    }

    private static void ConfigureGuidanceAudio(
        AudioSource source,
        AudioClip clip,
        bool created)
    {
        Undo.RecordObject(source, "Configure Phase 3 guidance audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        if (created)
            source.volume = 1f;
        EditorUtility.SetDirty(source);
    }

    private static void ConfigureClosingAudio(
        AudioSource source,
        AudioClip clip,
        bool created)
    {
        Undo.RecordObject(source, "Configure Phase 3 closing audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        if (created)
            source.volume = 1f;
        EditorUtility.SetDirty(source);
    }

    private static void ConnectPhaseTwoCompletion(Scene scene, WindowShadowSequence shadowSequence)
    {
        FirstHallucinationSequence[] phaseTwoSequences =
            UnityEngine.Object.FindObjectsByType<FirstHallucinationSequence>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(sequence => sequence.gameObject.scene == scene)
                .ToArray();
        if (phaseTwoSequences.Length != 1)
            throw new InvalidOperationException(
                $"Expected one FirstHallucinationSequence; found {phaseTwoSequences.Length}.");

        FirstHallucinationSequence phaseTwo = phaseTwoSequences[0];
        if (phaseTwo.OnCompleted == null)
            throw new InvalidOperationException("FirstHallucinationSequence completion event is unavailable.");

        int firstMatchingIndex = -1;
        for (int index = phaseTwo.OnCompleted.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            bool matches = phaseTwo.OnCompleted.GetPersistentTarget(index) == shadowSequence &&
                           phaseTwo.OnCompleted.GetPersistentMethodName(index) ==
                           nameof(WindowShadowSequence.BeginSequence);
            if (!matches)
                continue;

            if (firstMatchingIndex < 0)
                firstMatchingIndex = index;
            else
                UnityEventTools.RemovePersistentListener(phaseTwo.OnCompleted, index);
        }

        if (firstMatchingIndex < 0)
            UnityEventTools.AddPersistentListener(phaseTwo.OnCompleted, shadowSequence.BeginSequence);

        EditorUtility.SetDirty(phaseTwo);
    }

    private static Material GetOrCreateShadowMaterial()
    {
        EnsureFolder("Assets/Generated", "Phase3");
        EnsureFolder("Assets/Generated/Phase3", "Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP/Lit shader was not found for the shadow material.");

        material = new Material(shader) { name = "MAT_Phase3_Shadow" };
        Color shadowColor = new Color(0.015f, 0.015f, 0.018f, 1f);
        material.color = shadowColor;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", shadowColor);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.renderQueue = -1;
        AssetDatabase.CreateAsset(material, ShadowMaterialPath);
        return material;
    }

    private static Transform GetOrCreateCapsule(Transform parent, string name, out bool created)
    {
        Transform existing = FindSingleDirectChild(parent, name);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = name;
        Undo.RegisterCreatedObjectUndo(capsule, $"Create {name}");
        Undo.SetTransformParent(capsule.transform, parent, $"Parent {name}");
        created = true;
        return capsule.transform;
    }

    private static T GetOrAddSingleComponent<T>(GameObject target, out bool created)
        where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException(
                $"{GetPath(target.transform)} has duplicate {typeof(T).Name} components.");
        if (components.Length == 1)
        {
            created = false;
            return components[0];
        }

        created = true;
        return Undo.AddComponent<T>(target);
    }

    private static T RequireSingleComponent<T>(GameObject target) where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length != 1)
            throw new InvalidOperationException(
                $"{GetPath(target.transform)} must have one {typeof(T).Name}; found {components.Length}.");
        return components[0];
    }

    private static void RemoveComponents<T>(GameObject target) where T : Component
    {
        foreach (T component in target.GetComponents<T>())
            Undo.DestroyObjectImmediate(component);
    }

    private static Transform GetOrCreateDirectChild(Transform parent, string name, out bool created)
    {
        Transform existing = FindSingleDirectChild(parent, name);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        Undo.SetTransformParent(child.transform, parent, $"Parent {name}");
        created = true;
        return child.transform;
    }

    private static Transform FindSingleDirectChild(Transform parent, string name)
    {
        Transform[] matches = parent.Cast<Transform>().Where(child => child.name == name).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"{GetPath(parent)}/{name} is duplicated.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindRequiredDirectChild(Transform parent, string name)
    {
        Transform child = FindSingleDirectChild(parent, name);
        if (child == null)
            throw new InvalidOperationException($"Missing {GetPath(parent)}/{name}.");
        return child;
    }

    private static Camera FindRequiredMainCamera(Scene scene)
    {
        XROrigin[] origins = UnityEngine.Object.FindObjectsByType<XROrigin>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(origin => origin.gameObject.scene == scene)
            .ToArray();
        if (origins.Length != 1)
            throw new InvalidOperationException(
                $"Expected one XROrigin in SampleScene; found {origins.Length}.");

        Camera xrCamera = origins[0].Camera;
        if (xrCamera == null || xrCamera.gameObject.scene != scene)
            throw new InvalidOperationException("XROrigin has no scene Camera assigned.");

        Camera[] activeCameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(camera => camera.gameObject.scene == scene &&
                             camera.enabled && camera.gameObject.activeInHierarchy)
            .ToArray();
        if (activeCameras.Length != 1 || activeCameras[0] != xrCamera)
            throw new InvalidOperationException(
                "SampleScene must have exactly one active Camera and it must belong to XROrigin.");
        if (!xrCamera.CompareTag("MainCamera"))
            throw new InvalidOperationException("The XROrigin Camera must use the MainCamera tag.");

        return xrCamera;
    }

    private static Transform FindRequiredRoot(Scene scene, string name)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one root named {name}; found {matches.Length}.");
        return matches[0];
    }

    private static void SetLocalIdentity(Transform target)
    {
        Undo.RecordObject(target, $"Configure {target.name}");
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void SetWorldPosition(Transform target, Vector3 position)
    {
        Undo.RecordObject(target, $"Position {target.name}");
        target.position = position;
        target.rotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void SetLocalTransform(Transform target, Vector3 position, Vector3 scale)
    {
        Undo.RecordObject(target, $"Configure {target.name}");
        target.localPosition = position;
        target.localRotation = Quaternion.identity;
        target.localScale = scale;
    }

    private static void EnsureFolder(string parentPath, string name)
    {
        string path = parentPath + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parentPath, name);
    }

    private static void SaveSceneAndAssets(Scene scene)
    {
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException($"Unity could not save {ScenePath}.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string GetPath(Transform target)
    {
        return target.parent == null ? target.name : GetPath(target.parent) + "/" + target.name;
    }
}
