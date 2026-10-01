using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = RogueDrive.EditorTools.JourneyLandscapeAuthoring;

namespace RogueDrive.EditorTools
{
    public static class JourneyRoadUpgrade
    {
        const string Folder="Assets/Content/JourneyRoad";
        const string Pack="Assets/Downloads/BrokenVector/LowPolyRoadPack/Prefabs/";
        const string City="Assets/Downloads/SimplePoly City - Low Poly Assets/Prefab/";
        static StageRoute route;

        [MenuItem("RogueDrive/Route/Upgrade Asset Roads And Side Workshop")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="Stage1_Outskirts")throw new InvalidOperationException("Open Stage1_Outskirts.");
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            if(route.transform.Find("Asset_Road_And_Service")!=null)throw new InvalidOperationException("Upgrade already saved; edit scene objects.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Road Straight 2.prefab").GetComponentInChildren<MeshFilter>();
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Content","JourneyRoad");
            L.route=route;
            float oldLength=route.Length;
            var data=new SerializedObject(route).FindProperty("points");var points=new List<Vector3>();
            for(int i=0;i<data.arraySize;i++)points.Add(data.GetArrayElementAtIndex(i).vector3Value);
            route.Evaluate(oldLength,out var end,out var heading);
            for(int i=1;i<=20;i++)points.Add(end+heading*Vector3.forward*(i*100));
            route.Configure(points.ToArray());
            var root=L.Child(route.transform,"Asset_Road_And_Service");
            // Replace both visible surface and collision, including old centre dashes.
            foreach(var chunk in route.GetComponentsInChildren<TrackChunk>(true))
            {
                foreach(var renderer in chunk.RoadRenderers)
                    if(renderer!=null){renderer.enabled=false;foreach(var c in renderer.GetComponents<Collider>())c.enabled=false;}
                foreach(var t in chunk.GetComponentsInChildren<Transform>(true))
                    if(t.name.StartsWith("Dash_"))t.gameObject.SetActive(false);
            }
            var initial=route.transform.parent.Find("Initial_Road");if(initial!=null)initial.gameObject.SetActive(false);
            foreach(var t in route.transform.Find("Journey_Landscape").GetComponentsInChildren<Transform>(true))
                if(t.name=="Gravel_Shoulder")t.gameObject.SetActive(false);
            for(float start=0;start<route.Length;start+=240)
                Road(root,source,start,Mathf.Min(start+240,route.Length),false);
            // Flat open continuation to the next journey, with full-width ground collision.
            var meadow=L.Mat("Meadow",new Color(.31f,.38f,.23f));
            for(float start=oldLength+240;start<route.Length+240;start+=240)
            {
                L.Ground(root,start,Mathf.Min(start+240,route.Length+240));
                var ground=root.GetChild(root.childCount-1);ground.GetComponent<Renderer>().sharedMaterial=meadow;
            }
            var finish=route.GetComponentInChildren<StageFinishOutpost>(true);
            if(finish!=null)finish.gameObject.SetActive(false);
            // Remove the previous isolated service facade, now superseded by the full yard.
            foreach(var t in route.transform.Find("Journey_Landscape/Landmarks_And_Gardens").Cast<Transform>())
                if(t.name=="Building_Auto Service")t.gameObject.SetActive(false);
            var service=L.Child(root,"Northern_Workshop_Side_Stop");
            float centre=13040;
            route.Evaluate(centre,out var p,out var q);
            service.position=L.At(centre,86);
            var zone=service.gameObject.AddComponent<WorkshopServiceZone>();zone.Configure("СТО Северная",true,WorkshopEquipment.All);
            var so=new SerializedObject(zone);so.FindProperty("radius").floatValue=32;so.ApplyModifiedPropertiesWithoutUndo();
            var yard=L.Mat("ServiceYard",new Color(.34f,.35f,.32f));
            // Helpers author world-space meshes, so keep their parent at world origin.
            L.Pad(root,"Workshop_Courtyard",centre,86,68,90,yard);
            var lane=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"SingleLane Straight 2.prefab").GetComponentInChildren<MeshFilter>();
            Road(root,lane,centre-250,centre+250,true);
            L.Place(City+"Buildings/Building_Auto Service.prefab",root,L.At(centre+9,106),q.eulerAngles.y-90,25,1);
            L.Place(City+"Buildings/Building_Gas Station.prefab",root,L.At(centre-26,101),q.eulerAngles.y-90,18,1);
            L.Place(City+"Vehicles/Vehicle with Static Wheels/Vehicle_Truck_color01.prefab",root,L.At(centre+30,100),q.eulerAngles.y+5,8,1);
            L.Place(City+"Props/Props_Street Light.prefab",root,L.At(centre-32,61),q.eulerAngles.y,7,0);
            L.Place(City+"Props/Props_Street Light.prefab",root,L.At(centre+33,61),q.eulerAngles.y,7,0);
            for(int i=0;i<3;i++)L.Place("Assets/Prefabs/LowPolyPallet.prefab",root,L.At(centre+24+i*2,110),q.eulerAngles.y,1.6f,1);
            // Clear previous housing out of the new entrance, return lane and workshop yard.
            var landscape=route.transform.Find("Journey_Landscape");
            foreach(var t in landscape.Find("07_Northern_Workshop").Cast<Transform>())
            {
                if(t.name.StartsWith("Terrain_"))continue;
                float d=route.ProjectDistance(t.position);route.Evaluate(d,out var cp,out var cq);
                float x=Vector3.Dot(t.position-cp,cq*Vector3.right);
                if(d>centre-280&&d<centre+280&&x>10&&x<150)t.gameObject.SetActive(false);
            }
            foreach(var gen in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProceduralTrackGenerator>(true)))gen.StageTargetDistance=route.Length;
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("RogueDrive/Route/Add World Activity Details")]
        public static void Dress()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();L.route=route;
            if(route.transform.Find("World_Activity_Details")!=null)throw new InvalidOperationException("Details already authored.");
            var root=L.Child(route.transform,"World_Activity_Details");
            var random=new System.Random(91277);
            string forest="Assets/Downloads/Low Poly Forest - Free Starter Pack/Prefabs/";
            for(float d=1900;d<route.Length-100;d+=22)
            {
                bool woods=d<4200||d>6100&&d<8500||d>13500;
                if(!woods)continue;
                route.Evaluate(d,out _,out var q);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*(14+(float)random.NextDouble()*24);if(L.Reserved(d,x))continue;
                    for(int n=0;n<3;n++)L.Place(forest+"Vegetations/small_grass.prefab",root,L.At(d+n*1.8f,x+n*.8f),random.Next(360),1.1f,0);
                    if(random.Next(4)==0)L.Place(forest+"Props/fallen_log_small_2.prefab",root,L.At(d+3,side*32),random.Next(360),4.5f,1);
                    if(random.Next(3)==0)L.Place(forest+"Props/tree_stump_medium.prefab",root,L.At(d,side*25),random.Next(360),1.1f,1);
                    if(d>13500)
                    {
                        L.Place(forest+"Trees/pine_large.prefab",root,L.At(d,side*24),random.Next(360),14+random.Next(5),2);
                        L.Place(forest+"Trees/birch_medium.prefab",root,L.At(d+5,side*53),random.Next(360),12,2);
                    }
                }
            }
            var paved=L.Mat("ServiceYard",new Color(.34f,.35f,.32f));
            // Small parking courts, street furniture and abandoned deliveries.
            foreach(float d in new[]{360f,820f,1200f,4490f,4810f,5810f})
            {
                route.Evaluate(d,out _,out var q);float side=d<1800?-1:1;
                L.Pad(root,"Parking_Court",d,side*55,30,34,paved);
                for(int n=0;n<3;n++)
                {
                    string vehicle=n==2?"Vehicle_Pick up Truck_color02":"Vehicle_Car_color0"+(n+1);
                    L.Place(City+"Vehicles/Vehicle with Static Wheels/"+vehicle+".prefab",root,L.At(d-7+n*6,side*56),q.eulerAngles.y+side*90,4.7f,1);
                }
                L.Place(City+"Props/Props_Bench_2.prefab",root,L.At(d+15,side*47),q.eulerAngles.y,2.1f,1);
                L.Place(City+"Props/Props_Dustbin.prefab",root,L.At(d+12,side*47),q.eulerAngles.y,1.2f,1);
            }
            // Props sit beside the walking space, not across vehicle access.
            float[] stops={1400,3200,5400,7600,9600,11600};
            for(int i=0;i<stops.Length;i++)
            {
                float d=stops[i],side=i%2==0?1:-1;route.Evaluate(d,out _,out var q);
                for(int n=0;n<3;n++)L.Place("Assets/Downloads/GarageAssetPack/Prefabs/Barrelfbx.prefab",root,L.At(d+22+n,side*102),q.eulerAngles.y,1.1f,1);
                L.Place("Assets/Prefabs/LowPolyWoodPile.prefab",root,L.At(d+19,side*104),q.eulerAngles.y,3.5f,1);
            }
            Sign(root,12740,11,"СТО 300 м  →");Sign(root,12820,13,"СЕВЕРНАЯ  →");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static void Sign(Transform parent,float d,float x,string title)
        {
            var sign=L.Child(parent,"Direction_"+title);route.Evaluate(d,out _,out var q);sign.SetPositionAndRotation(L.At(d,x),q);
            var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.transform.SetParent(sign,false);post.transform.localPosition=new Vector3(0,1.25f,0);post.transform.localScale=new Vector3(.10f,1.25f,.10f);
            post.GetComponent<Renderer>().sharedMaterial=L.Mat("SignPost",new Color(.32f,.34f,.33f));
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(sign,false);board.transform.localPosition=new Vector3(0,2.6f,0);board.transform.localScale=new Vector3(3.2f,.9f,.12f);board.GetComponent<Renderer>().sharedMaterial=L.Mat("SignBlue",new Color(.10f,.24f,.28f));
            var label=L.Child(sign,"Lettering");label.localPosition=new Vector3(0,2.6f,-.065f);var text=label.gameObject.AddComponent<TextMesh>();text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=72;text.characterSize=.09f;text.color=new Color(.92f,.93f,.86f);
        }

        static void Road(Transform root,MeshFilter source,float start,float end,bool detour)
        {
            var template=source.sharedMesh;var bounds=template.bounds;
            var sourceVertices=template.vertices;var sourceUv=template.uv;var sourceTriangles=template.triangles;
            if(sourceVertices.Length==0||sourceTriangles.Length==0)
                throw new InvalidOperationException("Road source has no readable geometry. Enable Read/Write on "+AssetDatabase.GetAssetPath(template));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float scale=detour?1.6f:2f;
            int pieces=Mathf.CeilToInt((end-start)/10);
            for(int n=0;n<pieces;n++)
            {
                float a=Mathf.Lerp(start,end,(float)n/pieces),b=Mathf.Lerp(start,end,(float)(n+1)/pieces);int first=vertices.Count;
                foreach(var vertex in sourceVertices)
                {
                    float d=Mathf.Lerp(a,b,Mathf.InverseLerp(bounds.min.z,bounds.max.z,vertex.z));
                    float offset=detour?80*Mathf.Pow(Mathf.Sin(Mathf.InverseLerp(start,end,d)*Mathf.PI),2):0;
                    route.Evaluate(d,out var p,out var q);
                    // Lower the asset's raised curbs so freely crossing the shoulder remains possible.
                    p+=q*Vector3.right*((vertex.x-bounds.center.x)*scale+offset);
                    p.y+=vertex.y<-.01f?-.2f:.035f+Mathf.Min(vertex.y*scale,.02f)+(detour?.006f:0);
                    vertices.Add(p);
                }
                uv.AddRange(sourceUv);triangles.AddRange(sourceTriangles.Select(i=>i+first));
            }
            var mesh=new Mesh{name=detour?"Service_Return_Road":"BrokenVector_Road_"+start};
            mesh.vertices=vertices.ToArray();mesh.uv=uv.ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            var existing=root.Find(mesh.name);
            if(existing!=null)
            {
                var saved=existing.GetComponent<MeshFilter>().sharedMesh;
                existing.GetComponent<MeshCollider>().sharedMesh=null;
                // CopySerialized alone can leave the native graphics buffer at its old size.
                saved.Clear();saved.vertices=mesh.vertices;saved.uv=mesh.uv;saved.triangles=mesh.triangles;
                saved.RecalculateNormals();saved.RecalculateBounds();saved.UploadMeshData(false);EditorUtility.SetDirty(saved);
                existing.GetComponent<MeshCollider>().sharedMesh=saved;
                UnityEngine.Object.DestroyImmediate(mesh);return;
            }
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+mesh.name+".asset"));
            var go=L.Child(root,mesh.name).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        public static void RebuildRoadMeshes()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            route=UnityEngine.Object.FindFirstObjectByType<StageRoute>(FindObjectsInactive.Include);
            var root=route.transform.Find("Asset_Road_And_Service");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Road Straight 2.prefab").GetComponentInChildren<MeshFilter>();
            for(float d=0;d<route.Length;d+=240)Road(root,source,d,Mathf.Min(d+240,route.Length),false);
            source=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"SingleLane Straight 2.prefab").GetComponentInChildren<MeshFilter>();
            Road(root,source,12790,13290,true);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(route.gameObject.scene);EditorSceneManager.SaveScene(route.gameObject.scene);
        }
    }
}
