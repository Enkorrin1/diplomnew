using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SeamlessBunkerRoadAuthoring
{
    [MenuItem("RogueDrive/Bunker/Connect Authored First Stage")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var scene = SceneManager.GetSceneByName("GarageScene");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open GarageScene first.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Seamless_FirstStage"))
            throw new InvalidOperationException("First stage is already connected; edit its authored objects directly.");

        var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var car = transforms.Single(t => t.name == "Classic Car_9").gameObject;
        var camera = transforms.Select(t => t.GetComponent<Camera>()).First(c => c != null);
        var exit = transforms.Select(t => t.GetComponent<GarageExitTrigger>()).First(t => t != null);
        var outside = transforms.Single(t => t.name == "Outside_Road").GetComponent<Collider>().bounds;
        var offset = new Vector3(outside.center.x, outside.max.y, outside.max.z);

        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1_Outskirts.unity");
        GameObject clone;
        try
        {
            var container = new GameObject("Seamless_FirstStage");
            SceneManager.MoveGameObjectToScene(container, preview);
            foreach (var root in preview.GetRootGameObjects().Where(g => g != container))
                root.transform.SetParent(container.transform, true);
            clone = Object.Instantiate(container);
            clone.name = "Seamless_FirstStage";
            SceneManager.MoveGameObjectToScene(clone, scene);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        Undo.RegisterCreatedObjectUndo(clone, "Connect bunker road");
        var roadCar = clone.GetComponentInChildren<ArcadeCarController>(true);
        var templateCar = roadCar.gameObject;
        var templateCamera = clone.GetComponentInChildren<Camera>(true);
        var body = car.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(car);
        body.isKinematic = true;
        var bridge = Undo.AddComponent<ArcadeCarController>(car);
        bridge.enabled = false;
        var bridgeData = new SerializedObject(bridge);
        bridgeData.FindProperty("useGarageDriving").boolValue = true;
        bridgeData.ApplyModifiedPropertiesWithoutUndo();

        // Keep the template's socket layout and remap every serialized reference to the bunker car.
        foreach (Transform child in templateCar.transform.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Socket_")) child.SetParent(car.transform, false);
        var replacements = new Dictionary<Object, Object>
        {
            [templateCar] = car, [templateCar.transform] = car.transform,
            [templateCamera.gameObject] = camera.gameObject, [templateCamera.transform] = camera.transform,
            [templateCamera] = camera
        };
        foreach (var component in templateCar.GetComponents<Component>())
        {
            if (component is Transform) continue;
            var replacement = car.GetComponent(component.GetType());
            if (replacement == null && component.GetType().Name == "SocketRegistry")
            {
                replacement = Undo.AddComponent(car, component.GetType());
                EditorUtility.CopySerialized(component, replacement);
            }
            replacements[component] = replacement;
        }

        foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true).Concat(car.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyPath != "m_GameObject" && property.propertyPath != "m_Script"
                    && property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null
                    && replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                    property.objectReferenceValue = replacement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        var run = clone.GetComponentInChildren<GameRunController>(true);
        bridge.Configure(run);
        Object.DestroyImmediate(templateCar);
        Object.DestroyImmediate(templateCamera.gameObject);
        foreach (Transform child in clone.transform.Cast<Transform>().ToArray())
            if (child.name == "AudioManager" || child.name == "EventSystem" || child.name == "Directional Light" || child.name == "TouchController")
                Object.DestroyImmediate(child.gameObject);

        var world = new GameObject("Road_World");
        world.transform.SetParent(clone.transform, false);
        var systems = new GameObject("Road_Gameplay");
        systems.transform.SetParent(clone.transform, false);
        foreach (Transform child in clone.transform.Cast<Transform>().ToArray())
        {
            if (child == world.transform || child == systems.transform) continue;
            bool system = child.GetComponent<GameRunController>() != null || child.GetComponent<Canvas>() != null
                || child.name == "PauseController" || child.name == "ComboController" || child.name == "CombatVfxCatalog";
            child.SetParent(system ? systems.transform : world.transform, true);
        }
        var generator = systems.GetComponentInChildren<ProceduralTrackGenerator>(true);
        var authored = generator.transform.Find("AuthoredStageWorld");
        authored.SetParent(world.transform, true);
        generator.AuthoredCampaign = authored;
        var baked = generator.transform.Find("BakedFirstMap");
        if (baked != null) { baked.SetParent(world.transform, true); baked.gameObject.SetActive(false); }
        world.transform.position += offset;
        systems.SetActive(false);

        var flow = clone.AddComponent<SeamlessBunkerRoad>();
        var flowData = new SerializedObject(flow);
        flowData.FindProperty("gameplayRoot").objectReferenceValue = systems;
        flowData.FindProperty("playerCar").objectReferenceValue = bridge;
        flowData.ApplyModifiedPropertiesWithoutUndo();
        var exitData = new SerializedObject(exit);
        exitData.FindProperty("seamlessRoad").objectReferenceValue = flow;
        exitData.ApplyModifiedPropertiesWithoutUndo();
        // Start gameplay only once the car is actually leaving the bunker's outside apron.
        exit.transform.position = new Vector3(outside.center.x, outside.max.y + 2f, outside.max.z - 2f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SeamlessBunkerRoad] First stage connected. The original car, camera and inventory stay alive.");
    }
}
