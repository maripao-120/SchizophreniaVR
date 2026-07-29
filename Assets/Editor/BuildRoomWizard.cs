using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildRoomWizard
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RoomName = "Room";

    [MenuItem("Tools/Build Room Base")]
    private static void BuildRoomBase()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("Build Room Base", "Open Assets/Scenes/SampleScene.unity first.", "OK");
            return;
        }

        if (GameObject.Find(RoomName) != null)
        {
            EditorUtility.DisplayDialog("Build Room Base", "A Room object already exists. No changes were made.", "OK");
            return;
        }

        Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        GameObject room = new GameObject(RoomName);
        Undo.RegisterCreatedObjectUndo(room, "Create Room");

        CreateBox(room.transform, "Piso", new Vector3(0f, -0.05f, -16.22f), new Vector3(3.94f, 0.1f, 4f), defaultMaterial, true);
        CreateBox(room.transform, "Pared_Norte", new Vector3(0f, 1.4f, -18.17f), new Vector3(3.94f, 2.8f, 0.1f), defaultMaterial, true);
        CreateBox(room.transform, "Pared_Sur", new Vector3(0f, 1.4f, -14.27f), new Vector3(3.94f, 2.8f, 0.1f), defaultMaterial, true);
        CreateBox(room.transform, "Pared_Este", new Vector3(2.02f, 1.4f, -16.22f), new Vector3(0.1f, 2.8f, 4f), defaultMaterial, true);
        CreateBox(room.transform, "Pared_Oeste", new Vector3(-2.02f, 1.4f, -16.22f), new Vector3(0.1f, 2.8f, 4f), defaultMaterial, true);
        CreateBox(room.transform, "Techo", new Vector3(0f, 2.85f, -16.22f), new Vector3(3.94f, 0.1f, 4f), defaultMaterial, true);

        CreateBox(room.transform, "Cama", new Vector3(0f, 0.25f, -17.72f), new Vector3(1.6f, 0.5f, 2f), defaultMaterial, false);
        CreateBox(room.transform, "Velador", new Vector3(1.1f, 0.3f, -17.2f), new Vector3(0.4f, 0.6f, 0.4f), defaultMaterial, false);
        CreateBox(room.transform, "Ropero", new Vector3(-1.72f, 0.9f, -15.5f), new Vector3(0.5f, 1.8f, 0.8f), defaultMaterial, false);
        CreateBox(room.transform, "Escritorio", new Vector3(-0.8f, 0.4f, -14.72f), new Vector3(1f, 0.8f, 0.6f), defaultMaterial, false);
        CreateBox(room.transform, "Televisor", new Vector3(1f, 1.2f, -14.22f), new Vector3(0.8f, 0.6f, 0.05f), defaultMaterial, false);
        CreateBox(room.transform, "Espejo", new Vector3(-1.97f, 1.2f, -17f), new Vector3(0.05f, 0.8f, 0.5f), defaultMaterial, false);
        CreateBox(room.transform, "Ventana1", new Vector3(1.97f, 1.2f, -16.22f), new Vector3(0.05f, 1f, 1.2f), defaultMaterial, false);
        CreateBox(room.transform, "Ventana2", new Vector3(1.3f, 1.2f, -18.22f), new Vector3(0.8f, 1f, 0.05f), defaultMaterial, false);
        CreateBox(room.transform, "Puerta", new Vector3(-1.3f, 1f, -18.17f), new Vector3(0.7f, 2f, 0.05f), defaultMaterial, false);
        CreateBox(room.transform, "Perchero", new Vector3(1.7f, 0.75f, -14.5f), new Vector3(0.15f, 1.5f, 0.15f), defaultMaterial, false);

        GameObject grabCube = GameObject.Find("GrabCube");
        if (grabCube != null)
        {
            Undo.SetTransformParent(grabCube.transform, room.transform, "Parent GrabCube");
            grabCube.transform.localPosition = new Vector3(0.5f, 0.25f, -14.5f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = room;
    }

    private static void CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        Undo.RegisterCreatedObjectUndo(box, "Create Room Placeholder");
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;

        MeshRenderer renderer = box.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        if (!keepCollider)
        {
            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        }
    }
}
