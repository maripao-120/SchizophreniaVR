using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TesisPhase2Builder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GuidanceClipPath = "Assets/Audio/Phase2/Phase2_Guidance.mp3";
    private const string HallucinationClipPath =
        "Assets/Audio/Phase2/Phase2_FirstHallucination.mp3";
    private const string GeneratedRoot = "Assets/Generated/Phase2";
    private const string MaterialsPath = GeneratedRoot + "/Materials";
    private const string TextPath = GeneratedRoot + "/Text";
    private const string ResourcesPath = GeneratedRoot + "/Resources";
    private const string TmpSettingsPath = ResourcesPath + "/TMP Settings.asset";
    private const string FontAssetPath = TextPath + "/Phase2_SystemFont SDF.asset";

    [MenuItem("Tools/Tesis VR/Phase 2/Build or Configure")]
    private static void BuildFromMenu()
    {
        try
        {
            BuildOrConfigure();
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 2",
                "Phase 2 was built/configured and SampleScene was saved.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Tesis VR - Phase 2 Error", exception.Message, "OK");
        }
    }

    public static void BuildOrConfigureFromCommandLine()
    {
        BuildOrConfigure();
    }

    private static void BuildOrConfigure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException($"Could not open {ScenePath}.");

        Transform room = FindRequiredRoot(scene, "Room");
        Transform furniture = FindRequiredDirectChild(room, "Furniture");
        Transform nightstand = FindRequiredDirectChild(furniture, "Nightstand");
        Transform structure = FindRequiredDirectChild(room, "Structure");
        Transform northWall = FindRequiredDirectChild(structure, "Wall_North");
        XRInteractionManager interactionManager =
            FindRequiredSceneComponent<XRInteractionManager>(scene);

        Material bottleMaterial = GetOrCreateMaterial(
            "MAT_Phase2_MedicineBottle",
            new Color(0.55f, 0.32f, 0.12f, 1f));
        Material capMaterial = GetOrCreateMaterial(
            "MAT_Phase2_MedicineCap",
            new Color(0.85f, 0.85f, 0.80f, 1f));
        Material noteMaterial = GetOrCreateMaterial(
            "MAT_Phase2_Note",
            new Color(0.92f, 0.87f, 0.72f, 1f));
        TMP_FontAsset fontAsset = GetOrCreateFontAsset();

        Transform narrativeProps = GetOrCreateDirectChild(room, "NarrativeProps", out _);
        Transform bottle = BuildBottle(
            narrativeProps,
            nightstand,
            interactionManager,
            bottleMaterial,
            capMaterial,
            out FirstGrabTrigger firstGrabTrigger);
        NoteView noteView = BuildNote(
            narrativeProps,
            nightstand,
            northWall,
            noteMaterial,
            fontAsset);

        Transform experience = GetOrCreateRoot(scene, "Experience", out _);
        Transform phaseObject = GetOrCreateDirectChild(
            experience,
            "Phase2_FirstHallucination",
            out _);
        SetIdentityIfUnconfigured(phaseObject);

        FirstHallucinationSequence sequence =
            GetOrAddSingleComponent<FirstHallucinationSequence>(phaseObject.gameObject);
        AudioSource[] audioSources = EnsureTwoAudioSources(phaseObject.gameObject);
        AudioClip guidanceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GuidanceClipPath);
        AudioClip hallucinationClip =
            AssetDatabase.LoadAssetAtPath<AudioClip>(HallucinationClipPath);

        ConfigureAudioSource(audioSources[0], guidanceClip, 0.8f);
        ConfigureAudioSource(audioSources[1], hallucinationClip, 0.9f);

        Light roomLight = room.Find("Lighting/RoomLight")?.GetComponent<Light>();
        if (roomLight == null)
            Debug.LogWarning(
                "TesisPhase2Builder: Room/Lighting/RoomLight was not found; visual flow will still work.");

        ConfigureSequence(
            sequence,
            noteView,
            audioSources[0],
            audioSources[1],
            guidanceClip,
            hallucinationClip,
            roomLight);
        ConfigureFirstGrabEvent(firstGrabTrigger, sequence);

        EditorUtility.SetDirty(bottle.gameObject);
        EditorUtility.SetDirty(phaseObject.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException($"Unity could not save {ScenePath}.");

        if (guidanceClip == null)
            Debug.LogWarning($"TesisPhase2Builder: missing optional audio clip {GuidanceClipPath}.");
        if (hallucinationClip == null)
            Debug.LogWarning($"TesisPhase2Builder: missing optional audio clip {HallucinationClipPath}.");

        Debug.Log(
            "TesisPhase2Builder: Phase 2 configured. " +
            "MedicineBottle=1, Note=1, Phase2_FirstHallucination=1, AudioSources=2, " +
            "first-grab listener connected, RoomLight assigned when available, SampleScene saved.");
    }

    private static Transform BuildBottle(
        Transform parent,
        Transform nightstand,
        XRInteractionManager interactionManager,
        Material bottleMaterial,
        Material capMaterial,
        out FirstGrabTrigger firstGrabTrigger)
    {
        Transform bottle = GetOrCreateDirectChild(parent, "MedicineBottle", out bool created);
        if (created)
        {
            Bounds nightstandBounds = GetWorldBounds(nightstand);
            bottle.position = new Vector3(
                nightstandBounds.center.x,
                nightstandBounds.max.y + 0.15f,
                nightstandBounds.center.z);
            bottle.rotation = Quaternion.identity;
            bottle.localScale = Vector3.one;
        }

        bottle.gameObject.isStatic = false;

        Transform body = GetOrCreatePrimitiveChild(
            bottle,
            "BottleBody",
            PrimitiveType.Cylinder,
            out bool bodyCreated);
        if (bodyCreated)
        {
            body.localPosition = new Vector3(0f, -0.015f, 0f);
            body.localRotation = Quaternion.identity;
            body.localScale = new Vector3(0.10f, 0.12f, 0.10f);
        }
        SetMaterial(body, bottleMaterial, bodyCreated);
        DisableVisualColliders(body);

        Transform cap = GetOrCreatePrimitiveChild(
            bottle,
            "BottleCap",
            PrimitiveType.Cylinder,
            out bool capCreated);
        if (capCreated)
        {
            cap.localPosition = new Vector3(0f, 0.13f, 0f);
            cap.localRotation = Quaternion.identity;
            cap.localScale = new Vector3(0.11f, 0.025f, 0.11f);
        }
        SetMaterial(cap, capMaterial, capCreated);
        DisableVisualColliders(cap);

        CapsuleCollider collider = GetOrAddSingleComponent<CapsuleCollider>(bottle.gameObject);
        Undo.RecordObject(collider, "Configure Phase 2 bottle collider");
        collider.enabled = true;
        collider.isTrigger = false;
        collider.direction = 1;
        collider.center = new Vector3(0f, 0.005f, 0f);
        collider.radius = 0.06f;
        collider.height = 0.30f;

        Rigidbody rigidbody = GetOrAddSingleComponent<Rigidbody>(bottle.gameObject);
        Undo.RecordObject(rigidbody, "Configure Phase 2 bottle Rigidbody");
        rigidbody.mass = 0.15f;
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rigidbody.constraints = RigidbodyConstraints.None;

        XRGrabInteractable grabInteractable =
            GetOrAddSingleComponent<XRGrabInteractable>(bottle.gameObject);
        Undo.RecordObject(grabInteractable, "Configure Phase 2 bottle interaction");
        grabInteractable.interactionManager = interactionManager;
        grabInteractable.interactionLayers = InteractionLayerMask.GetMask("Default");
        grabInteractable.colliders.Clear();
        grabInteractable.colliders.Add(collider);

        firstGrabTrigger = GetOrAddSingleComponent<FirstGrabTrigger>(bottle.gameObject);
        EditorUtility.SetDirty(firstGrabTrigger);
        return bottle;
    }

    private static NoteView BuildNote(
        Transform parent,
        Transform nightstand,
        Transform northWall,
        Material noteMaterial,
        TMP_FontAsset fontAsset)
    {
        Transform note = GetOrCreateDirectChild(parent, "Note", out bool created);
        if (created)
        {
            Bounds nightstandBounds = GetWorldBounds(nightstand);
            Bounds wallBounds = GetWorldBounds(northWall);
            note.position = new Vector3(
                nightstandBounds.center.x,
                Mathf.Max(nightstandBounds.max.y + 0.55f, 1.15f),
                wallBounds.max.z + 0.012f);
            note.rotation = Quaternion.Euler(0f, 180f, 0f);
            note.localScale = Vector3.one;
        }

        SetStaticRecursively(note, true);

        Transform surface = GetOrCreatePrimitiveChild(
            note,
            "NoteSurface",
            PrimitiveType.Cube,
            out bool surfaceCreated);
        if (surfaceCreated)
        {
            surface.localPosition = Vector3.zero;
            surface.localRotation = Quaternion.identity;
            surface.localScale = new Vector3(0.58f, 0.36f, 0.012f);
        }
        SetMaterial(surface, noteMaterial, surfaceCreated);
        DisableVisualColliders(surface);

        Transform textTransform = GetOrCreateDirectChild(note, "NoteText", out bool textCreated);
        TextMeshPro textMesh = GetOrAddSingleComponent<TextMeshPro>(textTransform.gameObject);
        // Adding TextMeshPro replaces a regular Transform with RectTransform.
        textTransform = textMesh.rectTransform;
        if (textCreated)
        {
            textTransform.localPosition = new Vector3(0f, 0f, -0.008f);
            textTransform.localRotation = Quaternion.identity;
            textTransform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            textMesh.rectTransform.sizeDelta = new Vector2(10.5f, 6.2f);
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.textWrappingMode = TextWrappingModes.Normal;
            textMesh.enableAutoSizing = true;
            textMesh.fontSizeMin = 18f;
            textMesh.fontSizeMax = 42f;
            textMesh.overflowMode = TextOverflowModes.Ellipsis;
            textMesh.text = "NO OLVIDES TOMAR LA PASTILLA\nSI EMPIEZAS A SENTIRTE MAL.";
            textMesh.color = new Color(0.12f, 0.10f, 0.08f, 1f);
        }
        if (textMesh.font == null)
            textMesh.font = fontAsset;
        textMesh.raycastTarget = false;

        NoteView noteView = GetOrAddSingleComponent<NoteView>(note.gameObject);
        SerializedObject serializedView = new SerializedObject(noteView);
        serializedView.FindProperty("noteText").objectReferenceValue = textMesh;
        serializedView.FindProperty("backgroundRenderer").objectReferenceValue =
            surface.GetComponent<Renderer>();
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(noteView);
        SetStaticRecursively(note, true);
        return noteView;
    }

    private static void ConfigureSequence(
        FirstHallucinationSequence sequence,
        NoteView noteView,
        AudioSource guidanceSource,
        AudioSource hallucinationSource,
        AudioClip guidanceClip,
        AudioClip hallucinationClip,
        Light roomLight)
    {
        SerializedObject serializedSequence = new SerializedObject(sequence);
        serializedSequence.FindProperty("noteView").objectReferenceValue = noteView;
        serializedSequence.FindProperty("guidanceAudioSource").objectReferenceValue = guidanceSource;
        serializedSequence.FindProperty("hallucinationAudioSource").objectReferenceValue =
            hallucinationSource;
        serializedSequence.FindProperty("guidanceClip").objectReferenceValue = guidanceClip;
        serializedSequence.FindProperty("firstHallucinationClip").objectReferenceValue =
            hallucinationClip;

        SerializedProperty lightsProperty = serializedSequence.FindProperty("affectedLights");
        if (roomLight != null)
        {
            lightsProperty.arraySize = 1;
            lightsProperty.GetArrayElementAtIndex(0).objectReferenceValue = roomLight;
        }
        else
        {
            lightsProperty.arraySize = 0;
        }

        serializedSequence.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static AudioSource[] EnsureTwoAudioSources(GameObject target)
    {
        AudioSource[] existing = target.GetComponents<AudioSource>();
        if (existing.Length > 2)
            throw new InvalidOperationException(
                "Phase2_FirstHallucination has more than two AudioSource components; " +
                "remove extras manually to avoid deleting user configuration.");

        while (existing.Length < 2)
        {
            Undo.AddComponent<AudioSource>(target);
            existing = target.GetComponents<AudioSource>();
        }

        return existing;
    }

    private static void ConfigureAudioSource(AudioSource source, AudioClip clip, float defaultVolume)
    {
        Undo.RecordObject(source, "Configure Phase 2 audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        if (source.volume <= 0f || source.volume > 1f)
            source.volume = defaultVolume;
        EditorUtility.SetDirty(source);
    }

    private static void ConfigureFirstGrabEvent(
        FirstGrabTrigger trigger,
        FirstHallucinationSequence sequence)
    {
        UnityEvent onFirstGrab = trigger.OnFirstGrab;
        int matchingListeners = 0;
        for (int index = onFirstGrab.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            bool matches = onFirstGrab.GetPersistentTarget(index) == sequence &&
                           onFirstGrab.GetPersistentMethodName(index) ==
                           nameof(FirstHallucinationSequence.BeginHallucination);
            if (!matches)
                continue;

            matchingListeners++;
            if (matchingListeners > 1)
                UnityEventTools.RemovePersistentListener(onFirstGrab, index);
        }

        if (matchingListeners == 0)
            UnityEventTools.AddPersistentListener(
                onFirstGrab,
                sequence.BeginHallucination);

        EditorUtility.SetDirty(trigger);
    }

    private static Material GetOrCreateMaterial(string assetName, Color defaultColor)
    {
        EnsureFolder(MaterialsPath);
        string path = $"{MaterialsPath}/{assetName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException($"No compatible shader found for {assetName}.");

        material = new Material(shader) { name = assetName, color = defaultColor };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", defaultColor);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        EnsureFolder(TextPath);
        TMP_Settings settings = GetOrCreateTmpSettings();
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
            return existing;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset("Arial", "Regular") ??
                                  TMP_FontAsset.CreateFontAsset("Segoe UI", "Regular");
        if (fontAsset == null)
            throw new InvalidOperationException(
                "TextMeshPro could not create a DynamicOS font asset from Arial or Segoe UI.");

        fontAsset.name = "Phase2_SystemFont SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        Texture2D[] atlasTextures = fontAsset.atlasTextures;
        for (int index = 0; index < atlasTextures.Length; index++)
        {
            Texture2D atlas = atlasTextures[index];
            if (atlas == null || AssetDatabase.Contains(atlas))
                continue;

            atlas.name = $"{fontAsset.name} Atlas {index}";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        SerializedObject serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static TMP_Settings GetOrCreateTmpSettings()
    {
        EnsureFolder(ResourcesPath);
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TMP_Settings>();
            settings.name = "TMP Settings";
            AssetDatabase.CreateAsset(settings, TmpSettingsPath);
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("assetVersion").stringValue = "2";
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        if (TMP_Settings.LoadDefaultSettings() == null)
            throw new InvalidOperationException("TextMeshPro could not load the generated TMP Settings asset.");

        return settings;
    }

    private static Transform GetOrCreateRoot(Scene scene, string name, out bool created)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"Found duplicate root objects named '{name}'.");
        if (matches.Length == 1)
        {
            created = false;
            return matches[0];
        }

        GameObject gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        created = true;
        return gameObject.transform;
    }

    private static Transform GetOrCreateDirectChild(
        Transform parent,
        string name,
        out bool created)
    {
        Transform[] matches = parent.Cast<Transform>()
            .Where(child => child.name == name)
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException(
                $"Found duplicate objects at {GetPath(parent)}/{name}.");
        if (matches.Length == 1)
        {
            created = false;
            return matches[0];
        }

        GameObject gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        gameObject.transform.SetParent(parent, false);
        created = true;
        return gameObject.transform;
    }

    private static Transform GetOrCreatePrimitiveChild(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        out bool created)
    {
        Transform existing = parent.Cast<Transform>().FirstOrDefault(child => child.name == name);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        gameObject.transform.SetParent(parent, false);
        created = true;
        return gameObject.transform;
    }

    private static T GetOrAddSingleComponent<T>(GameObject target) where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException(
                $"{GetPath(target.transform)} has duplicate {typeof(T).Name} components.");
        return components.Length == 1 ? components[0] : Undo.AddComponent<T>(target);
    }

    private static Transform FindRequiredRoot(Scene scene, string name)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .Select(root => root.transform)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected exactly one root '{name}', found {matches.Length}.");
        return matches[0];
    }

    private static Transform FindRequiredDirectChild(Transform parent, string name)
    {
        Transform[] matches = parent.Cast<Transform>()
            .Where(child => child.name == name)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected exactly one {GetPath(parent)}/{name}, found {matches.Length}.");
        return matches[0];
    }

    private static T FindRequiredSceneComponent<T>(Scene scene) where T : Component
    {
        T[] matches = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected exactly one scene {typeof(T).Name}, found {matches.Length}.");
        return matches[0];
    }

    private static Bounds GetWorldBounds(Transform target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
            return collider.bounds;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            return renderer.bounds;

        throw new InvalidOperationException($"{GetPath(target)} has no Collider or Renderer bounds.");
    }

    private static void SetMaterial(Transform target, Material material, bool targetCreated)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null && (targetCreated || renderer.sharedMaterial == null))
            renderer.sharedMaterial = material;
    }

    private static void DisableVisualColliders(Transform target)
    {
        foreach (Collider collider in target.GetComponents<Collider>())
        {
            Undo.RecordObject(collider, "Disable visual-only collider");
            collider.enabled = false;
        }
    }

    private static void SetStaticRecursively(Transform root, bool value)
    {
        root.gameObject.isStatic = value;
        foreach (Transform child in root)
            SetStaticRecursively(child, value);
    }

    private static void SetIdentityIfUnconfigured(Transform target)
    {
        if (target.localScale == Vector3.zero)
            target.localScale = Vector3.one;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static string GetPath(Transform target)
    {
        return target.parent == null ? target.name : GetPath(target.parent) + "/" + target.name;
    }
}
