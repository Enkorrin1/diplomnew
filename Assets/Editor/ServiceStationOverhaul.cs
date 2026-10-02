using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using static SceneDressingKit;

// Переделка трёх СТО маршрута. Оболочка ангара получает материалы, внутри и снаружи
// расставляются объекты; игровые компоненты, ворота, верстак, приёмка и пульт не меняются.
// Всё новое лежит в STO_Overhaul и пересобирается при повторном запуске.
public static class ServiceStationOverhaul
{
    const string Dir = "Assets/Art/STO/";
    const string D = "Assets/Downloads/";
    const string KitName = "STO_Overhaul";
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Stage1_Outskirts.unity", "Assets/Scenes/Stage2_Wasteland.unity", "Assets/Scenes/Stage3_Industrial.unity",
        "Assets/Scenes/Journey/Route01_World.unity", "Assets/Scenes/Journey/Route02_World.unity", "Assets/Scenes/Journey/Route03_World.unity",
    };
    static readonly string[] Names = { "", "СЕВЕРНАЯ", "КАНЬОН", "РУБЕЖ" };

    // Пути к ассетам набора.
    const string Container = D + "FREE Low Poly Shipping Container/Prefabs/Low Poly Shipping Container.prefab";
    const string ArmoredCar = D + "ithappy/Apocalypse_Free/Prefabs/Props/Apocalypse_Car_01.prefab";
    const string CarBody = D + "ithappy/Apocalypse_Free/Prefabs/Props/Apocalypse_Car_01_Body.prefab";
    const string RoadBarrier = D + "ithappy/Apocalypse_Free/Prefabs/Environment/Road_Barrier_01.prefab";
    const string SandBags = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/SandBagBarriers/SandBagWall_04_v2.prefab";
    const string SandPallet = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/SandBagBarriers/SandBagPallet_16_v3.prefab";
    const string BarbedFence = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/BarbedWireBarriers/BarbedWireMetalFence_26_v1.prefab";
    const string ConcreteBlock = D + "LowPolyBarriersPackFree/BarriersAssets/Prefabs/ConcreteBarriers/ConcreteBarrier_08_v2.prefab";
    const string Wreck1 = D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 05.prefab";
    const string Wreck2 = D + "Hodaart/LowPoly Vehicle Collection 2/Prefabs/Car 02.prefab";
    const string Truck = D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Truck_color01.prefab";
    const string Pickup = D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color02.prefab";
    const string Bus = D + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color02.prefab";
    const string StreetLight = D + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light.prefab";
    const string Antenna = D + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Antenna.prefab";
    const string Solar = D + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Solar Panel.prefab";
    const string WaterTank = D + "Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/Prop_RoofWaterStore_02.prefab";
    const string RoofVent = D + "Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/Prop_RoofVent_04.prefab";
    const string Cabinet = D + "Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/Prop_ElectracityCabinet_02.prefab";
    const string Barrel = D + "GarageAssetPack/Prefabs/Barrelfbx.prefab";
    const string Wheel = D + "GarageAssetPack/Prefabs/CarWheel.prefab";
    const string Shelf = D + "GarageAssetPack/Prefabs/StorageShelfFull.prefab";
    const string Pallet = D + "GarageAssetPack/Prefabs/WoodenPallet.prefab";
    const string Workbench = D + "GarageAssetPack/Prefabs/WorkbenchFull.prefab";
    const string Jerrycan = D + "GarageAssetPack/Prefabs/JerrycanLarge.prefab";
    const string Lockers = D + "Low-Poly 3D Lockers/Low-Poly 3D Lockers/Prefabs/rack Obj.prefab";
    const string Box = D + "JeffamazedDev/HouseholdPropsPack/Prefabs/Decoration/Storeroom/CardboardBoxes/DEC_CardboardBox_CLOSED.prefab";
    const string Fire = D + "JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR Fire.prefab";
    const string Smoke = D + "JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Smoke Source 3D.prefab";

    sealed class Theme
    {
        public Color wall, roof, ground, rust, faded, paint;
        public int fortify;
    }

    static Theme For(int n) => n switch
    {
        2 => new Theme { wall = new Color(.62f, .50f, .36f), roof = new Color(.36f, .29f, .23f), ground = new Color(.55f, .47f, .38f),
                         rust = new Color(.62f, .38f, .22f), faded = new Color(.78f, .68f, .55f), paint = new Color(.85f, .55f, .2f), fortify = 1 },
        3 => new Theme { wall = new Color(.33f, .36f, .30f), roof = new Color(.2f, .21f, .2f), ground = new Color(.42f, .42f, .41f),
                         rust = new Color(.45f, .3f, .22f), faded = new Color(.55f, .58f, .52f), paint = new Color(.75f, .2f, .15f), fortify = 2 },
        _ => new Theme { wall = new Color(.46f, .54f, .49f), roof = new Color(.22f, .23f, .24f), ground = new Color(.46f, .45f, .43f),
                         rust = new Color(.55f, .33f, .22f), faded = new Color(.6f, .64f, .62f), paint = new Color(.9f, .7f, .15f), fortify = 0 },
    };

    sealed class Kit
    {
        public Material wall, roof, floor, apron, hazard, shutter, steel, board, neon, glow, beacon, pad, window, concrete;
        public Mesh shade;
        public Font font;
        public Material text;
    }

    [MenuItem("RogueDrive/Route/Overhaul Service Stations (all scenes)")]
    public static void AuthorAll()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        int count = 0;
        foreach(var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            count += Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(Scenes[0]);
        AssetDatabase.SaveAssets();
        Debug.Log("[STO] Overhauled " + count + " stations in " + Scenes.Length + " scenes.");
    }

    [MenuItem("RogueDrive/Route/Overhaul Service Stations (open scene)")]
    public static void AuthorOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static int Apply(UnityEngine.SceneManagement.Scene scene)
    {
        if(!AssetDatabase.IsValidFolder("Assets/Art/STO")) AssetDatabase.CreateFolder("Assets/Art", "STO");
        if(!AssetDatabase.IsValidFolder("Assets/Art/STO/Tinted")) AssetDatabase.CreateFolder("Assets/Art/STO", "Tinted");
        var stations = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JourneyServiceStation>(true)).ToArray();
        foreach(var station in stations)
        {
            int number = new SerializedObject(station).FindProperty("stationNumber").intValue;
            Overhaul(station.transform, Mathf.Clamp(number, 1, 3));
        }
        return stations.Length;
    }

    static void Overhaul(Transform root, int number)
    {
        var old = root.Find(KitName);
        if(old != null) Object.DestroyImmediate(old.gameObject);
        var kitRoot = new GameObject(KitName).transform;
        kitRoot.SetParent(root, false);
        var theme = For(number);
        var kit = Materials(theme, number);
        Shell(root, kit);
        FaceLabels(root);
        Interior(Child(kitRoot, "Interior"), kit, theme, number);
        Exterior(Child(kitRoot, "Exterior"), kit, theme, number);
    }

    // ---------- Оболочка ----------

    static void Shell(Transform root, Kit k)
    {
        foreach(Transform c in root)
        {
            Material m = c.name switch
            {
                "Hangar_Left" or "Hangar_Right" or "Workbench_Backdrop" => k.wall,
                "Filtered_Roof" => k.roof,
                "Arrival_Shutter" or "Departure_Shutter" => k.shutter,
                "Support" or "Entrance_Left_Jamb" or "Entrance_Right_Jamb" or "Lift_Post" or "Engine_Crane_Base" or "Engine_Crane_Mast" or "Engine_Crane_Arm" => k.hazard,
                "Roof_Beam" or "Entrance_Header" or "Entrance_Lintel" or "Filter_Housing" or "Vent_Duct" or "Filter_Louvre" or "Wall_Panel"
                    or "Tool_Shelf" or "Tool_Tray" or "Receiving_Counter" or "Lift_Platform" or "Floor_Lift_Rail" or "Lift_Arm" => k.steel,
                "Apron" or "Entry_Ramp" or "Exit_Ramp" => k.apron,
                _ => null,
            };
            var r = c.GetComponent<Renderer>();
            if(m != null && r != null) r.sharedMaterial = m;
            if(c.name == "Light_Rail" || c.name == "Cargo_Crate") c.gameObject.SetActive(false);
        }
    }

    // TextMesh читается, когда камера смотрит вдоль его forward: надписи на фасаде смотрят внутрь,
    // надписи у стен — к стене, то есть лицом к игроку в центре бокса.
    static void FaceLabels(Transform root)
    {
        foreach(Transform c in root)
        {
            var text = c.GetComponent<TextMesh>();
            if(text == null) continue;
            var p = c.localPosition;
            Vector3 forward = p.z < -31f ? Vector3.forward : p.z > 31f ? Vector3.back : new Vector3(Mathf.Sign(p.x), 0, 0);
            c.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }

    // ---------- Интерьер ----------

    static void Interior(Transform parent, Kit k, Theme t, int number)
    {
        Block(parent, "Floor_Interior", new Vector3(0, .012f, 0), new Vector3(29.2f, .024f, 62f), k.floor, false);
        // Разметка: рамка поста подъёмника и пороги ворот.
        foreach(float x in new[] { -3.7f, 3.7f }) Block(parent, "Bay_Hazard", new Vector3(x, .03f, 3), new Vector3(.3f, .02f, 10.4f), k.hazard, false);
        foreach(float z in new[] { -2.2f, 8.2f }) Block(parent, "Bay_Hazard", new Vector3(0, .03f, z), new Vector3(7.7f, .02f, .3f), k.hazard, false);
        foreach(float z in new[] { -30.4f, 30.4f }) Block(parent, "Gate_Threshold", new Vector3(0, .03f, z), new Vector3(24f, .02f, .5f), k.hazard, false);

        var lamps = Child(parent, "Lamps");
        foreach(float x in new[] { -7.5f, 7.5f })
        foreach(float z in new[] { -22f, -8f, 6f, 20f })
            Lamp(lamps, new Vector3(x, 7.85f, z), k, x < 0 && z == 6f);
        foreach(float z in new[] { -15f, 0f, 15f }) Lamp(lamps, new Vector3(0, 7.85f, z), k, false);

        // Трубы и кабельные лотки вдоль стен под потолком.
        foreach(float x in new[] { -14.2f, 14.2f })
        {
            Pipe(parent, new Vector3(x, 6.3f, 0), .26f, 60f, k.steel);
            Pipe(parent, new Vector3(x, 6.75f, 0), .16f, 60f, Paint(t, number));
            Block(parent, "Cable_Tray", new Vector3(x * .985f, 5.7f, 0), new Vector3(.35f, .06f, 60f), k.steel, false);
        }

        var props = Child(parent, "Props");
        // Левая стена: шкафчики, стеллажи, бочки и покрышки.
        for(int i = 0; i < 3; i++) Prop(props, Lockers, new Vector3(-13.9f, 0, -27.2f + i * 1.75f), 90, 1, t.faded, "Faded" + number, true);
        for(int i = 0; i < 3; i++) Prop(props, Shelf, new Vector3(-14.1f, 0, -14.5f + i * 1.45f), 90, 1.35f, Color.white, null, true);
        Barrels(props, new Vector3(-13.2f, 0, 18f), t, number);
        Tires(props, new Vector3(-12.9f, 0, 23.3f), 4);
        Tires(props, new Vector3(-11.6f, 0, 24.6f), 3);
        for(int i = 0; i < 3; i++) Prop(props, Jerrycan, new Vector3(-12f + i * .55f, 0, 20.6f), 90 + i * 12, 1, t.paint, "Paint" + number, false);
        // Правая стена: электрощиты, стеллажи, верстак, поддоны с коробками.
        for(int i = 0; i < 2; i++) Prop(props, Cabinet, new Vector3(14.2f, 0, -28.3f + i * 1.6f), -90, 1.15f, t.faded, "Faded" + number, true);
        Prop(props, RoofVent, new Vector3(12.9f, 0, -24.8f), 0, 1.3f, t.faded, "Faded" + number, true);
        for(int i = 0; i < 3; i++) Prop(props, Shelf, new Vector3(14.1f, 0, -8.6f + i * 1.45f), -90, 1.35f, Color.white, null, true);
        Prop(props, Workbench, new Vector3(13.6f, 0, -1.8f), -90, 1, Color.white, null, true);
        PalletOfBoxes(props, new Vector3(12.6f, 0, 4.2f), 0);
        PalletOfBoxes(props, new Vector3(12.6f, 0, 6.6f), 1);
        // Кузов на домкратах под кран-балкой: здесь меняют двигатели.
        var body = Prop(props, CarBody, new Vector3(7.4f, .62f, 21f), 90, 1, t.rust, "Rust" + number, true);
        if(body != null) foreach(float dx in new[] { -1.6f, 1.6f }) foreach(float dz in new[] { -.8f, .8f })
            Block(props, "Jack_Stand", new Vector3(7.4f + dx, .31f, 21f + dz), new Vector3(.25f, .62f, .25f), k.hazard, false);
        Prop(props, Wheel, new Vector3(4.2f, 0, 23.5f), 0, 1, Color.white, null, false, lay: true);

        // Надписи по трафарету на стенах.
        Label(parent, "Stencil_Bay", new Vector3(-14.55f, 5.2f, -6f), "БОКС " + number, .14f, new Color(.92f, .9f, .8f, .55f), Vector3.left, k);
        Label(parent, "Stencil_NoFire", new Vector3(14.55f, 4.4f, -16f), "НЕ КУРИТЬ\nГСМ", .09f, new Color(.95f, .75f, .2f, .6f), Vector3.right, k);
        Label(parent, "Stencil_Name", new Vector3(14.55f, 5.4f, 12f), "«" + Names[number] + "»", .12f, new Color(.92f, .9f, .8f, .45f), Vector3.right, k);

        Dust(parent);
    }

    static void Lamp(Transform parent, Vector3 ceiling, Kit k, bool shadows)
    {
        var lamp = Child(parent, "Hanging_Lamp");
        lamp.localPosition = ceiling + Vector3.down * .9f;
        Block(lamp, "Cable", new Vector3(0, .45f, 0), new Vector3(.03f, .9f, .03f), k.steel, false);
        var shade = new GameObject("Shade", typeof(MeshFilter), typeof(MeshRenderer));
        shade.transform.SetParent(lamp, false);
        shade.transform.localScale = Vector3.one * .9f;
        shade.GetComponent<MeshFilter>().sharedMesh = k.shade;
        shade.GetComponent<MeshRenderer>().sharedMaterial = k.steel;
        shade.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bulb"; Object.DestroyImmediate(bulb.GetComponent<Collider>());
        bulb.transform.SetParent(lamp, false);
        bulb.transform.localPosition = Vector3.down * .32f; bulb.transform.localScale = new Vector3(.3f, .13f, .3f);
        bulb.GetComponent<Renderer>().sharedMaterial = k.glow;
        bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var light = Child(lamp, "Light").gameObject.AddComponent<Light>();
        light.transform.localPosition = Vector3.down * .4f;
        light.transform.localRotation = Quaternion.Euler(90, 0, 0);
        light.type = LightType.Spot; light.spotAngle = 95; light.innerSpotAngle = 50; light.range = 13; light.intensity = 2.8f;
        light.color = new Color(1f, .87f, .7f);
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    static void Pipe(Transform parent, Vector3 at, float diameter, float length, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Pipe"; Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = new Vector3(diameter, length * .5f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Barrels(Transform parent, Vector3 at, Theme t, int number)
    {
        var offsets = new[] { Vector3.zero, new Vector3(.8f, 0, .5f), new Vector3(-.1f, 0, 1.1f), new Vector3(.9f, 0, 1.4f) };
        for(int i = 0; i < offsets.Length; i++)
            Prop(parent, Barrel, at + offsets[i], i * 37, 1.15f, i % 2 == 0 ? t.rust : t.faded, (i % 2 == 0 ? "Rust" : "Faded") + number, true);
    }

    static void Tires(Transform parent, Vector3 at, int count)
    {
        float y = 0;
        for(int i = 0; i < count; i++)
        {
            var go = Prop(parent, Wheel, at + new Vector3(UnityEngine.Random.Range(-.05f, .05f), 0, 0), i * 23, 1, Color.white, null, false, lay: true);
            if(go == null) return;
            SitOn(go, parent.TransformPoint(at).y + y);
            y += Bounds(go).size.y;
        }
    }

    static void PalletOfBoxes(Transform parent, Vector3 at, int variant)
    {
        var pallet = Prop(parent, Pallet, at, 0, 1.3f, Color.white, null, true);
        if(pallet == null) return;
        float top = Bounds(pallet).max.y;
        for(int i = 0; i < 4 - variant; i++)
        {
            var box = Prop(parent, Box, at + new Vector3((i % 2 - .5f) * .55f, 0, (i / 2 - .5f) * .55f), i * 9, 1.4f, Color.white, null, false);
            if(box == null) continue;
            SitOn(box, i < 2 ? top : top + .4f);
        }
    }

    static void Dust(Transform parent)
    {
        var ps = Child(parent, "Dust_Motes").gameObject.AddComponent<ParticleSystem>();
        ps.transform.localPosition = new Vector3(0, 3.6f, 0);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = new ParticleSystem.MinMaxCurve(10, 16); main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.015f, .035f); main.maxParticles = 360;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .9f, .75f, .4f), new Color(1f, .95f, .85f, .85f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.rateOverTime = 26;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(26, 6, 56);
        var noise = ps.noise; noise.enabled = true; noise.strength = .12f; noise.frequency = .25f;
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GarageUpgrade/Garage_Dust.mat");
        pr.shadowCastingMode = ShadowCastingMode.Off; pr.maxParticleSize = .02f;
    }

    // ---------- Снаружи ----------

    static void Exterior(Transform parent, Kit k, Theme t, int number)
    {
        // Бетонные площадки продолжают перрон в стороны; толщина 3 м закрывает перепады рельефа.
        Block(parent, "Pad_Right", new Vector3(31.5f, -1.5f, 0), new Vector3(21f, 3f, 88f), k.pad, true);
        Block(parent, "Pad_Left", new Vector3(-31f, -1.5f, -2f), new Vector3(20f, 3f, 44f), k.pad, true);

        Pylon(Child(parent, "Pylon_Sign"), new Vector3(-34f, 0, -10f), k, number);
        Facade(Child(parent, "Facade"), parent.parent.parent, k);
        FuelCanopy(Child(parent, "Fuel_Canopy"), new Vector3(31.5f, 0, -29f), k, t, number);

        var yard = Child(parent, "Yard");
        // Стена из контейнеров вдоль дальнего края.
        Color[] tints = { t.rust, new Color(.32f, .42f, .52f), new Color(.34f, .42f, .32f) };
        for(int i = 0; i < 3; i++)
            Prop(yard, Container, new Vector3(39.6f, 0, -8f + i * 13f), 0, 2.45f, tints[i], "Container" + i + "_" + number, true);
        var top = Prop(yard, Container, new Vector3(39.6f, 0, 5f), 0, 2.45f, tints[0], "Container0_" + number, true);
        if(top != null) SitOn(top, parent.TransformPoint(Vector3.zero).y + 3.9f);
        // Брошенные машины и грузовик за ангаром.
        Prop(yard, Wreck1, new Vector3(27f, 0, 9f), 35, 1.05f, t.rust, "Rust" + number, true);
        Prop(yard, Wreck2, new Vector3(31.5f, 0, 17f), -20, 1.05f, t.faded, "Faded" + number, true);
        Prop(yard, Truck, new Vector3(31f, 0, 33f), 90, 1, t.faded, "Faded" + number, true);
        Prop(yard, Pickup, new Vector3(-30f, 0, 12f), 200, 1, t.rust, "Rust" + number, true);
        // Своя бронемашина станции у навеса.
        Prop(yard, ArmoredCar, new Vector3(26.5f, 0, -12f), 160, 1, Color.white, null, true);
        // Забор по периметру двора, автобус-баррикада, генератор и штабели поддонов.
        // Забор из бетонных плит со столбами.
        for(int i = 0; i < 25; i++)
        {
            Block(yard, "Fence_Panel", new Vector3(42.2f, 1.1f, -42f + i * 3.5f), new Vector3(.25f, 2.2f, 3.4f), k.pad, true);
            Block(yard, "Fence_Post", new Vector3(42.2f, 1.25f, -43.75f + i * 3.5f), new Vector3(.4f, 2.5f, .4f), k.pad, false);
        }
        for(int i = 0; i < 5; i++)
            Block(yard, "Fence_Panel", new Vector3(23.6f + i * 3.5f, 1.1f, 43.8f), new Vector3(3.4f, 2.2f, .25f), k.pad, true);
        Prop(yard, Bus, new Vector3(31f, 0, 39.5f), 90, 1, t.rust, "Rust" + number, true);
        Prop(yard, RoofVent, new Vector3(34.5f, 0, -6f), 90, 2.2f, t.faded, "Faded" + number, true);
        for(int i = 0; i < 4; i++)
        {
            var pallet = Prop(yard, Pallet, new Vector3(35.5f, 0, 26f), i * 4, 1.3f, Color.white, null, i == 0);
            if(pallet != null) SitOn(pallet, parent.TransformPoint(Vector3.zero).y + i * .19f);
        }
        Tires(yard, new Vector3(24.2f, 0, 1.5f), 5);
        Tires(yard, new Vector3(25.2f, 0, 2.6f), 3);
        BurningBarrel(yard, new Vector3(24.3f, 0, -3.5f), t, number);
        Barrels(yard, new Vector3(25.5f, 0, 24f), t, number);

        // Прожекторы по углам перрона.
        foreach(var p in new[] { new Vector3(22.6f, 0, -43f), new Vector3(22.6f, 0, 43f), new Vector3(-22.6f, 0, -20f), new Vector3(-22.6f, 0, 20f) })
            FloodLight(yard, p, p.x > 0 ? -90 : 90, t, number);

        // Мешки с песком у въездных ворот.
        foreach(float x in new[] { -16.3f, 16.3f })
            for(int row = 0; row < 2 + (t.fortify > 0 ? 1 : 0); row++)
            {
                var bag = Prop(yard, SandBags, new Vector3(x, 0, -34.2f), 90 + row * 8, 1.6f, Color.white, null, row == 0);
                if(bag != null) SitOn(bag, parent.TransformPoint(Vector3.zero).y + row * .55f);
            }

        Roof(Child(parent, "Roof_Props"), k, t, number);
        if(t.fortify >= 1) Fortify(Child(parent, "Fortification"), t, number);
    }

    // Вывеска-короб над въездом; табло состояния станции переезжает на её лицевую сторону.
    static void Facade(Transform facade, Transform station, Kit k)
    {
        // Короб ниже свеса крыши (8.2 м), иначе верхняя строка табло прячется под ним.
        Block(facade, "Sign_Box", new Vector3(0, 6.9f, -32.85f), new Vector3(18f, 2.4f, .5f), k.board, false);
        Block(facade, "Sign_Neon", new Vector3(0, 5.75f, -33.12f), new Vector3(17.6f, .07f, .07f), k.neon, false);
        foreach(float x in new[] { -6.5f, 6.5f })
        {
            Block(facade, "Sign_Lamp_Arm", new Vector3(x, 8.75f, -33.6f), new Vector3(.08f, .08f, 1.4f), k.steel, false);
            var lamp = Child(facade, "Sign_Lamp").gameObject.AddComponent<Light>();
            lamp.transform.localPosition = new Vector3(x, 8.7f, -34.3f);
            lamp.transform.localRotation = Quaternion.Euler(60, 0, 0);
            lamp.type = LightType.Spot; lamp.spotAngle = 70; lamp.range = 8; lamp.intensity = 2.2f; lamp.color = new Color(1f, .9f, .75f);
        }
        var status = station.Find("Station_Status");
        if(status != null)
        {
            status.localPosition = new Vector3(0, 6.9f, -33.15f);
            var text = status.GetComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.characterSize = .12f;
        }
        // Светящиеся окна под крышей и трафарет на боковых стенах.
        foreach(float x in new[] { -15.4f, 15.4f })
        {
            for(float z = -24f; z <= 24f; z += 6f)
                Block(facade, "Clerestory_Window", new Vector3(x, 6.6f, z), new Vector3(.06f, .7f, 2.4f), k.window, false);
            Label(facade, "Side_Stencil", new Vector3(x * 1.003f, 3.4f, -4f), station.GetComponent<RogueDrive.Gameplay.Hub.WorkshopServiceZone>() != null
                ? new SerializedObject(station.GetComponent<RogueDrive.Gameplay.Hub.WorkshopServiceZone>()).FindProperty("displayName").stringValue : "СТО",
                .26f, new Color(.9f, .88f, .8f, .5f), new Vector3(-Mathf.Sign(x), 0, 0), k);
        }
        var banner = station.Find("Arrival_Banner");
        if(banner != null) banner.gameObject.SetActive(false);
    }

    static void Pylon(Transform pylon, Vector3 at, Kit k, int number)
    {
        pylon.localPosition = at;
        pylon.localRotation = Quaternion.Euler(0, -50, 0);   // лицом к съезду с трассы
        foreach(float z in new[] { -3.2f, 3.2f }) Block(pylon, "Post", new Vector3(0, 4.6f, z), new Vector3(.45f, 9.2f, .45f), k.steel, true);
        Block(pylon, "Board", new Vector3(0, 7.6f, 0), new Vector3(.35f, 3.4f, 7.6f), k.board, false);
        // Неоновая рамка.
        Block(pylon, "Neon_Top", new Vector3(-.2f, 9.25f, 0), new Vector3(.08f, .08f, 7.5f), k.neon, false);
        Block(pylon, "Neon_Bottom", new Vector3(-.2f, 5.95f, 0), new Vector3(.08f, .08f, 7.5f), k.neon, false);
        foreach(float z in new[] { -3.75f, 3.75f }) Block(pylon, "Neon_Side", new Vector3(-.2f, 7.6f, z), new Vector3(.08f, 3.3f, .08f), k.neon, false);
        Label(pylon, "Title", new Vector3(-.2f, 8.45f, 0), "СТО №" + number, .19f, new Color(1f, .82f, .45f), Vector3.right, k);
        Label(pylon, "Name", new Vector3(-.2f, 7.5f, 0), "«" + Names[number] + "»", .13f, new Color(.95f, .93f, .86f), Vector3.right, k);
        Label(pylon, "Services", new Vector3(-.2f, 6.55f, 0), "РЕМОНТ · ТОПЛИВО · УКРЫТИЕ", .06f, new Color(.8f, .85f, .82f), Vector3.right, k);
        var spot = Child(pylon, "Board_Light").gameObject.AddComponent<Light>();
        spot.transform.localPosition = new Vector3(-3.5f, 3.2f, 0);
        spot.transform.LookAt(pylon.TransformPoint(new Vector3(0, 7.6f, 0)));
        spot.type = LightType.Spot; spot.spotAngle = 60; spot.range = 14; spot.intensity = 3f; spot.color = new Color(1f, .9f, .75f);
    }

    static void FuelCanopy(Transform canopy, Vector3 at, Kit k, Theme t, int number)
    {
        canopy.localPosition = at;
        foreach(float x in new[] { -4.6f, 4.6f }) foreach(float z in new[] { -3.6f, 3.6f })
            Block(canopy, "Column", new Vector3(x, 2.6f, z), new Vector3(.45f, 5.2f, .45f), k.steel, true);
        Block(canopy, "Roof", new Vector3(0, 5.45f, 0), new Vector3(11.5f, .5f, 9.2f), k.roof, false);
        Block(canopy, "Fascia", new Vector3(0, 5.45f, -4.65f), new Vector3(11.5f, .9f, .12f), k.board, false);
        Block(canopy, "Fascia_Glow", new Vector3(0, 5.0f, -4.73f), new Vector3(11.3f, .06f, .06f), k.neon, false);
        Label(canopy, "Fuel_Title", new Vector3(0, 5.5f, -4.75f), "ТОПЛИВО · ВОДА · МАСЛО", .1f, new Color(1f, .82f, .45f), Vector3.forward, k);
        var under = Child(canopy, "Canopy_Light").gameObject.AddComponent<Light>();
        under.transform.localPosition = new Vector3(0, 4.9f, 0);
        under.type = LightType.Point; under.range = 11; under.intensity = 1.8f; under.color = new Color(1f, .92f, .8f);
        foreach(float x in new[] { -2.2f, 2.2f })
        {
            Block(canopy, "Pump_Island", new Vector3(x, .14f, 0), new Vector3(1.3f, .28f, 4.4f), k.pad, true);
            foreach(float z in new[] { -1.1f, 1.1f })
            {
                Block(canopy, "Pump", new Vector3(x, 1.2f, z), new Vector3(.75f, 1.85f, .55f), Paint(t, number), true);
                Block(canopy, "Pump_Display", new Vector3(x - .38f, 1.55f, z), new Vector3(.02f, .35f, .4f), k.neon, false);
                Block(canopy, "Pump_Hose", new Vector3(x - .42f, .95f, z + .18f), new Vector3(.05f, .8f, .05f), k.steel, false);
            }
        }
        Prop(canopy, Jerrycan, new Vector3(-2.2f, .28f, 2.5f), 0, 1, t.paint, "Paint" + number, false);
    }

    static void BurningBarrel(Transform parent, Vector3 at, Theme t, int number)
    {
        var barrel = Prop(parent, Barrel, at, 0, 1.15f, t.rust, "Rust" + number, true);
        if(barrel == null) return;
        var top = Bounds(barrel).max.y;
        var fire = Fx(parent, Fire, new Vector3(at.x, 0, at.z), .7f);
        if(fire != null) fire.transform.position = new Vector3(fire.transform.position.x, top - .05f, fire.transform.position.z);
        var smoke = Fx(parent, Smoke, new Vector3(at.x, 0, at.z), .8f);
        if(smoke != null) smoke.transform.position = new Vector3(smoke.transform.position.x, top + .6f, smoke.transform.position.z);
        var glow = Child(parent, "Fire_Light").gameObject.AddComponent<Light>();
        glow.transform.position = new Vector3(barrel.transform.position.x, top + .6f, barrel.transform.position.z);
        glow.type = LightType.Point; glow.range = 9; glow.intensity = 1.6f; glow.color = new Color(1f, .55f, .2f);
    }

    static void FloodLight(Transform parent, Vector3 at, float yaw, Theme t, int number)
    {
        var pole = Prop(parent, StreetLight, at, yaw, 1.35f, new Color(.35f, .36f, .37f), "Pole", true);
        if(pole == null) return;
        var b = Bounds(pole);
        var light = Child(parent, "Flood_Light").gameObject.AddComponent<Light>();
        light.transform.position = new Vector3(b.center.x, b.max.y - .3f, b.center.z);
        light.transform.rotation = Quaternion.Euler(70, parent.rotation.eulerAngles.y + yaw + 90, 0);
        light.type = LightType.Spot; light.spotAngle = 100; light.range = 26; light.intensity = 2.2f; light.color = new Color(1f, .93f, .82f);
    }

    static void Roof(Transform roof, Kit k, Theme t, int number)
    {
        float y = 8.55f;
        var mast = Prop(roof, Antenna, new Vector3(11.5f, y, 26f), 0, 1.6f, new Color(.6f, .6f, .62f), "Mast", false);
        if(mast != null)
        {
            var b = Bounds(mast);
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon"; Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(roof, true);
            beacon.transform.position = new Vector3(b.center.x, b.max.y + .25f, b.center.z); beacon.transform.localScale = Vector3.one * .45f;
            beacon.GetComponent<Renderer>().sharedMaterial = k.beacon;
            var light = Child(beacon.transform, "Beacon_Light").gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 30; light.intensity = 2.5f; light.color = new Color(1f, .15f, .1f);
            var blink = beacon.AddComponent<BlinkingBeacon>();
            var so = new SerializedObject(blink);
            so.FindProperty("lamp").objectReferenceValue = beacon.GetComponent<Renderer>();
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("phase").floatValue = number * .3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        for(int i = 0; i < 2; i++) Prop(roof, WaterTank, new Vector3(-10.5f + i * 3f, y, 22f), 0, 1.2f, t.faded, "Faded" + number, false);
        for(int i = 0; i < 3; i++) Prop(roof, RoofVent, new Vector3(-8f + i * 8f, y, -4f), 90, 1.2f, t.faded, "Faded" + number, false);
        for(int i = 0; i < 4; i++) Prop(roof, Solar, new Vector3(3f + i * 2.6f, y, -14f), 0, 1, Color.white, null, false);
        // Пост наблюдения: мешки по кругу и прожектор на дорогу.
        var nest = Child(roof, "Lookout");
        nest.localPosition = new Vector3(-11.5f, y, -27f);
        for(int i = 0; i < 4; i++)
        {
            float a = i * 90f;
            var bag = Prop(nest, SandBags, Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 1.5f), a, 1.4f, Color.white, null, false);
            if(bag != null) SitOn(bag, nest.position.y);
        }
        var spot = Child(nest, "Lookout_Spot").gameObject.AddComponent<Light>();
        spot.transform.localPosition = new Vector3(0, 1.6f, 0);
        spot.transform.localRotation = Quaternion.Euler(15, -120, 0);
        spot.type = LightType.Spot; spot.spotAngle = 28; spot.range = 70; spot.intensity = 3f; spot.color = new Color(1f, .96f, .88f);
    }

    // «Каньон» и «Рубеж» укреплены сильнее: бетонные блоки у дороги, колючка, у «Рубежа» второй ряд.
    static void Fortify(Transform parent, Theme t, int number)
    {
        for(int i = 0; i < 7; i++)
            Prop(parent, ConcreteBlock, new Vector3(-41.5f, 0, -20f + i * 6f), 90, 1.8f, Color.white, null, true);
        for(int i = 0; i < 36; i++)   // колючка поверх бетонного забора
            Prop(parent, BarbedFence, new Vector3(42.2f, 2.2f, -42f + i * 2.4f), 90, 1.6f, Color.white, null, false);
        if(t.fortify < 2) return;
        for(int i = 0; i < 6; i++)
            Prop(parent, RoadBarrier, new Vector3(-23f - (i % 2) * 2f, 0, -32f + i * 2.8f), 30, 1.3f, Color.white, null, true);
        for(int i = 0; i < 8; i++)
        {
            var bag = Prop(parent, SandPallet, new Vector3(22.8f, 0, -12f + i * 2.2f), 90, 1.6f, Color.white, null, true);
            if(bag != null) SitOn(bag, parent.TransformPoint(Vector3.zero).y);
        }
    }

    // ---------- Материалы и текстуры ----------

    static Kit Materials(Theme t, int n)
    {
        var corrugated = Texture("STO_Corrugated", 256, Corrugated);
        var shutter = Texture("STO_Shutter", 256, ShutterStripes);
        var hazard = Texture("STO_Hazard", 256, HazardStripes);
        var grunge = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/GarageUpgrade/Garage_ConcreteGrunge.png");
        var k = new Kit
        {
            wall = Tri("STO" + n + "_Wall", corrugated, t.wall, .5f, .25f, grunge, .6f),
            roof = Tri("STO" + n + "_Roof", corrugated, t.roof, .5f, .2f, grunge, .5f),
            floor = Tri("STO" + n + "_Floor", grunge, new Color(.44f, .42f, .39f), .12f, .28f, grunge, .35f),
            apron = Tri("STO" + n + "_Apron", grunge, t.ground, .08f, .1f, grunge, .4f),
            pad = Tri("STO" + n + "_Pad", grunge, t.ground * .92f, .08f, .08f, grunge, .45f),
            hazard = Tri("STO_Hazard", hazard, Color.white, .9f, .2f, grunge, .35f),
            shutter = Std("STO" + n + "_Shutter", new Color(.6f, .6f, .57f), .35f, .45f, shutter, new Vector2(1, 7)),
            steel = Std("STO_Steel", new Color(.16f, .17f, .18f), .35f, .5f),
            board = Std("STO_Board", new Color(.07f, .075f, .08f), .2f, 0),
            neon = Emissive("STO_Neon", new Color(1f, .55f, .18f) * 3.5f),
            glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GarageUpgrade/Garage_Bulb_Glow.mat"),
            beacon = Emissive("STO_Beacon", new Color(4f, .3f, .2f)),
            window = Emissive("STO_Window", new Color(1f, .78f, .5f) * 1.6f),
            shade = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/GarageUpgrade/Garage_LampShade.asset"),
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
        };
        k.text = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/WorldText/WorldText_LegacyRuntime.mat");
        return k;
    }

    static Material Paint(Theme t, int n) => Std("STO" + n + "_Paint", t.paint * .85f, .3f, .1f);

    const string Folder = "Assets/Art/STO";
    static Material Tri(string name, Texture2D tex, Color color, float scale, float gloss, Texture2D dirt, float dirtStrength)
        => SceneDressingKit.Tri(Folder, name, tex, color, scale, gloss, dirt, dirtStrength);
    static Material Std(string name, Color color, float gloss, float metallic, Texture2D tex = null, Vector2 tiling = default)
        => SceneDressingKit.Std(Folder, name, color, gloss, metallic, tex, tiling);
    static Material Emissive(string name, Color hdr) => SceneDressingKit.Emissive(Folder, name, hdr);

    static Texture2D Texture(string name, int size, Func<int, int, int, Color> pixel)
    {
        string path = Dir + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(existing != null) return existing;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for(int y = 0; y < size; y++) for(int x = 0; x < size; x++) tex.SetPixel(x, y, pixel(x, y, size));
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f);

    // Вертикальные гофры, ржавые потёки сверху вниз; альфа — маска гладкости.
    static Color Corrugated(int x, int y, int size)
    {
        float rib = .5f + .5f * Mathf.Sin(x / (float)size * Mathf.PI * 2 * 8);
        float shade = .7f + .3f * rib;
        int col = x / 6;
        float streak = 0;
        if(Hash(col) < .3f)
        {
            float length = .25f + Hash(col + 101) * .7f;
            float fromTop = 1 - y / (float)size;
            streak = Mathf.Clamp01(1 - fromTop / length) * (.5f + .5f * Hash(col + 7));
        }
        var c = Color.Lerp(new Color(shade, shade, shade), new Color(.62f, .42f, .3f) * shade, streak * .8f);
        c.a = 1 - streak * .7f;
        return c;
    }

    static Color ShutterStripes(int x, int y, int size)
    {
        float rib = .5f + .5f * Mathf.Sin(y / (float)size * Mathf.PI * 2 * 12);
        float v = .72f + .28f * rib - Mathf.PerlinNoise(x * .05f, y * .02f) * .12f;
        return new Color(v, v, v, .8f);
    }

    static Color HazardStripes(int x, int y, int size)
    {
        bool yellow = ((x + y) / 32) % 2 == 0;
        float wear = Mathf.PerlinNoise(x * .04f, y * .04f);
        var c = yellow ? new Color(.92f, .72f, .12f) : new Color(.07f, .07f, .07f);
        c = Color.Lerp(c, new Color(.35f, .33f, .3f), Mathf.Clamp01((wear - .62f) * 3f));
        c.a = .6f;
        return c;
    }

    static void Label(Transform parent, string name, Vector3 local, string text, float size, Color color, Vector3 forward, Kit k)
        => SceneDressingKit.Label(parent, name, local, text, size, color, forward);
}
