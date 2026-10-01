using System;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// Оформление непрерывного маршрута: базовая постобработка, тон каждого региона,
// штормовой слой и надписи с проверкой глубины. Объекты сохраняются в сценах;
// повторный запуск пересобирает только корень Journey_Look.
public static class JourneyLookAuthoring
{
    const string Dir = "Assets/Art/JourneyLook/";
    const string RootName = "Journey_Look";
    static readonly string[] EntryScenes =
    {
        "Assets/Scenes/Stage1_Outskirts.unity", "Assets/Scenes/Stage2_Wasteland.unity",
        "Assets/Scenes/Stage3_Industrial.unity", "Assets/Scenes/Stage4_Citadel.unity",
    };
    const string CoopScene = "Assets/Scenes/Coop_Outskirts.unity";
    static readonly string[] RegionNames = { "Outskirts", "Canyon", "Pass", "Citadel" };

    [MenuItem("RogueDrive/Route/Author Journey Look (all scenes)")]
    public static void AuthorAll()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Profiles();
        for(int i = 0; i < EntryScenes.Length; i++) AuthorScene(EntryScenes[i], i);
        AuthorScene(CoopScene, 0);
        var catalog = AssetDatabase.LoadAssetAtPath<JourneyStreamCatalog>("Assets/Content/SeamlessJourney/Campaign.asset");
        foreach(var segment in catalog.segments) WorldTextOnly(segment.scenePath);
        EditorSceneManager.OpenScene(EntryScenes[0]);
        Debug.Log("[JourneyLook] Authored " + (EntryScenes.Length + 1) + " scenes and " + catalog.segments.Length + " journey worlds.");
    }

    [MenuItem("RogueDrive/Route/Author Journey Look (open scene)")]
    public static void AuthorOpenScene()
    {
        Profiles();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int region = Array.IndexOf(EntryScenes, scene.path);
        Author(scene, Mathf.Max(region, 0));
        EditorSceneManager.SaveScene(scene);
    }

    static void AuthorScene(string path, int region)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Author(scene, region);
        EditorSceneManager.SaveScene(scene);
    }

    static void WorldTextOnly(string path)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if(WorldTextMaterials.Apply(scene) > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
    }

    static void Author(UnityEngine.SceneManagement.Scene scene, int region)
    {
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if(old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

        Volume(root.transform, "Base", Load("Base"), 0, 1);
        var regions = new PostProcessVolume[RegionNames.Length];
        for(int i = 0; i < regions.Length; i++)
            regions[i] = Volume(root.transform, "Region_" + RegionNames[i], Load(RegionNames[i]), 1, i == region ? 1 : 0);
        var storm = Volume(root.transform, "Storm", Load("Storm"), 2, 0);
        var look = root.AddComponent<JourneyLook>();
        var so = new SerializedObject(look);
        var list = so.FindProperty("regionVolumes");
        list.arraySize = regions.Length;
        for(int i = 0; i < regions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = regions[i];
        so.FindProperty("stormVolume").objectReferenceValue = storm;
        so.FindProperty("fallbackRegion").intValue = region;
        so.ApplyModifiedPropertiesWithoutUndo();

        var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:PostProcessResources")[0]));
        foreach(var camera in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
        {
            if(camera.targetTexture != null) continue;
            var layer = camera.GetComponent<PostProcessLayer>();
            if(layer == null) layer = camera.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(resources);
            layer.volumeTrigger = camera.transform;
            layer.volumeLayer = 1 << root.layer;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            camera.allowHDR = true;
            EditorUtility.SetDirty(layer);
        }
        WorldTextMaterials.Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static PostProcessVolume Volume(Transform parent, string name, PostProcessProfile profile, float priority, float weight)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var volume = go.AddComponent<PostProcessVolume>();
        volume.isGlobal = true; volume.priority = priority; volume.weight = weight; volume.sharedProfile = profile;
        return volume;
    }

    static PostProcessProfile Load(string name) => AssetDatabase.LoadAssetAtPath<PostProcessProfile>(Dir + "Journey_" + name + ".asset");

    // Базовый слой задаёт тонмаппинг и общие эффекты; регионы и шторм переопределяют цвет поверх него.
    public static void Profiles()
    {
        if(!AssetDatabase.IsValidFolder("Assets/Art/JourneyLook")) AssetDatabase.CreateFolder("Assets/Art", "JourneyLook");
        var b = Profile("Base");
        var g = Get<ColorGrading>(b);
        g.gradingMode.Override(GradingMode.HighDefinitionRange); g.tonemapper.Override(Tonemapper.ACES);
        g.postExposure.Override(.45f); g.contrast.Override(8); g.saturation.Override(6); g.temperature.Override(0); g.tint.Override(0);
        g.colorFilter.Override(Color.white); g.lift.Override(new Vector4(1, 1, 1, 0)); g.gain.Override(new Vector4(1, 1, 1, 0));
        var bloom = Get<Bloom>(b); bloom.intensity.Override(1.4f); bloom.threshold.Override(1.1f); bloom.softKnee.Override(.55f); bloom.diffusion.Override(7);
        var ao = Get<AmbientOcclusion>(b); ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance); ao.intensity.Override(.6f); ao.thicknessModifier.Override(1.4f);
        var grain = Get<Grain>(b); grain.intensity.Override(.12f); grain.size.Override(.8f); grain.colored.Override(false);
        var vignette = Get<Vignette>(b); vignette.intensity.Override(.18f); vignette.smoothness.Override(.4f); vignette.color.Override(new Color(.05f, .03f, .02f));
        var ca = Get<ChromaticAberration>(b); ca.intensity.Override(0);
        Save(b);

        Region("Outskirts", exposure: .45f, temperature: 9, tint: 2, saturation: 10, contrast: 10, filter: new Color(1f, .97f, .92f), lift: new Vector4(.99f, 1f, 1.03f, 0), gain: new Vector4(1.04f, 1f, .95f, 0));
        Region("Canyon", exposure: .40f, temperature: 20, tint: 4, saturation: 10, contrast: 18, filter: new Color(1f, .93f, .84f), lift: new Vector4(1f, .98f, .98f, 0), gain: new Vector4(1.07f, 1f, .9f, 0));
        Region("Pass", exposure: .40f, temperature: -6, tint: -5, saturation: 0, contrast: 12, filter: new Color(.96f, 1f, .99f), lift: new Vector4(.97f, 1f, 1.04f, 0), gain: new Vector4(1f, 1f, 1f, 0));
        Region("Citadel", exposure: .55f, temperature: -12, tint: 3, saturation: -12, contrast: 14, filter: new Color(.94f, .96f, 1f), lift: new Vector4(.96f, .98f, 1.06f, -.01f), gain: new Vector4(1.02f, 1f, .97f, 0));

        var s = Profile("Storm");
        var sg = Get<ColorGrading>(s);
        sg.postExposure.Override(.3f); sg.temperature.Override(-12); sg.tint.Override(-2); sg.saturation.Override(-30); sg.contrast.Override(18);
        sg.colorFilter.Override(new Color(.86f, .92f, 1f));
        var sv = Get<Vignette>(s); sv.intensity.Override(.38f); sv.smoothness.Override(.45f); sv.color.Override(new Color(.02f, .03f, .05f));
        var sgrain = Get<Grain>(s); sgrain.intensity.Override(.38f); sgrain.size.Override(1.1f);
        var sca = Get<ChromaticAberration>(s); sca.intensity.Override(.14f);
        var sbloom = Get<Bloom>(s); sbloom.intensity.Override(.6f);
        Save(s);
        AssetDatabase.SaveAssets();
    }

    static void Region(string name, float exposure, float temperature, float tint, float saturation, float contrast, Color filter, Vector4 lift, Vector4 gain)
    {
        var p = Profile(name);
        var g = Get<ColorGrading>(p);
        g.postExposure.Override(exposure); g.temperature.Override(temperature); g.tint.Override(tint);
        g.saturation.Override(saturation); g.contrast.Override(contrast); g.colorFilter.Override(filter);
        g.lift.Override(lift); g.gain.Override(gain);
        Save(p);
    }

    static PostProcessProfile Profile(string name)
    {
        string path = Dir + "Journey_" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if(profile == null) { profile = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(profile, path); }
        return profile;
    }

    static T Get<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        if(!profile.TryGetSettings(out T s)) { s = profile.AddSettings<T>(); s.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(s, profile); }
        s.enabled.Override(true);
        return s;
    }

    static void Save(PostProcessProfile profile)
    {
        foreach(var s in profile.settings) EditorUtility.SetDirty(s);
        EditorUtility.SetDirty(profile);
    }
}
