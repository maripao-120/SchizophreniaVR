using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TesisRoomBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialsPath = "Assets/Materials/TesisRoom";
    private const string RoomName = "Room";
    private const float RoomCenterZ = -16.22f;

    [MenuItem("Tools/Tesis VR/Build Placeholder Room")]
    private static void BuildRoomFromMenu()
    {
        try
        {
            BuildRoom();
            EditorUtility.DisplayDialog("Tesis VR", "SampleScene: habitación placeholder construida correctamente.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Tesis VR - Error", exception.Message, "OK");
        }
    }

    public static void BuildRoomFromCommandLine()
    {
        BuildRoom();
    }

    private static void BuildRoom()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException("No se pudo abrir correctamente Assets/Scenes/SampleScene.unity.");

        MaterialSet materials = CreateMaterials();
        Transform room = GetOrCreateRoot(scene, RoomName);
        SetIdentity(room);

        Transform structure = GetOrCreateChild(room, "Structure");
        Transform furniture = GetOrCreateChild(room, "Furniture");
        Transform architecture = GetOrCreateChild(room, "Architecture");
        Transform props = GetOrCreateChild(room, "Props");
        SetIdentity(structure);
        SetIdentity(furniture);
        SetIdentity(architecture);
        SetIdentity(props);

        CreatePlaceholder(structure, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, RoomCenterZ), new Vector3(3.94f, 0.1f, 4f), materials.Floor, true);
        CreatePlaceholder(structure, "Ceiling", PrimitiveType.Cube, new Vector3(0f, 2.85f, RoomCenterZ), new Vector3(3.94f, 0.1f, 4f), materials.Wall, true);
        CreatePlaceholder(structure, "Wall_North", PrimitiveType.Cube, new Vector3(0f, 1.4f, -18.17f), new Vector3(3.94f, 2.8f, 0.1f), materials.Wall, true);
        CreatePlaceholder(structure, "Wall_South", PrimitiveType.Cube, new Vector3(0f, 1.4f, -14.27f), new Vector3(3.94f, 2.8f, 0.1f), materials.Wall, true);
        CreatePlaceholder(structure, "Wall_East", PrimitiveType.Cube, new Vector3(2.02f, 1.4f, RoomCenterZ), new Vector3(0.1f, 2.8f, 4f), materials.Wall, true);
        CreatePlaceholder(structure, "Wall_West", PrimitiveType.Cube, new Vector3(-2.02f, 1.4f, RoomCenterZ), new Vector3(0.1f, 2.8f, 4f), materials.Wall, true);

        CreatePlaceholder(furniture, "Bed", PrimitiveType.Cube, new Vector3(0f, 0.25f, -17.20f), new Vector3(1.4f, 0.5f, 1.7f), materials.Bed, true);
        CreatePlaceholder(furniture, "Nightstand", PrimitiveType.Cube, new Vector3(1.1f, 0.3f, -17.20f), new Vector3(0.4f, 0.6f, 0.4f), materials.Wood, true);
        CreatePlaceholder(furniture, "Wardrobe", PrimitiveType.Cube, new Vector3(-1.65f, 0.9f, -15.45f), new Vector3(0.5f, 1.8f, 0.8f), materials.Wood, true);
        CreatePlaceholder(furniture, "Desk", PrimitiveType.Cube, new Vector3(-0.75f, 0.4f, -14.70f), new Vector3(1f, 0.8f, 0.55f), materials.Wood, true);
        CreatePlaceholder(furniture, "Television", PrimitiveType.Cube, new Vector3(0.95f, 1.2f, -14.36f), new Vector3(0.8f, 0.55f, 0.08f), materials.Dark, false);
        CreatePlaceholder(furniture, "Mirror", PrimitiveType.Cube, new Vector3(-1.91f, 1.3f, -17f), new Vector3(0.08f, 1f, 0.55f), materials.Glass, false);
        CreatePlaceholder(furniture, "CoatRack", PrimitiveType.Cylinder, new Vector3(1.65f, 0.75f, -14.65f), new Vector3(0.12f, 0.75f, 0.12f), materials.Wood, true);

        CreatePlaceholder(architecture, "Door", PrimitiveType.Cube, new Vector3(-1.3f, 1f, -18.10f), new Vector3(0.7f, 2f, 0.08f), materials.Wood, false);
        CreatePlaceholder(architecture, "Window_01", PrimitiveType.Cube, new Vector3(1.91f, 1.35f, -16.20f), new Vector3(0.08f, 1f, 1.1f), materials.Glass, false);
        CreatePlaceholder(architecture, "Window_02", PrimitiveType.Cube, new Vector3(1.2f, 1.35f, -18.10f), new Vector3(0.75f, 1f, 0.08f), materials.Glass, false);

        MoveGrabCube(scene, props);
        CreateRoomLight(room);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity no pudo guardar SampleScene.unity.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TesisRoomBuilder: SampleScene construida. Room: Structure (6), Furniture (7), Architecture (3), Props y Lighting/RoomLight. GrabCube reubicado si existía.");
    }

    private static Transform GetOrCreateRoot(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
            if (rootObject.name == objectName)
                return rootObject.transform;

        GameObject created = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static GameObject CreatePlaceholder(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 worldPosition, Vector3 scale, Material material, bool colliderEnabled)
    {
        Transform existing = parent.Find(objectName);
        GameObject placeholder = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitiveType);
        placeholder.name = objectName;
        placeholder.transform.SetParent(parent, false);
        placeholder.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        placeholder.transform.localScale = scale;

        MeshRenderer renderer = placeholder.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = placeholder.GetComponent<Collider>();
        if (colliderEnabled && collider == null)
            placeholder.AddComponent<BoxCollider>();
        else if (collider != null)
            collider.enabled = colliderEnabled;

        return placeholder;
    }

    private static void MoveGrabCube(Scene scene, Transform props)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name != "GrabCube")
                continue;

            Transform grabCube = rootObject.transform;
            grabCube.SetParent(props, true);
            grabCube.position = new Vector3(0.55f, 0.30f, -15.00f);
            return;
        }
    }

    private static void CreateRoomLight(Transform room)
    {
        Transform lighting = GetOrCreateChild(room, "Lighting");
        SetIdentity(lighting);
        Transform lightTransform = GetOrCreateChild(lighting, "RoomLight");
        lightTransform.position = new Vector3(0f, 2.4f, RoomCenterZ);
        lightTransform.rotation = Quaternion.identity;
        lightTransform.localScale = Vector3.one;
        Light light = lightTransform.GetComponent<Light>();
        if (light == null)
            light = lightTransform.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 2f;
        light.color = new Color(1f, 0.92f, 0.82f);
    }

    private static void SetIdentity(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static MaterialSet CreateMaterials()
    {
        EnsureFolder("Assets/Materials", "TesisRoom");
        return new MaterialSet(
            GetOrCreateMaterial("Mat_Floor", new Color(0.38f, 0.40f, 0.42f)),
            GetOrCreateMaterial("Mat_Wall", new Color(0.88f, 0.86f, 0.78f)),
            GetOrCreateMaterial("Mat_Wood", new Color(0.42f, 0.23f, 0.12f)),
            GetOrCreateMaterial("Mat_Dark", new Color(0.035f, 0.04f, 0.05f)),
            GetOrCreateMaterial("Mat_GlassPlaceholder", new Color(0.45f, 0.75f, 0.85f)),
            GetOrCreateMaterial("Mat_Bed", new Color(0.28f, 0.38f, 0.48f)));
    }

    private static Material GetOrCreateMaterial(string materialName, Color color)
    {
        string path = MaterialsPath + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No se encontró un shader compatible para " + materialName + ".");
            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(parentPath + "/" + folderName))
            AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private readonly struct MaterialSet
    {
        public readonly Material Floor;
        public readonly Material Wall;
        public readonly Material Wood;
        public readonly Material Dark;
        public readonly Material Glass;
        public readonly Material Bed;

        public MaterialSet(Material floor, Material wall, Material wood, Material dark, Material glass, Material bed)
        {
            Floor = floor;
            Wall = wall;
            Wood = wood;
            Dark = dark;
            Glass = glass;
            Bed = bed;
        }
    }
}
