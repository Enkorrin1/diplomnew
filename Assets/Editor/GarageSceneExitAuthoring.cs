using System;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GarageSceneExitAuthoring
{
    [MenuItem("RogueDrive/Bunker/Author Garage to Stage1 Cinematic")]
    public static void Author()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        var garage = SceneManager.GetSceneByName("GarageScene");
        if (!garage.isLoaded) garage = EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity", OpenSceneMode.Additive);
        var stage = SceneManager.GetSceneByName("Stage1_Outskirts");
        if (!stage.isLoaded) stage = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity", OpenSceneMode.Additive);
        var source = All(garage);
        if (source.Any(t => t.GetComponent<GarageSceneExitCinematic>() != null))
            throw new InvalidOperationException("Already authored. Edit the saved markers in the scenes.");
        var gate = source.Single(t => t.name == "Bunker_SwingBlastGate");
        var exit = source.Select(t => t.GetComponent<GarageExitTrigger>()).First(c => c != null);
        var oldSeam = source.FirstOrDefault(t => t.name == "Seamless_FirstStage");
        if (oldSeam != null) oldSeam.gameObject.SetActive(false); // Keep authored edits as a disabled backup.
        var sourceRoot = Root("Garage_Exit_Cinematic", garage);
        var cinematic = sourceRoot.AddComponent<GarageSceneExitCinematic>();
        var inside = Marker("Interior_Camera", sourceRoot.transform, new Vector3(4f, 4f, -7f));
        Set(cinematic, "gatePlane", gate);
        Set(cinematic, "interiorShot", inside);
        Set(exit, "cinematic", cinematic);
        Set(exit, "seamlessRoad", null);
        exit.transform.position = new Vector3(0f, 2.5f, 4f);
        var box = exit.GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(6.5f, 5f, 1.5f);
        box.isTrigger = true;

        var stageRoots = stage.GetRootGameObjects();
        var entryRoot = Root("Garage_Arrival", stage);
        var entry = entryRoot.AddComponent<StageGarageArrival>();
        var world = Root("Stage_World", stage);
        var gameplay = Root("Stage_Gameplay", stage);
        var standalone = Root("Stage_Standalone_Player", stage);
        var systemRoot = stageRoots.Single(g => g.GetComponent<GameRunController>() != null);
        // Direct Stage1 starts must put the follow camera outside the new bunker shell.
        foreach (var root in stageRoots)
            if (root.GetComponent<ArcadeCarController>() != null || root.GetComponent<Camera>() != null)
                root.transform.position += Vector3.forward * 11f;
        foreach (Transform child in systemRoot.transform.Cast<Transform>().ToArray())
            child.SetParent(world.transform, true);
        foreach (var root in stageRoots)
        {
            bool defaults = root.GetComponent<ArcadeCarController>() != null || root.GetComponent<Camera>() != null || root.name == "AudioManager";
            bool system = root == systemRoot || root.GetComponent<Canvas>() != null
                || root.name == "PauseController" || root.name == "ComboController"
                || root.name == "CombatVfxCatalog" || root.name == "EventSystem" || root.name == "TouchController";
            root.transform.SetParent(defaults ? standalone.transform : system ? gameplay.transform : world.transform, true);
        }
        var exterior = new GameObject("Bunker_Exterior");
        exterior.transform.SetParent(world.transform, false);
        var gateCopy = Object.Instantiate(gate.gameObject, exterior.transform);
        gateCopy.name = "Bunker_SwingBlastGate";
        gateCopy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in gateCopy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        var left = gateCopy.transform.Find("Door_Left_Hinge");
        var right = gateCopy.transform.Find("Door_Right_Hinge");
        left.localRotation = Quaternion.Euler(0f, -95f, 0f);
        right.localRotation = Quaternion.Euler(0f, 95f, 0f);
        var renderer = gateCopy.GetComponentInChildren<MeshRenderer>();
        var material = renderer != null ? renderer.sharedMaterial : null;
        Cube("Apron", exterior.transform, new Vector3(0f, -0.2f, -5f), new Vector3(24f, 0.4f, 10f), material);
        Cube("Left_Wall", exterior.transform, new Vector3(-8.5f, 3f, -1f), new Vector3(10f, 6f, 2f), material);
        Cube("Right_Wall", exterior.transform, new Vector3(8.5f, 3f, -1f), new Vector3(10f, 6f, 2f), material);
        Cube("Lintel", exterior.transform, new Vector3(0f, 5.8f, -1f), new Vector3(7f, 1.6f, 2f), material);
        Cube("Tunnel_Left", exterior.transform, new Vector3(-4.3f, 2.5f, -6f), new Vector3(1.5f, 5f, 10f), material);
        Cube("Tunnel_Right", exterior.transform, new Vector3(4.3f, 2.5f, -6f), new Vector3(1.5f, 5f, 10f), material);
        Cube("Tunnel_Roof", exterior.transform, new Vector3(0f, 5.1f, -6f), new Vector3(10f, 0.5f, 10f), material);
        var plane = Marker("Gate_Plane", entryRoot.transform, Vector3.zero);
        var outside = Marker("Exterior_Camera", entryRoot.transform, new Vector3(8f, 3.4f, 9f));
        Set(entry, "worldRoot", world);
        Set(entry, "gameplayRoot", gameplay);
        Set(entry, "standalonePlayerRoot", standalone);
        Set(entry, "gatePlane", plane);
        Set(entry, "exteriorShot", outside);
        Set(entry, "leftDoor", left);
        Set(entry, "rightDoor", right);
        world.SetActive(false);
        gameplay.SetActive(false);
        standalone.SetActive(false);
        var build = EditorBuildSettings.scenes.ToList();
        if (!build.Any(s => s.path == stage.path)) build.Add(new EditorBuildSettingsScene(stage.path, true));
        foreach (var item in build) if (item.path == stage.path) item.enabled = true;
        EditorBuildSettings.scenes = build.ToArray();
        EditorSceneManager.MarkSceneDirty(garage);
        EditorSceneManager.MarkSceneDirty(stage);
        EditorSceneManager.SaveScene(garage);
        EditorSceneManager.SaveScene(stage);
        EditorSceneManager.CloseScene(stage, true);
        SceneManager.SetActiveScene(garage);
        Debug.Log("[GarageExit] Authored GarageScene -> Stage1_Outskirts two-shot transition.");
    }

    private static Transform[] All(Scene scene) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
    private static GameObject Root(string name, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static Transform Marker(string name, Transform parent, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        return t;
    }
    private static void Set(Object target, string name, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
