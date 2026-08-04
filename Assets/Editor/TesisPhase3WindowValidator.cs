using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TesisPhase3WindowValidator
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private static readonly string[] FramePieceNames =
    {
        "Frame_Top",
        "Frame_Bottom",
        "Frame_Left",
        "Frame_Right"
    };

    private static readonly string[] WallPieceNames =
    {
        "WallEast_Window_Left",
        "WallEast_Window_Right",
        "WallEast_Window_Bottom",
        "WallEast_Window_Top"
    };

    [MenuItem("Tools/Tesis VR/Phase 3/Validate Window")]
    private static void ValidateFromMenu()
    {
        try
        {
            ValidationReport report = Validate();
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3A Validation",
                report.ErrorCount == 0
                    ? $"Validation completed with {report.WarningCount} warning(s)."
                    : $"Validation found {report.ErrorCount} error(s). See Console.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Tesis VR - Phase 3A Validation Error",
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
        Transform architecture = FindUniqueDirectChild(room, "Architecture", report);
        Transform furniture = FindUniqueDirectChild(room, "Furniture", report);
        Transform wall = FindUniqueDirectChild(structure, "Wall_East", report);
        Transform window = FindUniqueDirectChild(architecture, "Window_01", report);
        Transform frame = FindUniqueDirectChild(window, "Frame", report);
        Transform glass = FindUniqueDirectChild(window, "Glass", report);
        Transform opening = FindUniqueDirectChild(window, "ViewOpening", report);

        ValidateWindowRoot(window, architecture, report);
        ValidateFrame(frame, report, out Bounds frameBounds, out bool hasFrameBounds);
        ValidateGlass(glass, report);
        ValidateOpening(opening, report);
        ValidateWall(wall, report, out Bounds wallBounds, out bool hasWallBounds);
        ValidateDimensions(frameBounds, hasFrameBounds, wallBounds, hasWallBounds, report);
        ValidateUnblockedOpening(window, wall, opening, report);
        ValidateNoteVisibility(scene, frameBounds, hasFrameBounds, report);
        ValidatePhaseOne(scene, report);
        ValidatePhaseTwo(scene, furniture, report);
        ValidateNoGrabCube(scene, report);
        ValidateNoMissingScripts(scene, report);

        return Finish(report);
    }

    private static void ValidateWindowRoot(
        Transform window,
        Transform architecture,
        ValidationReport report)
    {
        if (window == null)
            return;

        report.Check(
            window.parent == architecture,
            "Window_01 is under Room/Architecture",
            "Window_01 must remain a direct child of Room/Architecture.");
        report.Check(
            window.GetComponents<Renderer>().Length == 0 &&
            window.GetComponents<MeshFilter>().Length == 0,
            "Window_01 is a non-rendering container",
            "Window_01 root must not retain the legacy cube mesh or renderer.");
        report.Check(
            window.GetComponents<Collider>().Length == 0,
            "Window_01 root has no solid collider",
            "Window_01 root still has a collider that can close the opening.");
        report.Check(
            window.GetComponentsInChildren<Rigidbody>(true).Length == 0,
            "Window_01 hierarchy has no Rigidbody",
            "Window_01 or one of its children has a Rigidbody.");
        report.Check(
            window.localScale == Vector3.one,
            "Window_01 root scale is normalized",
            "Window_01 root should use scale (1, 1, 1); size belongs to its children.");
    }

    private static void ValidateFrame(
        Transform frame,
        ValidationReport report,
        out Bounds bounds,
        out bool hasBounds)
    {
        bounds = default;
        hasBounds = false;
        if (frame == null)
            return;

        Transform[] directChildren = frame.Cast<Transform>().ToArray();
        report.Check(
            frame.GetComponents<Renderer>().Length == 0 &&
            frame.GetComponents<MeshFilter>().Length == 0 &&
            frame.GetComponents<Collider>().Length == 0,
            "Frame is a non-blocking container",
            "Frame container must not render or collide; only its four pieces may do so.");
        report.Check(
            directChildren.Length == FramePieceNames.Length,
            "Frame has exactly four pieces",
            $"Frame must have exactly four direct children; found {directChildren.Length}.");

        foreach (string pieceName in FramePieceNames)
        {
            Transform piece = FindUniqueDirectChild(frame, pieceName, report);
            if (piece == null)
                continue;

            MeshRenderer[] renderers = piece.GetComponents<MeshRenderer>();
            BoxCollider[] boxColliders = piece.GetComponents<BoxCollider>();
            Collider[] allColliders = piece.GetComponents<Collider>();
            report.Check(
                renderers.Length == 1 && renderers[0].enabled,
                $"{pieceName} renderer",
                $"{pieceName} needs exactly one enabled MeshRenderer.");
            report.Check(
                boxColliders.Length == 1 && allColliders.Length == 1 &&
                boxColliders[0].enabled && !boxColliders[0].isTrigger,
                $"{pieceName} solid BoxCollider",
                $"{pieceName} needs exactly one enabled, non-trigger BoxCollider.");
            report.Check(
                piece.GetComponents<MeshFilter>().Length == 1,
                $"{pieceName} mesh",
                $"{pieceName} needs exactly one MeshFilter.");
            ValidateNoDuplicateComponents(piece.gameObject, report);

            if (renderers.Length == 1)
                Encapsulate(ref bounds, ref hasBounds, renderers[0].bounds);
        }
    }

    private static void ValidateGlass(Transform glass, ValidationReport report)
    {
        if (glass == null)
            return;

        Renderer[] renderers = glass.GetComponents<Renderer>();
        report.Check(
            glass.gameObject.activeSelf,
            "Glass object is active",
            "Glass must remain active as a prepared placeholder.");
        report.Check(
            renderers.Length == 1 && renderers.All(renderer => !renderer.enabled),
            "Glass does not block visually",
            "Glass must have one disabled Renderer in Phase 3A.");
        report.Check(
            glass.GetComponents<Collider>().Length == 0,
            "Glass has no collider",
            "Glass must not have a collider in Phase 3A.");
        ValidateNoDuplicateComponents(glass.gameObject, report);
    }

    private static void ValidateOpening(Transform opening, ValidationReport report)
    {
        if (opening == null)
            return;

        report.Check(
            opening.GetComponents<Renderer>().Length == 0 &&
            opening.GetComponents<MeshFilter>().Length == 0 &&
            opening.GetComponents<Collider>().Length == 0,
            "ViewOpening is an empty reference",
            "ViewOpening must not render or collide in Phase 3A.");
        report.Check(
            opening.localPosition.sqrMagnitude < 0.0001f,
            "ViewOpening is centered",
            "ViewOpening must be centered in Window_01.");
        report.Check(
            opening.localScale.y > 1f && Mathf.Max(opening.localScale.x, opening.localScale.z) > 2f,
            "ViewOpening represents a human-scale visible area",
            "ViewOpening dimensions are too small for the intended visible area.");
        ValidateNoDuplicateComponents(opening.gameObject, report);
    }

    private static void ValidateWall(
        Transform wall,
        ValidationReport report,
        out Bounds bounds,
        out bool hasBounds)
    {
        bounds = default;
        hasBounds = false;
        if (wall == null)
            return;

        report.Check(
            wall.GetComponents<Renderer>().Length == 0 &&
            wall.GetComponents<MeshFilter>().Length == 0 &&
            wall.GetComponents<Collider>().Length == 0,
            "Wall_East root is a segmented container",
            "Wall_East root still contains a full solid wall component.");

        foreach (string pieceName in WallPieceNames)
        {
            Transform piece = FindUniqueDirectChild(wall, pieceName, report);
            if (piece == null)
                continue;

            Renderer renderer = piece.GetComponent<Renderer>();
            BoxCollider collider = piece.GetComponent<BoxCollider>();
            report.Check(
                renderer != null && renderer.enabled,
                $"{pieceName} renderer",
                $"{pieceName} needs an enabled Renderer.");
            report.Check(
                collider != null && piece.GetComponents<Collider>().Length == 1 &&
                collider.enabled && !collider.isTrigger,
                $"{pieceName} solid collider",
                $"{pieceName} needs one enabled, non-trigger BoxCollider.");
            ValidateNoDuplicateComponents(piece.gameObject, report);
            if (renderer != null)
                Encapsulate(ref bounds, ref hasBounds, renderer.bounds);
        }
    }

    private static void ValidateDimensions(
        Bounds frameBounds,
        bool hasFrameBounds,
        Bounds wallBounds,
        bool hasWallBounds,
        ValidationReport report)
    {
        if (!hasFrameBounds)
        {
            report.Error("Window frame bounds could not be calculated.");
            return;
        }

        float windowWidth = Mathf.Max(frameBounds.size.x, frameBounds.size.z);
        float windowHeight = frameBounds.size.y;
        report.Check(
            windowWidth >= 2.4f && windowWidth <= 2.7f,
            $"Window width is reasonable ({windowWidth:F2} m)",
            $"Window width must be about 2.4-2.7 m; found {windowWidth:F2} m.");
        report.Check(
            windowHeight >= 1.4f && windowHeight <= 1.6f,
            $"Window height is reasonable ({windowHeight:F2} m)",
            $"Window height must be about 1.4-1.6 m; found {windowHeight:F2} m.");
        report.Check(
            frameBounds.min.y >= 0.85f && frameBounds.min.y <= 1.15f,
            $"Window lower edge is reasonable ({frameBounds.min.y:F2} m)",
            $"Window lower edge must be about 0.9-1.1 m; found {frameBounds.min.y:F2} m.");

        if (!hasWallBounds)
        {
            report.Error("Wall_East bounds could not be calculated.");
            return;
        }

        float wallWidth = Mathf.Max(wallBounds.size.x, wallBounds.size.z);
        report.Check(
            windowWidth < wallWidth,
            $"Window width ({windowWidth:F2} m) is less than wall width ({wallWidth:F2} m)",
            "Window width must remain less than Wall_East width.");
        report.Warn(
            windowWidth <= wallWidth * 0.7f,
            "Visible wall remains around the window",
            "Window consumes more than 70% of the wall width; review spacing.");
    }

    private static void ValidateUnblockedOpening(
        Transform window,
        Transform wall,
        Transform opening,
        ValidationReport report)
    {
        if (window == null || wall == null || opening == null)
            return;

        Vector3 center = opening.position;
        Collider[] solidColliders = window.GetComponentsInChildren<Collider>(true)
            .Concat(wall.GetComponentsInChildren<Collider>(true))
            .Where(collider => collider.enabled && !collider.isTrigger)
            .ToArray();
        Collider[] blockers = solidColliders
            .Where(collider => collider.bounds.Contains(center))
            .ToArray();
        report.Check(
            blockers.Length == 0,
            "No solid collider covers ViewOpening",
            "Solid collider(s) cover the opening center: " +
            string.Join(", ", blockers.Select(blocker => GetPath(blocker.transform))));
    }

    private static void ValidateNoteVisibility(
        Scene scene,
        Bounds frameBounds,
        bool hasFrameBounds,
        ValidationReport report)
    {
        Transform note = FindUniqueByName(scene, "Note", report);
        if (note == null || !hasFrameBounds)
            return;

        Renderer[] noteRenderers = note.GetComponentsInChildren<Renderer>(true);
        bool covered = noteRenderers.Any(renderer => renderer.bounds.Intersects(frameBounds));
        report.Check(!covered, "Note is not covered by Window_01", "Window frame overlaps the Note.");
    }

    private static void ValidatePhaseOne(Scene scene, ValidationReport report)
    {
        XROrigin[] origins = FindSceneComponents<XROrigin>(scene);
        report.Check(origins.Length == 1, "Phase 1 XR Origin preserved", $"Expected one XR Origin; found {origins.Length}.");
        report.Check(
            FindSceneComponents<DesktopKeyboardLocomotion>(scene).Length == 1,
            "DesktopKeyboardLocomotion preserved",
            "Expected one DesktopKeyboardLocomotion in SampleScene.");
        report.Check(
            FindSceneComponents<XRInteractionManager>(scene).Length == 1,
            "XR Interaction Manager preserved",
            "Expected one XR Interaction Manager in SampleScene.");
    }

    private static void ValidatePhaseTwo(
        Scene scene,
        Transform furniture,
        ValidationReport report)
    {
        report.Check(
            furniture != null && FindDirectChildren(furniture, "Nightstand").Length == 1,
            "Nightstand preserved",
            "Room/Furniture/Nightstand is missing or duplicated.");

        Transform bottle = FindUniqueByName(scene, "MedicineBottle", report);
        if (bottle != null)
        {
            report.Check(
                bottle.GetComponents<Rigidbody>().Length == 1,
                "MedicineBottle Rigidbody preserved",
                "MedicineBottle needs exactly one Rigidbody.");
            report.Check(
                bottle.GetComponents<XRGrabInteractable>().Length == 1,
                "MedicineBottle remains grabbable",
                "MedicineBottle needs exactly one XRGrabInteractable.");
            report.Check(
                bottle.GetComponents<FirstGrabTrigger>().Length == 1,
                "MedicineBottle first-grab trigger preserved",
                "MedicineBottle needs exactly one FirstGrabTrigger.");
        }

        Transform note = FindUniqueByName(scene, "Note", report);
        report.Check(
            note != null && note.GetComponents<NoteView>().Length == 1,
            "NoteView preserved",
            "Note needs exactly one NoteView.");

        Transform sequenceObject = FindUniqueByName(scene, "Phase2_FirstHallucination", report);
        FirstHallucinationSequence sequence =
            sequenceObject != null ? sequenceObject.GetComponent<FirstHallucinationSequence>() : null;
        report.Check(
            sequence != null && sequenceObject.GetComponents<FirstHallucinationSequence>().Length == 1,
            "FirstHallucinationSequence preserved",
            "Phase2_FirstHallucination needs exactly one FirstHallucinationSequence.");
        if (sequence != null)
        {
            SerializedObject serializedSequence = new SerializedObject(sequence);
            report.Check(
                serializedSequence.FindProperty("noteView").objectReferenceValue != null &&
                serializedSequence.FindProperty("guidanceAudioSource").objectReferenceValue != null &&
                serializedSequence.FindProperty("hallucinationAudioSource").objectReferenceValue != null,
                "FirstHallucinationSequence references preserved",
                "FirstHallucinationSequence has missing NoteView or AudioSource references.");
        }
    }

    private static void ValidateNoGrabCube(Scene scene, ValidationReport report)
    {
        int count = FindSceneTransforms(scene).Count(transform => transform.name == "GrabCube");
        report.Check(count == 0, "GrabCube was not reintroduced", $"Found {count} object(s) named GrabCube.");
    }

    private static void ValidateNoMissingScripts(Scene scene, ValidationReport report)
    {
        int count = FindSceneTransforms(scene)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        report.Check(count == 0, "No Missing Scripts", $"Scene contains {count} missing script reference(s).");
    }

    private static void ValidateNoDuplicateComponents(GameObject target, ValidationReport report)
    {
        Component[] components = target.GetComponents<Component>();
        int duplicateGroups = components
            .Where(component => component != null && component is not Transform)
            .GroupBy(component => component.GetType())
            .Count(group => group.Count() > 1);
        report.Check(
            duplicateGroups == 0,
            $"No duplicate components on {GetPath(target.transform)}",
            $"{GetPath(target.transform)} has duplicated component types.");
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
        Transform[] matches = FindDirectChildren(parent, name);
        report.Check(
            matches.Length == 1,
            GetPath(parent) + "/" + name,
            $"Expected one {GetPath(parent)}/{name}; found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform FindUniqueByName(Scene scene, string name, ValidationReport report)
    {
        Transform[] matches = FindSceneTransforms(scene).Where(transform => transform.name == name).ToArray();
        report.Check(matches.Length == 1, $"Single {name}", $"Expected one object named {name}; found {matches.Length}.");
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Transform[] FindDirectChildren(Transform parent, string name)
    {
        return parent.Cast<Transform>().Where(child => child.name == name).ToArray();
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

    private static void Encapsulate(ref Bounds target, ref bool initialized, Bounds addition)
    {
        if (!initialized)
        {
            target = addition;
            initialized = true;
        }
        else
        {
            target.Encapsulate(addition);
        }
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

        public void Error(string message)
        {
            ErrorCount++;
            lines.Add("[ERROR] " + message);
        }

        public string BuildSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Phase 3A Window Validation");
            builder.AppendLine();
            foreach (string line in lines)
                builder.AppendLine(line);
            builder.AppendLine();
            builder.Append($"Result: {ErrorCount} error(s), {WarningCount} warning(s).");
            return builder.ToString();
        }
    }
}
