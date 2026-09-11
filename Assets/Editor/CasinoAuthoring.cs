using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Размещение придорожных казино в сценах этапов. Никакой рантайм-генерации:
    /// команда один раз создаёт обычные объекты сцены (арка, будка, слот-машина, неон),
    /// панель казино с рядом ставок внутри UI_Canvas и элитные засады на подступах,
    /// после чего всё можно править руками и сохранить.
    ///
    /// Пункты ставятся на авторской трассе у чанков с порядком 10 и 20 (≈1000 м и ≈2000 м),
    /// засады — на чанках 9 и 19, то есть за сто метров до каждого казино.
    /// </summary>
    public static class CasinoAuthoring
    {
        const string MaterialFolder = "Assets/Content/Casino";
        static readonly int[] StopChunkOrders = { 10, 20 };
        static readonly string[] StopNames = { "Казино «Фортуна»", "Казино «Последний шанс»" };

        static readonly string[] StageScenes =
        {
            "Assets/Scenes/Stage1_Outskirts.unity",
            "Assets/Scenes/Stage2_Wasteland.unity",
            "Assets/Scenes/Stage3_Industrial.unity",
            "Assets/Scenes/Stage4_Citadel.unity"
        };

        [MenuItem("RogueDrive/Казино/Разместить пункты казино в открытой сцене", priority = 40)]
        public static void PlaceStopsInOpenScene()
        {
            int placed = PlaceStops();
            Debug.Log($"[CasinoAuthoring] Размещено пунктов казино: {placed}.");
        }

        [MenuItem("RogueDrive/Казино/Создать панель казино в UI_Canvas", priority = 41)]
        public static void BuildPanelInOpenScene()
        {
            bool created = BuildPanel();
            bool upgraded = EnsureBetRow();
            Debug.Log(created ? "[CasinoAuthoring] Панель казино создана."
                    : upgraded ? "[CasinoAuthoring] В панель казино добавлен ряд ставок."
                    : "[CasinoAuthoring] Панель казино уже есть или не найден Canvas.");
        }

        [MenuItem("RogueDrive/Казино/Разместить элитные засады перед казино", priority = 42)]
        public static void PlaceAmbushesInOpenScene()
        {
            int placed = PlaceAmbushes();
            Debug.Log($"[CasinoAuthoring] Размещено элитных засад: {placed}.");
        }

        [MenuItem("RogueDrive/Казино/Подготовить все сцены этапов (пункты + панель + засады)", priority = 43)]
        public static void PrepareAllStageScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string previous = EditorSceneManager.GetActiveScene().path;

            foreach (string path in StageScenes)
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[CasinoAuthoring] Сцена не найдена: {path}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int stops = PlaceStops();
                bool panel = BuildPanel();
                bool bets = EnsureBetRow();
                int ambushes = PlaceAmbushes();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CasinoAuthoring] {Path.GetFileName(path)}: пунктов добавлено {stops}, " +
                          $"панель {(panel ? "создана" : "уже была")}, ряд ставок {(bets ? "добавлен" : "уже был")}, " +
                          $"засад добавлено {ambushes}.");
            }

            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        #region Stops

        static int PlaceStops()
        {
            var chunks = Object.FindObjectsByType<BakedMapChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (chunks.Length == 0)
            {
                Debug.LogWarning("[CasinoAuthoring] В сцене нет авторской трассы (BakedMapChunk) — пункты не размещены.");
                return 0;
            }

            var existing = Object.FindObjectsByType<BuffCasinoStop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var materials = EnsureMaterials();

            int placed = 0;
            for (int i = 0; i < StopChunkOrders.Length; i++)
            {
                int order = StopChunkOrders[i];
                int stopIndex = i + 1;

                if (existing.Any(s => s != null && s.StopIndex == stopIndex))
                    continue;

                BakedMapChunk chunk = chunks.FirstOrDefault(c => c != null && c.Order == order);
                if (chunk == null)
                {
                    Debug.LogWarning($"[CasinoAuthoring] Не найден чанк с порядком {order} — пункт {stopIndex} пропущен.");
                    continue;
                }

                Transform chunkT = chunk.transform;
                var stopObj = new GameObject($"CasinoStop_{stopIndex}");
                Undo.RegisterCreatedObjectUndo(stopObj, "Place casino stop");
                stopObj.transform.SetParent(chunkT, true);
                stopObj.transform.position = chunkT.position + chunkT.forward * 30f;
                stopObj.transform.rotation = chunkT.rotation;

                var col = stopObj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(24f, 8f, 5f);
                col.center = new Vector3(0f, 4f, 0f);

                var stop = stopObj.AddComponent<BuffCasinoStop>();
                stop.Configure(stopIndex, StopNames[Mathf.Clamp(i, 0, StopNames.Length - 1)]);

                BuildStopVisual(stopObj.transform, materials, out Light[] lights, out Renderer[] banners);
                stop.BindVisuals(lights, banners);
                EditorUtility.SetDirty(stop);
                placed++;
            }

            if (placed > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return placed;
        }

        sealed class Mats
        {
            public Material Steel, Booth, Gold, NeonMagenta, NeonCyan;
        }

        static Mats EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Content"))
                AssetDatabase.CreateFolder("Assets", "Content");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Content", "Casino");

            return new Mats
            {
                Steel = EnsureMaterial("Mat_Casino_Steel", false, new Color(0.22f, 0.2f, 0.24f)),
                Booth = EnsureMaterial("Mat_Casino_Booth", false, new Color(0.55f, 0.12f, 0.2f)),
                Gold = EnsureMaterial("Mat_Casino_Gold", false, new Color(0.85f, 0.65f, 0.15f)),
                NeonMagenta = EnsureMaterial("Mat_Casino_NeonMagenta", true, new Color(1f, 0.25f, 0.85f)),
                NeonCyan = EnsureMaterial("Mat_Casino_NeonCyan", true, new Color(0.25f, 0.9f, 1f))
            };
        }

        static Material EnsureMaterial(string name, bool unlit, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = unlit
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"))
                : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void BuildStopVisual(Transform root, Mats m, out Light[] lights, out Renderer[] banners)
        {
            Transform visual = new GameObject("CasinoStopVisual").transform;
            visual.SetParent(root, false);

            // 1. Арка над дорогой
            Box(visual, "Arch_Pillar_L", new Vector3(-11f, 4f, 0f), new Vector3(1.2f, 8f, 1.2f), m.Steel);
            Box(visual, "Arch_Pillar_R", new Vector3(11f, 4f, 0f), new Vector3(1.2f, 8f, 1.2f), m.Steel);
            Box(visual, "Arch_Crossbar", new Vector3(0f, 7.6f, 0f), new Vector3(23.5f, 1f, 1.4f), m.Steel);
            var banner = Box(visual, "Arch_NeonBanner", new Vector3(0f, 7.6f, -0.8f), new Vector3(15f, 0.7f, 0.12f), m.NeonMagenta);
            var bannerBack = Box(visual, "Arch_NeonBanner_Back", new Vector3(0f, 7.6f, 0.8f), new Vector3(15f, 0.7f, 0.12f), m.NeonMagenta);

            // Гирлянда лампочек на перекладине
            for (int i = -5; i <= 5; i++)
                Box(visual, $"Bulb_{i + 5}", new Vector3(i * 2f, 8.35f, 0f), new Vector3(0.35f, 0.35f, 0.35f), (i % 2 == 0) ? m.NeonMagenta : m.NeonCyan);

            // 2. Придорожная будка-казино справа от дороги
            Transform booth = new GameObject("Casino_Booth").transform;
            booth.SetParent(visual, false);
            booth.localPosition = new Vector3(15.5f, 0f, 4f);

            Box(booth, "Booth_Body", new Vector3(0f, 1.6f, 0f), new Vector3(4.2f, 3.2f, 3.6f), m.Booth);
            Box(booth, "Booth_Roof", new Vector3(0f, 3.35f, 0f), new Vector3(4.8f, 0.3f, 4.2f), m.Gold);
            var boothSign = Box(booth, "Booth_Sign", new Vector3(-2.15f, 2.4f, 0f), new Vector3(0.12f, 0.9f, 3f), m.NeonMagenta);
            Box(booth, "Booth_Window", new Vector3(-2.12f, 1.3f, 0f), new Vector3(0.08f, 1.1f, 2.4f), m.NeonCyan);

            // Слот-машина у входа
            Transform slot = new GameObject("Slot_Machine").transform;
            slot.SetParent(booth, false);
            slot.localPosition = new Vector3(-3.2f, 0f, -1.2f);
            Box(slot, "Slot_Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 0.7f), m.Gold);
            Box(slot, "Slot_Reel_1", new Vector3(-0.22f, 1.25f, -0.37f), new Vector3(0.18f, 0.3f, 0.05f), m.NeonCyan);
            Box(slot, "Slot_Reel_2", new Vector3(0f, 1.25f, -0.37f), new Vector3(0.18f, 0.3f, 0.05f), m.NeonMagenta);
            Box(slot, "Slot_Reel_3", new Vector3(0.22f, 1.25f, -0.37f), new Vector3(0.18f, 0.3f, 0.05f), m.NeonCyan);
            Box(slot, "Slot_Lever", new Vector3(0.55f, 1.5f, 0f), new Vector3(0.08f, 0.8f, 0.08f), m.Steel);

            // 3. Неоновая подсветка
            Light l1 = PointLight(visual, "Neon_Light_Arch", new Vector3(0f, 7f, -2f), new Color(1f, 0.3f, 0.85f), 18f);
            Light l2 = PointLight(visual, "Neon_Light_Booth", new Vector3(13f, 3f, 3f), new Color(0.3f, 0.9f, 1f), 12f);

            lights = new[] { l1, l2 };
            banners = new[] { banner.GetComponent<Renderer>(), bannerBack.GetComponent<Renderer>(), boothSign.GetComponent<Renderer>() };
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            var col = obj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 2.5f;
            l.range = range;
            return l;
        }

        #endregion

        #region Elite ambushes

        static readonly int[] AmbushChunkOrders = { 9, 19 };
        static readonly string[] AmbushNames = { "Охрана «Фортуны»", "Охрана «Последнего шанса»" };

        const string GemPrefabPath = "Assets/Prefabs/ExperienceGem.prefab";
        const string CoinPrefabPath = "Assets/Prefabs/CoinPickup.prefab";
        const string KamikazeModelPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Evil_Clown.prefab";
        const string JuggernautModelPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Pumpkinhead.prefab";
        const string LeaderModelPath = "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
        const string KamikazeWeaponPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Сlown hammer.prefab";
        const string JuggernautWeaponPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Pitchfork.prefab";

        enum EliteKind { Kamikaze, Juggernaut, PackLeader }

        static int PlaceAmbushes()
        {
            var chunks = Object.FindObjectsByType<BakedMapChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (chunks.Length == 0)
            {
                Debug.LogWarning("[CasinoAuthoring] В сцене нет авторской трассы (BakedMapChunk) — засады не размещены.");
                return 0;
            }

            var existing = Object.FindObjectsByType<EliteAmbushZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int stage = StageIndexFromScene();

            int placed = 0;
            for (int i = 0; i < AmbushChunkOrders.Length; i++)
            {
                int ambushIndex = i + 1;
                if (existing.Any(z => z != null && z.AmbushIndex == ambushIndex))
                    continue;

                BakedMapChunk chunk = chunks.FirstOrDefault(c => c != null && c.Order == AmbushChunkOrders[i]);
                if (chunk == null)
                {
                    Debug.LogWarning($"[CasinoAuthoring] Не найден чанк с порядком {AmbushChunkOrders[i]} — засада {ambushIndex} пропущена.");
                    continue;
                }

                Transform chunkT = chunk.transform;
                var zoneObj = new GameObject($"EliteAmbush_{ambushIndex}");
                Undo.RegisterCreatedObjectUndo(zoneObj, "Place elite ambush");
                zoneObj.transform.SetParent(chunkT, true);
                zoneObj.transform.position = chunkT.position + chunkT.forward * 15f;
                zoneObj.transform.rotation = chunkT.rotation;

                var col = zoneObj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(26f, 8f, 4f);
                col.center = new Vector3(0f, 4f, 0f);

                var zone = zoneObj.AddComponent<EliteAmbushZone>();
                zone.Configure(ambushIndex, AmbushNames[Mathf.Clamp(i, 0, AmbushNames.Length - 1)]);

                Transform squad = new GameObject("AmbushSquad").transform;
                squad.SetParent(zoneObj.transform, false);

                var elites = new List<EnemyBase>();
                foreach ((EliteKind kind, Vector3 offset) in SquadLayout(stage, ambushIndex))
                    elites.Add(CreateElite(squad, kind, offset));

                zone.BindElites(elites.ToArray());
                EditorUtility.SetDirty(zone);
                placed++;
            }

            if (placed > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return placed;
        }

        /// <summary>Состав отряда растёт с номером этапа: лидер стаи и камикадзе всегда, громила с третьего этапа.</summary>
        static IEnumerable<(EliteKind, Vector3)> SquadLayout(int stage, int ambushIndex)
        {
            // Локальные смещения относительно зоны: x — поперёк дороги, z — вперёд по ходу движения.
            yield return (EliteKind.PackLeader, new Vector3(0f, 0.5f, 48f));
            yield return (EliteKind.Kamikaze, new Vector3(-4.5f, 0.5f, 40f));
            yield return (EliteKind.Kamikaze, new Vector3(4.5f, 0.5f, 42f));

            if (stage >= 2 || ambushIndex >= 2)
                yield return (EliteKind.Kamikaze, new Vector3(-7f, 0.5f, 56f));

            if (stage >= 3)
                yield return (EliteKind.Juggernaut, new Vector3(2.5f, 0.5f, 62f));

            if (stage >= 4)
                yield return (EliteKind.Juggernaut, new Vector3(-3f, 0.5f, 70f));
        }

        static EnemyBase CreateElite(Transform squad, EliteKind kind, Vector3 localOffset)
        {
            GameObject obj = GameObject.CreatePrimitive(kind == EliteKind.PackLeader ? PrimitiveType.Capsule : PrimitiveType.Cube);
            obj.transform.SetParent(squad, false);
            obj.transform.localPosition = localOffset;
            obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            EnemyBase enemy;
            string modelPath, weaponPath;
            switch (kind)
            {
                case EliteKind.Kamikaze:
                    obj.name = "Elite_Kamikaze";
                    enemy = obj.AddComponent<EliteKamikaze>();
                    modelPath = KamikazeModelPath; weaponPath = KamikazeWeaponPath;
                    break;
                case EliteKind.Juggernaut:
                    obj.name = "Elite_JuggernautMinion";
                    enemy = obj.AddComponent<EliteJuggernautMinion>();
                    modelPath = JuggernautModelPath; weaponPath = JuggernautWeaponPath;
                    break;
                default:
                    obj.name = "Elite_PackLeader";
                    enemy = obj.AddComponent<ElitePackLeader>();
                    modelPath = LeaderModelPath; weaponPath = null;
                    break;
            }

            // Ссылки на префабы выпадения и модель задаются в сцене, чтобы элита работала и в сборке
            var so = new SerializedObject(enemy);
            SetRef(so, "xpGemPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(GemPrefabPath));
            SetRef(so, "coinPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath));
            SetRef(so, "visualModelPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            if (!string.IsNullOrEmpty(weaponPath))
                SetRef(so, "weaponPropPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath));
            so.ApplyModifiedPropertiesWithoutUndo();

            // Отряд ждёт выключенным: зона включит его при въезде машины
            obj.SetActive(false);
            return enemy;
        }

        static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null && value != null) p.objectReferenceValue = value;
        }

        static int StageIndexFromScene()
        {
            string name = EditorSceneManager.GetActiveScene().name ?? string.Empty;
            for (int i = 1; i <= 4; i++)
                if (name.StartsWith("Stage" + i)) return i;
            return 1;
        }

        #endregion

        #region UI panel

        static bool BuildPanel()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[CasinoAuthoring] В сцене нет Canvas — панель казино не создана.");
                return false;
            }

            BuffCasinoView view = FindView(canvas);
            if (view != null && view.HasSceneUI)
                return false;

            if (view == null) view = Undo.AddComponent<BuffCasinoView>(canvas.gameObject);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Размеры под reference resolution 1280x720 (как остальные панели сцены)
            var root = new GameObject("CasinoPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create casino panel");
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            Stretch(root.GetComponent<RectTransform>());

            Image dim = Img(root.transform, "Dim", new Color(0.02f, 0.01f, 0.04f, 0.82f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image panel = Img(root.transform, "Panel", new Color(0.85f, 0.65f, 0.15f, 1f));
            PlaceRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(620f, 600f));
            Image inner = Img(panel.transform, "Inner", new Color(0.08f, 0.05f, 0.1f, 1f));
            Stretch(inner.rectTransform, 3f);

            Text title = Txt(panel.transform, "Title", font, 30, FontStyle.Bold, new Color(1f, 0.3f, 0.85f), "ПРИДОРОЖНОЕ КАЗИНО");
            PlaceRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(590f, 40f));

            Text subtitle = Txt(panel.transform, "Subtitle", font, 16, FontStyle.Normal, new Color(0.85f, 0.65f, 0.15f), "ЖЕТОНОВ: 0");
            PlaceRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(590f, 24f));

            // Барабан
            Image reelBg = Img(panel.transform, "ReelBg", new Color(0.03f, 0.02f, 0.05f, 1f));
            PlaceRect(reelBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(540f, 140f));

            Text prev = Txt(reelBg.transform, "ReelPrev", font, 16, FontStyle.Normal, new Color(0.55f, 0.55f, 0.6f, 0.7f), "");
            PlaceRect(prev.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(520f, 26f));

            Image frame = Img(reelBg.transform, "ReelFrame", new Color(0.85f, 0.65f, 0.15f, 0.9f));
            PlaceRect(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(530f, 48f));
            Image frameInner = Img(frame.transform, "ReelFrameInner", new Color(0.1f, 0.06f, 0.12f, 1f));
            Stretch(frameInner.rectTransform, 2f);

            Text current = Txt(reelBg.transform, "ReelCurrent", font, 26, FontStyle.Bold, Color.white, "· · ·");
            PlaceRect(current.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 44f));

            Text next = Txt(reelBg.transform, "ReelNext", font, 16, FontStyle.Normal, new Color(0.55f, 0.55f, 0.6f, 0.7f), "");
            PlaceRect(next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(520f, 26f));

            // Результат
            Image result = Img(panel.transform, "ResultPanel", new Color(0.15f, 0.1f, 0.2f, 0.92f));
            PlaceRect(result.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(560f, 140f));

            Text resTitle = Txt(result.transform, "ResultTitle", font, 21, FontStyle.Bold, Color.white, "ВЫПАЛО:");
            PlaceRect(resTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(540f, 30f));

            Text resBody = Txt(result.transform, "ResultBody", font, 15, FontStyle.Normal, new Color(0.9f, 0.9f, 0.92f), "");
            PlaceRect(resBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(540f, 96f));

            // Подвал и кнопка
            Text footer = Txt(panel.transform, "Footer", font, 15, FontStyle.Italic, new Color(0.75f, 0.75f, 0.8f), "");
            PlaceRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(590f, 22f));

            Image btnImg = Img(panel.transform, "ContinueButton", new Color(0.85f, 0.65f, 0.15f, 1f));
            PlaceRect(btnImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(280f, 40f));
            btnImg.raycastTarget = true;
            Button button = btnImg.gameObject.AddComponent<Button>();
            button.targetGraphic = btnImg;
            UnityEventTools.AddPersistentListener(button.onClick, view.OnContinuePressed);
            Text btnText = Txt(btnImg.transform, "Label", font, 18, FontStyle.Bold, new Color(0.08f, 0.05f, 0.1f), "ПРОДОЛЖИТЬ ПУТЬ");
            Stretch(btnText.rectTransform);

            view.BindSceneUI(root, title, subtitle, prev, current, next, resTitle, resBody, footer, frame, result, button);
            BuildBetRow(view, panel.transform, font);
            root.SetActive(false);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        /// <summary>
        /// Добавляет ряд ставок в уже существующую панель казино (сцены, подготовленные
        /// до появления ставок). Панель при этом подрастает по высоте.
        /// </summary>
        static bool EnsureBetRow()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null) return false;

            BuffCasinoView view = FindView(canvas);
            if (view == null || !view.HasSceneUI || view.HasBetUI) return false;

            Transform panel = view.PanelTransform;
            if (panel == null) return false;

            var rt = panel as RectTransform;
            if (rt != null && rt.sizeDelta.y < 600f)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 600f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildBetRow(view, panel, font);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        static void BuildBetRow(BuffCasinoView view, Transform panel, Font font)
        {
            var row = new GameObject("BetRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(row, "Create casino bet row");
            row.transform.SetParent(panel, false);
            PlaceRect(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(580f, 78f));

            Text hint = Txt(row.transform, "BetHint", font, 13, FontStyle.Italic, new Color(0.75f, 0.75f, 0.8f), "Ставка сужает пул, из которого выбирает слот-машина");
            PlaceRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(580f, 18f));

            string[] names = { "BetStandard", "BetRare", "BetSynergy" };
            Color[] colors =
            {
                new Color(0.85f, 0.65f, 0.15f, 1f),
                new Color(0.35f, 0.7f, 1f, 1f),
                new Color(0.95f, 0.45f, 1f, 1f)
            };
            var buttons = new Button[3];
            var labels = new Text[3];

            for (int i = 0; i < 3; i++)
            {
                Image img = Img(row.transform, names[i], colors[i]);
                PlaceRect(img.rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 190f, 0f), new Vector2(180f, 52f));
                img.raycastTarget = true;

                Button b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                var cb = b.colors;
                cb.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);
                b.colors = cb;

                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(b.onClick, view.OnBetStandardPressed); break;
                    case 1: UnityEventTools.AddPersistentListener(b.onClick, view.OnBetRarePressed); break;
                    default: UnityEventTools.AddPersistentListener(b.onClick, view.OnBetSynergyPressed); break;
                }

                Text label = Txt(img.transform, "Label", font, 14, FontStyle.Bold, new Color(0.08f, 0.05f, 0.1f),
                    i == 0 ? "[1] ОБЫЧНЫЙ\n1 жетон" : i == 1 ? "[2] РЕДКИЙ+\n2 жетона" : "[3] СИНЕРГИЯ\n3 жетона");
                Stretch(label.rectTransform);

                buttons[i] = b;
                labels[i] = label;
            }

            view.BindBetUI(row, buttons, labels, hint);
            row.SetActive(false);
        }

        static Canvas FindCanvas()
        {
            Canvas canvas = null;
            GameObject canvasObj = GameObject.Find("UI_Canvas");
            if (canvasObj != null) canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            return canvas;
        }

        static BuffCasinoView FindView(Canvas canvas)
        {
            BuffCasinoView view = canvas.GetComponent<BuffCasinoView>();
            if (view == null) view = Object.FindFirstObjectByType<BuffCasinoView>(FindObjectsInactive.Include);
            return view;
        }

        static Image Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(Transform parent, string name, Font font, int size, FontStyle style, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        static void PlaceRect(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        #endregion
    }
}
