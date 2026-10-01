using System;
using System.Linq;
using RogueDrive.Gameplay.Track;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// Видимая стена бури и зеркало заднего вида. Один раз размещает объекты в сценах-входах маршрута
// и в кооперативе; повторный запуск пересобирает только корень Storm_Front.
public static class StormFrontAuthoring
{
    const string Dir = "Assets/Art/Storm/";
    const string RootName = "Storm_Front";
    const float Width = 1000, Height = 300;
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Stage1_Outskirts.unity", "Assets/Scenes/Stage2_Wasteland.unity",
        "Assets/Scenes/Stage3_Industrial.unity", "Assets/Scenes/Stage4_Citadel.unity",
        "Assets/Scenes/Coop_Outskirts.unity",
    };
    static readonly string[] ThunderClips =
    {
        "Assets/Libraries/Soundbits_freeSFX_2025/Sounds/ji-b_impact-255.wav",
        "Assets/Libraries/Soundbits_freeSFX_2025/Sounds/ji-b_impact-297.wav",
        "Assets/Libraries/Soundbits_freeSFX_2025/Sounds/ji-d_impact-300.wav",
    };

    [MenuItem("RogueDrive/Route/Author Storm Front (all scenes)")]
    public static void AuthorAll()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach(var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Author(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(Scenes[0]);
        Debug.Log("[StormFront] Authored " + Scenes.Length + " scenes.");
    }

    [MenuItem("RogueDrive/Route/Author Storm Front (open scene)")]
    public static void AuthorOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Author(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Author(UnityEngine.SceneManagement.Scene scene)
    {
        if(!AssetDatabase.IsValidFolder("Assets/Art/Storm")) AssetDatabase.CreateFolder("Assets/Art", "Storm");
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if(old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

        var wall = Child(root.transform, "Wall");
        var mesh = WallMesh();
        var shells = new[]
        {
            Shell(wall, "Shell_Back", mesh, new Vector3(0, 0, -60), new Vector3(1.1f, 1.15f, 1),
                Wall("Back", new Color(.10f, .07f, .05f), new Color(.36f, .30f, .25f), .72f, .96f, .016f, 0f, new Vector3(0, 2.5f, 5))),
            Shell(wall, "Shell_Main", mesh, Vector3.zero, Vector3.one,
                Wall("Main", new Color(.16f, .11f, .08f), new Color(.55f, .45f, .35f), .6f, .9f, .02f, 7.3f, new Vector3(0, 3.5f, 7))),
            Shell(wall, "Shell_Front", mesh, new Vector3(0, 0, 30), new Vector3(.95f, .9f, 1),
                Wall("Front", new Color(.22f, .16f, .11f), new Color(.62f, .52f, .42f), .42f, .6f, .028f, 13.1f, new Vector3(0, 4.5f, 9))),
        };
        BaseDust(wall);

        var flash = Child(wall, "Lightning_Flash").gameObject.AddComponent<Light>();
        flash.transform.localPosition = new Vector3(0, 150, 60);
        flash.type = LightType.Point; flash.range = 900; flash.intensity = 0; flash.color = new Color(.8f, .82f, 1f); flash.shadows = LightShadows.None;
        var boltMat = AssetOrCreate(Dir + "Storm_Bolt.mat", () => new Material(Shader.Find("RogueDrive/AdditiveGlow")));
        boltMat.SetColor("_Color", new Color(4f, 4.2f, 5.5f)); EditorUtility.SetDirty(boltMat);
        var bolts = new LineRenderer[3];
        for(int i = 0; i < bolts.Length; i++)
        {
            var line = Child(wall, "Bolt_" + i).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = 14; line.sharedMaterial = boltMat;
            line.widthMultiplier = 2.4f; line.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, .25f));
            line.numCapVertices = 2; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false;
            bolts[i] = line;
        }

        var thunder = root.AddComponent<AudioSource>();
        thunder.playOnAwake = false; thunder.spatialBlend = 0; thunder.volume = 1;
        var engulf = EngulfDust(root.transform);

        var view = root.AddComponent<StormFrontView>();
        var so = new SerializedObject(view);
        so.FindProperty("wall").objectReferenceValue = wall;
        Fill(so.FindProperty("shells"), shells.Select(s => (Object)s.GetComponent<Renderer>()).ToArray());
        so.FindProperty("flashLight").objectReferenceValue = flash;
        Fill(so.FindProperty("bolts"), bolts);
        so.FindProperty("thunder").objectReferenceValue = thunder;
        Fill(so.FindProperty("thunderClips"), ThunderClips.Select(p => (Object)AssetDatabase.LoadAssetAtPath<AudioClip>(p)).ToArray());
        so.FindProperty("engulfDust").objectReferenceValue = engulf;
        so.ApplyModifiedPropertiesWithoutUndo();

        Mirror(root);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // Изогнутая к игроку завеса с нависающим верхом: края и «козырёк» тянутся вперёд,
    // поэтому при сближении буря закрывает небо над машиной.
    static Mesh WallMesh() => MeshAsset("Storm_WallMesh", () =>
    {
        const int cols = 60, rows = 30;
        var verts = new Vector3[(cols + 1) * (rows + 1)]; var uvs = new Vector2[verts.Length];
        for(int r = 0; r <= rows; r++)
        for(int c = 0; c <= cols; c++)
        {
            float u = c / (float)cols, v = r / (float)rows;
            float x = (u - .5f) * Width, y = Mathf.Lerp(-15, Height, v);
            float bend = Mathf.Pow(Mathf.Abs(x) / (Width * .5f), 2) * 260;
            float overhang = v > .5f ? Mathf.Pow((v - .5f) / .5f, 2) * 300 : 0;
            verts[r * (cols + 1) + c] = new Vector3(x, y, bend + overhang);
            uvs[r * (cols + 1) + c] = new Vector2(u, v);
        }
        var tris = new int[cols * rows * 6]; int t = 0;
        for(int r = 0; r < rows; r++)
        for(int c = 0; c < cols; c++)
        {
            int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
            tris[t++] = a; tris[t++] = d; tris[t++] = b; tris[t++] = b; tris[t++] = d; tris[t++] = e;
        }
        var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    });

    static Material Wall(string name, Color low, Color high, float coverage, float opacity, float scale, float seed, Vector3 scroll)
    {
        var m = AssetOrCreate(Dir + "Storm_Shell_" + name + ".mat", () => new Material(Shader.Find("RogueDrive/StormWall")));
        m.SetColor("_ColorLow", low); m.SetColor("_ColorHigh", high); m.SetFloat("_Coverage", coverage); m.SetFloat("_Opacity", opacity);
        m.SetFloat("_NoiseScale", scale); m.SetFloat("_Seed", seed); m.SetVector("_Scroll", scroll);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Shell(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    // Клубы пыли катятся перед стеной у самой земли.
    static void BaseDust(Transform wall)
    {
        var ps = Child(wall, "Base_Dust").gameObject.AddComponent<ParticleSystem>();
        ps.transform.localPosition = new Vector3(0, 22, 20);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9, 13); main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(35, 80); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new Color(.42f, .33f, .25f, .55f); main.maxParticles = 400;
        var emission = ps.emission; emission.rateOverTime = 32;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(900, 40, 80);
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-2, 2); velocity.y = new ParticleSystem.MinMaxCurve(1, 3); velocity.z = new ParticleSystem.MinMaxCurve(7, 12);
        var rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-.2f, .2f);
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .7f), new Keyframe(1, 1.6f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.2f, .7f);
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = DustMaterial(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        pr.maxParticleSize = 3;
    }

    // Пыль вокруг камеры, когда буря накрыла машину.
    static ParticleSystem EngulfDust(Transform parent)
    {
        var ps = Child(parent, "Engulf_Dust").gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(6, 14);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4.5f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new Color(.45f, .36f, .27f, .45f); main.maxParticles = 700;
        var emission = ps.emission; emission.rateOverTime = 220;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(30, 12, 30);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(.15f, .7f);
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = DustMaterial(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    static void Mirror(GameObject root)
    {
        string rtPath = Dir + "Storm_RearMirror.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if(rt == null) { rt = new RenderTexture(768, 168, 24) { name = "Storm_RearMirror" }; AssetDatabase.CreateAsset(rt, rtPath); }

        var cam = Child(root.transform, "Rear_Mirror_Camera").gameObject.AddComponent<Camera>();
        cam.targetTexture = rt; cam.fieldOfView = 22;   // вертикальный угол; при 768×168 по горизонтали ~83° cam.nearClipPlane = .3f; cam.farClipPlane = 1400;
        cam.clearFlags = CameraClearFlags.Skybox; cam.cullingMask = ~(1 << 5); cam.allowHDR = false; cam.allowMSAA = false;
        cam.depth = -5; cam.enabled = false;

        var canvasGo = Child(root.transform, "Rear_Mirror_UI").gameObject;
        canvasGo.layer = 5;
        var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var frame = UiChild(canvasGo.transform, "Mirror_Frame");
        frame.anchorMin = new Vector2(.385f, .885f); frame.anchorMax = new Vector2(.615f, .975f); frame.offsetMin = frame.offsetMax = Vector2.zero;
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); frameImage.type = Image.Type.Sliced;
        frameImage.color = new Color(.04f, .05f, .06f, .92f); frameImage.raycastTarget = false;
        var view = UiChild(frame, "Mirror_View");
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = new Vector2(6, 6); view.offsetMax = new Vector2(-6, -6);
        var raw = view.gameObject.AddComponent<RawImage>();
        raw.texture = rt; raw.uvRect = new Rect(1, 0, -1, 1); raw.raycastTarget = false;   // зеркальное отражение

        var mirror = root.AddComponent<StormRearMirror>();
        var so = new SerializedObject(mirror);
        so.FindProperty("mirrorCamera").objectReferenceValue = cam;
        so.FindProperty("mirrorPanel").objectReferenceValue = frame.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        frame.gameObject.SetActive(false);
    }

    static Material DustMaterial() => AssetOrCreate(Dir + "Storm_Dust.mat", () =>
    {
        var m = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        m.mainTexture = DustPuff();
        m.SetColor("_TintColor", new Color(.5f, .5f, .5f, .5f));
        return m;
    });

    // Мягкий рваный клуб для частиц пыли.
    static Texture2D DustPuff()
    {
        string path = Dir + "Storm_DustPuff.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(existing != null) return existing;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for(int y = 0; y < size; y++)
        for(int x = 0; x < size; x++)
        {
            float dx = x / (float)size - .5f, dy = y / (float)size - .5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2;
            float n = Mathf.PerlinNoise(x * .06f + 3, y * .06f + 7) * .6f + Mathf.PerlinNoise(x * .15f, y * .15f) * .4f;
            float a = Mathf.Clamp01(1 - r) * Mathf.Clamp01(n * 1.6f - .2f);
            tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
        }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Gradient Fade(float inAt, float outAt)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, inAt), new GradientAlphaKey(1, outAt), new GradientAlphaKey(0, 1) });
        return g;
    }

    static void Fill(SerializedProperty list, Object[] items)
    {
        list.arraySize = items.Length;
        for(int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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

    static RectTransform UiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
