using System;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class RoadsideSupplyAuthoring
    {
        const string Folder = "Assets/Content/RoadsideSupply";
        static readonly string[] Sites = { "Encounter_LastGasStation", "Encounter_BirchCamp", "Encounter_FreightYard" };
        static readonly string[] Titles = { "СХРОН ПАТРУЛЯ", "АПТЕЧНЫЙ РЕЗЕРВ", "РЕМОНТНЫЙ РЕЗЕРВ" };
        static readonly string[] Contents = { "Патроны 28 · Аптечка 1 · Болты 4", "Аптечки 2 · Патроны 14 · Батарейка 1", "Болты 8 · Ключ 1 · Патроны 7" };
        static readonly float[] Seconds = { 10, 6, 14 };

        public static string Build()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop play and save current edits first.");
            Directory.CreateDirectory("Temp/RoadsideSupplyBackup");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var steel = Material("Steel", new Color(.16f, .22f, .23f));
            var stripe = Material("Stripe", new Color(.76f, .59f, .24f));
            var paper = Material("Paper", new Color(.31f, .37f, .33f));
            int created = 0;
            foreach (var path in new[] { "Assets/Scenes/Stage1_Outskirts.unity", "Assets/Scenes/Journey/Route01_World.unity" })
            {
                string backup = "Temp/RoadsideSupplyBackup/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
                var scene = EditorSceneManager.OpenScene(path);
                var world = scene.GetRootGameObjects().Single(g => g.name == "Stage_World");
                bool active = world.activeSelf;
                world.SetActive(true);
                for (int i = 0; i < Sites.Length; i++)
                {
                    string id = "route0/supply/" + i;
                    var existing = world.GetComponentsInChildren<RoadsideSupplyCache>(true).SingleOrDefault(c => c.Id == id);
                    if (existing != null) { Enhance(existing, i); UpdateNotice(world, i); continue; }
                    var encounter = world.GetComponentsInChildren<RoadsideEncounter>(true).Single(e => e.name == Sites[i] && ActiveWithin(e.transform, world.transform));
                    // Keep the cache on the encounter's already baked walkable court.
                    Vector3 desired = encounter.transform.position + new Vector3(3, 0, -5);
                    desired.y = encounter.GetComponentsInChildren<EncounterZombie>(true).First().transform.position.y;
                    if (!NavMesh.SamplePosition(desired, out var hit, 7, NavMesh.AllAreas))
                        throw new InvalidOperationException("No walkable cache position: " + Sites[i]);
                    var root = new GameObject("SupplyCache_" + i);
                    root.transform.SetParent(world.transform, false);
                    root.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0, i == 0 ? 145 : 180, 0));
                    Undo.RegisterCreatedObjectUndo(root, "Add roadside supply cache");
                    var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .5f, 0); collider.size = new Vector3(1.6f, 1, .8f);
                    Box(root.transform, "Steel_Chest", new Vector3(0, .45f, 0), new Vector3(1.6f, .85f, .8f), steel);
                    Box(root.transform, "Lid", new Vector3(0, .92f, 0), new Vector3(1.65f, .12f, .85f), stripe);
                    foreach (int side in new[] { -1, 1 })
                        Box(root.transform, "Reinforced_Strap", new Vector3(side * .53f, .47f, -.415f), new Vector3(.1f, .8f, .025f), stripe);
                    Box(root.transform, "Status_Board", new Vector3(0, 1.5f, .22f), new Vector3(3.6f, .95f, .08f), paper);
                    var label = new GameObject("Cache_Status").AddComponent<TextMesh>();
                    label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 1.5f, .165f);
                    label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.fontSize = 64; label.characterSize = .052f;
                    var cache = root.AddComponent<RoadsideSupplyCache>();
                    cache.Configure(id, Titles[i], Contents[i], Seconds[i], Supplies(i), encounter.GetComponentsInChildren<EncounterZombie>(true), label);
                    root.AddComponent<JourneyPersistentObject>().Configure(id);
                    Enhance(cache, i); UpdateNotice(world, i);
                    created++;
                }
                world.SetActive(active);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            return "Created " + created + " cache instances across entry and streamed world; existing instances preserved.";
        }
        static bool ActiveWithin(Transform child, Transform world)
        {
            for (; child != null && child != world; child = child.parent)
                if (!child.gameObject.activeSelf) return false;
            return true;
        }
        static void Enhance(RoadsideSupplyCache cache, int index)
        {
            var root = cache.transform;
            var obstacle = cache.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = cache.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = new Vector3(0, .5f, 0);
            obstacle.size = new Vector3(1.6f, 1, .8f); obstacle.carving = true; obstacle.carveOnlyStationary = true;
            var accent = Material("Accent_" + index, index == 0 ? new Color(.23f, .34f, .39f) :
                index == 1 ? new Color(.73f, .75f, .64f) : new Color(.72f, .42f, .16f));
            var board = root.Find("Status_Board"); board.localPosition = new Vector3(0, 2.35f, .65f);
            board.localScale = new Vector3(2.4f, 1.05f, .08f);
            var label = root.GetComponentInChildren<TextMesh>(); label.characterSize = .019f;
            label.transform.localPosition = new Vector3(0, 2.35f, .595f);
            var data = new SerializedObject(cache); data.FindProperty("title").stringValue = Titles[index]; data.ApplyModifiedPropertiesWithoutUndo();
            var hinge = root.Find("Lid_Hinge");
            if (hinge == null)
            {
                hinge = new GameObject("Lid_Hinge").transform; hinge.SetParent(root, false); hinge.localPosition = new Vector3(0, .92f, .425f);
                var lid = root.Find("Lid"); lid.SetParent(hinge, true); lid.GetComponent<Renderer>().sharedMaterial = accent;
            }
            cache.ConfigureLid(hinge);
            if (root.Find("Sign_Post_-1") == null)
                foreach (int side in new[] { -1, 1 }) Box(root, "Sign_Post_" + side,
                    new Vector3(side * .78f, 1.05f, .65f), new Vector3(.07f, 1.8f, .07f), Material("Steel", new Color(.16f, .22f, .23f)));
            foreach (int side in new[] { -1, 1 })
            {
                var post = root.Find("Sign_Post_" + side);
                post.localPosition = new Vector3(side * .78f, 1.3f, .65f); post.localScale = new Vector3(.07f, 2.6f, .07f);
            }
            if (root.Find("Pallet_Slat_0") == null)
            {
                var wood = Material("Wood", new Color(.35f, .27f, .18f));
                for (int n = 0; n < 6; n++) Box(root, "Pallet_Slat_" + n, new Vector3(-.85f + n * .34f, .015f, 0), new Vector3(.25f, .06f, 1.2f), wood);
                if (index == 1)
                {
                    var green = Material("Medical", new Color(.22f, .43f, .31f));
                    Box(root, "Medical_Horizontal", new Vector3(0, .47f, -.43f), new Vector3(.48f, .13f, .02f), green);
                    Box(root, "Medical_Vertical", new Vector3(0, .47f, -.445f), new Vector3(.13f, .48f, .02f), green);
                }
            }
            EditorUtility.SetDirty(cache); EditorUtility.SetDirty(label);
        }
        static void UpdateNotice(GameObject world, int index)
        {
            string title = index == 0 ? "ПОСЛЕДНЯЯ АЗС" : index == 1 ? "ЛЕСНОЙ ЛАГЕРЬ" : "ГРУЗОВОЙ ДВОР";
            var notice = world.GetComponentsInChildren<JourneyStop>(true).Single(s => s.Title == title);
            var data = new SerializedObject(notice);
            string directions = data.FindProperty("directions").stringValue;
            if (!directions.Contains("Время вскрытия")) directions += $"\n{Titles[index]}: {Contents[index]}. Время вскрытия {Seconds[index]:0} с; шум привлекает заражённых.";
            notice.Configure(title, Contents[index] + $" · Тайник {Seconds[index]:0} с", directions,
                data.FindProperty("roadDistance").floatValue, data.FindProperty("side").floatValue, data.FindProperty("safe").boolValue);
            EditorUtility.SetDirty(notice);
        }
        static RoadsideSupplyCache.Supply Item(string id, string title, int count) => new RoadsideSupplyCache.Supply { id = id, title = title, count = count };
        static RoadsideSupplyCache.Supply[] Supplies(int i)
        {
            if (i == 0) return new[] { Item("ammo_9mm", "Патроны 9мм", 28), Item("first_aid_medkit", "Аптечка", 1), Item("bolt_repair", "Ремонтные болты", 4) };
            if (i == 1) return new[] { Item("first_aid_medkit", "Аптечка", 2), Item("ammo_9mm", "Патроны 9мм", 14), Item("battery_small", "Батарейка", 1) };
            return new[] { Item("bolt_repair", "Ремонтные болты", 8), Item("wrench_tool", "Гаечный ключ", 1), Item("ammo_9mm", "Патроны 9мм", 7) };
        }
        static Material Material(string name, Color color)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Standard shader unavailable.");
            material = new Material(shader) { name = "Supply_" + name, color = color }; material.SetFloat("_Glossiness", .15f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = size; obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
    }
}
