using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    /// <summary>One-time authored campaign scenery. All meshes and objects are saved, never generated in Play Mode.</summary>
    public static class RemainingRoutesAuthoring
    {
        const string Folder = "Assets/Content/RemainingRoutes";
        const string City = "Assets/Downloads/SimplePoly City - Low Poly Assets/Prefab/";
        static readonly string[] Scenes = { "", "", "Stage2_Wasteland", "Stage3_Industrial", "Stage4_Citadel" };
        static readonly string[] Titles = { "", "", "ОХРИСТЫЙ КАНЬОН", "ПЕРЕВАЛ И ПРОМЗОНА", "ДОРОГА К ЦИТАДЕЛИ" };
        static StageRoute route;
        static int stage;
        static float extent;
        static Mesh meshArchive;
        static Material asphalt, soil, rock, concrete, steel, paint, water, glow;
        static Vector2[] bridges, tunnels;
        static readonly float[] Cross = { -240, -160, -100, -65, -38, -20, -11, -8, 0, 8, 11, 20, 38, 65, 100, 160, 240 };

        [MenuItem("RogueDrive/Route/Build Remaining Three Journeys")]
        public static void BuildAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene edits first.");
            for (int i = 2; i <= 4; i++) BuildStage(i);
        }

        public static void BuildStage(int number)
        {
            if (Application.isPlaying || number < 2 || number > 4) throw new InvalidOperationException("Editor stages 2–4 only.");
            stage = number;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + Scenes[stage] + ".unity");
            if (scene.GetRootGameObjects().Any(g => g.name == "Authored_Journey"))
                throw new InvalidOperationException("Journey already authored. Edit its saved objects; rebuild is intentionally refused.");
            Directory.CreateDirectory("Temp/RemainingRoutes");
            string backup = "Temp/RemainingRoutes/" + Scenes[stage] + "_before.unity";
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "RemainingRoutes");
            meshArchive = new Mesh { name = Scenes[stage] + "_Geometry" };
            AssetDatabase.CreateAsset(meshArchive, Folder + "/" + Scenes[stage] + ".asset");
            Palette();
            var generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (generator == null) throw new InvalidOperationException("Missing existing stage gameplay.");
            var oldFinish = Object.FindObjectsByType<StageFinishOutpost>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var legacy = Child(null, "Legacy_Route_Backup");
            foreach (Transform child in generator.transform.Cast<Transform>().ToArray()) child.SetParent(legacy, true);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == legacy.gameObject || go == generator.gameObject) continue;
                if (go.name == "Initial_Road" || go.name.Contains("Guardrail") || go.name.StartsWith("Container_") ||
                    go.name.StartsWith("StoneCluster_") || go.name.StartsWith("Lamp_") || go.name.StartsWith("WarningTriangle_") ||
                    go.name.StartsWith("Rock_Blocker") || go.name == "Road_Barrier") go.transform.SetParent(legacy, true);
            }
            legacy.gameObject.SetActive(false);
            var root = Child(null, "Authored_Journey");
            route = root.gameObject.AddComponent<StageRoute>();
            route.Configure(Alignment());
            var routeSettings = new SerializedObject(route);
            routeSettings.FindProperty("preserveAuthoredEnvironment").boolValue = true;
            routeSettings.ApplyModifiedPropertiesWithoutUndo();
            var roadRoot = Child(root, "01_Road_And_Shoulders");
            var terrain = Child(root, "02_Sculpted_Landscape");
            var structures = Child(root, "03_Bridges_And_Tunnels");
            var dressing = Child(root, "04_Landmarks_And_Scenery");
            var stops = Child(root, "05_Service_Laybys");
            for (float d = 0; d < extent; d += 200)
            {
                float end = Mathf.Min(d + 200, extent);
                var chunkRoot = Child(roadRoot, "Road_" + d.ToString("00000"));
                var road = Ribbon(chunkRoot, "Asphalt", d, end, -7, 7, .04f, asphalt, true);
                Ribbon(chunkRoot, "Left_Edge", d, end, -6.65f, -6.48f, .058f, paint, false);
                Ribbon(chunkRoot, "Right_Edge", d, end, 6.48f, 6.65f, .058f, paint, false);
                for (float mark = d; mark < end; mark += 18) Ribbon(chunkRoot, "Centre_Dash", mark, Mathf.Min(mark + 7, end), -.09f, .09f, .06f, paint, false);
                var connection = Child(chunkRoot, "Connection");
                Sample(end, out var p, out var q); connection.SetPositionAndRotation(p, q);
                Sample(d, out p, out q);
                // Geometry is world-space; chunk roots stay at identity to keep its saved mesh stable.
                chunkRoot.gameObject.AddComponent<TrackChunk>().Configure(ChunkType.SCurve, connection, null, null, end - d, new[] { road.GetComponent<Renderer>() });
                chunkRoot.gameObject.AddComponent<BakedMapChunk>().Configure((int)(d / 200));
                Ground(terrain, d, end);
            }
            Ground(terrain, -160, 0); Ground(terrain, extent, extent + 180);
            Ribbon(roadRoot, "Start_Apron", -100, 0, -10, 10, .04f, asphalt, true);
            Ribbon(roadRoot, "Finish_Apron", extent, extent + 100, -13, 13, .04f, asphalt, true);
            foreach (var span in bridges) Bridge(structures, span);
            foreach (var span in tunnels) Tunnel(structures, span);
            Dress(dressing);
            float[] stopDistances = stage == 2 ? new[] { 1200f, 4000, 7100 } : stage == 3 ? new[] { 1400f, 4700, 7900 } : new[] { 1300f, 4600, 7400 };
            for (int i = 0; i < stopDistances.Length; i++) Layby(stops, stopDistances[i], i);
            var finish = oldFinish != null ? oldFinish : Child(root, "Stage_Finish").gameObject.AddComponent<StageFinishOutpost>();
            finish.transform.SetParent(root, true); finish.gameObject.SetActive(true);
            Sample(extent - 20, out var finishPos, out var finishRot); finish.transform.SetPositionAndRotation(finishPos, finishRot);
            finish.Configure(stage, stage == 2 ? "Форпост у перевала" : stage == 3 ? "Восточная проходная" : "Цитадель эвакуации");
            generator.AuthoredCampaign = root; generator.SceneAuthoredMode = true;
            generator.DynamicBiomesByDistance = false; generator.EndlessMode = false; generator.StageTargetDistance = route.Length;
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            if (car != null) { Sample(20, out var p, out var q); car.transform.SetPositionAndRotation(p + Vector3.up * 1.1f, q); }
            Sign(dressing, 100, Titles[stage]);
            Atmosphere();
            PolishScene();
            SealPortals();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[RemainingRoutes] Saved " + scene.name + ": " + route.Length.ToString("F0") + " m.");
        }

        static Vector3[] Alignment()
        {
            float[] headings, heights;
            if (stage == 2)
            {
                headings = new float[] { 0,0,35,65,25,-35,-60,-15,35,45,5,-45,-65,-20,25,55,15,0,0 };
                heights = new float[] { 0,0,12,28,38,38,24,8,3,18,42,55,48,28,15,20,10,0,0 };
                bridges = new[] { new Vector2(2250,2550), new Vector2(5450,5700) };
                tunnels = new[] { new Vector2(5950,6250) };
            }
            else if (stage == 3)
            {
                headings = new float[] { 0,0,-40,-78,-25,65,82,20,-65,-80,-10,65,80,15,-55,-45,0,25,0,0 };
                heights = new float[] { 0,0,22,52,77,92,98,110,118,108,88,62,48,45,32,18,8,0,0,0 };
                bridges = new[] { new Vector2(3850,4200), new Vector2(7150,7500) };
                tunnels = new[] { new Vector2(2700,3080), new Vector2(6200,6580) };
            }
            else
            {
                headings = new float[] { 0,0,15,40,10,-40,-40,5,55,55,0,-50,-50,0,35,0,0,0,0 };
                heights = new float[] { 0,0,8,22,32,32,25,14,4,4,15,28,38,38,24,10,0,0,0 };
                bridges = new[] { new Vector2(2050,2950), new Vector2(5800,6550) };
                tunnels = new[] { new Vector2(4050,4450) };
            }
            extent = (headings.Length - 1) * 500;
            var points = new List<Vector3>(); Vector3 cursor = Vector3.zero;
            for (int step = 0; step <= (int)(extent / 5); step++)
            {
                float d = step * 5; int i = Mathf.Min((int)(d / 500), headings.Length - 2);
                float t = Mathf.SmoothStep(0, 1, (d - i * 500) / 500);
                float yaw = Mathf.Lerp(headings[i], headings[i + 1], t) * Mathf.Deg2Rad;
                if (step > 0) cursor += new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * 5;
                cursor.y = Mathf.Lerp(heights[i], heights[i + 1], t); points.Add(cursor);
            }
            return points.ToArray();
        }

        static void Palette()
        {
            asphalt = Mat("Asphalt", new Color(.19f,.20f,.21f));
            soil = Mat("Soil_" + stage, stage == 2 ? new Color(.55f,.39f,.23f) : stage == 3 ? new Color(.32f,.37f,.29f) : new Color(.34f,.40f,.32f));
            rock = Mat("Rock_" + stage, stage == 2 ? new Color(.57f,.32f,.18f) : new Color(.38f,.40f,.40f));
            concrete = Mat("Concrete", new Color(.52f,.53f,.49f)); steel = Mat("Weathered_Steel",new Color(.23f,.29f,.28f));
            paint = Mat("Road_Paint",new Color(.91f,.82f,.56f)); water = Mat("River",new Color(.17f,.37f,.39f));
            glow = Mat("Tunnel_Lights",new Color(1f,.74f,.32f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(1.5f,1.05f,.45f));
        }

        static Material Mat(string name, Color c)
        {
            string path = Folder + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = c };
            mat.SetFloat("_Glossiness", .12f); AssetDatabase.CreateAsset(mat, path); return mat;
        }

        static Transform Child(Transform parent, string name)
        { var t = new GameObject(name).transform; if (parent != null) t.SetParent(parent, false); return t; }

        static void Sample(float d, out Vector3 p, out Quaternion q) => route.Evaluate(d, out p, out q);
        static Vector3 At(float d, float x, float y = 0)
        { Sample(d, out var p, out var q); return p + q * Vector3.right * x + Vector3.up * y; }
        static bool In(float d, Vector2 span) => d >= span.x && d <= span.y;
        static float Relief(float d, float x)
        {
            float a = Mathf.Abs(x);
            float h = -.12f + Mathf.SmoothStep(0,1,(a-40)/110) * (Mathf.PerlinNoise(d*.003f, x*.013f+stage*10)*38 + (stage==3?34:5));
            foreach(var span in bridges)
            {
                float fade = Mathf.SmoothStep(0,1,(d-span.x+70)/70) * Mathf.SmoothStep(0,1,(span.y+70-d)/70);
                h -= fade * (stage == 4 ? 23 : 32);
            }
            foreach(var span in tunnels)
            {
                if (In(d,span)) h += Mathf.SmoothStep(0,1,(d-span.x)/25)*Mathf.SmoothStep(0,1,(span.y-d)/25) * (24 + 12*Mathf.Sin((d-span.x)/(span.y-span.x)*Mathf.PI));
            }
            return h;
        }

        static GameObject MeshObject(Transform parent, string name, List<Vector3> vertices, List<int> triangles, Material material, bool collision)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, meshArchive);
            var go = Child(parent,name).gameObject; go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static GameObject Ribbon(Transform parent,string name,float start,float end,float left,float right,float height,Material mat,bool collision)
        {
            var v = new List<Vector3>(); var t = new List<int>(); int rows = Mathf.Max(1,Mathf.CeilToInt((end-start)/5));
            for(int i=0;i<=rows;i++){float d=Mathf.Lerp(start,end,(float)i/rows);v.Add(At(d,left,height));v.Add(At(d,right,height));}
            for(int i=0;i<rows;i++){int a=i*2;t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            return MeshObject(parent,name,v,t,mat,collision);
        }

        static void Ground(Transform parent,float start,float end)
        {
            var v=new List<Vector3>();var t=new List<int>();int rows=Mathf.CeilToInt((end-start)/10),cols=Cross.Length;
            for(int i=0;i<=rows;i++){float d=Mathf.Lerp(start,end,(float)i/rows);foreach(float x in Cross)v.Add(At(d,x,Relief(d,x)));}
            for(int i=0;i<rows;i++)for(int j=0;j<cols-1;j++)
            { int a=i*cols+j; float d=Mathf.Lerp(start,end,(i+.5f)/rows);
                // Tunnel portals have an actual opening; the mountain begins above the shell.
                if(tunnels.Any(s=>d>s.x-5&&d<s.x+30||d>s.y-30&&d<s.y+5)&&Cross[j]>=-11&&Cross[j+1]<=11)continue;
                t.AddRange(new[]{a,a+cols,a+1,a+1,a+cols,a+cols+1}); }
            MeshObject(parent,"Terrain_"+start,v,t,soil,true);
        }

        static GameObject Box(Transform parent,string name,Vector3 p,Quaternion q,Vector3 size,Material mat,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation(p,q);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;go.isStatic=true;
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }

        static void Bridge(Transform parent,Vector2 span)
        {
            var root=Child(parent,"Bridge_"+span.x); Ribbon(root,"Deck_Underside",span.x,span.y,-8.4f,8.4f,-.25f,concrete,false);
            for(float d=span.x;d<span.y;d+=10)
            {
                float end=Mathf.Min(d+10,span.y);Sample((d+end)/2,out var p,out var q);
                foreach(int side in new[]{-1,1})
                { Box(root,"Safety_Rail",p+q*Vector3.right*(side*7.8f)+Vector3.up*.85f,q,new Vector3(.32f,1.1f,end-d+.15f),concrete);
                  if(((int)(d-span.x))%30==0)Box(root,"Reflector",p+q*Vector3.right*(side*7.58f)+Vector3.up*1.15f,q,new Vector3(.08f,.22f,.32f),paint,false); }
            }
            for(float d=span.x+22;d<span.y;d+=48)
            { Sample(d,out var p,out var q);foreach(int side in new[]{-1,1})Box(root,"Pier",p+q*Vector3.right*(side*5.5f)-Vector3.up*16,q,new Vector3(2.2f,32,3.5f),concrete); }
            if(stage==2)
            {
                // Side trusses frame the canyon crossing without blocking the lane.
                for(float d=span.x;d<span.y-20;d+=25)foreach(int side in new[]{-1,1})
                { Sample(d,out var p,out var q);Box(root,"Truss_Upright",p+q*Vector3.right*(side*8.7f)+Vector3.up*3,q,new Vector3(.5f,6,.5f),steel); }
            }
            Sample((span.x+span.y)/2,out var middle,out var rot);
            Box(root,stage==2?"Dry_Canyon_Bed":"River_Channel",middle-Vector3.up*(stage==4?22:31),rot,new Vector3(470,.2f,span.y-span.x-12),stage==2?rock:water,false);
            Sign(root,span.x-65,"МОСТ  /  40");
        }

        static void Tunnel(Transform parent,Vector2 span)
        {
            var root=Child(parent,"Tunnel_"+span.x);var v=new List<Vector3>();var tris=new List<int>();
            int rows=Mathf.CeilToInt((span.y-span.x)/5), sides=12;
            for(int i=0;i<=rows;i++)for(int j=0;j<=sides;j++)
            {float angle=Mathf.PI*j/sides;float d=Mathf.Lerp(span.x,span.y,(float)i/rows);v.Add(At(d,Mathf.Cos(angle)*9.6f,Mathf.Sin(angle)*8.5f));}
            for(int i=0;i<rows;i++)for(int j=0;j<sides;j++)
            {int a=i*(sides+1)+j,b=a+sides+1;tris.AddRange(new[]{b,a+1,a,b,b+1,a+1});}
            MeshObject(root,"Vault_Interior",v,tris,concrete,true);
            for(float d=span.x;d<=span.y;d+=25)
            {Sample(d,out var p,out var q);foreach(int side in new[]{-1,1})
                Box(root,"Wall_Lamp",At(d,side*7.6f,4.5f),q,new Vector3(.22f,.30f,2.5f),glow,false);
             if(((int)(d-span.x))%75==0){var lamp=Child(root,"Interior_Light");lamp.position=p+Vector3.up*5;var light=lamp.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=24;light.intensity=2;light.color=new Color(1,.79f,.51f);light.shadows=LightShadows.None;}}
            foreach(float d in new[]{span.x,span.y})
            {Sample(d,out var p,out var q);foreach(int side in new[]{-1,1})Box(root,"Portal_Pillar",At(d,side*10.6f,5.2f),q,new Vector3(2,10.4f,3),rock);
             Box(root,"Portal_Lintel",p+Vector3.up*10.1f,q,new Vector3(23,3.2f,4),rock);}
            Sign(root,span.x-80,"ТОННЕЛЬ  /  ВКЛЮЧИ ФАРЫ");
        }

        static void Dress(Transform root)
        {
            for(float d=240;d<extent-180;d+=85)
            {
                if(bridges.Any(s=>d>s.x-50&&d<s.y+50)||tunnels.Any(s=>d>s.x-60&&d<s.y+60))continue;
                Sample(d,out _,out var q);
                foreach(int side in new[]{-1,1})
                {
                    float x=side*(45+14*Mathf.Sin(d*.013f));
                    string path="Assets/Downloads/SimpleNaturePack/Prefabs/Rock_0"+(1+((int)d%5))+".prefab";
                    Place(path,root,At(d,x,Relief(d,x)),q.eulerAngles.y+d,stage==2?16:12);
                    if(stage==3 && ((int)d)%2==0)
                        Place("Assets/Downloads/FinottiGames/LowPoly Trees Game-Ready Free-Asset/Prefabs/StandardTree.01_LP.prefab",root,At(d+15,side*31,Relief(d+15,side*31)),d,13);
                    if(stage==4 || stage==3&&d>6800)
                    {
                        string building=stage==3?"Building_Factory":"Building_Residential_color0"+(1+((int)d%3));
                        Place(City+"Buildings/"+building+".prefab",root,At(d,side*55,Relief(d,side*55)),q.eulerAngles.y+side*90,stage==3?40:28);
                        if(stage==4&&d>4500)Place(City+"Buildings/Building Sky_small_color01.prefab",root,At(d+30,side*110,Relief(d+30,side*110)),q.eulerAngles.y,55);
                    }
                }
            }
            // Large faceted mountain silhouettes sit beyond the drivable verge.
            for(float d=350;d<extent;d+=310)foreach(int side in new[]{-1,1})
            {
                if(stage==4&&d>4500)continue;
                float x=side*(145+25*Mathf.Sin(d));
                Place("Assets/Downloads/SimpleNaturePack/Prefabs/Rock_03.prefab",root,At(d,x,Relief(d,x)-9),d,stage==3?135:95);
            }
            if(stage==4)
            {
                foreach(float d in new[]{3450f,7100f})
                {Sample(d,out var p,out var q);Box(root,"Crossing_Overpass",p+Vector3.up*9,q,new Vector3(130,1.6f,13),concrete);
                 foreach(int side in new[]{-1,1})Box(root,"Overpass_Pier",p+q*Vector3.right*(side*22)+Vector3.up*4,q,new Vector3(3,8,8),concrete);}
            }
        }

        static void Place(string path,Transform parent,Vector3 p,float yaw,float size,bool required=true)
        {if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null){if(required)throw new InvalidOperationException("Missing art: "+path);return;}
         JourneyLandscapeAuthoring.Place(path,parent,p,yaw,size,1);}

        static void Layby(Transform parent,float d,int index)
        {
            int side=index%2==0?1:-1;var root=Child(parent,"Layby_"+d);Sample(d,out var p,out var q);
            Box(root,"Parking_Apron",At(d,side*20,-.13f),q,new Vector3(32,.28f,65),asphalt);
            string building=stage==2?"Building_Gas Station":stage==3?"Building_Auto Service":"Building_House_01_color01";
            Place(City+"Buildings/"+building+".prefab",root,At(d+12,side*37),q.eulerAngles.y-side*90,18);
            Place(City+"Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",root,At(d-15,side*25,.03f),q.eulerAngles.y,5);
            for(int i=0;i<2;i++)
            {string kind=i==0?"FuelCanister":"WaterCanister";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VehicleService/"+kind+".prefab");
             if(prefab==null)throw new InvalidOperationException("Missing supply "+kind);var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);item.transform.position=At(d-4+i*2,side*24,1.2f);}
            Sign(root,d-65,"КАРМАН ОТДЫХА  /  ПРИПАСЫ");
        }

        static void Sign(Transform parent,float d,string title)
        {
            Sample(d,out _,out var q);var root=Child(parent,"Sign_"+title);root.SetPositionAndRotation(At(d,10.5f),q);
            Box(root,"Post",root.TransformPoint(new Vector3(0,1.7f,0)),q,new Vector3(.12f,3.4f,.12f),steel);
            Box(root,"Board",root.TransformPoint(new Vector3(0,3.4f,0)),q,new Vector3(6.5f,1.1f,.14f),steel);
            var label=Child(root,"Lettering");label.localPosition=new Vector3(0,3.4f,-.08f);var text=label.gameObject.AddComponent<TextMesh>();
            text.text=title;text.anchor=TextAnchor.MiddleCenter;text.fontSize=64;text.characterSize=.11f;text.color=new Color(.96f,.9f,.7f);
        }

        static void Atmosphere()
        {
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=250;RenderSettings.fogEndDistance=1100;
            RenderSettings.fogColor=stage==2?new Color(.70f,.61f,.46f):stage==3?new Color(.60f,.68f,.71f):new Color(.67f,.66f,.64f);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.65f,.70f);
            RenderSettings.ambientEquatorColor=new Color(.40f,.39f,.35f);RenderSettings.ambientGroundColor=new Color(.23f,.23f,.21f);
            var sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(x=>x.type==LightType.Directional);
            if(sun!=null){sun.transform.rotation=Quaternion.Euler(stage==4?24:38,-35,0);sun.intensity=1.15f;sun.color=new Color(1,.88f,.72f);RenderSettings.sun=sun;}
        }

        public static void PolishStage(int number)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode.");
            stage=number;var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+Scenes[stage]+".unity");
            route=Object.FindFirstObjectByType<StageRoute>();Alignment();Palette();
            meshArchive=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+Scenes[stage]+".asset");
            // Repair normals on meshes created before the one-sided vault correction.
            foreach(var mf in route.GetComponentsInChildren<MeshFilter>())if(mf.name=="Vault_Interior")
            {
                var mesh=mf.sharedMesh;int rows=(mesh.vertexCount/13)-1;
                if(mesh.triangles.Length==rows*12*12)
                {var previous=mesh.triangles;var corrected=new List<int>();for(int i=0;i<previous.Length;i+=12)for(int j=6;j<12;j++)corrected.Add(previous[i+j]);
                 mesh.triangles=corrected.ToArray();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);mf.GetComponent<MeshCollider>().sharedMesh=null;mf.GetComponent<MeshCollider>().sharedMesh=mesh;}
            }
            PolishScene();SealPortals();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static void SealPortals()
        {
            if(route.transform.Find("07_Tunnel_Portal_Faces")!=null)return;
            var root=Child(route.transform,"07_Tunnel_Portal_Faces");
            foreach(var span in tunnels)foreach(bool entrance in new[]{true,false})
            {
                float d=entrance?span.x-.03f:span.y+.03f;
                var v=new List<Vector3>();var t=new List<int>();
                for(int i=0;i<=12;i++)
                {float a=Mathf.PI*i/12;float x=Mathf.Cos(a)*9.6f;v.Add(At(d,x,Mathf.Sin(a)*8.5f));v.Add(At(d,x,12));}
                for(int i=0;i<12;i++){int a=i*2;if(entrance)t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});else t.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}
                MeshObject(root,"Arch_Spandrel",v,t,concrete,true);
            }
        }

        static void PolishScene()
        {
            if(route.transform.Find("06_Finish_Landscape")!=null)return;
            var root=Child(route.transform,"06_Finish_Landscape");
            var bounds=new Bounds(Vector3.zero,Vector3.one);
            for(float d=0;d<extent;d+=50){Sample(d,out var p,out _);bounds.Encapsulate(p);}
            Box(root,"Distant_Landscape",new Vector3(bounds.center.x,-75,bounds.center.z),Quaternion.identity,new Vector3(bounds.size.x+12000,2,bounds.size.z+12000),soil);
            foreach(int side in new[]{-1,1})
            {
                var v=new List<Vector3>();var t=new List<int>();
                for(float d=-150;d<=extent+150;d+=25)
                {Vector3 a=At(d,side*240,Relief(d,side*240)),b=At(d,side*650);b.y=-74;v.Add(a);v.Add(b);}
                for(int i=0;i<v.Count/2-1;i++){int a=i*2;if(side>0)t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});else t.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}
                MeshObject(root,"Outer_Slopes_"+side,v,t,soil,true);
            }
            foreach(var span in tunnels)foreach(bool entrance in new[]{true,false})
            {
                float start=entrance?span.x:span.y-35,end=entrance?span.x+35:span.y;
                var v=new List<Vector3>();var t=new List<int>();
                for(int i=0;i<=7;i++){float d=Mathf.Lerp(start,end,i/7f);float y=Mathf.Max(12,Relief(d,0));v.Add(At(d,-11,y));v.Add(At(d,11,y));}
                for(int i=0;i<7;i++){int a=i*2;t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
                MeshObject(root,"Portal_Mountain_Cap",v,t,soil,true);
            }
            foreach(var label in route.GetComponentsInChildren<TextMesh>())
            {var r=label.GetComponent<Renderer>();if(r.bounds.size.x>5.9f)label.characterSize*=5.9f/r.bounds.size.x;}
            if(stage==3)
            {
                for(float d=220;d<6800;d+=42)
                {
                    if(bridges.Any(s=>d>s.x-80&&d<s.y+80)||tunnels.Any(s=>d>s.x-90&&d<s.y+90))continue;
                    foreach(int side in new[]{-1,1})
                    {float x=side*(28+20*Mathf.Abs(Mathf.Sin(d*.031f)));Place("Assets/Downloads/FinottiGames/LowPoly Trees Game-Ready Free-Asset/Prefabs/StandardTree.01_LP.prefab",root,At(d,x,Relief(d,x)),d,11+4*Mathf.Abs(Mathf.Sin(d)));}
                }
            }
            if(stage==4)
            {
                for(float d=4700;d<extent-180;d+=60)
                {
                    if(bridges.Any(s=>d>s.x-30&&d<s.y+30))continue;
                    Sample(d,out _,out var q);
                    foreach(int side in new[]{-1,1})
                    {Place(City+"Props/Props_Street Light.prefab",root,At(d,side*12),q.eulerAngles.y,8);
                     if(((int)d)%120==20)Place(City+"Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01.prefab",root,At(d+12,side*18),q.eulerAngles.y,4.7f);}
                }
            }
            foreach(var span in bridges)for(float d=span.x+1;d<span.y-25;d+=25)foreach(int side in new[]{-1,1})
            {
                if(stage!=2)continue;
                Vector3 a=At(d,side*8.7f,.4f),b=At(d+25,side*8.7f,5.8f);
                Box(root,"Steel_Truss_Diagonal",(a+b)*.5f,Quaternion.LookRotation(b-a),new Vector3(.28f,.28f,Vector3.Distance(a,b)),steel);
                a=At(d,side*8.7f,5.8f);Box(root,"Steel_Truss_Top",(a+b)*.5f,Quaternion.LookRotation(b-a),new Vector3(.4f,.4f,Vector3.Distance(a,b)),steel);
            }
        }
    }
}
