using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using static SceneDressingKit;

// Оформление начала и конца маршрута: брошенный город с пунктом эвакуации и подступы к Цитадели
// с кордонами и воротами-крепостью. План (позиции по маршруту, высота земли, проверка свободного
// места) считается в исходной сцене, затем один и тот же план строится в ней и в стриминговой копии
// Route0N_World в локальных координатах корня мира. Повторный запуск пересобирает Journey_Dressing.
public static class JourneyDressingAuthoring
{
    const string RootName = "Journey_Dressing";
    const string Folder = "Assets/Art/Dressing";
    const string D = "Assets/Downloads/";
    const float RoadClear = 9.2f;   // проезжая часть ±8.5 м плюс запас: ближе к оси ничего не ставится

    static readonly string[] Cars =
    {
        D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 01.prefab", D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 02.prefab",
        D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 03.prefab", D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 05.prefab",
        D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 06.prefab", D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 08.prefab",
        D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01.prefab",
        D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color03.prefab",
        D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",
        D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car.prefab",
    };
    const string Truck = D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Truck_color01.prefab";
    const string Bus = D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color02.prefab";
    const string Container = D + "FREE Low Poly Shipping Container/Prefabs/Low Poly Shipping Container.prefab";
    const string ConcreteBlock = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/ConcreteBarriers/ConcreteBarrier_08_v2.prefab";
    const string SandBags = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/SandBagBarriers/SandBagWall_04_v2.prefab";
    const string BarbedFence = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/BarbedWireBarriers/BarbedWireMetalFence_26_v1.prefab";
    const string StreetLight = D + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light.prefab";
    const string Barrel = D + "GarageAssetPack/Prefabs/Barrelfbx.prefab";
    const string Wheel = D + "GarageAssetPack/Prefabs/CarWheel.prefab";
    const string Pallet = D + "GarageAssetPack/Prefabs/WoodenPallet.prefab";
    const string Box = D + "JeffamazedDev/HouseholdPropsPack/Prefabs/Decoration/Storeroom/CardboardBoxes/DEC_CardboardBox_CLOSED.prefab";
    const string Fire = D + "JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR Fire.prefab";
    const string Smoke = D + "JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Smoke Source 3D.prefab";

    static readonly Color Rust = new Color(.55f, .36f, .26f), Burnt = new Color(.22f, .2f, .19f), Faded = new Color(.72f, .7f, .66f),
        Olive = new Color(.38f, .42f, .3f), White = new Color(.95f, .92f, .85f), Red = new Color(.85f, .22f, .16f);

    // Точка на обочине в локальных координатах корня мира: позиция с высотой земли и направление дороги.
    struct Spot { public Vector3 p; public Quaternion q; }

    sealed class Plan
    {
        public readonly List<Action<Transform>> steps = new List<Action<Transform>>();
        public StageRoute route;
        public Transform world;
        public System.Random rng;
        public void Add(Action<Transform> step) => steps.Add(step);

        public Spot At(float d, float lateral)
        {
            route.Evaluate(d, out var p, out var q);
            var at = p + q * Vector3.right * lateral;
            at.y = Ground(at);
            return new Spot { p = world.InverseTransformPoint(at), q = Quaternion.Inverse(world.rotation) * q };
        }

        // Свободно, если коробка 0.6–2.6 м над землёй ничего не задевает: дома, столбы, ограды, крутой склон.
        // Плоская земля и дорога под коробкой не мешают; уже запланированное учитывается отдельно.
        public bool Free(float d, float lateral, float radius)
        {
            route.Evaluate(d, out var p, out var q);
            var at = p + q * Vector3.right * lateral;
            float ground = Ground(at);
            if(Physics.CheckBox(new Vector3(at.x, ground + 1.6f, at.z), new Vector3(radius, 1f, radius), q, ~0, QueryTriggerInteraction.Ignore)) return false;
            foreach(var taken in reserved) if((taken - at).sqrMagnitude < radius * radius * 4) return false;
            reserved.Add(at);
            return true;
        }
        readonly List<Vector3> reserved = new List<Vector3>();
    }

    sealed class Region
    {
        public string entryScene, worldScene, worldRoot;
        public Action<Plan> plan;
    }

    static readonly Region[] Regions =
    {
        new Region { entryScene = "Assets/Scenes/Stage1_Outskirts.unity", worldScene = "Assets/Scenes/Journey/Route01_World.unity", worldRoot = "Stage_World", plan = PlanCity },
        new Region { entryScene = "Assets/Scenes/Stage4_Citadel.unity", worldScene = "Assets/Scenes/Journey/Route04_World.unity", worldRoot = "Authored_Journey", plan = PlanCitadel },
    };

    [MenuItem("RogueDrive/Route/Dress City And Citadel")]
    public static void AuthorAll()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Dressing");
        int steps = 0;
        foreach(var region in Regions)
        {
            var scene = EditorSceneManager.OpenScene(region.entryScene, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().Single(g => g.name == region.worldRoot);
            bool wasActive = world.activeSelf;
            world.SetActive(true);
            var old = world.transform.Find(RootName);
            if(old != null) Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();
            var plan = new Plan { route = world.GetComponentInChildren<StageRoute>(true), world = world.transform, rng = new System.Random(2026) };
            region.plan(plan);
            Build(world.transform, plan);
            world.SetActive(wasActive);
            WorldTextMaterials.Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            steps += plan.steps.Count;

            var copy = EditorSceneManager.OpenScene(region.worldScene, OpenSceneMode.Single);
            var copyWorld = copy.GetRootGameObjects().Select(g => g.GetComponent<JourneyStreamWorld>()).First(m => m != null).World.transform;
            var copyOld = copyWorld.Find(RootName);
            if(copyOld != null) Object.DestroyImmediate(copyOld.gameObject);
            Build(copyWorld, plan);
            WorldTextMaterials.Apply(copy);
            EditorSceneManager.MarkSceneDirty(copy); EditorSceneManager.SaveScene(copy);
        }
        EditorSceneManager.OpenScene(Regions[0].entryScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Dressing] Built " + steps + " placements in " + Regions.Length + " regions.");
    }

    // Корень с единичной трансформацией под миром: локальные координаты плана совпадают в обеих сценах.
    static void Build(Transform world, Plan plan)
    {
        var root = Child(world, RootName);
        int group = 0;
        Transform bucket = null;
        for(int i = 0; i < plan.steps.Count; i++)
        {
            // Группы по 40 шагов: стриминг включает регион порциями второго уровня.
            if(i % 40 == 0) bucket = Child(root, "Group_" + group++);
            plan.steps[i](bucket);
        }
    }

    // ---------- Город: «Последние кварталы» и пригород ----------

    static void PlanCity(Plan plan)
    {
        Jams(plan, 150, 1750, 55, 105);
        Jams(plan, 4250, 5950, 140, 220);
        EvacuationPoint(plan, 900);
        string[] notes = { "ЭВАКУАЦИЯ\n→ СЕВЕР", "ЗАРАЖЕНО\nНЕ ВХОДИТЬ", "ВОДЫ НЕТ", "КТО ЖИВ —\nНА СЕВЕР", "БУРЯ ИДЁТ\nНЕ ОСТАНАВЛИВАЙСЯ",
                           "МАРИНА, МЫ\nНА СТО №1", "МАРОДЁРОВ\nРАССТРЕЛИВАЕМ", "СТО «СЕВЕРНАЯ»\n13 КМ" };
        int n = 0;
        foreach(var range in new[] { (320f, 1700f), (4380f, 5900f) })
            for(float d = range.Item1; d < range.Item2; d += 260, n++)
                Board(plan, d, n % 2 == 0 ? 1 : -1, notes[n % notes.Length], n % 3 == 1 ? Red : White, 1f);
    }

    static void Jams(Plan plan, float from, float to, float minStep, float maxStep)
    {
        var rng = plan.rng;
        int cluster = 0;
        for(float d = from; d < to; d += Range(rng, minStep, maxStep), cluster++)
        {
            int side = rng.NextDouble() < .5 ? -1 : 1;
            int count = 1 + rng.Next(4);
            bool burning = cluster % 5 == 3;
            for(int i = 0; i < count; i++)
            {
                bool first = i == 0;
                var tint = burning && first ? Burnt : rng.NextDouble() < .5 ? Rust : Faded;
                Vehicle(plan, Cars[rng.Next(Cars.Length)], d + i * Range(rng, 5.5f, 7f), side * Range(rng, RoadClear + 1.2f, RoadClear + 3.6f),
                    (rng.NextDouble() < .5 ? 0 : 180) + Range(rng, -28, 28), rng.NextDouble() < .1, tint, burning && first);
            }
            for(int j = 0, n = 2 + rng.Next(3); j < n; j++)
            {
                string prop = new[] { Barrel, Wheel, Pallet, Box }[rng.Next(4)];
                Placed(plan, prop, d + Range(rng, -4, 10), side * Range(rng, RoadClear + .6f, RoadClear + 5f), (float)rng.NextDouble() * 360,
                    prop == Box ? 1.4f : 1.15f, prop == Barrel ? Rust : Color.white, prop == Barrel ? "Rust" : null, prop == Barrel, .8f,
                    prop == Wheel ? (go, y) => { LayFlat(go); SitOn(go, y); } : (Action<GameObject, float>)null);
            }
        }
    }

    static void EvacuationPoint(Plan plan, float d)
    {
        foreach(int side in new[] { -1, 1 })
        {
            for(int i = 0; i < 9; i++) Placed(plan, ConcreteBlock, d - 10 + i * 2.4f, side * (RoadClear + .5f), 90, 1.8f, Color.white, null, true, 1f);
            for(int i = 0; i < 4; i++) Placed(plan, SandBags, d - 4 + i * 2.6f, side * (RoadClear + 3.4f), 0, 1.6f, Color.white, null, true, 1f);
            var lamp = plan.At(d + 12, side * (RoadClear + 2f));
            Placed(plan, StreetLight, d + 12, side * (RoadClear + 2f), side > 0 ? 180 : 0, 1.3f, new Color(.35f, .36f, .37f), "Pole", true, 1.5f);
            plan.Add(root => AddLight(root, "Flood", LightType.Point, lamp.p + Vector3.up * 7.5f, new Color(1f, .93f, .8f), 2f, 22));
        }
        Placed(plan, Truck, d + 22, RoadClear + 5.5f, 180, 1, Olive, "Olive", true, 4f);
        Placed(plan, Bus, d - 26, -(RoadClear + 6.5f), 10, 1, Rust, "Rust", true, 5f);
        if(plan.Free(d + 2, RoadClear + 1.2f, .8f))
        {
            var post = plan.At(d + 2, RoadClear + 1.2f);
            plan.Add(root =>
            {
                Block(root, "Barrier_Post", post.p + Vector3.up * .6f, new Vector3(.35f, 1.2f, .35f), Mat("Barrier_Post", new Color(.25f, .26f, .27f)), true);
                // Поднятая стрела: смотрит поперёк дороги, задрана на 70°.
                var arm = Block(root, "Barrier_Arm", Vector3.zero, new Vector3(.18f, .18f, 7f), Mat("Barrier_Arm", Red), false);
                arm.transform.localRotation = post.q * Quaternion.Euler(0, -90, 0) * Quaternion.Euler(-70, 0, 0);
                arm.transform.localPosition = post.p + Vector3.up * 1.2f + arm.transform.localRotation * Vector3.forward * 3.4f;
            });
        }
        Board(plan, d - 34, 1, "ЭВАКУАЦИОННЫЙ\nПУНКТ №3\nЗАКРЫТ", White, 1.4f);
    }

    // ---------- Подступы к Цитадели ----------

    static void PlanCitadel(Plan plan)
    {
        var rng = plan.rng;
        float gate = FinishDistance(plan) - 230;
        for(int km = 5; km >= 1; km--)
            Board(plan, gate - km * 1000, km % 2 == 0 ? -1 : 1, "ЦИТАДЕЛЬ\n" + km + " КМ", White, 1.2f);
        for(float d = 1500; d < gate - 3200; d += Range(rng, 380, 520))
        {
            int side = rng.NextDouble() < .5 ? -1 : 1;
            for(int i = 0, n = 2 + rng.Next(3); i < n; i++) Placed(plan, ConcreteBlock, d + i * 2.4f, side * (RoadClear + .5f), 90, 1.8f, Color.white, null, true, 1f);
            Vehicle(plan, Cars[rng.Next(Cars.Length)], d + 14, side * (RoadClear + 2.6f), Range(rng, 0, 360), rng.NextDouble() < .2, Burnt, rng.NextDouble() < .35);
        }
        // Ближние кордоны: колючка участками по 220 м вдоль обочин, посты наблюдения, грузовики.
        for(float d = gate - 3000; d < gate - 60; d += 2.4f)
            if(Mathf.FloorToInt((gate - d) / 220f) % 2 == 0)
                foreach(int side in new[] { -1, 1 }) Placed(plan, BarbedFence, d, side * (RoadClear + 3.5f), 90, 1.6f, Color.white, null, false, .6f);
        int post = 0;
        for(float d = gate - 2800; d < gate - 250; d += 560, post++) WatchPost(plan, d, post % 2 == 0 ? 1 : -1);
        for(int i = 0; i < 3; i++)
            Placed(plan, Truck, gate - 420 - i * 140, (i % 2 == 0 ? 1 : -1) * (RoadClear + 5f), i * 60 + 20, 1, Olive, "Olive", true, 4f);
        Fortress(plan, gate);
    }

    static float FinishDistance(Plan plan)
    {
        foreach(var mb in plan.world.GetComponentsInChildren<MonoBehaviour>(true))
            if(mb != null && mb.GetType().Name == "StageFinishOutpost") return plan.route.ProjectDistance(mb.transform.position);
        return plan.route.Length - 60;
    }

    static void WatchPost(Plan plan, float d, int side)
    {
        float lateral = side * (RoadClear + 7f);
        if(!plan.Free(d, lateral, 4f)) return;
        Placed(plan, Container, d, lateral + side * 2.5f, 90, 2.45f, Olive, "Olive", true, 0f);
        for(int i = 0; i < 4; i++) Placed(plan, SandBags, d - 4 + i * 2.6f, lateral - side * 2.8f, 0, 1.6f, Color.white, null, true, 0f);
        var spot = plan.At(d, lateral);
        plan.Add(root =>
        {
            var light = AddLight(root, "Post_Spot", LightType.Spot, spot.p + Vector3.up * 4.4f, new Color(1f, .95f, .85f), 3f, 60);
            light.spotAngle = 30;
            light.transform.localRotation = spot.q * Quaternion.Euler(12, 180 - side * 15, 0);   // навстречу машине
            Beacon(root, spot.p + Vector3.up * 4.8f, d * .013f);
        });
    }

    // Ворота-крепость: стены из контейнеров ярусами, бетонные башни, открытые створки,
    // балка с вывеской над проездом и прожекторы навстречу машине.
    static void Fortress(Plan plan, float d)
    {
        var concrete = Mat("Fortress_Concrete", new Color(.46f, .45f, .43f));
        var steel = Mat("Fortress_Steel", new Color(.16f, .17f, .18f));
        var board = Mat("Fortress_Board", new Color(.07f, .075f, .08f));
        var hazard = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/STO/STO_Hazard.mat");
        Color[] tints = { Rust, Olive, new Color(.32f, .4f, .5f) };
        foreach(int side in new[] { -1, 1 })
        {
            for(int i = 0; i < 4; i++)
            {
                var slot = plan.At(d, side * (RoadClear + 10f + i * 12.4f));
                for(int level = 0; level < 3 - i / 3; level++)
                {
                    int t = (i + level + (side > 0 ? 1 : 0)) % 3;
                    int lvl = level;
                    plan.Add(root =>
                    {
                        var go = Prop(root, Container, slot.p, 0, 2.45f, tints[t], "Fort" + t, true);
                        if(go == null) return;
                        go.transform.localRotation = slot.q * Quaternion.Euler(0, 90, 0);
                        SitOn(go, root.TransformPoint(slot.p).y + lvl * 3.95f);
                    });
                }
            }
            var tower = plan.At(d, side * (RoadClear + 2.4f));
            plan.Add(root =>
            {
                Block(root, "Gate_Tower", tower.p + Vector3.up * 6f, new Vector3(4.2f, 12f, 4.2f), concrete, true).transform.localRotation = tower.q;
                Block(root, "Tower_Hazard", tower.p + Vector3.up * 1.2f, new Vector3(4.3f, .6f, 4.3f), hazard, false).transform.localRotation = tower.q;
                var door = Block(root, "Gate_Door", tower.p + tower.q * new Vector3(side * 2.6f, 3.75f, 5.6f), new Vector3(.4f, 7.5f, 7f), steel, true);
                door.transform.localRotation = tower.q * Quaternion.Euler(0, side * 75, 0);
                var flood = AddLight(root, "Gate_Flood", LightType.Spot, tower.p + Vector3.up * 12.6f, new Color(1f, .93f, .82f), 4f, 90);
                flood.spotAngle = 45; flood.transform.localRotation = tower.q * Quaternion.Euler(20, 180 - side * 8, 0);
                Beacon(root, tower.p + Vector3.up * 12.4f + tower.q * Vector3.back * 1.6f, side * .5f);
            });
        }
        var centre = plan.At(d, 0);
        plan.Add(root =>
        {
            var q = centre.q;
            Block(root, "Gate_Beam", centre.p + Vector3.up * 10.6f, new Vector3((RoadClear + 4.5f) * 2f, 1.4f, 1.6f), concrete, false).transform.localRotation = q;
            Block(root, "Gate_Sign", centre.p + Vector3.up * 8.7f + q * Vector3.back * .3f, new Vector3(13f, 2.5f, .3f), board, false).transform.localRotation = q;
            Block(root, "Gate_Neon", centre.p + Vector3.up * 7.4f + q * Vector3.back * .48f, new Vector3(12.6f, .08f, .08f),
                Emissive(Folder, "Fortress_Neon", new Color(1f, .3f, .15f) * 3f), false).transform.localRotation = q;
            var title = Label(root, "Gate_Title", centre.p + Vector3.up * 8.7f + q * Vector3.back * .5f, "ЦИТАДЕЛЬ\nКАРАНТИННЫЙ КОНТРОЛЬ", .13f, new Color(1f, .82f, .45f), Vector3.forward);
            title.transform.localRotation = q;
        });
    }

    // ---------- Общие шаги ----------

    static void Vehicle(Plan plan, string prefab, float d, float lateral, float yaw, bool rolled, Color tint, bool burning)
    {
        string key = tint == Burnt ? "Burnt" : tint == Rust ? "Rust" : "Faded";
        Placed(plan, prefab, d, lateral, yaw, 1, tint, key, true, 2.4f, (go, y) =>
        {
            if(rolled) { go.transform.Rotate(0, 0, 180, Space.Self); SitOn(go, y); }
            if(!burning) return;
            var root = go.transform.parent;
            var b = Bounds(go);
            Fx(root, Fire, root.InverseTransformPoint(new Vector3(b.center.x, b.max.y - .3f, b.center.z)), .9f);
            Fx(root, Smoke, root.InverseTransformPoint(new Vector3(b.center.x, b.max.y + .8f, b.center.z)), 1.4f);
            AddLight(root, "Fire_Light", LightType.Point, root.InverseTransformPoint(b.center + Vector3.up * 1.5f), new Color(1f, .55f, .2f), 1.8f, 12);
        });
    }

    // Префаб у дороги: место проверено на плане, высота — по земле, поворот — по направлению дороги.
    static void Placed(Plan plan, string prefab, float d, float lateral, float yaw, float scale, Color tint, string key, bool collider, float radius,
        Action<GameObject, float> after = null)
    {
        if(Mathf.Abs(lateral) < RoadClear || (radius > 0 && !plan.Free(d, lateral, radius))) return;
        var spot = plan.At(d, lateral);
        plan.Add(root =>
        {
            var go = Prop(root, prefab, spot.p, 0, scale, tint, key, collider);
            if(go == null) return;
            go.transform.localRotation = spot.q * Quaternion.Euler(0, yaw, 0);
            float y = root.TransformPoint(spot.p).y;
            SitOn(go, y);
            after?.Invoke(go, y);
        });
    }

    // Фанерный щит на двух столбах у обочины, повёрнутый к водителю.
    static void Board(Plan plan, float d, int side, string text, Color color, float scale)
    {
        float lateral = side * (RoadClear + 2.4f);
        if(!plan.Free(d, lateral, 1.8f)) return;
        var spot = plan.At(d, lateral);
        plan.Add(root =>
        {
            var facing = spot.q * Quaternion.Euler(0, side * 30, 0);   // forward — направление взгляда водителя
            var post = Mat("Board_Post", new Color(.3f, .24f, .18f));
            foreach(float x in new[] { -1.1f, 1.1f })
                Block(root, "Board_Post", spot.p + facing * new Vector3(x * scale, 1.25f * scale, .05f), new Vector3(.12f, 2.5f * scale, .12f), post, false).transform.localRotation = facing;
            Block(root, "Board", spot.p + facing * new Vector3(0, 1.95f * scale, .1f), new Vector3(2.8f * scale, 1.6f * scale, .06f), Mat("Board_Plywood", new Color(.52f, .44f, .34f)), false)
                .transform.localRotation = facing;
            Label(root, "Board_Text", spot.p + facing * new Vector3(0, 1.95f * scale, .06f), text, .07f * scale, color, Vector3.forward).transform.localRotation = facing;
        });
    }

    static void Beacon(Transform root, Vector3 at, float phase)
    {
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        beacon.name = "Beacon"; Object.DestroyImmediate(beacon.GetComponent<Collider>());
        beacon.transform.SetParent(root, false);
        beacon.transform.localPosition = at; beacon.transform.localScale = Vector3.one * .4f;
        beacon.GetComponent<Renderer>().sharedMaterial = Emissive(Folder, "Dressing_Beacon", new Color(4f, .3f, .2f));
        var light = AddLight(beacon.transform, "Beacon_Light", LightType.Point, Vector3.zero, new Color(1f, .15f, .1f), 2.2f, 24);
        var so = new SerializedObject(beacon.AddComponent<BlinkingBeacon>());
        so.FindProperty("lamp").objectReferenceValue = beacon.GetComponent<Renderer>();
        so.FindProperty("glow").objectReferenceValue = light;
        so.FindProperty("phase").floatValue = Mathf.Repeat(phase, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Ближайшая к точке дороги поверхность (не крыши и не мосты над ней).
    static float Ground(Vector3 at)
    {
        float best = at.y, bestDelta = float.MaxValue;
        foreach(var h in Physics.RaycastAll(at + Vector3.up * 60, Vector3.down, 140, ~0, QueryTriggerInteraction.Ignore))
        {
            float delta = Mathf.Abs(h.point.y - at.y);
            if(delta < bestDelta) { bestDelta = delta; best = h.point.y; }
        }
        return best;
    }

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Material Mat(string name, Color color)
    {
        if(!mats.TryGetValue(name, out var m) || m == null) mats[name] = m = Std(Folder, name, color, .2f, 0);
        return m;
    }

    static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
