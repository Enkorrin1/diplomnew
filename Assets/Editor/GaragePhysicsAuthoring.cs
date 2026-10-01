using System;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GaragePhysicsAuthoring
{
    static Bounds Bounds(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>();
        var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static bool Portable(string name)
    {
        return new[]{"Wrench","Hammer","Screwdriver","Bolt","CarBattery","CarWheel","Jerrycan","BatterySmall","BatteryLarge","Barrelfbx","CardboardBox","CoffeeMug","DrinkingGlass","Ceramic","CerealBowl","CookingPot","NonStickPan","ChefsKnife","SoftDrink","PizzaBox","Walkie_talkie","Flashlight","First_Aid","Fuel_Canister","Gas_Burner"}.Any(name.Contains);
    }
    static bool Furniture(string name) => new[]{"sofa","SofaThree","Workbench","StorageShelf","rack","DiningTable","DiningChair","CoffeeTable","SideTable","KitchenCounter","Refrigerator","metal box","M.C.C","M.O.C","M.W.O.C","WoodenPallet"}.Any(name.Contains);
    static void Unpack(GameObject go)
    {
        var instance=PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if(instance!=null)PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
    }
    public static void Author()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="GarageScene")throw new InvalidOperationException("Open GarageScene");
        if(GameObject.Find("Garage_PhysicalItems")!=null)throw new InvalidOperationException("Already authored");
        Directory.CreateDirectory("Temp/GaragePhysics");
        File.Copy("Assets/Scenes/GarageScene.unity","Temp/GaragePhysics/GarageScene.before.unity",true);
        var root=new GameObject("Garage_PhysicalItems").transform;
        var targets=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.gameObject.scene==scene && t.gameObject.activeInHierarchy && Portable(t.name))
            .OrderBy(t=>Depth(t)).ToArray();
        int count=0;
        foreach(var target in targets)
        {
            if(target==null || target.IsChildOf(root) || target.GetComponentInParent<RogueDrive.Gameplay.VehicleModularState>()!=null ||
                target.GetComponentInParent<GarageVehicleBoarding>()!=null || target.GetComponentInParent<CarPartItem>()!=null || target.GetComponentInParent<PhysicsProp>()!=null)continue;
            // Leave other authored quest and pocket interactions intact.
            if(target.GetComponents<MonoBehaviour>().Any(m=>m is IGarageInteractable))continue;
            if(target.GetComponentsInChildren<Renderer>().Length==0)continue;
            var b=Bounds(target.gameObject);if(b.size.magnitude>3.2f || b.size.magnitude<.04f)continue;
            string title=target.name;
            Unpack(target.gameObject);
            var wrapper=new GameObject(title+"_Portable");wrapper.transform.SetParent(root);wrapper.transform.position=b.center;
            target.SetParent(wrapper.transform,true);wrapper.transform.position+=Vector3.up*.015f;
            foreach(var c in wrapper.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            foreach(var body in wrapper.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(body);
            var box=wrapper.AddComponent<BoxCollider>();box.size=b.size;box.center=Vector3.zero;
            var rb=wrapper.AddComponent<Rigidbody>();rb.mass=Mass(title);rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode=CollisionDetectionMode.Continuous;rb.maxAngularVelocity=12;
            if(title.Contains("CarWheel") || title.Contains("CarBattery") || title.Contains("Jerrycan") || title=="Fuel_Canister")
            {
                var part=wrapper.AddComponent<BunkerAssemblyItem>();
                part.Configure(title.Contains("CarWheel")?BunkerAssemblyItemType.Wheel:title.Contains("CarBattery")?BunkerAssemblyItemType.Battery:BunkerAssemblyItemType.FuelCanister,Name(title));
                if(title.Contains("Jerrycan") || title=="Fuel_Canister")wrapper.AddComponent<BunkerFluidContainer>().Configure(BunkerFluidType.Gasoline,20,10);
            }
            else
            {
                var prop=wrapper.AddComponent<BunkerPhysicsProp>();prop.Configure(Name(title));
                prop.SetPocketSized(new[]{"Wrench","Hammer","Screwdriver","Bolt","BatterySmall","BatteryLarge","CoffeeMug","DrinkingGlass","CerealBowl","ChefsKnife","SoftDrink","Walkie","Flashlight","First_Aid"}.Any(title.Contains));
            }
            AddUse(wrapper,title);
            count++;
        }
        // Existing quest parts remain the same objects with their original references.
        foreach(var item in Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).Where(i=>i.gameObject.scene==scene))
        {
            var rb=item.GetComponent<Rigidbody>();if(rb==null)rb=item.gameObject.AddComponent<Rigidbody>();rb.useGravity=true;rb.isKinematic=false;
            if(item.GetComponent<Collider>()==null){var b=Bounds(item.gameObject);var c=item.gameObject.AddComponent<BoxCollider>();c.center=item.transform.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/item.transform.lossyScale.x,b.size.y/item.transform.lossyScale.y,b.size.z/item.transform.lossyScale.z);}
            if(item.ItemType==BunkerAssemblyItemType.FuelCanister || item.ItemType==BunkerAssemblyItemType.WaterCanister)
            {
                var fluid=item.GetComponent<FluidContainer>();if(fluid==null)fluid=item.gameObject.AddComponent<BunkerFluidContainer>();
                if(item.ItemType==BunkerAssemblyItemType.WaterCanister)fluid.Configure(BunkerFluidType.Water,10,10);
                Use(item.gameObject,GarageItemUse.UseKind.Fluid);
            }
            if(item.ItemType==BunkerAssemblyItemType.Wheel)Use(item.gameObject,GarageItemUse.UseKind.SpareWheel);
        }
        int furniture=0;
        foreach(var target in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.gameObject.scene==scene && Furniture(t.name)).ToArray())
        {
            if(target.IsChildOf(root) || target.GetComponentInParent<CarPartItem>()!=null)continue;
            Unpack(target.gameObject);
            foreach(var c in target.GetComponents<Collider>())Object.DestroyImmediate(c);
            foreach(var rb in target.GetComponents<Rigidbody>())Object.DestroyImmediate(rb);
            foreach(var mesh in target.GetComponentsInChildren<MeshFilter>())
            {
                if(mesh.GetComponentInParent<PhysicsProp>()!=null || mesh.GetComponentInParent<CarPartItem>()!=null || mesh.sharedMesh==null)continue;
                foreach(var c in mesh.GetComponents<Collider>())Object.DestroyImmediate(c);
                var cmesh=mesh.gameObject.AddComponent<MeshCollider>();cmesh.sharedMesh=mesh.sharedMesh;cmesh.convex=false;
            }
            furniture++;
        }
        var sofa=GameObject.Find("sofa");var bounds=Bounds(sofa);
        var markers=new GameObject("Sofa_Awakening").transform;markers.SetParent(sofa.transform,true);
        var eyes=new GameObject("Eyes_Lying").transform;eyes.SetParent(markers,true);
        eyes.position=new Vector3(bounds.center.x+.45f,bounds.min.y+.68f,bounds.center.z+.05f);
        eyes.rotation=Quaternion.Euler(-15,-70,-12);
        var standing=new GameObject("Standing_Clearance").transform;standing.SetParent(markers,true);
        standing.position=new Vector3(bounds.min.x-.85f,.08f,bounds.center.z+.7f);
        standing.rotation=Quaternion.LookRotation(new Vector3(-standing.position.x,0,-standing.position.z));
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();var so=new SerializedObject(intro);
        so.FindProperty("wakeEyePose").objectReferenceValue=eyes;so.FindProperty("wakeStandingPose").objectReferenceValue=standing;
        so.ApplyModifiedPropertiesWithoutUndo();
        var player=Object.FindFirstObjectByType<GaragePlayerController>();player.transform.SetPositionAndRotation(standing.position,standing.rotation);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/GaragePhysics/authored.txt",$"New portable objects: {count}; furniture processed: {furniture}; sofa bounds: {bounds}");
    }
    static int Depth(Transform t){int n=0;while(t.parent!=null){n++;t=t.parent;}return n;}
    static float Mass(string n)=>n.Contains("Barrel")?15:n.Contains("CarWheel")?9:n.Contains("CarBattery")?12:n.Contains("Cardboard")?2:n.Contains("Wrench")?1:.4f;
    static string Name(string n)
    {
        if(n.Contains("Wrench"))return "Гаечный ключ";
        if(n.Contains("Hammer"))return "Молоток";
        if(n.Contains("Screwdriver"))return "Отвёртка";
        if(n.Contains("CardboardBox"))return "Ящик для вещей";
        if(n.Contains("Flashlight"))return "Фонарь";
        if(n.Contains("Walkie"))return "Рация";
        if(n.Contains("First_Aid"))return "Аптечка";
        if(n.Contains("CarWheel"))return "Запасное колесо";
        if(n.Contains("CarBattery"))return "Аккумулятор";
        if(n.Contains("Jerrycan") || n.Contains("Fuel_Canister"))return "Канистра бензина";
        if(n.Contains("CoffeeMug"))return "Кружка";
        if(n.Contains("Glass"))return "Стакан";
        if(n.Contains("Ceramic"))return "Тарелка";
        if(n.Contains("Bowl"))return "Миска";
        if(n.Contains("CookingPot"))return "Кастрюля";
        if(n.Contains("NonStickPan"))return "Сковорода";
        if(n.Contains("Knife"))return "Кухонный нож";
        if(n.Contains("SoftDrink"))return "Стакан с напитком";
        if(n.Contains("Pizza"))return "Коробка пиццы";
        if(n.Contains("Barrel"))return "Пустая бочка";
        if(n.Contains("Bolt"))return "Болт";
        if(n.Contains("Battery"))return "Батарейка";
        if(n.Contains("Burner"))return "Газовая горелка";
        return n;
    }
    static GarageItemUse Use(GameObject go,GarageItemUse.UseKind kind)
    {var use=go.GetComponent<GarageItemUse>();if(use==null)use=go.AddComponent<GarageItemUse>();use.Configure(kind);return use;}
    static void AddUse(GameObject go,string n)
    {
        if(n.Contains("CardboardBox")){go.AddComponent<GaragePortableContainer>();Use(go,GarageItemUse.UseKind.Container);}
        else if(n.Contains("Wrench") || n.Contains("Hammer") || n.Contains("Screwdriver"))Use(go,GarageItemUse.UseKind.Repair);
        else if(n.Contains("Walkie"))Use(go,GarageItemUse.UseKind.Radio);
        else if(n.Contains("First_Aid"))Use(go,GarageItemUse.UseKind.FirstAid);
        else if(n.Contains("Flashlight"))
        {
            var beam=new GameObject("LightBeam").AddComponent<Light>();beam.transform.SetParent(go.transform,false);
            beam.type=LightType.Spot;beam.intensity=4;beam.range=18;beam.spotAngle=48;beam.color=new Color(1,.94f,.8f);beam.enabled=false;
            Use(go,GarageItemUse.UseKind.Flashlight).Configure(GarageItemUse.UseKind.Flashlight,beam);
        }
    }
}

