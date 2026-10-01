using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay.Hub;

public static class BunkerLaunchUpgrade
{
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var garage = SceneManager.GetSceneByName("GarageScene");
        var stage = SceneManager.GetSceneByName("Stage1_Outskirts");
        if (!garage.isLoaded) garage = EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity", OpenSceneMode.Additive);
        if (!stage.isLoaded) stage = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity", OpenSceneMode.Additive);
        var world = stage.GetRootGameObjects().Single(g => g.name == "Stage_World");
        PlaceDirectStart(stage);
        if (world.transform.Find("Bunker_Launch_Set") != null) throw new InvalidOperationException("Launch set already exists; edit saved objects.");
        var set = new GameObject("Bunker_Launch_Set").transform;
        set.SetParent(world.transform, false);
        var concrete = Material("Launch_Concrete", new Color(.24f, .28f, .27f));
        var asphalt = Material("Launch_Asphalt", new Color(.09f, .11f, .12f));
        var amber = Material("Launch_Amber", new Color(.95f, .57f, .12f));
        var steel = Material("Launch_Steel", new Color(.13f, .18f, .20f));
        var rock = Material("Launch_Rock", new Color(.29f, .34f, .30f));
        // Continuous physical crest: 0 -> 1.6 -> 0, matching the cinematic flight.
        Segment("Exit_Climb", set, new Vector3(0, 0, 0), new Vector3(0, 1.6f, 18), 7.5f, asphalt);
        Segment("Crest_Descent", set, new Vector3(0, 1.6f, 18), new Vector3(0, 0, 32.3f), 7.5f, asphalt);
        Cube("Landing_Apron", set, new Vector3(0, -.15f, 45), new Vector3(10, .3f, 26), asphalt);
        for (int side = -1; side <= 1; side += 2)
        {
            Cube("Portal_Buttress", set, new Vector3(side * 7, 3, -1.7f), new Vector3(3, 6, 5), concrete);
            Cube("Portal_Reinforcement", set, new Vector3(side * 5.3f, 2.6f, -.1f), new Vector3(.45f, 5.2f, .5f), steel);
            for (int i = 0; i < 9; i++)
            {
                float z = 3 + i * 4.5f;
                float y = z < 18 ? z / 18 * 1.6f : z < 32.3f ? (32.3f - z) / 14.3f * 1.6f : 0;
                Cube("Concrete_Barrier", set, new Vector3(side * 4.7f, y + .5f, z), new Vector3(.65f, 1, 3.6f), concrete);
                Cube("Amber_Reflector", set, new Vector3(side * 4.34f, y + .66f, z), new Vector3(.035f, .19f, .6f), amber, false);
            }
            for (int i = 0; i < 6; i++)
            {
                float z = -6 + i * 10;
                var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/LowPoly Environment Pack/Prefabs/Rock_" + (i % 3 + 1) + ".prefab");
                var boulder = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, set);
                boulder.name = "Rock_Embankment";
                boulder.transform.SetParent(set, false);
                boulder.transform.position = new Vector3(side * (13 + i % 2 * 4), -.5f, z);
                boulder.transform.localScale = new Vector3(2.3f, 2.2f, 2.5f);
                boulder.transform.rotation = Quaternion.Euler(0, i * 47, 0);
                foreach (var r in boulder.GetComponentsInChildren<Renderer>()) r.sharedMaterial = rock;
            }
            for (int i = 0; i < 3; i++)
            {
                float z = 5 + i * 16;
                Cube("Lamp_Mast", set, new Vector3(side * 7, 3.3f, z), new Vector3(.14f, 6.6f, .14f), steel);
                Cube("Lamp_Head", set, new Vector3(side * 6.5f, 6.5f, z), new Vector3(1.2f, .18f, .55f), amber, false);
                var lamp = new GameObject("Warm_Exit_Light").AddComponent<Light>();
                lamp.transform.SetParent(set, false);
                lamp.transform.position = new Vector3(side * 6.3f, 6.2f, z);
                lamp.type = LightType.Point; lamp.range = 12; lamp.intensity = 2;
                lamp.color = new Color(1, .72f, .39f); lamp.shadows = LightShadows.None;
            }
        }
        Cube("Portal_Header", set, new Vector3(0, 6, -1), new Vector3(16, 1.2f, 4), concrete);
        var sign = new GameObject("Bunker_07_Sign").AddComponent<TextMesh>();
        sign.transform.SetParent(set, false); sign.transform.position = new Vector3(0, 5.9f, 1.05f);
        sign.transform.rotation = Quaternion.Euler(0, 180, 0); sign.text = "BUNKER 07";
        sign.anchor = TextAnchor.MiddleCenter; sign.characterSize = .28f; sign.fontSize = 64; sign.color = new Color(.95f, .78f, .45f);
        var arrival = stage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGarageArrival>(true)).Single();
        arrival.ExteriorShot.position = new Vector3(3.3f, 3.5f, 25);
        var flow = garage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GarageSceneExitCinematic>(true)).Single();
        var data = new SerializedObject(flow);
        data.FindProperty("landingImpact").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Libraries/Soundbits_freeSFX_2025/Sounds/ji-b_impact-255.wav");
        data.FindProperty("cameraReturnDuration").floatValue = 2.6f;
        var car = garage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Classic Car_9");
        ConfigureEngine(car);
        var smokeMaterial = Material("Launch_TireSmoke", new Color(.65f, .63f, .57f, .35f), "Particles/Standard Unlit");
        smokeMaterial.SetFloat("_Mode", 2); smokeMaterial.SetInt("_SrcBlend", 5); smokeMaterial.SetInt("_DstBlend", 10); smokeMaterial.SetInt("_ZWrite", 0);
        smokeMaterial.EnableKeyword("_ALPHABLEND_ON"); smokeMaterial.renderQueue = 3000;
        var array = data.FindProperty("tireSmoke"); array.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            var smoke = new GameObject("Launch_TireSmoke_" + i).AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke.transform.SetParent(car, false); smoke.transform.localPosition = new Vector3(i == 0 ? -.78f : .78f, .15f, -1.35f);
            smoke.transform.localRotation = Quaternion.Euler(-65, 180, 0);
            var main = smoke.main; main.playOnAwake = false; main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 1.7f;
            main.startSize = new ParticleSystem.MinMaxCurve(.3f, .75f); main.startColor = new Color(.65f, .63f, .57f, .4f); main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = smoke.emission; emission.rateOverTime = 32;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25; shape.radius = .12f;
            var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 2));
            var fade = smoke.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); fade.color = gradient;
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
            array.GetArrayElementAtIndex(i).objectReferenceValue = smoke;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(garage); EditorSceneManager.MarkSceneDirty(stage);
        EditorSceneManager.SaveScene(garage); EditorSceneManager.SaveScene(stage); AssetDatabase.SaveAssets();
        EditorSceneManager.CloseScene(stage, true); SceneManager.SetActiveScene(garage);
    }

    public static void RefineSavedSet()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var garage = SceneManager.GetSceneByName("GarageScene");
        var stage = SceneManager.GetSceneByName("Stage1_Outskirts");
        if (!garage.isLoaded) garage = EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity", OpenSceneMode.Additive);
        if (!stage.isLoaded) stage = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity", OpenSceneMode.Additive);
        PlaceDirectStart(stage);
        var set = stage.GetRootGameObjects().Single(g => g.name == "Stage_World").transform.Find("Bunker_Launch_Set");
        var rocks = set.Cast<Transform>().Where(t => t.name == "Rock_Embankment").ToArray();
        for (int i = 0; i < rocks.Length; i++)
        {
            Vector3 position = rocks[i].position;
            UnityEngine.Object.DestroyImmediate(rocks[i].gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/LowPoly Environment Pack/Prefabs/Rock_" + (i % 3 + 1) + ".prefab");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, set);
            replacement.name = "Rock_Embankment"; replacement.transform.position = new Vector3(position.x, -.5f, position.z);
            replacement.transform.localScale = new Vector3(2.3f, 2.2f, 2.5f); replacement.transform.rotation = Quaternion.Euler(0, i * 47, 0);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BunkerLaunch/Launch_Rock.mat");
            foreach (var r in replacement.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }
        stage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGarageArrival>(true)).Single().ExteriorShot.position = new Vector3(3.3f, 3.5f, 25);
        var car = garage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Classic Car_9");
        ConfigureEngine(car);
        EditorSceneManager.MarkSceneDirty(garage); EditorSceneManager.MarkSceneDirty(stage);
        EditorSceneManager.SaveScene(garage); EditorSceneManager.SaveScene(stage); AssetDatabase.SaveAssets();
        EditorSceneManager.CloseScene(stage, true);
    }

    private static void ConfigureEngine(Transform car)
    {
        var engine = car.GetComponent<AudioSource>();
        if (engine == null) engine = car.gameObject.AddComponent<AudioSource>();
        engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Libraries/Soundbits_freeSFX_2025/Sounds/crs-oa_driving_short_engine_01_04.wav");
        engine.loop = true; engine.playOnAwake = false; engine.spatialBlend = .8f; engine.volume = .55f; engine.minDistance = 5; engine.maxDistance = 45;
    }

    private static void PlaceDirectStart(Scene stage)
    {
        var direct = stage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageDirectStart>(true)).Single();
        direct.transform.root.position += Vector3.forward * (64f - direct.Car.transform.position.z);
    }

    private static Material Material(string name, Color color, string shader = "Standard")
    {
        const string dir = "Assets/Materials/BunkerLaunch";
        System.IO.Directory.CreateDirectory(dir);
        string path = dir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
        material.color = color; EditorUtility.SetDirty(material); return material;
    }
    private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = true)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent, false);
        cube.transform.position = position; cube.transform.localScale = scale; cube.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
    }
    private static void Segment(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material)
    {
        Vector3 direction = end - start;
        Cube(name, parent, (start + end) * .5f - Vector3.up * .15f, new Vector3(width, .3f, direction.magnitude), material);
        parent.Find(name).rotation = Quaternion.LookRotation(direction);
    }
}
