using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public static class TesisCollisionSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MenuPath = "Tools/Tesis VR/Configure Player Collision";

    private static readonly string[] StructureObjects =
    {
        "Floor",
        "Ceiling",
        "Wall_North",
        "Wall_South",
        "Wall_East",
        "Wall_West"
    };

    private static readonly string[] FurnitureObjects =
    {
        "Bed",
        "Nightstand",
        "Wardrobe",
        "Desk"
    };

    private static readonly string[] ArchitectureObjects =
    {
        "Door",
        "Window_01",
        "Window_02"
    };

    [MenuItem(MenuPath)]
    private static void ConfigureFromMenu()
    {
        try
        {
            Configure();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Player Collision",
                "La configuración no pudo completarse. Revisa la Console para ver el error.",
                "Aceptar");
        }
    }

    public static void ConfigureFromCommandLine()
    {
        Configure();
    }

    private static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        XROrigin xrOrigin = FindSingleXROrigin(scene);
        GameObject xrRoot = xrOrigin.Origin.gameObject;
        HashSet<int> originalXrComponentIds = CaptureComponentIds(xrRoot);

        CharacterController characterController = GetOrAddComponent<CharacterController>(xrRoot);
        ConfigureCharacterController(characterController);

        DynamicMoveProvider moveProvider = FindCompatibleMoveProvider(scene, xrOrigin);

        LocomotionMediator mediator = moveProvider.mediator;
        if (mediator == null)
            throw new InvalidOperationException("El ContinuousMoveProvider no tiene LocomotionMediator asignado.");

        XRBodyTransformer bodyTransformer = mediator.GetComponent<XRBodyTransformer>();
        if (bodyTransformer == null)
            throw new InvalidOperationException("El LocomotionMediator no tiene XRBodyTransformer.");

        bodyTransformer.useCharacterControllerIfExists = true;
        EditorUtility.SetDirty(bodyTransformer);

        if (xrOrigin.Camera == null)
            throw new InvalidOperationException("El XR Origin existente no tiene una cámara configurada.");

        if (xrOrigin.CameraFloorOffsetObject == null)
            throw new InvalidOperationException("El XR Origin no tiene Camera Floor Offset Object configurado.");

        DesktopKeyboardLocomotion desktopLocomotion =
            GetOrAddComponent<DesktopKeyboardLocomotion>(xrRoot);
        desktopLocomotion.ConfigureReferences(
            characterController,
            xrOrigin.Camera.transform,
            true);
        desktopLocomotion.enabled = true;
        EditorUtility.SetDirty(desktopLocomotion);

        moveProvider.enabled = false;
        EditorUtility.SetDirty(moveProvider);

        Transform room = FindRequiredRoot(scene, "Room");
        int configuredColliders = 0;
        configuredColliders += ConfigureGroup(room, "Structure", StructureObjects, true);
        configuredColliders += ConfigureGroup(room, "Furniture", FurnitureObjects, false);
        configuredColliders += ConfigureGroup(room, "Architecture", ArchitectureObjects, false);

        EditorUtility.SetDirty(xrRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        Validate(
            scene,
            xrOrigin,
            xrRoot,
            room,
            originalXrComponentIds,
            moveProvider,
            mediator,
            bodyTransformer,
            characterController,
            desktopLocomotion);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException($"No se pudo guardar la escena en {ScenePath}.");

        Debug.Log(
            "TesisCollisionSetup: configuración completada y guardada. " +
            $"XR Origin='{xrOrigin.name}', MoveProvider='{moveProvider.name}', " +
            $"LocomotionMediator='{mediator.name}', XRBodyTransformer='{bodyTransformer.name}', " +
            $"UseCharacterController={bodyTransformer.useCharacterControllerIfExists}, CharacterController=OK, " +
            "DesktopKeyboardLocomotion=activo, " +
            "DynamicMoveProvider=desactivado temporalmente para desktop, " +
            $"colliders configurados={configuredColliders}, Rigidbody XR Origin=ausente.");
    }

    private static XROrigin FindSingleXROrigin(Scene scene)
    {
        XROrigin[] origins = UnityEngine.Object.FindObjectsByType<XROrigin>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(origin => origin.gameObject.scene == scene)
            .ToArray();

        if (origins.Length != 1)
            throw new InvalidOperationException(
                $"Se esperaba exactamente un XR Origin en {ScenePath}, pero se encontraron {origins.Length}.");

        if (origins[0].Origin == null)
            throw new InvalidOperationException("El XR Origin existente no tiene configurado su objeto Origin.");

        return origins[0];
    }

    private static DynamicMoveProvider FindCompatibleMoveProvider(Scene scene, XROrigin xrOrigin)
    {
        DynamicMoveProvider[] moveProviders = UnityEngine.Object.FindObjectsByType<DynamicMoveProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(provider => provider.gameObject.scene == scene)
            .ToArray();

        if (moveProviders.Length == 0)
            throw new InvalidOperationException(
                "No se encontró el ContinuousMoveProvider existente; no se modificó la locomoción.");

        DynamicMoveProvider providerForOrigin = moveProviders.FirstOrDefault(provider =>
        {
            XRBodyTransformer transformer = provider.mediator != null
                ? provider.mediator.GetComponent<XRBodyTransformer>()
                : null;
            return transformer != null && transformer.xrOrigin == xrOrigin;
        });

        if (providerForOrigin == null && moveProviders.Length == 1)
            providerForOrigin = moveProviders[0];

        if (providerForOrigin == null)
            throw new InvalidOperationException(
                "No se pudo identificar de forma inequívoca el ContinuousMoveProvider del XR Origin.");

        return providerForOrigin;
    }

    private static void ConfigureCharacterController(CharacterController controller)
    {
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.height = 1.8f;
        controller.radius = 0.25f;
        controller.skinWidth = 0.03f;
        controller.stepOffset = 0.20f;
        controller.slopeLimit = 45f;
        controller.minMoveDistance = 0f;
        controller.enabled = true;
        EditorUtility.SetDirty(controller);
    }

    private static int ConfigureGroup(
        Transform room,
        string groupName,
        IEnumerable<string> objectNames,
        bool requireBoxCollider)
    {
        Transform group = room.Find(groupName);
        if (group == null)
            throw new InvalidOperationException($"No se encontró Room/{groupName}.");

        int count = 0;
        foreach (string objectName in objectNames)
        {
            Transform target = group.Find(objectName);
            if (target == null)
                throw new InvalidOperationException($"No se encontró Room/{groupName}/{objectName}.");

            Collider[] existingColliders = target.GetComponents<Collider>();
            if (existingColliders.Length > 1)
                throw new InvalidOperationException(
                    $"Room/{groupName}/{objectName} ya tiene colliders duplicados; no se modificó.");

            Collider collider;
            if (existingColliders.Length == 0)
            {
                collider = target.gameObject.AddComponent<BoxCollider>();
            }
            else
            {
                collider = existingColliders[0];
                if (requireBoxCollider && collider is not BoxCollider)
                    throw new InvalidOperationException(
                        $"Room/{groupName}/{objectName} tiene {collider.GetType().Name}; las paredes requieren BoxCollider.");
            }

            collider.enabled = true;
            collider.isTrigger = false;
            target.gameObject.isStatic = true;
            EditorUtility.SetDirty(target.gameObject);
            EditorUtility.SetDirty(collider);
            count++;
        }

        return count;
    }

    private static void Validate(
        Scene scene,
        XROrigin xrOrigin,
        GameObject xrRoot,
        Transform room,
        HashSet<int> originalXrComponentIds,
        DynamicMoveProvider moveProvider,
        LocomotionMediator mediator,
        XRBodyTransformer bodyTransformer,
        CharacterController characterController,
        DesktopKeyboardLocomotion desktopLocomotion)
    {
        List<string> errors = new List<string>();

        if (xrRoot.GetComponent<CharacterController>() == null)
            errors.Add("falta CharacterController en el XR Origin");

        if (xrRoot.GetComponent<Rigidbody>() != null)
            errors.Add("el XR Origin tiene un Rigidbody");

        if (xrRoot.GetComponents<CharacterController>().Length != 1)
            errors.Add("el XR Origin no tiene exactamente un CharacterController");

        if (xrRoot.GetComponents<DesktopKeyboardLocomotion>().Length != 1)
            errors.Add("el XR Origin no tiene exactamente un DesktopKeyboardLocomotion");

        if (moveProvider.enabled)
            errors.Add("DynamicMoveProvider debe estar desactivado temporalmente para desktop");

        if (moveProvider.mediator != mediator)
            errors.Add("el ContinuousMoveProvider no referencia el LocomotionMediator validado");

        if (mediator.GetComponent<XRBodyTransformer>() != bodyTransformer)
            errors.Add("el LocomotionMediator no comparte GameObject con el XRBodyTransformer validado");

        if (bodyTransformer.xrOrigin != xrOrigin)
            errors.Add("el XRBodyTransformer no referencia el XR Origin actual");

        if (!bodyTransformer.useCharacterControllerIfExists)
            errors.Add("Use Character Controller está desactivado en XRBodyTransformer");

        if (characterController.gameObject != xrOrigin.Origin)
            errors.Add("el CharacterController no está en el objeto Origin");

        if (!desktopLocomotion.enabled)
            errors.Add("DesktopKeyboardLocomotion debe estar activo");

        if (!desktopLocomotion.HasExpectedReferences(
                characterController,
                xrOrigin.Camera.transform))
            errors.Add("DesktopKeyboardLocomotion no tiene las referencias esperadas");

        ValidateBoxColliders(room, "Structure",
            new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" }, errors);
        ValidateActiveColliders(room, "Structure", StructureObjects, errors);
        ValidateActiveColliders(room, "Furniture", FurnitureObjects, errors);
        ValidateActiveColliders(room, "Architecture", ArchitectureObjects, errors);

        HashSet<int> currentXrComponentIds = CaptureComponentIds(xrRoot);
        if (!originalXrComponentIds.IsSubsetOf(currentXrComponentIds))
            errors.Add("se eliminó o reemplazó un componente XR existente");

        if (xrOrigin.gameObject.scene != scene)
            errors.Add("el XR Origin validado no pertenece a SampleScene");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Falló la validación de colisiones: " + string.Join("; ", errors) + ".");

        Debug.Log(
            "TesisCollisionSetup validación: XR Origin único=OK; componentes XR existentes conservados=OK; " +
            $"DynamicMoveProvider='{moveProvider.name}' desactivado temporalmente; LocomotionMediator='{mediator.name}'=OK; " +
            $"XRBodyTransformer='{bodyTransformer.name}' UseCharacterController=ON; CharacterController=único; " +
            "DesktopKeyboardLocomotion=activo/configurado; " +
            "paredes BoxCollider=OK; " +
            "puerta y ventanas collider=OK; Rigidbody XR Origin=ausente.");
    }

    private static void ValidateBoxColliders(
        Transform room,
        string groupName,
        IEnumerable<string> objectNames,
        ICollection<string> errors)
    {
        foreach (string objectName in objectNames)
        {
            Transform target = room.Find($"{groupName}/{objectName}");
            BoxCollider collider = target != null ? target.GetComponent<BoxCollider>() : null;
            if (collider == null || !collider.enabled || collider.isTrigger)
                errors.Add($"{groupName}/{objectName} no tiene un BoxCollider sólido activo");
        }
    }

    private static void ValidateActiveColliders(
        Transform room,
        string groupName,
        IEnumerable<string> objectNames,
        ICollection<string> errors)
    {
        foreach (string objectName in objectNames)
        {
            Transform target = room.Find($"{groupName}/{objectName}");
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider == null || !collider.enabled || collider.isTrigger)
                errors.Add($"{groupName}/{objectName} no tiene un collider sólido activo");
        }
    }

    private static Transform FindRequiredRoot(Scene scene, string objectName)
    {
        Transform[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == objectName)
            .Select(root => root.transform)
            .ToArray();

        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Se esperaba exactamente un objeto raíz '{objectName}', pero se encontraron {matches.Length}.");

        return matches[0];
    }

    private static HashSet<int> CaptureComponentIds(GameObject gameObject)
    {
        return gameObject.GetComponents<Component>()
            .Where(component => component != null)
            .Select(component => component.GetInstanceID())
            .ToHashSet();
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
