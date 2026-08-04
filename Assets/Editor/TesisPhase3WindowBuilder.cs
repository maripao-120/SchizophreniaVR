using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TesisPhase3WindowBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FrameMaterialPath =
        "Assets/Generated/Phase3/Materials/MAT_Phase3_WindowFrame.mat";

    private const float WindowWidth = 2.5f;
    private const float WindowHeight = 1.5f;
    private const float FrameWidth = 0.12f;
    private const float FrameDepth = 0.14f;
    private const float GlassDepth = 0.01f;
    private const float WallWidth = 4f;
    private const float WallHeight = 2.8f;
    private const float WallDepth = 0.1f;

    private static readonly Vector3 DefaultWallPosition = new Vector3(2.02f, 1.4f, -16.22f);
    private static readonly Vector3 DefaultWindowPosition = new Vector3(2.02f, 1.65f, -16.20f);

    [MenuItem("Tools/Tesis VR/Phase 3/Build or Configure Window")]
    private static void BuildFromMenu()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform room = FindRequiredRoot(scene, "Room");
            Transform structure = FindRequiredDirectChild(room, "Structure");
            Transform architecture = FindRequiredDirectChild(room, "Architecture");
            Material wallMaterial = FindWallMaterial(structure.Find("Wall_East"));

            BuildOrConfigureForRoom(scene, structure, architecture, wallMaterial);
            SaveSceneAndAssets(scene);

            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3A",
                "Window_01 was built or configured. Run Validate Window next.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3A Error",
                "Window configuration failed. See Console for details.",
                "OK");
        }
    }

    public static void BuildOrConfigureForRoom(
        Scene scene,
        Transform structure,
        Transform architecture,
        Material wallMaterial)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException($"The loaded scene must be {ScenePath}.");
        if (structure == null || architecture == null)
            throw new ArgumentNullException(nameof(structure), "Structure and Architecture are required.");

        Material frameMaterial = GetOrCreateFrameMaterial();
        Transform wall = GetOrCreateDirectChild(structure, "Wall_East", out bool wallCreated);
        Transform window = GetOrCreateDirectChild(architecture, "Window_01", out bool windowCreated);

        ConfigureWindow(window, frameMaterial, windowCreated);
        ConfigureSegmentedWall(wall, wallMaterial, wallCreated, window.position);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            "TesisPhase3WindowBuilder: Window_01 root preserved; framed opening, disabled Glass, " +
            "ViewOpening, four frame colliders, and segmented Wall_East configured.");
    }

    private static void ConfigureWindow(Transform window, Material frameMaterial, bool rootCreated)
    {
        bool legacyRoot = window.GetComponent<MeshFilter>() != null ||
                          window.GetComponent<Renderer>() != null ||
                          window.GetComponent<Collider>() != null;

        if (rootCreated || legacyRoot)
        {
            Undo.RecordObject(window, "Migrate Window_01 root");
            window.SetPositionAndRotation(DefaultWindowPosition, Quaternion.identity);
            window.localScale = Vector3.one;
        }

        RemoveComponents<MeshRenderer>(window.gameObject, "Remove Window_01 root renderer");
        RemoveComponents<MeshFilter>(window.gameObject, "Remove Window_01 root mesh");
        RemoveComponents<Collider>(window.gameObject, "Remove Window_01 root collider");
        RemoveComponents<Rigidbody>(window.gameObject, "Remove Window_01 root Rigidbody");
        window.gameObject.isStatic = true;

        Transform frame = GetOrCreateDirectChild(window, "Frame", out bool frameCreated);
        if (frameCreated)
            SetLocalTransform(frame, Vector3.zero, Vector3.one);
        RemoveComponents<MeshRenderer>(frame.gameObject, "Keep Frame as a container");
        RemoveComponents<MeshFilter>(frame.gameObject, "Keep Frame as a container");
        RemoveComponents<Collider>(frame.gameObject, "Keep Frame opening clear");
        RemoveComponents<Rigidbody>(frame.gameObject, "Remove Frame Rigidbody");

        float innerWidth = WindowWidth - (2f * FrameWidth);
        float innerHeight = WindowHeight - (2f * FrameWidth);

        ConfigureFramePiece(
            frame,
            "Frame_Top",
            new Vector3(0f, (WindowHeight - FrameWidth) * 0.5f, 0f),
            new Vector3(FrameDepth, FrameWidth, WindowWidth),
            frameMaterial);
        ConfigureFramePiece(
            frame,
            "Frame_Bottom",
            new Vector3(0f, -(WindowHeight - FrameWidth) * 0.5f, 0f),
            new Vector3(FrameDepth, FrameWidth, WindowWidth),
            frameMaterial);
        ConfigureFramePiece(
            frame,
            "Frame_Left",
            new Vector3(0f, 0f, -(WindowWidth - FrameWidth) * 0.5f),
            new Vector3(FrameDepth, innerHeight, FrameWidth),
            frameMaterial);
        ConfigureFramePiece(
            frame,
            "Frame_Right",
            new Vector3(0f, 0f, (WindowWidth - FrameWidth) * 0.5f),
            new Vector3(FrameDepth, innerHeight, FrameWidth),
            frameMaterial);

        Transform glass = GetOrCreateCube(window, "Glass", out bool glassCreated);
        if (glassCreated)
            SetLocalTransform(glass, Vector3.zero, new Vector3(GlassDepth, innerHeight, innerWidth));
        glass.gameObject.SetActive(true);
        MeshRenderer glassRenderer = GetSingleRequiredComponent<MeshRenderer>(glass.gameObject);
        GetSingleRequiredComponent<MeshFilter>(glass.gameObject);
        Undo.RecordObject(glassRenderer, "Disable provisional Glass renderer");
        glassRenderer.enabled = false;
        EditorUtility.SetDirty(glassRenderer);
        RemoveComponents<Collider>(glass.gameObject, "Remove Glass collider");
        RemoveComponents<Rigidbody>(glass.gameObject, "Remove Glass Rigidbody");

        Transform opening = GetOrCreateDirectChild(window, "ViewOpening", out bool openingCreated);
        if (openingCreated)
            SetLocalTransform(opening, Vector3.zero, new Vector3(GlassDepth, innerHeight, innerWidth));
        RemoveComponents<MeshRenderer>(opening.gameObject, "Remove ViewOpening renderer");
        RemoveComponents<MeshFilter>(opening.gameObject, "Remove ViewOpening mesh");
        RemoveComponents<Collider>(opening.gameObject, "Remove ViewOpening collider");
        RemoveComponents<Rigidbody>(opening.gameObject, "Remove ViewOpening Rigidbody");
    }

    private static void ConfigureFramePiece(
        Transform frame,
        string name,
        Vector3 defaultPosition,
        Vector3 defaultScale,
        Material frameMaterial)
    {
        Transform piece = GetOrCreateCube(frame, name, out bool created);
        if (created)
            SetLocalTransform(piece, defaultPosition, defaultScale);

        MeshRenderer renderer = GetSingleRequiredComponent<MeshRenderer>(piece.gameObject);
        GetSingleRequiredComponent<MeshFilter>(piece.gameObject);
        if (created || renderer.sharedMaterial == null)
        {
            Undo.RecordObject(renderer, "Assign window frame material");
            renderer.sharedMaterial = frameMaterial;
        }
        renderer.enabled = true;

        BoxCollider collider = GetOrAddSingleBoxCollider(piece.gameObject);
        collider.enabled = true;
        collider.isTrigger = false;
        RemoveComponents<Rigidbody>(piece.gameObject, "Remove frame Rigidbody");
        piece.gameObject.isStatic = true;
        EditorUtility.SetDirty(piece.gameObject);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureSegmentedWall(
        Transform wall,
        Material material,
        bool rootCreated,
        Vector3 windowPosition)
    {
        Material resolvedMaterial = material != null ? material : FindWallMaterial(wall);
        bool legacyRoot = wall.GetComponent<MeshFilter>() != null ||
                          wall.GetComponent<Renderer>() != null ||
                          wall.GetComponent<Collider>() != null;

        if (rootCreated || legacyRoot)
        {
            Undo.RecordObject(wall, "Migrate Wall_East root");
            wall.SetPositionAndRotation(DefaultWallPosition, Quaternion.identity);
            wall.localScale = Vector3.one;
        }

        RemoveComponents<MeshRenderer>(wall.gameObject, "Remove Wall_East root renderer");
        RemoveComponents<MeshFilter>(wall.gameObject, "Remove Wall_East root mesh");
        RemoveComponents<Collider>(wall.gameObject, "Remove Wall_East root collider");
        RemoveComponents<Rigidbody>(wall.gameObject, "Remove Wall_East root Rigidbody");
        wall.gameObject.isStatic = true;

        float openingWidth = WindowWidth - (2f * FrameWidth);
        float openingHeight = WindowHeight - (2f * FrameWidth);
        Vector3 openingCenter = wall.InverseTransformPoint(windowPosition);
        float sideWidth = (WallWidth - openingWidth) * 0.5f;
        float sideOffset = (openingWidth + sideWidth) * 0.5f;
        float wallBottom = -WallHeight * 0.5f;
        float wallTop = WallHeight * 0.5f;
        float openingBottom = openingCenter.y - (openingHeight * 0.5f);
        float openingTop = openingCenter.y + (openingHeight * 0.5f);
        float bottomHeight = openingBottom - wallBottom;
        float topHeight = wallTop - openingTop;

        ConfigureWallPiece(
            wall,
            "WallEast_Window_Left",
            new Vector3(0f, 0f, openingCenter.z - sideOffset),
            new Vector3(WallDepth, WallHeight, sideWidth),
            resolvedMaterial);
        ConfigureWallPiece(
            wall,
            "WallEast_Window_Right",
            new Vector3(0f, 0f, openingCenter.z + sideOffset),
            new Vector3(WallDepth, WallHeight, sideWidth),
            resolvedMaterial);
        ConfigureWallPiece(
            wall,
            "WallEast_Window_Bottom",
            new Vector3(0f, wallBottom + (bottomHeight * 0.5f), openingCenter.z),
            new Vector3(WallDepth, bottomHeight, openingWidth),
            resolvedMaterial);
        ConfigureWallPiece(
            wall,
            "WallEast_Window_Top",
            new Vector3(0f, openingTop + (topHeight * 0.5f), openingCenter.z),
            new Vector3(WallDepth, topHeight, openingWidth),
            resolvedMaterial);
    }

    private static void ConfigureWallPiece(
        Transform wall,
        string name,
        Vector3 defaultPosition,
        Vector3 defaultScale,
        Material material)
    {
        Transform piece = GetOrCreateCube(wall, name, out bool created);
        if (created)
            SetLocalTransform(piece, defaultPosition, defaultScale);

        MeshRenderer renderer = GetSingleRequiredComponent<MeshRenderer>(piece.gameObject);
        GetSingleRequiredComponent<MeshFilter>(piece.gameObject);
        if ((created || renderer.sharedMaterial == null) && material != null)
            renderer.sharedMaterial = material;
        renderer.enabled = true;

        BoxCollider collider = GetOrAddSingleBoxCollider(piece.gameObject);
        collider.enabled = true;
        collider.isTrigger = false;
        RemoveComponents<Rigidbody>(piece.gameObject, "Remove wall segment Rigidbody");
        piece.gameObject.isStatic = true;
        EditorUtility.SetDirty(piece.gameObject);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(collider);
    }

    private static Material GetOrCreateFrameMaterial()
    {
        EnsureFolder("Assets/Generated", "Phase3");
        EnsureFolder("Assets/Generated/Phase3", "Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FrameMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP/Lit shader was not found for the window frame.");

        material = new Material(shader) { name = "MAT_Phase3_WindowFrame" };
        Color neutralColor = new Color(0.24f, 0.22f, 0.20f, 1f);
        material.color = neutralColor;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", neutralColor);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        AssetDatabase.CreateAsset(material, FrameMaterialPath);
        return material;
    }

    private static Material FindWallMaterial(Transform wall)
    {
        if (wall == null)
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TesisRoom/Mat_Wall.mat");

        Renderer renderer = wall.GetComponent<Renderer>() ?? wall.GetComponentInChildren<Renderer>(true);
        return renderer != null && renderer.sharedMaterial != null
            ? renderer.sharedMaterial
            : AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TesisRoom/Mat_Wall.mat");
    }

    private static Transform GetOrCreateCube(Transform parent, string name, out bool created)
    {
        Transform existing = FindSingleDirectChild(parent, name);
        if (existing != null)
        {
            created = false;
            if (existing.GetComponent<MeshFilter>() == null)
                Undo.AddComponent<MeshFilter>(existing.gameObject).sharedMesh = GetUnityCubeMesh();
            if (existing.GetComponent<MeshRenderer>() == null)
                Undo.AddComponent<MeshRenderer>(existing.gameObject);
            GetOrAddSingleBoxCollider(existing.gameObject);
            return existing;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        Undo.RegisterCreatedObjectUndo(cube, $"Create {name}");
        Undo.SetTransformParent(cube.transform, parent, $"Parent {name}");
        created = true;
        return cube.transform;
    }

    private static Mesh GetUnityCubeMesh()
    {
        GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        return mesh;
    }

    private static BoxCollider GetOrAddSingleBoxCollider(GameObject target)
    {
        Collider[] colliders = target.GetComponents<Collider>();
        if (colliders.Length > 1)
            throw new InvalidOperationException($"{GetPath(target.transform)} has duplicate colliders.");
        if (colliders.Length == 1 && colliders[0] is not BoxCollider)
            throw new InvalidOperationException($"{GetPath(target.transform)} must use a BoxCollider.");
        return colliders.Length == 1
            ? (BoxCollider)colliders[0]
            : Undo.AddComponent<BoxCollider>(target);
    }

    private static T GetSingleRequiredComponent<T>(GameObject target) where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length != 1)
            throw new InvalidOperationException(
                $"{GetPath(target.transform)} must have exactly one {typeof(T).Name}; found {components.Length}.");
        return components[0];
    }

    private static void RemoveComponents<T>(GameObject target, string undoName) where T : Component
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

    private static Transform FindRequiredDirectChild(Transform parent, string name)
    {
        Transform child = FindSingleDirectChild(parent, name);
        if (child == null)
            throw new InvalidOperationException($"Missing {GetPath(parent)}/{name}.");
        return child;
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
