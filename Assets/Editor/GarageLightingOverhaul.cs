using System;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// Световая и материальная переработка GarageScene: один раз размещает объекты в сцене,
// дальше они правятся руками. Повторный запуск пересобирает только Garage_LightingOverhaul.
public static class GarageLightingOverhaul
{
    const string Dir = "Assets/Art/GarageUpgrade/";
    const string RootName = "Garage_LightingOverhaul";
    static readonly Vector3 ProbeCenter = new Vector3(0, 3.0f, 0.25f), ProbeSize = new Vector3(24.2f, 6.2f, 25.2f);
    static readonly Color PoweredAmbient = new Color(.16f, .145f, .135f);
    static Transform root;

    [MenuItem("RogueDrive/Bunker/Lighting Overhaul")]
    public static void Author()
    {
        if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GarageScene")
            throw new InvalidOperationException("Open GarageScene in Edit mode.");
        var old = GameObject.Find(RootName);
        if(old != null) Object.DestroyImmediate(old);
        root = new GameObject(RootName).transform;
        Surfaces();
        TuneLights();
        var powered = PoweredVisuals();
        WorldText();
        PostFx();
        Environment(powered);
        BakeProbe(powered);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    // Голубой глянец «примитивов» заменён тёплым матовым бетоном с пятнами.
    static void Surfaces()
    {
        var grunge = GrungeTexture();
        var floor = Mat("Garage_Floor_Concrete", new Color(.30f, .29f, .27f), .32f, grunge, 6);
        var wall = Mat("Garage_Wall_Concrete", new Color(.30f, .29f, .27f), .08f, grunge, 3);
        var ceiling = Mat("Garage_Ceiling", new Color(.11f, .11f, .11f), .05f, grunge, 2);
        foreach(var name in new[] { "Wall_Left", "Wall_Right", "Wall_Back_Left", "Wall_Back_Right", "Wall_Back_Top", "Wall_Front" })
        {
            var t = GameObject.Find("GarageHubRoot/Environment/" + name);
            if(t != null) t.GetComponent<Renderer>().sharedMaterial = wall;
        }
        GameObject.Find("GarageHubRoot/Environment/Floor").GetComponent<Renderer>().sharedMaterial = floor;
        var ramp = GameObject.Find("GarageHubRoot/Exit_Ramp_And_Gate/Placeholder_InclineRamp");
        if(ramp != null) ramp.GetComponent<Renderer>().sharedMaterial = floor;
        GameObject.Find("GarageHubRoot/Environment/Ceiling").GetComponent<Renderer>().sharedMaterial = ceiling;
    }

    // Меньше равномерной заливки, больше направленных пятен света с тенями.
    static void TuneLights()
    {
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(l.transform.position.z > 14 || l.transform.root.name == "Seamless_FirstStage") continue;
            switch(l.name)
            {
                case "Podium_Spotlight":
                    l.intensity = 5f; l.range = 14; l.spotAngle = 74; l.innerSpotAngle = 40; l.color = new Color(1f, .93f, .82f);
                    Important(l, LightShadows.Soft); break;
                case "Workbench_WarmLight": l.intensity = 2.6f; Important(l, LightShadows.Soft); break;
                case "Recreation_WarmLight": l.intensity = 2.0f; Important(l, LightShadows.None); break;
                case "Armory_CoolLight": l.intensity = 1.5f; l.color = new Color(.68f, .82f, 1f); break;
                case "Generator_AmberLight": l.intensity = 1.8f; break;
                case "Workshop_Fill": l.intensity = 1.1f; l.color = new Color(1f, .85f, .66f); break;
                case "Zone_Light": l.intensity = 1.6f; break;
                case "Preparation_Light": l.intensity = 1.4f; break;
                case "Emergency_GuideLight": l.intensity = .45f; break;
                case "Map_Practical": l.intensity = 1.8f; Important(l, LightShadows.None); break;
            }
        }
    }

    static void Important(Light l, LightShadows shadows)
    {
        l.renderMode = LightRenderMode.ForcePixel;
        l.shadows = shadows;
        l.shadowStrength = .85f;
        l.shadowBias = .03f; l.shadowNormalBias = .25f;
    }

    // Всё, что светится только от генератора: свечение ламп, лампа над постом, световой конус и пыль.
    static GameObject PoweredVisuals()
    {
        var powered = new GameObject("Powered_Visuals").transform;
        powered.SetParent(root, false);
        var glowRoot = Child(powered, "Lamp_Glow");
        var glow = Emissive("Garage_Lamp_Glow", new Color(1f, .88f, .7f) * 3.2f);
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(r.name != "Ceiling_Luminaire") continue;
            var b = r.bounds;
            Box(glowRoot, "Luminaire_Tube", new Vector3(b.center.x, b.min.y - .02f, b.center.z), new Vector3(b.size.x * .9f, .035f, b.size.z * .45f), glow);
        }

        // Промышленный плафон над постом сборки: свет идёт из видимого источника.
        var podium = GameObject.Find("GarageHubRoot/Lighting/Podium_Spotlight").transform;
        var lamp = Child(root, "Podium_Lamp");
        lamp.position = new Vector3(podium.position.x, 5.35f, podium.position.z);
        var steel = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Steel.mat");
        var shade = new GameObject("Shade", typeof(MeshFilter), typeof(MeshRenderer));
        shade.transform.SetParent(lamp, false);
        shade.GetComponent<MeshFilter>().sharedMesh = MeshAsset("Garage_LampShade", () => Frustum(.12f, .62f, .42f, 32, true, false));
        shade.GetComponent<MeshRenderer>().sharedMaterial = steel;
        shade.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Box(lamp, "Cable", new Vector3(0, .45f, 0), new Vector3(.025f, .5f, .025f), steel).transform.localPosition = new Vector3(0, .45f, 0);
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bulb_Glow"; Object.DestroyImmediate(bulb.GetComponent<Collider>());
        bulb.transform.SetParent(glowRoot, false);
        bulb.transform.position = lamp.position + Vector3.down * .36f; bulb.transform.localScale = new Vector3(.32f, .14f, .32f);
        bulb.GetComponent<Renderer>().sharedMaterial = Emissive("Garage_Bulb_Glow", new Color(1f, .9f, .75f) * 6f);
        bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        podium.position = lamp.position + Vector3.down * .42f;

        // Световой конус под плафоном.
        var shafts = Child(powered, "Light_Shafts");
        var shaftMat = AssetOrCreate(Dir + "Garage_LightShaft.mat", () => new Material(Shader.Find("RogueDrive/LightShaft")));
        shaftMat.SetColor("_Color", new Color(1f, .86f, .64f)); shaftMat.SetFloat("_Intensity", .16f); shaftMat.SetFloat("_EdgePower", 2.2f); shaftMat.SetFloat("_NearFade", 2f);
        EditorUtility.SetDirty(shaftMat);
        var cone = new GameObject("Podium_Shaft", typeof(MeshFilter), typeof(MeshRenderer));
        cone.transform.SetParent(shafts, false);
        cone.transform.position = lamp.position + Vector3.down * .4f;
        float h = cone.transform.position.y;
        cone.transform.localScale = new Vector3(3.3f, h, 3.3f);
        cone.GetComponent<MeshFilter>().sharedMesh = MeshAsset("Garage_LightShaftCone", () => Frustum(.17f, 1f, 1f, 40, false, true));
        var cr = cone.GetComponent<MeshRenderer>();
        cr.sharedMaterial = shaftMat; cr.shadowCastingMode = ShadowCastingMode.Off; cr.receiveShadows = false;

        Dust(powered);
        return powered.gameObject;
    }

    static void Dust(Transform parent)
    {
        var go = new GameObject("Dust_Motes", typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0, 2.7f, 0);
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10, 16);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .9f, .75f, .45f), new Color(1f, .95f, .85f, .9f));
        main.maxParticles = 420;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.rateOverTime = 30;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(16, 5, 18);
        var noise = ps.noise; noise.enabled = true; noise.strength = .12f; noise.frequency = .25f; noise.scrollSpeed = .05f; noise.quality = ParticleSystemNoiseQuality.Medium;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = AssetOrCreate(Dir + "Garage_Dust.mat", () =>
        {
            var m = new Material(Shader.Find("Legacy Shaders/Particles/Additive (Soft)"));
            m.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            return m;
        });
        pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
        pr.maxParticleSize = .02f;
    }

    // GUI/Text Shader рисует TextMesh поверх всего: наружная табличка «07» просвечивала сквозь стену
    // зеркально. Надписи гаража получают материал с проверкой глубины и отсечением обратной стороны.
    static void WorldText()
    {
        var shader = Shader.Find("RogueDrive/WorldText");
        var scene = root.gameObject.scene;
        foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(text.gameObject.scene != scene || text.font == null) continue;
            var font = text.font;
            var mat = AssetOrCreate(Dir + "Garage_WorldText_" + font.name + ".mat", () => new Material(shader));
            mat.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(mat);
            text.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    static void PostFx()
    {
        string path = Dir + "Garage_PostProcess.asset";
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if(profile == null) { profile = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(profile, path); }
        T Get<T>() where T : PostProcessEffectSettings
        {
            if(!profile.TryGetSettings(out T s)) { s = profile.AddSettings<T>(); s.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(s, profile); }
            s.enabled.Override(true);
            return s;
        }
        var grade = Get<ColorGrading>();
        grade.gradingMode.Override(GradingMode.HighDefinitionRange);
        grade.tonemapper.Override(Tonemapper.ACES);
        grade.postExposure.Override(.95f);
        grade.temperature.Override(5);
        grade.contrast.Override(14);
        grade.saturation.Override(6);
        grade.lift.Override(new Vector4(.97f, 1f, 1.05f, -.01f));
        grade.gain.Override(new Vector4(1.05f, 1f, .94f, 0));
        var bloom = Get<Bloom>();
        bloom.intensity.Override(2.6f); bloom.threshold.Override(1.05f); bloom.softKnee.Override(.6f); bloom.diffusion.Override(7.5f);
        bloom.color.Override(new Color(1f, .9f, .78f));
        var ao = Get<AmbientOcclusion>();
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(.85f); ao.thicknessModifier.Override(1.6f); ao.color.Override(Color.black);
        var vignette = Get<Vignette>();
        vignette.intensity.Override(.34f); vignette.smoothness.Override(.42f); vignette.color.Override(new Color(.04f, .02f, .01f));
        var grain = Get<Grain>();
        grain.intensity.Override(.16f); grain.size.Override(.9f); grain.lumContrib.Override(.8f); grain.colored.Override(false);
        var ca = Get<ChromaticAberration>();
        ca.intensity.Override(.05f);
        EditorUtility.SetDirty(profile);

        var volume = Child(root, "PostProcess_Volume").gameObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;

        var cam = GameObject.Find("PlayerCamera");
        var layer = cam.GetComponent<PostProcessLayer>();
        if(layer == null) layer = cam.AddComponent<PostProcessLayer>();
        var guids = AssetDatabase.FindAssets("t:PostProcessResources");
        layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guids[0])));
        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = 1 << volume.gameObject.layer;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        cam.GetComponent<Camera>().allowHDR = true;
        EditorUtility.SetDirty(layer);
    }

    static void Environment(GameObject powered)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.reflectionIntensity = .35f;
        var manager = Object.FindFirstObjectByType<GaragePrologueManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("poweredAmbient").colorValue = PoweredAmbient;
        so.FindProperty("unpoweredAmbient").colorValue = new Color(.035f, .02f, .025f);
        var visuals = so.FindProperty("poweredVisuals");
        visuals.arraySize = 1; visuals.GetArrayElementAtIndex(0).objectReferenceValue = powered;
        so.ApplyModifiedPropertiesWithoutUndo();
        var enhancer = new SerializedObject(Object.FindFirstObjectByType<GarageAtmosphereEnhancer>());
        enhancer.FindProperty("fogColor").colorValue = new Color(.075f, .062f, .052f);
        enhancer.FindProperty("fogDensity").floatValue = .017f;
        enhancer.ApplyModifiedPropertiesWithoutUndo();
    }

    // Отражения интерьера вместо дневного неба: запекается при включённом рабочем свете.
    static void BakeProbe(GameObject powered)
    {
        var probe = Child(root, "Interior_ReflectionProbe").gameObject.AddComponent<ReflectionProbe>();
        probe.transform.position = ProbeCenter;
        probe.size = ProbeSize; probe.boxProjection = true; probe.blendDistance = 0;
        probe.resolution = 256; probe.hdr = true; probe.importance = 1;
        probe.mode = ReflectionProbeMode.Baked;

        var so = new SerializedObject(Object.FindFirstObjectByType<GaragePrologueManager>());
        var lights = so.FindProperty("mainWorkshopLights");
        var red = so.FindProperty("emergencyRedLight").objectReferenceValue as Light;
        var states = new bool[lights.arraySize];
        for(int i = 0; i < states.Length; i++)
            if(lights.GetArrayElementAtIndex(i).objectReferenceValue is Light l) { states[i] = l.enabled; l.enabled = true; }
        bool redState = red != null && red.enabled; if(red != null) red.enabled = false;
        var shafts = powered.transform.Find("Light_Shafts").gameObject; var dust = powered.transform.Find("Dust_Motes").gameObject;
        shafts.SetActive(false); dust.SetActive(false);
        var ambient = RenderSettings.ambientLight; RenderSettings.ambientLight = PoweredAmbient;
        string path = Dir + "Garage_InteriorReflection.exr";
        try
        {
            if(!Lightmapping.BakeReflectionProbe(probe, path)) throw new InvalidOperationException("Reflection probe bake failed.");
        }
        finally
        {
            for(int i = 0; i < states.Length; i++)
                if(lights.GetArrayElementAtIndex(i).objectReferenceValue is Light l) l.enabled = states[i];
            if(red != null) red.enabled = redState;
            shafts.SetActive(true); dust.SetActive(true);
            RenderSettings.ambientLight = ambient;
        }
        AssetDatabase.ImportAsset(path);
        probe.mode = ReflectionProbeMode.Custom;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
    }

    static Texture2D GrungeTexture()
    {
        string path = Dir + "Garage_ConcreteGrunge.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(existing != null) return existing;
        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var rng = new System.Random(707);
        var stains = new Vector3[14];
        for(int i = 0; i < stains.Length; i++) stains[i] = new Vector3(rng.Next(size), rng.Next(size), 20 + rng.Next(70));
        for(int y = 0; y < size; y++)
        for(int x = 0; x < size; x++)
        {
            float n = 0, amp = .5f, freq = 4f / size;
            for(int o = 0; o < 5; o++) { n += TileNoise(x, y, freq, size) * amp; amp *= .5f; freq *= 2; }
            float v = .82f + (n - .5f) * .36f + ((rng.Next(1000) / 1000f) - .5f) * .05f;
            foreach(var s in stains)
            {
                float dx = Mathf.Abs(x - s.x), dy = Mathf.Abs(y - s.y);
                dx = Mathf.Min(dx, size - dx); dy = Mathf.Min(dy, size - dy);
                v -= Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / s.z) * .16f * n;
            }
            tex.SetPixel(x, y, new Color(v, v * .985f, v * .96f));
        }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Бесшовный шум: периодические координаты по размеру текстуры.
    static float TileNoise(int x, int y, float freq, int size)
    {
        float period = size * freq;
        float u = x * freq, v = y * freq;
        float a = Mathf.PerlinNoise(u, v), b = Mathf.PerlinNoise(u - period, v);
        float c = Mathf.PerlinNoise(u, v - period), d = Mathf.PerlinNoise(u - period, v - period);
        float tx = (float)x / size, ty = (float)y / size;
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static Mesh Frustum(float topRadius, float bottomRadius, float height, int segments, bool doubleSided, bool fadeAlpha)
    {
        int ring = segments + 1, sides = doubleSided ? 2 : 1;
        var verts = new Vector3[ring * 2 * sides]; var normals = new Vector3[verts.Length]; var colors = new Color[verts.Length];
        var tris = new int[segments * 6 * sides];
        float slope = (bottomRadius - topRadius) / height;
        for(int s = 0; s < sides; s++)
        for(int i = 0; i < ring; i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var n = (dir + Vector3.up * slope).normalized * (s == 0 ? 1 : -1);
            int top = s * ring * 2 + i, bottom = top + ring;
            verts[top] = dir * topRadius; verts[bottom] = dir * bottomRadius + Vector3.down * height;
            normals[top] = normals[bottom] = n;
            colors[top] = new Color(1, 1, 1, 1); colors[bottom] = new Color(1, 1, 1, fadeAlpha ? .12f : 1);
        }
        int t = 0;
        for(int s = 0; s < sides; s++)
        for(int i = 0; i < segments; i++)
        {
            int a = s * ring * 2 + i, b = a + 1, c = a + ring, d = c + 1;
            if(s == 0) { tris[t++] = a; tris[t++] = b; tris[t++] = c; tris[t++] = b; tris[t++] = d; tris[t++] = c; }
            else { tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d; }
        }
        var mesh = new Mesh { vertices = verts, normals = normals, colors = colors, triangles = tris };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh MeshAsset(string name, Func<Mesh> build)
    {
        string path = Dir + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing != null) return existing;
        var mesh = build(); mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material Mat(string name, Color color, float gloss, Texture2D tex, float tiling)
    {
        var m = AssetOrCreate(Dir + name + ".mat", () => new Material(Shader.Find("Standard")));
        m.color = color; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0);
        m.mainTexture = tex; m.mainTextureScale = Vector2.one * tiling;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color hdr)
    {
        var m = AssetOrCreate(Dir + name + ".mat", () => new Material(Shader.Find("Standard")));
        m.color = Color.black; m.SetFloat("_Glossiness", .3f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", hdr);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material AssetOrCreate(string path, Func<Material> create)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m != null) return m;
        m = create(); AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }
}
