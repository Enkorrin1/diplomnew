using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>Authored scenery pass. Never runs at runtime or overwrites imported art.</summary>
    public static class JourneyLandscapeAuthoring
    {
        const string Folder = "Assets/Content/JourneyLandscape";
        const string City = "Assets/Downloads/SimplePoly City - Low Poly Assets/Prefab/";
        const string Forest = "Assets/Downloads/Low Poly Forest - Free Starter Pack/Prefabs/";
        const string Nature = "Assets/Downloads/SimpleNaturePack/Prefabs/";
        static readonly float[] Boundaries = { 0, 1800, 4200, 6000, 8500, 10400, 12000, 14000 };
        static readonly string[] Names = { "01_City_Exit", "02_Birch_Woods", "03_Suburban_Houses", "04_Pine_Forest", "05_Farmland_Freight", "06_Rocky_Uplands", "07_Northern_Workshop" };
        static readonly float[] Stops = { 1400, 3200, 5400, 7600, 9600, 11600 };
        internal static StageRoute route;
        static System.Random random;
        static Material soil, track, pavement;
        static string[] paths;
        static int placed;

        [MenuItem("RogueDrive/Route/Dress City Forest Journey")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage1_Outskirts") throw new InvalidOperationException("Open Stage1_Outskirts.");
            route = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageRoute>(true)).Single();
            if (route.transform.Find("Journey_Landscape") != null)
                throw new InvalidOperationException("Landscape already authored. Edit the saved objects; do not overwrite them.");
            paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Downloads" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            // Validate essentials before any scene mutation.
            foreach (string path in new[] { City + "Buildings/Building_Gas Station.prefab", City + "Buildings/Building_Auto Service.prefab", Forest + "Trees/pine_large.prefab", Forest + "Trees/birch_medium.prefab" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing art: " + path);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "JourneyLandscape");
            random = new System.Random(250925); placed = 0;
            soil = Mat("Meadow", new Color(.31f, .38f, .23f));
            track = Mat("Dirt", new Color(.44f, .38f, .27f));
            pavement = Mat("PavedCourtyard", new Color(.30f, .32f, .32f));
            var root = new GameObject("Journey_Landscape").transform;
            root.SetParent(route.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Author journey landscape");
            // Remove road-corridor walls locally; prefab sources stay unchanged.
            foreach (var chunk in route.GetComponentsInChildren<TrackChunk>(true))
            {
                foreach (var t in chunk.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf("Guardrail", StringComparison.OrdinalIgnoreCase) >= 0 || t.name == "Landscape")
                    { Undo.RecordObject(t.gameObject, "Open road verges"); t.gameObject.SetActive(false); }
            }
            var oldStops = route.transform.Find("Journey_RoadsideStops");
            if (oldStops != null) { Undo.RecordObject(oldStops.gameObject, "Replace prototype stops"); oldStops.gameObject.SetActive(false); }
            var zones = Names.Select(name => Child(root, name)).ToArray();
            // A continuous collidable landscape, 600 m wide; no perimeter colliders.
            for (float d = -200; d < route.Length + 250; d += 240)
                Ground(zones[Zone(Mathf.Max(0, d))], d, Mathf.Min(d + 240, route.Length + 250));
            for (float d = 90; d < route.Length - 120; d += 34)
            {
                int zone = Zone(d);
                var parent = zones[zone];
                bool town = zone == 0 || zone == 2 || zone == 6;
                bool dense = zone == 1 || zone == 3;
                if (town)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * Range(30, 42);
                        if (Reserved(d, x)) continue;
                        string building = zone == 0 && d < 1150
                            ? City + "Buildings/Building_Residential_color0" + (1 + random.Next(3)) + ".prefab"
                            : City + "Buildings/Building_House_0" + (1 + random.Next(4)) + "_color01.prefab";
                        Place(building, parent, At(d, x), side * 90 + Yaw(d), zone == 0 && d < 1150 ? Range(18, 26) : Range(12, 17), 1);
                        if (zone == 0 && d < 900)
                            Place(City + "Buildings/Building Sky_small_color01.prefab", parent, At(d + 8, side * 100), Yaw(d), Range(35, 55), 1);
                        if ((int)d % 3 == 0)
                            Place(City + "Props/Props_Street Light.prefab", parent, At(d, side * 13), Yaw(d), 7, 0);
                        Pad(parent, "Driveway", d, side * 22, 16, 7, pavement);
                    }
                }
                int trees = dense ? 20 : town ? 5 : zone == 4 ? 3 : 7;
                for (int n = 0; n < trees; n++)
                {
                    float sample = d + Range(-15, 15);
                    float x = Range(19, 220) * (n % 2 == 0 ? -1 : 1);
                    if (town && Mathf.Abs(x) < 75 || Reserved(sample, x)) continue;
                    string tree = zone == 3 || zone == 5 ? "pine_large" : n % 3 == 0 ? "oak_mdeium" : "birch_medium";
                    Place(Forest + "Trees/" + tree + ".prefab", parent, At(sample, x), Range(0, 360), dense ? Range(10, 19) : Range(9, 15), 2);
                }
                for (int n = 0; n < (dense ? 6 : 2); n++)
                {
                    float x = Range(17, 110) * (n % 2 == 0 ? -1 : 1);
                    if (Reserved(d, x) || town && Mathf.Abs(x) < 60) continue;
                    Place(Nature + "Bush_0" + (1 + random.Next(3)) + ".prefab", parent, At(d + Range(-12, 12), x), Range(0, 360), Range(1.2f, 3.2f), 0);
                }
                if (zone == 5 || (!town && random.Next(3) == 0))
                {
                    float x = Range(32, 170) * (random.Next(2) == 0 ? -1 : 1);
                    if (!Reserved(d, x)) Place(Forest + "Rocks/stone_large.prefab", parent, At(d, x), Range(0, 360), zone == 5 ? Range(9, 24) : Range(3, 7), 1);
                }
            }
            // Cross streets and open city blocks make the initial exit read as a town.
            foreach (float d in new float[] { 260, 650, 1050, 1550, 4570, 4950, 5780 })
                Pad(zones[Zone(d)], "Cross_Street", d, 0, 175, 9, pavement);
            for (int i = 0; i < Stops.Length; i++) BuildDetour(root, i);
            var lighting = root.gameObject.AddComponent<JourneyPresentation>();
            lighting.Configure(zones.Select(t => t.gameObject).ToArray());
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[JourneyLandscape] {placed} art instances; seven districts; six return loops; {route.Length:F0} m preserved.");
        }

        [MenuItem("RogueDrive/Route/Refine Journey Streets And Woodland")]
        public static void Refine()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            var landscape=route.transform.Find("Journey_Landscape");
            if(landscape.Find("Street_And_Woodland_Detail")!=null)throw new InvalidOperationException("Already refined.");
            var detail=Child(landscape,"Street_And_Woodland_Detail");
            random=new System.Random(7183);
            pavement=Mat("PavedCourtyard",new Color(.30f,.32f,.32f));
            track=Mat("Dirt",new Color(.44f,.38f,.27f));
            foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
                if(t.gameObject.activeSelf && (t.name=="Left_Guardrail"||t.name=="Right_Guardrail"))t.gameObject.SetActive(false);
            var city=landscape.Find(Names[0]);
            var houses=city.Cast<Transform>().Where(t=>t.name.StartsWith("Building_Residential")).ToArray();
            string[] shops={"Building_Coffee Shop","Building_Books Shop","Building_Drug Store","Building_Bakery","Building_Clothing","Building_Super Market","Building_Residential_color02"};
            for(int i=0;i<houses.Length;i++)
            {
                var old=houses[i];float d=route.ProjectDistance(old.position);route.Evaluate(d,out var p,out var q);
                float side=Mathf.Sign(Vector3.Dot(old.position-p,q*Vector3.right));
                old.gameObject.SetActive(false);
                Place(City+"Buildings/"+shops[i%shops.Length]+".prefab",detail,At(d,side*Range(25,34)),Yaw(d)-side*90,i%7==6?23:Range(13,18),1);
                Pad(detail,"Shop_Forecourt",d,side*22,22,22,pavement);
                Place(City+"Props/Props_Dustbin.prefab",detail,At(d+7,side*15),Yaw(d),1.2f,1);
                if(i%4==0)Place(City+"Props/Props_Bus Stop.prefab",detail,At(d-10,side*15),Yaw(d)-side*90,5,1);
            }
            // Break up the skyline: taller blocks stand behind the shops with open gaps.
            int sky=0;foreach(var t in city.Cast<Transform>().Where(t=>t.name.StartsWith("Building Sky")))
            {if(sky++%3!=0)t.gameObject.SetActive(false);else t.Rotate(0,90,0);}
            for(float d=1850;d<12000;d+=16)
            {
                int zone=Zone(d);if(zone!=1&&zone!=3)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*Range(17,31);if(Reserved(d,x))continue;
                    Place(Forest+"Trees/"+(zone==3?"pine_large":"birch_medium")+".prefab",detail,At(d,x),Range(0,360),Range(13,20),2);
                    for(int n=0;n<3;n++)
                    {
                        float px=side*Range(13,32);
                        Place(Nature+"Bush_0"+(1+random.Next(3))+".prefab",detail,At(d+Range(-7,7),px),Range(0,360),Range(1.2f,2.4f),0);
                    }
                }
            }
            // Narrow painted travel surface; the original collision mesh remains continuous.
            var asphalt=Mat("CountryAsphalt",new Color(.25f,.27f,.28f));
            var verge=Mat("RoadShoulder",new Color(.42f,.39f,.31f));
            for(float start=0;start<route.Length;start+=240)
            {
                float end=Mathf.Min(start+240,route.Length);
                foreach(int side in new[]{-1,1})
                {
                    var v=new List<Vector3>();var tris=new List<int>();int count=Mathf.CeilToInt((end-start)/5);
                    for(int n=0;n<=count;n++)
                    {
                        float d=Mathf.Lerp(start,end,(float)n/count);
                        var a=At(d,side*7.5f);var b=At(d,side*12.05f);a.y=b.y=.013f;v.Add(a);v.Add(b);
                        if(n<count){int j=n*2;if(side>0)tris.AddRange(new[]{j,j+2,j+1,j+1,j+2,j+3});else tris.AddRange(new[]{j,j+1,j+2,j+1,j+3,j+2});}
                    }
                    MeshObject(detail,"Gravel_Shoulder",v.ToArray(),tris.ToArray(),verge,false);
                }
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static int Zone(float d) { int i = 0; while (i < 6 && d >= Boundaries[i + 1]) i++; return i; }
        static float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        static float Yaw(float d) { route.Evaluate(d, out _, out var q); return q.eulerAngles.y; }
        static float Height(float d, float x)
        {
            float outside = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(110, 290, Mathf.Abs(x)));
            return -.16f + outside * (4 + 16 * Mathf.PerlinNoise(d * .003f, x * .008f + 25));
        }
        internal static Vector3 At(float d, float x)
        { route.Evaluate(d, out var p, out var q); p += q * Vector3.right * x; p.y = Height(d, x); return p; }
        internal static bool Reserved(float d, float x)
        {
            for (int i = 0; i < Stops.Length; i++)
            {
                float t = (d - Stops[i]) / 230;
                if (Mathf.Abs(t) > 1.2f) continue;
                float side = i % 2 == 0 ? 1 : -1;
                if (Mathf.Sign(x) == side && Mathf.Abs(x) < 120) return true;
            }
            return false;
        }
        internal static Transform Child(Transform p, string name)
        { var go = new GameObject(name); go.transform.SetParent(p, false); return go.transform; }
        internal static Material Mat(string name, Color color)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
            if (m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            m.SetFloat("_Glossiness", .06f); AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); return m;
        }
        internal static void Ground(Transform parent, float start, float end)
        {
            var vertices = new List<Vector3>(); var tris = new List<int>();
            int rows = Mathf.CeilToInt((end - start) / 8), columns = 61;
            for (int row = 0; row <= rows; row++)
            {
                float d = Mathf.Lerp(start, end, (float)row / rows);
                for (int col = 0; col < columns; col++) vertices.Add(At(d, -300 + col * 10));
            }
            for (int row = 0; row < rows; row++) for (int col = 0; col < columns - 1; col++)
            { int a = row * columns + col; tris.AddRange(new[] { a, a + columns, a + 1, a + 1, a + columns, a + columns + 1 }); }
            MeshObject(parent, "Terrain_" + start, vertices.ToArray(), tris.ToArray(), soil, true);
        }
        static void MeshObject(Transform parent, string name, Vector3[] vertices, int[] triangles, Material material, bool collision)
        {
            var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".asset"));
            var go = Child(parent, name).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        internal static void Pad(Transform parent, string name, float distance, float offset, float width, float length, Material material)
        {
            route.Evaluate(distance, out var p, out var q); p += q * Vector3.right * offset; p.y = -.115f;
            var v = new[] { new Vector3(-width/2,0,-length/2),new Vector3(width/2,0,-length/2),new Vector3(-width/2,0,length/2),new Vector3(width/2,0,length/2) };
            for(int i=0;i<4;i++) v[i]=p+q*v[i];
            MeshObject(parent, name, v, new[] { 0,2,1,1,2,3 }, material, false);
        }
        static void BuildDetour(Transform parent, int index)
        {
            float d = Stops[index], side = index % 2 == 0 ? 1 : -1;
            string[] names = { "Last_Gas_Station", "Birch_Camp", "Suburban_Service", "Forestry_Camp", "Freight_Yard", "Rocky_Picnic" };
            var stop = Child(parent, "Detour_" + (index + 1) + "_" + names[index]);
            var v = new List<Vector3>(); var tris = new List<int>();
            for (int n = 0; n <= 60; n++)
            {
                float t = n / 60f, dist = d - 210 + t * 420;
                float x = side * 76 * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2);
                Vector3 a = At(dist, x - 4), b = At(dist, x + 4); a.y = b.y = -.10f;
                v.Add(a);v.Add(b);
                if(n<60){int a0=n*2;tris.AddRange(new[]{a0,a0+2,a0+1,a0+1,a0+2,a0+3});}
            }
            MeshObject(stop, "Return_Loop", v.ToArray(), tris.ToArray(), index==0||index==2?pavement:track, false);
            Pad(stop,"Open_Courtyard",d,side*82,52,65,index==0||index==2?pavement:track);
            string landmark = index == 0 ? City + "Buildings/Building_Gas Station.prefab" : index == 2 ? City + "Buildings/Building_Auto Service.prefab" : index == 4 ? City + "Buildings/Building_Factory.prefab" : Find("LowPolyTent");
            if (landmark == null) landmark = "Assets/Prefabs/LowPolyTent.prefab";
            Place(landmark,stop,At(d+14,side*97),Yaw(d)-side*90,index==0?24:index==2?19:index==4?26:7,1);
            Place(City+"Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",stop,At(d-20,side*87),Yaw(d)+12,5.2f,1);
            Place(City+"Props/Props_Bench_1.prefab",stop,At(d-9,side*99),Yaw(d),2.2f,1);
            Place("Assets/Prefabs/LowPolyPallet.prefab",stop,At(d,side*92),Yaw(d),1.6f,1);
            for(int n=0;n<2;n++)
            {
                string kind=index%2==0?"FuelCanister":"WaterCanister";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VehicleService/"+kind+".prefab");
                var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,stop);
                item.transform.position=At(d-5+n,side*88)+Vector3.up;
            }
        }
        static string Find(string name) => paths.FirstOrDefault(p=>System.IO.Path.GetFileNameWithoutExtension(p)==name);
        internal static void Place(string path, Transform parent, Vector3 position, float yaw, float size, int collision)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null) throw new InvalidOperationException("Missing scenery prefab: "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,yaw,0));
            var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new InvalidOperationException("No geometry: "+path);
            Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            go.transform.localScale*=size/Mathf.Max(b.size.x,b.size.y,b.size.z);
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            go.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach(var body in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach(var col in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
            if(collision==1)
            {
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>()) if(mf.sharedMesh!=null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
            }
            else if(collision==2)
            {
                var trunk=Child(go.transform,"Trunk_Collider");trunk.position=position+Vector3.up*2;
                trunk.rotation=Quaternion.identity;trunk.localScale=new Vector3(1/go.transform.lossyScale.x,1/go.transform.lossyScale.y,1/go.transform.lossyScale.z);
                trunk.gameObject.AddComponent<CapsuleCollider>().height=4;trunk.GetComponent<CapsuleCollider>().radius=.35f;
            }
            placed++;
        }
    }
}
