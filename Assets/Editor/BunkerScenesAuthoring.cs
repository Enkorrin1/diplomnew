using System.IO;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// Разовое размещение сцен «Пробуждение» и «Карта эвакуации» в GarageScene.
/// Рисует текстуры (лист карты, мягкие веки, линия маршрута) как ассеты, создаёт объекты сцены
/// и связывает их с BunkerPrologueCutscene / BunkerEvacuationMap. Повторный запуск пересоздаёт объекты.
/// </summary>
public static class BunkerScenesAuthoring
{
    private const string ArtDir = "Assets/Art/Bunker";
    private const string Sounds = "Assets/Libraries/Soundbits_freeSFX_2025/Sounds/";
    private const string FontPath = "Assets/Resources/UI/LowPoly/Fonts/Heading.ttf";

    private const int MapW = 1536, MapH = 1024;
    private const float SheetWidth = 1.0f; // метры вдоль длинной стороны
    private static readonly Color Ink = new Color(0.2f, 0.14f, 0.09f, 0.92f);
    private static readonly Color RedInk = new Color(0.66f, 0.1f, 0.06f, 0.95f);

    // Нормированные координаты листа (u вправо, v вверх): бункер → 3 СТО → Цитадель.
    private static readonly Vector2[] Route =
    {
        new Vector2(0.08f, 0.17f), new Vector2(0.25f, 0.6f), new Vector2(0.5f, 0.36f),
        new Vector2(0.69f, 0.56f), new Vector2(0.88f, 0.66f)
    };
    private static readonly float[] SectorBounds = { 0.02f, 0.36f, 0.6f, 0.77f, 0.98f };

    [MenuItem("RogueDrive/Authoring/Bunker Wake-up & Evacuation Map")]
    public static void Author()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("GarageScene.unity"))
        {
            Debug.LogError("[BunkerScenesAuthoring] Откройте GarageScene.");
            return;
        }

        Directory.CreateDirectory(ArtDir);
        var eyelid = SaveTexture("EyelidSoft.png", EyelidTexture(), true);
        var ink = SaveTexture("MapInkLine.png", InkLineTexture(), true);
        var paper = SaveTexture("EvacuationMap_Paper.png", MapTexture(), false);
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        AuthorWake(eyelid);
        AuthorMap(AssetDatabase.LoadAssetAtPath<Texture2D>(paper), AssetDatabase.LoadAssetAtPath<Sprite>(ink), font);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BunkerScenesAuthoring] Пробуждение и карта эвакуации размещены.");
    }

    // ── Пробуждение ─────────────────────────────────────────────────────────

    private static void AuthorWake(string eyelidPath)
    {
        var intro = Object.FindFirstObjectByType<BunkerPrologueCutscene>(FindObjectsInactive.Include);
        var so = new SerializedObject(intro);

        // Позы относительно дивана: голова на левом подлокотнике, сесть, встать лицом к машине.
        var sofa = GameObject.Find("sofa").transform;
        var bounds = RendererBounds(sofa);
        Vector3 right = sofa.right, forward = sofa.forward, c = bounds.center;
        float floor = bounds.min.y;
        var markers = sofa.Find("Sofa_Awakening");
        var car = Object.FindFirstObjectByType<BunkerStarterCarAssembly>().transform;

        var eyes = Child(markers, "Eyes_Lying");
        eyes.position = new Vector3(c.x, floor, c.z) - right * 0.72f - forward * 0.05f + Vector3.up * 0.72f;
        // Лёжа на спине головой к -right: верх кадра уходит за макушку, голова чуть повёрнута к комнате.
        eyes.rotation = Quaternion.LookRotation(Vector3.up * 0.85f + forward * 0.42f + right * 0.32f, -right + forward * 0.35f);

        var sitting = Child(markers, "Eyes_Sitting");
        sitting.position = new Vector3(c.x, floor, c.z) - right * 0.42f + forward * 0.18f + Vector3.up * 1.12f;
        sitting.rotation = Quaternion.LookRotation(forward - Vector3.up * 0.22f, Vector3.up);

        var standing = Child(markers, "Standing_Clearance");
        standing.position = new Vector3(c.x, 0.08f, c.z) - right * 0.38f + forward * 1.05f;
        Vector3 toCar = car.position - standing.position; toCar.y = 0f;
        standing.rotation = Quaternion.LookRotation(toCar.normalized, Vector3.up);

        var look = Child(markers, "Look_Car");
        look.position = car.position + Vector3.up * 0.9f;

        so.FindProperty("wakeEyePose").objectReferenceValue = eyes;
        so.FindProperty("wakeSittingPose").objectReferenceValue = sitting;
        so.FindProperty("wakeStandingPose").objectReferenceValue = standing;
        so.FindProperty("wakeLookTarget").objectReferenceValue = look;
        so.FindProperty("radioStaticDuration").floatValue = 1.0f;

        // Мягкие веки: одна градиентная текстура, нижнее веко отражено по Y.
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(eyelidPath);
        foreach (var (prop, flip) in new[] { ("topEyelid", false), ("bottomEyelid", true) })
        {
            var rt = (RectTransform)so.FindProperty(prop).objectReferenceValue;
            var img = rt.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = Color.black;
            img.raycastTarget = false;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = new Vector3(1f, flip ? -1f : 1f, 1f);
        }

        // Субтитры: тёмная плашка снизу, шрифт с кириллицей.
        var text = (Text)so.FindProperty("subtitleText").objectReferenceValue;
        text.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        text.fontSize = 30;
        text.lineSpacing = 1.15f;
        text.supportRichText = true;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.93f, 0.93f, 0.9f);
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var panel = (GameObject)so.FindProperty("subtitlePanel").objectReferenceValue;
        var panelImg = panel.GetComponent<Image>();
        if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(1180f, 132f);
        panelRt.anchoredPosition = new Vector2(0f, 96f);

        // Сонная дымка — отдельный PostProcessVolume, вес анимирует пролог.
        var profilePath = ArtDir + "/Wake_Blur.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        // Сонная дымка: сильное мягкое свечение + блёклые цвета. DepthOfField и собственная Vignette
        // в смеси с профилем гаража дают белую пелену и светлые края, поэтому их здесь нет.
        var bloom = profile.AddSettings<Bloom>();
        bloom.intensity.Override(5f);
        bloom.threshold.Override(0.5f);
        bloom.softKnee.Override(0.8f);
        bloom.diffusion.Override(9f);
        var grading = profile.AddSettings<ColorGrading>();
        grading.saturation.Override(-45f);
        grading.postExposure.Override(-0.3f);
        foreach (var setting in profile.settings)
        {
            setting.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(setting, profile);
        }
        EditorUtility.SetDirty(profile);

        var root = intro.transform;
        var volumeGo = Child(root, "Wake_BlurVolume").gameObject;
        volumeGo.layer = 0;
        var volume = volumeGo.GetComponent<PostProcessVolume>();
        if (volume == null) volume = volumeGo.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
        so.FindProperty("wakeVolume").objectReferenceValue = volume;

        var staticGo = Child(root, "Wake_RadioStatic").gameObject;
        var src = staticGo.GetComponent<AudioSource>();
        if (src == null) src = staticGo.AddComponent<AudioSource>();
        src.clip = Clip("jw4_noise-interference-116.wav");
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        so.FindProperty("staticSource").objectReferenceValue = src;
        so.FindProperty("radioGlitchClip").objectReferenceValue = Clip("glitchfx_01_dirt_sfx_016.wav");
        so.FindProperty("sofaCreakClip").objectReferenceValue = Clip("ucns_woodenbed_creaks_04.wav");
        so.FindProperty("clothClip").objectReferenceValue = Clip("rmg_pocket_leather_jacket-03.wav");
        so.FindProperty("footstepClip").objectReferenceValue = Clip("jc_footsteps-addon_12.wav");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Карта ───────────────────────────────────────────────────────────────

    private static void AuthorMap(Texture2D paper, Sprite ink, Font font)
    {
        var oldMap = GameObject.Find("Map");
        if (oldMap != null && oldMap.GetComponent<BunkerEvacuationMap>() == null) Object.DestroyImmediate(oldMap);
        // GameObject.Find не видит выключенные объекты (оверлей лежит выключенным) — чистим по детям.
        var dressing = GameObject.Find("Garage_SetDressing").transform;
        for (int i = dressing.childCount - 1; i >= 0; i--)
        {
            var child = dressing.GetChild(i);
            if (child.name == "Evacuation_Map" || child.name == "Evacuation_Map_Overlay") Object.DestroyImmediate(child.gameObject);
        }

        // Освобождаем центр стола: мелочи к краям.
        Move("Garage_PhysicalItems/Flashlight_Portable", new Vector3(10.2f, 0.98f, 6.78f));
        Move("Garage_PhysicalItems/DEC_CoffeeMug_Portable", new Vector3(10.0f, 0.94f, 6.82f));
        Move("Garage_PhysicalItems/Walkie_talkie_Portable", new Vector3(9.45f, 1.0f, 5.05f));
        Move("Garage_CohesiveDetails/03_Desk_And_Living/SpiralNotebook", new Vector3(10.12f, 0.9f, 5.1f));
        Move("Garage_CohesiveDetails/03_Desk_And_Living/PenBlue", new Vector3(10.15f, 0.92f, 5.1f));

        // В сцене два обеденных стола — берём тот, что у стены рядом с верстаком.
        Renderer table = null;
        var near = new Vector3(9.8f, 0.4f, 5.9f);
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name == "DiningTableMesh" && (table == null || Vector3.Distance(r.bounds.center, near) < Vector3.Distance(table.bounds.center, near)))
                table = r;
        Vector3 center = new Vector3(table.bounds.center.x, table.bounds.max.y, table.bounds.center.z);
        float sheetH = SheetWidth * MapH / MapW;

        // Лист лежит лицом вверх: «верх» карты смотрит на +X (от подходящего игрока), правый край — на -Z.
        Quaternion flat = Quaternion.LookRotation(Vector3.down, Vector3.right);
        var root = new GameObject("Evacuation_Map").transform;
        root.SetParent(GameObject.Find("Garage_SetDressing").transform, false);
        root.position = center;
        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.02f, 0f);
        col.size = new Vector3(sheetH, 0.05f, SheetWidth);

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "Map_Sheet";
        Object.DestroyImmediate(sheet.GetComponent<Collider>());
        sheet.transform.SetParent(root, false);
        sheet.transform.localPosition = Vector3.up * 0.003f;
        sheet.transform.rotation = flat * Quaternion.Euler(0f, 0f, 1.5f);
        sheet.transform.localScale = new Vector3(SheetWidth, sheetH, 1f);
        var matPath = ArtDir + "/M_EvacuationMap.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.mainTexture = paper;
        mat.SetFloat("_Glossiness", 0.12f);
        EditorUtility.SetDirty(mat);
        var sheetRenderer = sheet.GetComponent<MeshRenderer>();
        sheetRenderer.sharedMaterial = mat;
        sheetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // World-space канвас поверх листа: чернила маршрута, метки, подписи и зоны секторов.
        var canvasGo = new GameObject("Map_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(sheet.transform, false);
        var canvasRt = (RectTransform)canvasGo.transform;
        canvasRt.sizeDelta = new Vector2(MapW, MapH);
        canvasRt.localPosition = new Vector3(0f, 0f, -0.002f);
        canvasRt.localRotation = Quaternion.identity;
        canvasRt.localScale = new Vector3(1f / MapW, 1f / MapH, 1f);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var sectorData = new[]
        {
            ("СЕКТОР 01", "ПРИГОРОД", "Пригородное шоссе", "Закат  ·  заражённые  ·  завалы",
             "Бывшие кварталы и автострада. Первая волна заражённых, брошенные машины на полосах.\n\n<b>СТО «Северная»</b> — укрытие, базовые детали, ремонт и торговля."),
            ("СЕКТОР 02", "ПУСТОШЬ", "Песчаная пустошь", "День  ·  жара  ·  рейдеры",
             "Разбитые конвои и развилки. Держитесь основной трассы.\n\n<b>Казино «Фортуна»</b> — слот-машина за золотые жетоны.\n<b>СТО «Каньон»</b> — двигатели, подвеска, броня."),
            ("СЕКТОР 03", "ПРОМЗОНА", "Токсичная промзона", "Ночь  ·  кислотный туман",
             "Заброшенные заводы и кислотные испарения. Без фар не проехать.\n\n<b>СТО «Рубеж»</b> — продвинутые детали, последняя подготовка."),
            ("СЕКТОР 04", "ЦИТАДЕЛЬ", "Подступы к Цитадели", "Пылевой шторм  ·  Джаггернаут",
             "Буря идёт по пятам, на подступах ждёт Тяжёлый Джаггернаут рейдеров.\n\n<b>Врата-01</b> закроются по Протоколу «Закат». Одна машина — одна попытка.")
        };

        var sectors = new EvacuationMapSector[4];
        for (int i = 0; i < 4; i++)
        {
            float u0 = SectorBounds[i], u1 = SectorBounds[i + 1];
            var zone = UiRect("Sector_" + (i + 1), canvasRt, new Vector2(u0, 0.03f), new Vector2(u1, 0.97f));
            var highlight = zone.gameObject.AddComponent<Image>();
            highlight.color = new Color(0.95f, 0.55f, 0.12f, 0.2f);
            highlight.raycastTarget = false;
            var label = Label(zone, "Label", font, $"{sectorData[i].Item1}\n<size=50>{sectorData[i].Item2}</size>", 32, Ink, TextAnchor.UpperCenter);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(0f, -28f); lr.sizeDelta = new Vector2(0f, 130f);

            var sector = zone.gameObject.AddComponent<EvacuationMapSector>();
            var sso = new SerializedObject(sector);
            sso.FindProperty("title").stringValue = sectorData[i].Item3;
            sso.FindProperty("conditions").stringValue = sectorData[i].Item4;
            sso.FindProperty("body").stringValue = sectorData[i].Item5;
            sso.FindProperty("hotspot").objectReferenceValue = zone;
            sso.FindProperty("highlight").objectReferenceValue = highlight;
            sso.FindProperty("label").objectReferenceValue = label;
            sso.ApplyModifiedPropertiesWithoutUndo();
            sectors[i] = sector;
        }

        // Маршрут красным маркером поверх карандашной трассы на бумаге.
        var segments = new Image[Route.Length - 1];
        for (int i = 0; i < segments.Length; i++)
        {
            Vector2 a = Px(Route[i]), b = Px(Route[i + 1]);
            var seg = new GameObject("Route_" + (i + 1), typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)seg.transform;
            rt.SetParent(canvasRt, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(Vector2.Distance(a, b), 16f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var img = seg.GetComponent<Image>();
            img.sprite = ink;
            img.color = RedInk;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.raycastTarget = false;
            segments[i] = img;
        }

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var here = Marker(canvasRt, "Mark_Here", Route[0], knob, 64f, new Color(0.8f, 0.1f, 0.05f, 0.45f), null, null, Vector2.zero);
        Marker(canvasRt, "Mark_Bunker", Route[0], knob, 34f, Ink, font, "БУНКЕР 07", new Vector2(0f, -46f));
        Marker(canvasRt, "Mark_STO1", Route[1], knob, 26f, Ink, font, "СТО «Северная»", new Vector2(0f, 40f));
        Marker(canvasRt, "Mark_Casino", new Vector2(0.44f, 0.17f), knob, 20f, new Color(0.55f, 0.35f, 0.05f, 0.9f), font, "казино «Фортуна»", new Vector2(0f, -36f));
        Marker(canvasRt, "Mark_STO2", Route[2], knob, 26f, Ink, font, "СТО «Каньон»", new Vector2(0f, -42f));
        Marker(canvasRt, "Mark_STO3", Route[3], knob, 26f, Ink, font, "СТО «Рубеж»", new Vector2(0f, -42f));
        var citadel = Marker(canvasRt, "Mark_Citadel", Route[4], knob, 44f, RedInk, font, "ЦИТАДЕЛЬ\n<size=26>ВРАТА-01</size>", new Vector2(0f, -72f));
        citadel.localRotation = Quaternion.Euler(0f, 0f, 45f);
        citadel.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, -45f);


        var title = Label(canvasRt, "Title", font, "ПЛАН ЭВАКУАЦИИ  ·  ПРОТОКОЛ «ЗАКАТ»", 40, Ink, TextAnchor.LowerLeft);
        Place((RectTransform)title.transform, new Vector2(0.03f, 0.02f), new Vector2(0.7f, 0.09f));
        var stamp = Label(canvasRt, "Stamp", font, "ШЛЮЗ ЗАКРЫВАЕТСЯ", 56, new Color(0.7f, 0.1f, 0.08f, 0.55f), TextAnchor.MiddleCenter);
        Place((RectTransform)stamp.transform, new Vector2(0.6f, 0.06f), new Vector2(0.97f, 0.2f));
        stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
        stamp.gameObject.AddComponent<Outline>().effectColor = new Color(0.7f, 0.1f, 0.08f, 0.35f);

        // Поза камеры над картой: смещена так, чтобы справа осталось место под карточку сектора.
        var view = new GameObject("Map_ViewPose").transform;
        view.SetParent(root, false);
        // Экранное «вправо» здесь = мировой -Z, поэтому точка прицела сдвинута к -Z: лист уезжает влево.
        Vector3 aim = center + Vector3.back * 0.17f + Vector3.right * 0.02f;
        view.position = aim + new Vector3(-0.42f, 0.78f, 0f);
        view.rotation = Quaternion.LookRotation(aim - view.position, Vector3.up);

        // Тёплая лампа над столом — карту видно и издалека.
        var lampGo = new GameObject("Map_Lamp", typeof(Light));
        lampGo.transform.SetParent(root, false);
        lampGo.transform.position = center + Vector3.up * 1.05f;
        var lamp = lampGo.GetComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.82f, 0.58f);
        lamp.intensity = 1.1f;
        lamp.range = 2.4f;
        lamp.shadows = LightShadows.None;

        var overlay = AuthorOverlay(font, out var index, out var titleText, out var conditions, out var body);

        var map = root.gameObject.AddComponent<BunkerEvacuationMap>();
        var mso = new SerializedObject(map);
        mso.FindProperty("viewPose").objectReferenceValue = view;
        mso.FindProperty("viewFieldOfView").floatValue = 50f;
        SetArray(mso.FindProperty("sectors"), sectors);
        SetArray(mso.FindProperty("routeSegments"), segments);
        mso.FindProperty("hereMarker").objectReferenceValue = here;
        mso.FindProperty("overlay").objectReferenceValue = overlay;
        mso.FindProperty("sectorIndexText").objectReferenceValue = index;
        mso.FindProperty("sectorTitleText").objectReferenceValue = titleText;
        mso.FindProperty("sectorConditionsText").objectReferenceValue = conditions;
        mso.FindProperty("sectorBodyText").objectReferenceValue = body;
        mso.FindProperty("mapLamp").objectReferenceValue = lamp;
        mso.FindProperty("paperOpenClip").objectReferenceValue = Clip("oc_book-01-12.wav");
        mso.FindProperty("paperCloseClip").objectReferenceValue = Clip("etw_3006_movement_paper-releae_04.wav");
        mso.FindProperty("sectorClip").objectReferenceValue = Clip("hw_marker_copics_033.wav");
        mso.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CanvasGroup AuthorOverlay(Font font, out Text index, out Text title, out Text conditions, out Text body)
    {
        var go = new GameObject("Evacuation_Map_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        go.transform.SetParent(GameObject.Find("Garage_SetDressing").transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        var rt = (RectTransform)go.transform;

        var card = UiRect("SectorCard", rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        card.pivot = new Vector2(1f, 0.5f);
        card.anchoredPosition = new Vector2(-56f, 30f);
        card.sizeDelta = new Vector2(470f, 470f);
        var bg = card.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.06f, 0.84f);
        bg.raycastTarget = false;
        var bar = UiRect("AccentBar", card, new Vector2(0f, 0f), new Vector2(0f, 1f));
        bar.pivot = new Vector2(0f, 0.5f);
        bar.sizeDelta = new Vector2(6f, 0f);
        bar.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.58f, 0.18f);

        index = Label(card, "Index", font, "СЕКТОР 01 / 04", 18, new Color(0.95f, 0.58f, 0.18f), TextAnchor.UpperLeft);
        Pin((RectTransform)index.transform, 30f, 26f, 26f);
        title = Label(card, "Title", font, "Пригородное шоссе", 38, new Color(0.96f, 0.94f, 0.9f), TextAnchor.UpperLeft);
        Pin((RectTransform)title.transform, 30f, 54f, 48f);
        conditions = Label(card, "Conditions", font, "Закат", 20, new Color(0.78f, 0.72f, 0.62f), TextAnchor.UpperLeft);
        Pin((RectTransform)conditions.transform, 30f, 108f, 28f);
        // Имя HeaderRule обязательно: иначе ExpeditionPresentation дорисует свою линию поверх заголовка.
        var divider = UiRect("HeaderRule", card, new Vector2(0f, 1f), new Vector2(1f, 1f));
        divider.pivot = new Vector2(0.5f, 1f);
        divider.offsetMin = new Vector2(30f, -150f); divider.offsetMax = new Vector2(-30f, -148f);
        divider.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        body = Label(card, "Body", font, "", 21, new Color(0.88f, 0.89f, 0.9f), TextAnchor.UpperLeft);
        var br = (RectTransform)body.transform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(30f, 26f); br.offsetMax = new Vector2(-28f, -168f);
        body.lineSpacing = 1.2f;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;

        var hints = Label(rt, "Hints", font, "[A] [D] или мышь — сектор        [E] / [Esc] — свернуть карту", 20, new Color(0.9f, 0.88f, 0.84f, 0.85f), TextAnchor.MiddleCenter);
        var hr = (RectTransform)hints.transform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f); hr.anchoredPosition = new Vector2(0f, 34f); hr.sizeDelta = new Vector2(0f, 34f);
        hints.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        go.SetActive(false);
        return group;
    }

    // ── Текстуры ────────────────────────────────────────────────────────────

    private static Texture2D EyelidTexture()
    {
        var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
        for (int y = 0; y < 256; y++)
        {
            float v = y / 255f; // 0 — внутренняя кромка (низ верхнего века)
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.3f));
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D InkLineTexture()
    {
        var tex = new Texture2D(256, 32, TextureFormat.RGBA32, false);
        for (int x = 0; x < 256; x++)
        for (int y = 0; y < 32; y++)
        {
            float d = Mathf.Abs(y - 15.5f) / 16f;
            float dash = (x % 64) < 40 ? 1f : 0f; // пунктир маркером
            float wobble = Mathf.PerlinNoise(x * 0.08f, y * 0.3f) * 0.25f;
            float a = Mathf.Clamp01((0.75f - d - wobble * 0.5f) * 4f) * dash;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D MapTexture()
    {
        var tex = new Texture2D(MapW, MapH, TextureFormat.RGB24, true);
        var px = new Color[MapW * MapH];
        Color paper = new Color(0.86f, 0.8f, 0.66f);
        Color[] tints =
        {
            new Color(0.62f, 0.68f, 0.58f), // пригород
            new Color(0.9f, 0.76f, 0.5f),   // пустошь
            new Color(0.5f, 0.55f, 0.42f),  // промзона
            new Color(0.6f, 0.6f, 0.62f)    // буря
        };

        for (int y = 0; y < MapH; y++)
        for (int x = 0; x < MapW; x++)
        {
            float u = x / (float)MapW, v = y / (float)MapH;
            float grain = Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.05f + Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.08f;
            Color col = paper * (0.95f + grain);

            // Зоны секторов с неровными краями.
            float wob = (Mathf.PerlinNoise(v * 6f, 3.1f) - 0.5f) * 0.035f;
            int sector = 3;
            for (int s = 0; s < 4; s++) if (u + wob < SectorBounds[s + 1]) { sector = s; break; }
            col = Color.Lerp(col, tints[sector] * (0.95f + grain), 0.32f);

            // Рельеф по секторам.
            if (sector == 0 && Mathf.PerlinNoise(x * 0.004f, y * 0.004f) > 0.45f && ((x / 26) % 2 == 0) && ((y / 22) % 2 == 0))
                col *= 0.9f; // кварталы
            if (sector == 1 && Mathf.Abs(Mathf.Sin((x * 0.6f + y * 1.6f + Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 120f) * 0.06f)) < 0.07f)
                col *= 0.92f; // дюны
            if (sector == 2 && Mathf.PerlinNoise(x * 0.02f, y * 0.02f) > 0.66f && (x % 40 < 28) && (y % 34 < 24))
                col *= 0.82f; // цеха
            if (sector == 3 && ((x + y) % 22 < 2))
                col *= 0.88f; // штриховка бури

            // Горизонтали.
            float h = Mathf.PerlinNoise(x * 0.006f + 10f, y * 0.006f + 4f) * 12f;
            if (Mathf.Abs(h - Mathf.Round(h)) < 0.025f) col *= 0.9f;

            // Карандашные границы секторов.
            for (int s = 1; s < 4; s++)
                if (Mathf.Abs(u + wob - SectorBounds[s]) < 0.0016f && (y / 14) % 2 == 0) col = Color.Lerp(col, (Color)Ink, 0.55f);

            // Сетка квадратов, сгибы листа и затемнение краёв.
            if (x % 128 == 0 || y % 128 == 0) col *= 0.95f;
            float foldU = Mathf.Abs(u - 0.5f), foldV = Mathf.Abs(v - 0.5f);
            if (foldU < 0.002f || foldV < 0.003f) col *= 0.84f;
            else if (foldU < 0.006f || foldV < 0.007f) col *= 1.04f;
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            col *= Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 1f, edge / 0.06f));

            // Кофейное кольцо.
            float ring = Vector2.Distance(new Vector2(x, y), new Vector2(MapW * 0.86f, MapH * 0.3f));
            if (Mathf.Abs(ring - 70f) < 4f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 3f) col *= 0.86f;

            col.a = 1f;
            px[y * MapW + x] = col;
        }

        // Дороги: автострада по маршруту и ответвления.
        for (int i = 0; i < Route.Length - 1; i++) Road(px, Px(Route[i]), Px(Route[i + 1]), 5f, 0.55f, i * 7);
        Road(px, Px(new Vector2(0.02f, 0.5f)), Px(Route[1]), 3f, 0.7f, 21);
        Road(px, Px(Route[2]), Px(new Vector2(0.44f, 0.17f)), 3f, 0.7f, 33);
        Road(px, Px(Route[2]), Px(new Vector2(0.6f, 0.95f)), 3f, 0.72f, 41);
        Road(px, Px(Route[3]), Px(new Vector2(0.83f, 0.12f)), 3f, 0.72f, 55);

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Road(Color[] px, Vector2 a, Vector2 b, float width, float darken, int seed)
    {
        float len = Vector2.Distance(a, b);
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized;
        for (float t = 0; t <= len; t += 0.5f)
        {
            float k = t / len;
            Vector2 p = Vector2.Lerp(a, b, k) + n * ((Mathf.PerlinNoise(k * 4f, seed) - 0.5f) * 40f * Mathf.Sin(k * Mathf.PI));
            for (int dx = -(int)width; dx <= width; dx++)
            for (int dy = -(int)width; dy <= width; dy++)
            {
                if (dx * dx + dy * dy > width * width) continue;
                int x = (int)p.x + dx, y = (int)p.y + dy;
                if (x < 0 || y < 0 || x >= MapW || y >= MapH) continue;
                px[y * MapW + x] = px[y * MapW + x] * Mathf.Lerp(darken, 1f, (dx * dx + dy * dy) / (width * width));
            }
        }
    }

    // ── Утилиты ─────────────────────────────────────────────────────────────

    private static string SaveTexture(string file, Texture2D tex, bool sprite)
    {
        string path = ArtDir + "/" + file;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = sprite;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = !sprite;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return path;
    }

    private static Vector2 Px(Vector2 uv) => new Vector2(uv.x * MapW, uv.y * MapH);

    private static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        return t;
    }

    private static Bounds RendererBounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static void Move(string path, Vector3 position)
    {
        // Одноимённых предметов бывает несколько — раскладываем все рядом, со сдвигом.
        var parts = path.Split('/');
        var parent = GameObject.Find(parts[0])?.transform;
        for (int i = 1; parent != null && i < parts.Length - 1; i++) parent = parent.Find(parts[i]);
        string name = parts[parts.Length - 1];
        int moved = 0;
        if (parent != null)
            foreach (Transform t in parent)
                if (t.name == name && Vector3.Distance(t.position, position) < 1f)
                    t.position = position + new Vector3(-0.16f, 0f, 0f) * moved++;
        if (moved == 0) Debug.LogWarning("[BunkerScenesAuthoring] Не найден " + path);
    }

    private static AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + file);

    private static RectTransform UiRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Pin(RectTransform rt, float left, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-24f, -top);
    }

    private static Text Label(Transform parent, string name, Font font, string text, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static RectTransform Marker(RectTransform canvas, string name, Vector2 uv, Sprite sprite, float size, Color color, Font font, string caption, Vector2 captionOffset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.anchoredPosition = Px(uv);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (font != null && caption != null)
        {
            var label = Label(rt, "Caption", font, caption, 31, Ink, TextAnchor.MiddleCenter);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = captionOffset;
            lr.sizeDelta = new Vector2(320f, 70f);
        }
        return rt;
    }

    private static void SetArray<T>(SerializedProperty prop, T[] items) where T : Object
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
