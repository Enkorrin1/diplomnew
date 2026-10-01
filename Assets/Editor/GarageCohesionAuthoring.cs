using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Canonical, shared visuals; existing gameplay roots and checkpoint IDs are retained.</summary>
public static class GarageCohesionAuthoring
{
    const string Output = "Assets/Prefabs/GarageProps";
    const string Garage = "Assets/Downloads/GarageAssetPack/Prefabs/";
    const string Apocalypse = "Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Props/";
    const string City = "Assets/Downloads/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/";
    const string Tabletop = "Assets/Downloads/Smiley's Low Poly Tabletop Items/Prefabs/";
    static readonly Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, string> sources = new Dictionary<string, string>();
    static readonly List<string> report = new List<string>();

    [MenuItem("RogueDrive/Garage/Unify props and enrich interior")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "GarageScene")
            throw new InvalidOperationException("Open GarageScene in Edit Mode first.");
        if (GameObject.Find("Garage_CohesiveDetails") != null)
            throw new InvalidOperationException("Cohesion pass already applied; edit the saved objects directly.");
        Directory.CreateDirectory("Temp/GarageCohesion");
        // Save the current in-memory state to a separate recovery scene, including user edits.
        EditorSceneManager.SaveScene(scene, "Temp/GarageCohesion/GarageScene.before.unity", true);
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        report.Clear(); visuals.Clear(); sources.Clear();
        Build("Battery", Garage + "CarBattery.prefab", .32f);
        Build("FuelCanister", Garage + "JerrycanLarge.prefab", .40f);
        Build("WaterCanister", Garage + "JerrycanLarge.prefab", .40f, new Color(.16f,.48f,.55f));
        Build("OilCanister", Garage + "JerrycanLarge.prefab", .40f, new Color(.65f,.40f,.12f));
        Build("Wrench", Garage + "WrenchMedium.prefab", .24f);
        Build("Screwdriver", Garage + "Screwdriver.prefab", .22f);
        Build("Hammer", Garage + "Hammer.prefab", .28f);
        Build("Cell", Garage + "BatterySmall.prefab", .085f);
        Build("Medkit", Apocalypse + "First_Aid.prefab", .30f);
        Build("Radio", Apocalypse + "Walkie_talkie.prefab", .22f);
        Build("Flashlight", Apocalypse + "Flashlight.prefab", .25f);

        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        int count = 0;
        foreach (var t in all)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            var key = Category(t.gameObject);
            if (key == null) continue;
            ReplaceVisual(t.gameObject, key);
            count++;
        }
        // The repair bundle uses the same tools as the loose pickups.
        var bundle = GameObject.Find("Departure_RepairKit");
        if (bundle != null)
        {
            foreach (var t in bundle.GetComponentsInChildren<Transform>().ToArray())
                if (t.name == "WrenchMedium" || t.name == "Screwdriver" || t.name == "Hammer")
                {
                    if (t.GetComponent<Renderer>() != null) continue;
                    ReplaceVisual(t.gameObject, t.name == "WrenchMedium" ? "Wrench" : t.name, false);
                }
            FitCollider(bundle);
        }

        var root = new GameObject("Garage_CohesiveDetails").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Garage cohesive details");
        var services = Group(root, "01_Ventilation_And_Power");
        // Ducts follow side walls; the central vehicle lane and exit remain clear.
        foreach (float x in new[] { -11.15f, 11.15f })
        {
            for (int i = 0; i < 9; i++)
                Decor(City + "Prop_ACVent_Stright.prefab", services, new Vector3(x,4.48f,-8+i*2), new Vector3(0,90,0), 2f, 2);
            for (int i = 0; i < 3; i++)
                Decor(City + "Prop_RoofVent_01.prefab", services, new Vector3(x,4.10f,-6+i*6), new Vector3(180,0,0), .68f);
        }
        Decor(City + "Prop_RoofVent_04.prefab", services, new Vector3(11.60f,2.9f,2.2f), new Vector3(0,0,90), .85f);
        Decor(City + "Prop_ElectracityCabinet_01.prefab", services, new Vector3(11.35f,.03f,-2.6f), new Vector3(0,-90,0), 1.65f, 1, true);
        Decor(City + "Prop_ElectracityCabinet_01.prefab", services, new Vector3(11.35f,.03f,-3.35f), new Vector3(0,-90,0), 1.65f, 1, true);
        var workshop = Group(root, "02_Workshop_Details");
        Decor(City + "Prop_RoadCone_01.prefab", workshop, new Vector3(-3.5f,.03f,6.6f), Vector3.zero, .58f, 1, true);
        Decor(City + "Prop_RoadCone_01.prefab", workshop, new Vector3(3.5f,.03f,6.6f), Vector3.zero, .58f, 1, true);
        Decor(City + "Prop_TrashBag_01.prefab", workshop, new Vector3(-9.45f,.03f,3.6f), new Vector3(0,15,0), .43f, 1, true);
        Decor(City + "Prop_TrashBag_01.prefab", workshop, new Vector3(-9.58f,.03f,4.04f), new Vector3(0,100,0), .43f, 1, true);
        var living = Group(root, "03_Desk_And_Living");
        Decor(Tabletop + "SpiralNotebook.prefab", living, new Vector3(9.85f,.89f,5.72f), new Vector3(0,12,0), .28f);
        Decor(Tabletop + "PenBlue.prefab", living, new Vector3(9.88f,.92f,5.7f), new Vector3(0,22,0), .15f);
        Decor(Tabletop + "SpiralNotebook.prefab", living, new Vector3(4.30f,.805f,-6.45f), new Vector3(0,-12,0), .28f);
        Decor(Tabletop + "PenBlue.prefab", living, new Vector3(4.40f,.82f,-6.40f), new Vector3(0,60,0), .15f);
        Decor(Tabletop + "TissueBoxRectangle.prefab", living, new Vector3(1.35f,.90f,-10.3f), Vector3.zero, .24f);

        // Keep the shop's spawned supplies visually identical to the authored supplies.
        foreach (var key in new[] { "FuelCanister", "WaterCanister", "OilCanister" })
        {
            string path = "Assets/Resources/VehicleService/" + key + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { ReplaceVisual(prefab, key); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        report.Insert(0, "Unified interactive roots: " + count);
        File.WriteAllLines("Temp/GarageCohesion/applied.txt", report);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Refine();
        Capture();
    }

    static string Category(GameObject go)
    {
        var part = go.GetComponent<CarPartItem>();
        if (part != null)
        {
            string type = part.ItemType.ToString();
            return new[] { "Battery", "FuelCanister", "WaterCanister", "OilCanister" }.Contains(type) ? type : null;
        }
        var function = go.GetComponent<GarageItemFunction>();
        if (function == null) return null;
        string kind = function.Kind.ToString();
        if (kind == "Battery") return "Cell";
        return new[] { "Wrench", "Screwdriver", "Hammer", "Medkit", "Radio", "Flashlight" }.Contains(kind) ? kind : null;
    }

    static void Build(string key, string path, float length, Color? tint = null)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing canonical asset: " + path);
        sources[key] = path;
        var root = new GameObject(key + "_Visual");
        try
        {
            var model = Object.Instantiate(source, root.transform);
            model.name = "Model";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
            var bounds = BoundsOf(root);
            model.transform.localScale *= length / Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            bounds = BoundsOf(root);
            model.transform.position -= bounds.center;
            if (tint.HasValue)
            {
                var mat = Material(key, tint.Value);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = r.sharedMaterials.Select(m => mat).ToArray();
            }
            visuals[key] = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + key + "_Visual.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void ReplaceVisual(GameObject go, string key, bool physics = true)
    {
        var oldRenderers = go.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        if (oldRenderers.Length == 0) return;
        var old = BoundsOf(go);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sources[key]);
        var reference = source.GetComponentInChildren<MeshRenderer>();
        var rotation = oldRenderers[0].transform.rotation * Quaternion.Inverse(reference.transform.rotation);
        bool tool = key == "Wrench" || key == "Screwdriver" || key == "Hammer" || key == "Flashlight" || key == "Radio";
        if (!tool) rotation = Quaternion.Euler(0,rotation.eulerAngles.y,0);
        foreach (var r in oldRenderers) { Undo.RecordObject(r,"Unify item visual"); r.enabled = false; }
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(visuals[key]);
        Undo.RegisterCreatedObjectUndo(visual,"Unify item visual");
        visual.name = "Canonical_" + key;
        visual.transform.rotation = rotation;
        var b = BoundsOf(visual);
        visual.transform.position += new Vector3(old.center.x-b.center.x,old.min.y-b.min.y,old.center.z-b.center.z);
        visual.transform.SetParent(go.transform,true);
        if (physics) FitCollider(go);
        report.Add(go.name + " -> " + key);
    }

    public static void FitCollider(GameObject go)
    {
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        Undo.RecordObject(box,"Fit unified prop collider");
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            if(c != box && !c.isTrigger) Undo.DestroyObjectImmediate(c);
        bool first = true; var bounds = new Bounds();
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy))
        {
            var b=r.localBounds;
            for(int i=0;i<8;i++)
            {
                var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=go.transform.InverseTransformPoint(r.transform.TransformPoint(p));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        box.center=bounds.center; box.size=Vector3.Max(bounds.size,new Vector3(.001f,.001f,.001f)); box.enabled=true; box.isTrigger=false;
        var body=go.GetComponent<Rigidbody>();
        if(body!=null){body.ResetCenterOfMass();body.ResetInertiaTensor();}
    }

    static Transform Group(Transform parent,string name)
    { var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform; }

    static GameObject Decor(string path,Transform parent,Vector3 bottom,Vector3 angles,float size,int axis=-1,bool solid=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null)throw new InvalidOperationException("Missing decoration: "+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        go.transform.rotation=Quaternion.Euler(angles);
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterials=r.sharedMaterials.Select(CompatibleMaterial).ToArray();
        var b=BoundsOf(go);float extent=axis<0?Mathf.Max(b.size.x,b.size.y,b.size.z):b.size[axis];
        go.transform.localScale*=size/extent;b=BoundsOf(go);
        go.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
        if(solid)FitCollider(go);
        report.Add("Added "+go.name);
        return go;
    }

    static Material CompatibleMaterial(Material original)
    {
        if(original==null)return Material("Fallback",new Color(.5f,.5f,.5f));
        if(original.shader!=null && original.shader.isSupported && !original.shader.name.Contains("Universal") && !original.shader.name.Contains("Shader Graph"))return original;
        string path=Output+"/Compatible_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original))+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null)return mat;
        mat=new Material(Shader.Find("Standard"));
        mat.color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
        mat.mainTexture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
        mat.SetFloat("_Glossiness",.15f);
        AssetDatabase.CreateAsset(mat,path);return mat;
    }

    static Material Material(string name,Color color)
    {
        string path=Output+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;mat.SetFloat("_Glossiness",.18f);return mat;
    }

    public static Bounds BoundsOf(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r is MeshRenderer).ToArray();
        if(rs.Length==0)throw new InvalidOperationException("No visible mesh: "+go.name);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }

    public static void Capture()
    {
        Directory.CreateDirectory("Temp/GarageDressing");
        GarageSetDressing.Capture("cohesion-overview",new Vector3(0,3.5f,-7),new Vector3(0,2,6));
        GarageSetDressing.Capture("cohesion-workshop",new Vector3(-3,2.4f,.5f),new Vector3(-10,1.4f,5.6f));
        GarageSetDressing.Capture("cohesion-utility",new Vector3(5,2.1f,-5),new Vector3(10.6f,1.7f,.6f));
        GarageSetDressing.Capture("cohesion-living",new Vector3(1,2.2f,-3.8f),new Vector3(6,1,-8));
    }

    [MenuItem("RogueDrive/Garage/Refine unified prop spacing")]
    public static void Refine()
    {
        if(Application.isPlaying || SceneManager.GetActiveScene().name!="GarageScene")throw new InvalidOperationException("Open GarageScene in Edit Mode.");
        foreach(var t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray())
            if(t.Cast<Transform>().Any(c=>c.name.StartsWith("Canonical_")))
            {
                RemoveRetiredMeshes(t.gameObject);
                if(t.GetComponent<Rigidbody>()!=null)FitCollider(t.gameObject);
            }
        // Enlarge the previous miniature pegboard tools without overlapping neighbours.
        foreach(float z in new[]{2.08f,4.03f,5.93f})
        {
            var tools=Object.FindObjectsByType<GarageItemFunction>(FindObjectsSortMode.None)
                .Where(f=>f.gameObject.scene.name=="GarageScene" && f.Kind==GarageItemFunction.ItemKind.Wrench)
                .Where(f=>{var b=BoundsOf(f.gameObject);return b.center.x < -10 && b.size.y>.2f && Mathf.Abs(b.center.z-z)<.2f;})
                .OrderBy(f=>BoundsOf(f.gameObject).center.z).ToArray();
            for(int i=0;i<tools.Length;i++)
            {
                var b=BoundsOf(tools[i].gameObject);
                Undo.RecordObject(tools[i].transform,"Space pegboard tools");
                tools[i].transform.position+=new Vector3(0,1.60f-b.center.y,z+(i-(tools.Length-1)*.5f)*.13f-b.center.z);
            }
        }
        // Two canisters on each lower shelf face the aisle, with their narrow side along the row.
        foreach(var part in Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None))
        {
            if(part.gameObject.scene.name!="GarageScene" || !part.ItemType.ToString().Contains("Canister"))continue;
            var b=BoundsOf(part.gameObject);
            if(b.center.x < -10.7f && b.center.z>8)
                OrientCanister(part.gameObject,0);
            else if(b.center.x>9.6f && b.center.z>9.6f)
                OrientCanister(part.gameObject,90);
        }
        var mounted=GameObject.Find("GarageHubRoot/PodiumRoot/PodiumAnchor/Hotspot_Battery_EngineBay")?.transform.Find("Placeholder_MountedBattery");
        if(mounted!=null && mounted.Find("Canonical_Battery")==null)
        {
            // Keep the placeholder object: the assembly system owns its active state.
            foreach(var r in mounted.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Battery_Visual.prefab"));
            visual.name="Canonical_Battery";visual.transform.position=mounted.position;visual.transform.SetParent(mounted,true);
        }
        foreach(var key in new[]{"FuelCanister","WaterCanister","OilCanister"})
        {
            string path="Assets/Resources/VehicleService/"+key+".prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{RemoveRetiredMeshes(prefab);FitCollider(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component) &&
                   (component is Transform || component is Renderer || component is Collider))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    static void RemoveRetiredMeshes(GameObject root)
    {
        // Runtime inventory/drop bounds include disabled renderers. Keep GameObjects and
        // gameplay references, but remove retired geometry so it cannot inflate those bounds.
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if(!r.enabled)
            {
                var mesh=r.GetComponent<MeshFilter>();
                Undo.DestroyObjectImmediate(r);
                if(mesh!=null)Undo.DestroyObjectImmediate(mesh);
            }
    }

    static void OrientCanister(GameObject item,float yaw)
    {
        var visual=item.transform.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("Canonical_"));if(visual==null)return;
        var old=BoundsOf(item);visual.rotation=Quaternion.Euler(0,yaw,0);var b=BoundsOf(item);
        visual.position+=new Vector3(old.center.x-b.center.x,old.min.y-b.min.y,old.center.z-b.center.z);
        FitCollider(item);
    }
}
