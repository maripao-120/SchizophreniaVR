using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TesisPhase4Builder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RoofImpactsClipPath =
        "Assets/Audio/Phase4/Phase4_RoofImpacts.mp3";
    private const string CeilingGuidanceClipPath =
        "Assets/Audio/Phase4/Phase4_CeilingGuidance.mp3";
    private const string ArmsMaterialDirectory = "Assets/Generated/Phase4/Materials";
    private const string ArmsMaterialPath =
        ArmsMaterialDirectory + "/MAT_Phase4_Arms.mat";
    private const float ArmHorizontalOffset = 0.55f;
    private const float MaximumAudioDistanceFromTarget = 1f;

    [MenuItem("Tools/Tesis VR/Phase 4/Build or Configure")]
    private static void BuildFromMenu()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildOrConfigure(scene);
            SaveSceneAndAssets(scene);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 4",
                "Ceiling arms hallucination configured. Run Phase 4 > Validate next.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 4 Error",
                "Phase 4 configuration failed. See Console for details.",
                "OK");
        }
    }

    private static void BuildOrConfigure(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException($"The loaded scene must be {ScenePath}.");

        Transform room = FindRequiredRoot(scene, "Room");
        Transform structure = FindRequiredDirectChild(room, "Structure");
        Transform ceiling = FindRequiredDirectChild(structure, "Ceiling");
        Camera mainCamera = FindRequiredMainCamera(scene);
        Transform experience = FindRequiredRoot(scene, "Experience");
        WindowShadowSequence phaseThree = FindRequiredSceneComponent<WindowShadowSequence>(scene);

        Transform lookTarget = GetOrCreateDirectChild(
            ceiling,
            "CeilingLookTarget",
            out bool lookTargetCreated);
        ConfigureLookTarget(lookTarget, lookTargetCreated);

        Transform environment = GetOrCreateDirectChild(
            room,
            "Phase4Environment",
            out bool environmentCreated);
        if (environmentCreated)
            SetLocalIdentity(environment);

        Transform arms = GetOrCreateDirectChild(
            environment,
            "CeilingArms",
            out bool armsCreated);
        if (armsCreated)
        {
            arms.position = lookTarget.position + (ceiling.TransformDirection(Vector3.down).normalized * 0.08f);
            arms.rotation = ceiling.rotation;
            arms.localScale = Vector3.one;
        }

        Material material = GetOrCreateArmsMaterial();
        ArmParts leftArm = BuildArm(arms, "LeftArm", -ArmHorizontalOffset, 8f, material);
        ArmParts rightArm = BuildArm(arms, "RightArm", ArmHorizontalOffset, -8f, material);

        AudioClip impactsClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoofImpactsClipPath);
        Transform impactsObject = GetOrCreateDirectChild(
            environment,
            "RoofImpactsAudio",
            out bool impactsObjectCreated);
        if (impactsObjectCreated ||
            Vector3.Distance(impactsObject.position, lookTarget.position) > MaximumAudioDistanceFromTarget)
        {
            impactsObject.position = lookTarget.position;
        }
        AudioSource impactsSource = GetOrAddSingleComponent<AudioSource>(
            impactsObject.gameObject,
            out bool impactsSourceCreated);
        ConfigureImpactsAudio(impactsSource, impactsClip, impactsSourceCreated);

        Transform phaseObject = GetOrCreateDirectChild(
            experience,
            "Phase4_CeilingArms",
            out bool phaseObjectCreated);
        if (phaseObjectCreated)
            SetLocalIdentity(phaseObject);

        AudioClip guidanceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CeilingGuidanceClipPath);
        Transform guidanceObject = GetOrCreateDirectChild(
            phaseObject,
            "CeilingGuidanceAudio",
            out bool guidanceObjectCreated);
        if (guidanceObjectCreated)
            SetLocalIdentity(guidanceObject);
        AudioSource guidanceSource = GetOrAddSingleComponent<AudioSource>(
            guidanceObject.gameObject,
            out bool guidanceSourceCreated);
        ConfigureGuidanceAudio(guidanceSource, guidanceClip, guidanceSourceCreated);

        CeilingArmsView view =
            GetOrAddSingleComponent<CeilingArmsView>(phaseObject.gameObject, out _);
        SimpleArmMovement movement =
            GetOrAddSingleComponent<SimpleArmMovement>(phaseObject.gameObject, out bool movementCreated);
        WindowLookDetector detector =
            GetOrAddSingleComponent<WindowLookDetector>(phaseObject.gameObject, out bool detectorCreated);
        CeilingArmsSequence sequence =
            GetOrAddSingleComponent<CeilingArmsSequence>(phaseObject.gameObject, out bool sequenceCreated);

        Renderer[] armRenderers =
        {
            leftArm.UpperRenderer,
            leftArm.ForearmRenderer,
            rightArm.UpperRenderer,
            rightArm.ForearmRenderer
        };
        ConfigureView(view, armRenderers);
        ConfigureMovement(
            movement,
            leftArm.Forearm,
            rightArm.Forearm,
            movementCreated);
        ConfigureDetector(
            detector,
            mainCamera.transform,
            lookTarget,
            detectorCreated);
        ConfigureSequence(
            sequence,
            view,
            movement,
            detector,
            impactsSource,
            guidanceSource,
            impactsClip,
            guidanceClip,
            sequenceCreated);
        ConnectLookConfirmation(detector, sequence);
        ConnectPhaseThreeCompletion(phaseThree, sequence);

        view.ResetView();
        movement.ResetMovement();
        detector.ResetDetection();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "TesisPhase4Builder: Phase 4 configured. " +
            $"Ceiling={GetPath(ceiling)}, target={GetPath(lookTarget)}, " +
            $"camera={GetPath(mainCamera.transform)}, renderers={armRenderers.Length}, " +
            "look listener=1, Phase 3 completion listener=1.");

        if (impactsClip == null)
            Debug.LogWarning($"TesisPhase4Builder: missing optional audio clip {RoofImpactsClipPath}.");
        if (guidanceClip == null)
            Debug.LogWarning($"TesisPhase4Builder: missing optional audio clip {CeilingGuidanceClipPath}.");
    }

    private static ArmParts BuildArm(
        Transform arms,
        string name,
        float horizontalOffset,
        float tiltDegrees,
        Material material)
    {
        Transform arm = GetOrCreateDirectChild(arms, name, out bool armCreated);
        if (armCreated)
        {
            arm.localPosition = new Vector3(horizontalOffset, 0f, 0f);
            arm.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
            arm.localScale = Vector3.one;
        }

        Transform upperArm = GetOrCreateCapsule(arm, "UpperArm", out bool upperCreated);
        if (upperCreated)
            SetLocalTransform(upperArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.22f, 0.32f, 0.22f));
        Renderer upperRenderer = ConfigureArmPart(upperArm, material, upperCreated);

        Transform forearm = GetOrCreateCapsule(arm, "Forearm", out bool forearmCreated);
        if (forearmCreated)
            SetLocalTransform(forearm, new Vector3(0f, -0.86f, 0f), new Vector3(0.2f, 0.3f, 0.2f));
        Renderer forearmRenderer = ConfigureArmPart(forearm, material, forearmCreated);

        return new ArmParts(upperArm, forearm, upperRenderer, forearmRenderer);
    }

    private static Renderer ConfigureArmPart(Transform part, Material material, bool created)
    {
        MeshRenderer renderer = RequireSingleComponent<MeshRenderer>(part.gameObject);
        RequireSingleComponent<MeshFilter>(part.gameObject);
        if (created || renderer.sharedMaterial == null)
            renderer.sharedMaterial = material;
        renderer.enabled = false;

        RemoveComponents<Collider>(part.gameObject);
        RemoveComponents<Rigidbody>(part.gameObject);
        RemoveComponents<XRGrabInteractable>(part.gameObject);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(part.gameObject);
        return renderer;
    }

    private static void ConfigureLookTarget(Transform target, bool created)
    {
        if (created)
        {
            target.localPosition = new Vector3(0f, -0.6f, 0f);
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        RemoveComponents<Renderer>(target.gameObject);
        RemoveComponents<Rigidbody>(target.gameObject);
        RemoveComponents<Collider>(target.gameObject);
        EditorUtility.SetDirty(target.gameObject);
    }

    private static void ConfigureView(CeilingArmsView view, Renderer[] renderers)
    {
        SerializedObject serializedView = new SerializedObject(view);
        SerializedProperty controlledRenderers = serializedView.FindProperty("controlledRenderers");
        controlledRenderers.arraySize = renderers.Length;
        for (int index = 0; index < renderers.Length; index++)
            controlledRenderers.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
        serializedView.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static void ConfigureMovement(
        SimpleArmMovement movement,
        Transform leftForearm,
        Transform rightForearm,
        bool created)
    {
        SerializedObject serializedMovement = new SerializedObject(movement);
        serializedMovement.FindProperty("leftForearm").objectReferenceValue = leftForearm;
        serializedMovement.FindProperty("rightForearm").objectReferenceValue = rightForearm;
        SerializedProperty duration = serializedMovement.FindProperty("movementDuration");
        SerializedProperty angle = serializedMovement.FindProperty("movementAngle");
        if (created || duration.floatValue <= 0f)
            duration.floatValue = 2f;
        if (created || angle.floatValue <= 0f || angle.floatValue > 45f)
            angle.floatValue = 15f;
        serializedMovement.ApplyModifiedProperties();
        EditorUtility.SetDirty(movement);
    }

    private static void ConfigureDetector(
        WindowLookDetector detector,
        Transform cameraTransform,
        Transform lookTarget,
        bool created)
    {
        SerializedObject serializedDetector = new SerializedObject(detector);
        serializedDetector.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
        serializedDetector.FindProperty("lookTarget").objectReferenceValue = lookTarget;
        SerializedProperty angle = serializedDetector.FindProperty("maximumLookAngle");
        SerializedProperty duration = serializedDetector.FindProperty("requiredLookDuration");
        if (created || angle.floatValue < 0.1f || angle.floatValue > 180f)
            angle.floatValue = 25f;
        if (created || duration.floatValue <= 0f)
            duration.floatValue = 0.5f;
        serializedDetector.ApplyModifiedProperties();
        EditorUtility.SetDirty(detector);
    }

    private static void ConfigureSequence(
        CeilingArmsSequence sequence,
        CeilingArmsView view,
        SimpleArmMovement movement,
        WindowLookDetector detector,
        AudioSource impactsSource,
        AudioSource guidanceSource,
        AudioClip impactsClip,
        AudioClip guidanceClip,
        bool created)
    {
        SerializedObject serializedSequence = new SerializedObject(sequence);
        serializedSequence.FindProperty("armsView").objectReferenceValue = view;
        serializedSequence.FindProperty("armMovement").objectReferenceValue = movement;
        serializedSequence.FindProperty("lookDetector").objectReferenceValue = detector;
        serializedSequence.FindProperty("roofImpactsAudioSource").objectReferenceValue = impactsSource;
        serializedSequence.FindProperty("ceilingGuidanceAudioSource").objectReferenceValue = guidanceSource;
        serializedSequence.FindProperty("roofImpactsClip").objectReferenceValue = impactsClip;
        serializedSequence.FindProperty("ceilingGuidanceClip").objectReferenceValue = guidanceClip;

        SetFloatDefault(serializedSequence, "phaseStartDelay", 3f, created, 0f, false);
        SetFloatDefault(serializedSequence, "guidanceDelayAfterImpacts", 1f, created, 0f, false);
        SetFloatDefault(serializedSequence, "maximumLookWait", 10f, created, 0f, true);
        SetFloatDefault(serializedSequence, "armsVisibleDuration", 4f, created, 0f, false);
        SetFloatDefault(serializedSequence, "roofImpactsVolume", 0.8f, created, 0f, false, 1f);
        SetFloatDefault(serializedSequence, "ceilingGuidanceVolume", 1f, created, 0f, false, 1f);
        serializedSequence.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequence);
    }

    private static void SetFloatDefault(
        SerializedObject serializedObject,
        string propertyName,
        float defaultValue,
        bool componentCreated,
        float minimum,
        bool strictlyPositive,
        float maximum = float.PositiveInfinity)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        bool belowMinimum = strictlyPositive
            ? property.floatValue <= minimum
            : property.floatValue < minimum;
        if (componentCreated || belowMinimum || property.floatValue > maximum)
            property.floatValue = defaultValue;
    }

    private static void ConfigureImpactsAudio(AudioSource source, AudioClip clip, bool created)
    {
        Undo.RecordObject(source, "Configure Phase 4 roof impacts audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.clip = clip;
        if (created)
        {
            source.volume = 0.8f;
            source.minDistance = 0.8f;
            source.maxDistance = 10f;
        }
        else if (source.minDistance <= 0f || source.maxDistance <= source.minDistance)
        {
            source.minDistance = 0.8f;
            source.maxDistance = 10f;
        }
        EditorUtility.SetDirty(source);
    }

    private static void ConfigureGuidanceAudio(AudioSource source, AudioClip clip, bool created)
    {
        Undo.RecordObject(source, "Configure Phase 4 ceiling guidance audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        if (created)
            source.volume = 1f;
        EditorUtility.SetDirty(source);
    }

    private static void ConnectLookConfirmation(
        WindowLookDetector detector,
        CeilingArmsSequence sequence)
    {
        if (detector.OnLookConfirmed == null)
            throw new InvalidOperationException("Phase 4 look confirmation event is unavailable.");

        RemoveDuplicateListeners(
            detector.OnLookConfirmed,
            sequence,
            nameof(CeilingArmsSequence.HandleLookConfirmed),
            () => UnityEventTools.AddPersistentListener(
                detector.OnLookConfirmed,
                sequence.HandleLookConfirmed));
        EditorUtility.SetDirty(detector);
    }

    private static void ConnectPhaseThreeCompletion(
        WindowShadowSequence phaseThree,
        CeilingArmsSequence phaseFour)
    {
        if (phaseThree.OnNarrativeCompleted == null)
            throw new InvalidOperationException("Phase 3 narrative completion event is unavailable.");

        RemoveDuplicateListeners(
            phaseThree.OnNarrativeCompleted,
            phaseFour,
            nameof(CeilingArmsSequence.BeginSequence),
            () => UnityEventTools.AddPersistentListener(
                phaseThree.OnNarrativeCompleted,
                phaseFour.BeginSequence));
        EditorUtility.SetDirty(phaseThree);
    }

    private static void RemoveDuplicateListeners(
        UnityEngine.Events.UnityEvent unityEvent,
        UnityEngine.Object target,
        string methodName,
        Action addListener)
    {
        int retainedIndex = -1;
        for (int index = unityEvent.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            bool matches = unityEvent.GetPersistentTarget(index) == target &&
                           unityEvent.GetPersistentMethodName(index) == methodName;
            if (!matches)
                continue;

            if (retainedIndex < 0)
                retainedIndex = index;
            else
                UnityEventTools.RemovePersistentListener(unityEvent, index);
        }

        if (retainedIndex < 0)
            addListener();
    }

    private static Material GetOrCreateArmsMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ArmsMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit shader was not found.");

        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets/Generated", "Phase4");
        EnsureFolder("Assets/Generated/Phase4", "Materials");
        material = new Material(shader)
        {
            name = "MAT_Phase4_Arms"
        };
        Color mutedSkinTone = new Color(0.55f, 0.48f, 0.45f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", mutedSkinTone);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.renderQueue = -1;
        AssetDatabase.CreateAsset(material, ArmsMaterialPath);
        return material;
    }

    private static Camera FindRequiredMainCamera(Scene scene)
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(camera => camera.gameObject.scene == scene && camera.CompareTag("MainCamera"))
            .ToArray();
        if (cameras.Length != 1)
            throw new InvalidOperationException($"Expected one tagged Main Camera; found {cameras.Length}.");
        return cameras[0];
    }

    private static T FindRequiredSceneComponent<T>(Scene scene) where T : Component
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(component => component.gameObject.scene == scene)
            .ToArray();
        if (components.Length != 1)
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name}; found {components.Length}.");
        return components[0];
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

    private static Transform FindRequiredRoot(Scene scene, string name)
    {
        Transform[] roots = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        if (roots.Length != 1)
            throw new InvalidOperationException($"Expected one root named {name}; found {roots.Length}.");
        return roots[0];
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

    private static void SetLocalIdentity(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        EditorUtility.SetDirty(target);
    }

    private static void SetLocalTransform(Transform target, Vector3 position, Vector3 scale)
    {
        target.localPosition = position;
        target.localRotation = Quaternion.identity;
        target.localScale = scale;
        EditorUtility.SetDirty(target);
    }

    private static void EnsureFolder(string parentPath, string name)
    {
        string path = parentPath + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parentPath, name);
    }

    private static string GetPath(Transform target)
    {
        return target.parent == null ? target.name : GetPath(target.parent) + "/" + target.name;
    }

    private static void SaveSceneAndAssets(Scene scene)
    {
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException($"Could not save {ScenePath}.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private readonly struct ArmParts
    {
        public ArmParts(
            Transform upperArm,
            Transform forearm,
            Renderer upperRenderer,
            Renderer forearmRenderer)
        {
            UpperArm = upperArm;
            Forearm = forearm;
            UpperRenderer = upperRenderer;
            ForearmRenderer = forearmRenderer;
        }

        public Transform UpperArm { get; }

        public Transform Forearm { get; }

        public Renderer UpperRenderer { get; }

        public Renderer ForearmRenderer { get; }
    }
}
