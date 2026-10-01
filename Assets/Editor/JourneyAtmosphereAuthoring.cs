using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>Saved geometry, not runtime terrain generation. Imported assets stay untouched.</summary>
    public static class JourneyAtmosphereAuthoring
    {
        const string Folder="Assets/Content/JourneyAtmosphere";
        static readonly float[] Stations={0,600,1400,3200,5400,7600,9600,11600,13040,14000,15500};
        static readonly float[] Levels={0,0,8,22,10,30,12,45,24,20,36};
        static StageRoute route;
        static Vector3[] points;
        static float[] lengths;
        static readonly HashSet<Mesh> edited=new HashSet<Mesh>();
        static int surfaces,objects;

        [MenuItem("RogueDrive/Route/Author Afternoon Landscape")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="Stage1_Outskirts")throw new InvalidOperationException("Open Stage1_Outskirts.");
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            if(route.transform.Find("Afternoon_Landscape")!=null)throw new InvalidOperationException("Atmosphere already authored; edit the saved result.");
            var shader=Shader.Find("RogueDrive/Journey Sky");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Journey sky shader is not ready.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Content","JourneyAtmosphere");
            var prop=new SerializedObject(route).FindProperty("points");
            points=new Vector3[prop.arraySize];lengths=new float[prop.arraySize];
            for(int i=0;i<points.Length;i++)
            {points[i]=prop.GetArrayElementAtIndex(i).vector3Value;if(i>0)lengths[i]=lengths[i-1]+Vector3.Distance(points[i],points[i-1]);}
            edited.Clear();surfaces=objects=0;
            foreach(Transform child in route.transform)
            {
                if(child.name=="Journey_Landscape")foreach(Transform group in child)ProcessChildren(group);
                else if(child.name=="Asset_Road_And_Service"||child.name=="World_Activity_Details")ProcessChildren(child);
                else if(child.GetComponent<TrackChunk>()!=null)Lift(child);
            }
            // Preserve flat stop courts with 500 m plateaus; ramps have gentle, continuous grades.
            var raised=points.Select((p,i)=>p+Vector3.up*RoadHeight(lengths[i])).ToArray();
            route.Configure(raised);EditorUtility.SetDirty(route);
            var root=new GameObject("Afternoon_Landscape").transform;root.SetParent(route.transform,false);
            BuildHorizon(root);
            var sky=new Material(shader){name="Journey Afternoon Sky"};
            AssetDatabase.CreateAsset(sky,Folder+"/AfternoonSky.mat");
            var presentation=route.GetComponentInChildren<JourneyPresentation>(true);
            presentation.ConfigureSky(sky);EditorUtility.SetDirty(presentation);
            var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).First(l=>l.type==LightType.Directional);
            JourneyPresentation.ApplyAtmosphere(0,sky,sun);
            foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)))
            {camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1800;EditorUtility.SetDirty(camera);}
            QualitySettings.shadowDistance=130;
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Afternoon] {surfaces} surfaces, {objects} art roots lifted; route {route.Length:F1} m. Sky and horizon saved.");
        }
        static float RoadHeight(float d)
        {
            int i=1;while(i<Stations.Length-1&&d>Stations[i])i++;
            return Mathf.Lerp(Levels[i-1],Levels[i],Mathf.SmoothStep(0,1,Mathf.InverseLerp(Stations[i-1]+250,Stations[i]-250,d)));
        }
        static float Project(Vector3 p,out float offset)
        {
            float best=float.MaxValue,d=0;offset=0;
            for(int i=1;i<points.Length;i++)
            {
                Vector3 edge=points[i]-points[i-1];edge.y=0;
                Vector3 delta=p-points[i-1];delta.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(delta,edge)/Mathf.Max(.001f,edge.sqrMagnitude));
                Vector3 remainder=delta-edge*t;float error=remainder.sqrMagnitude;
                if(error>=best)continue;best=error;d=Mathf.Lerp(lengths[i-1],lengths[i],t);
                offset=Vector3.Dot(remainder,Vector3.Cross(Vector3.up,edge.normalized));
            }
            return d;
        }
        static float SideHeight(float d,float x)
        {
            float shoulder=Mathf.SmoothStep(0,1,Mathf.InverseLerp(115,280,Mathf.Abs(x)));
            return shoulder*(14+32*Mathf.PerlinNoise(d*.0018f,17+x*.005f));
        }
        static float LiftAt(Vector3 p)
        {float d=Project(p,out float x);return RoadHeight(d)+SideHeight(d,x);}
        static void Lift(Transform t)
        {t.position+=Vector3.up*LiftAt(t.position);objects++;}
        static void ProcessChildren(Transform group)
        {
            foreach(Transform t in group)
            {
                var mf=t.GetComponent<MeshFilter>();
                string path=mf!=null&&mf.sharedMesh!=null?AssetDatabase.GetAssetPath(mf.sharedMesh):"";
                if(path.StartsWith("Assets/Content/JourneyLandscape/")||path.StartsWith("Assets/Content/JourneyRoad/"))
                {
                    var mesh=mf.sharedMesh;if(!edited.Add(mesh))continue;
                    var v=mesh.vertices;if(v.Length==0)throw new InvalidOperationException("Unreadable terrain: "+path);
                    for(int i=0;i<v.Length;i++){var p=t.TransformPoint(v[i]);p.y+=LiftAt(p);v[i]=t.InverseTransformPoint(p);}
                    var collider=t.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=null;
                    mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
                    if(collider!=null)collider.sharedMesh=mesh;surfaces++;
                }
                else Lift(t);
            }
        }
        static void BuildHorizon(Transform root)
        {
            // Broad world-space tiles avoid folded ribbons at bends. Inner vertices remain below the authored terrain.
            var material=new Material(Shader.Find("Standard")){name="Distant Heather",color=new Color(.29f,.35f,.29f),enableInstancing=true};
            material.SetFloat("_Glossiness",0);AssetDatabase.CreateAsset(material,Folder+"/DistantHeather.mat");
            for(int tile=0;tile<17;tile++)
            {
                int columns=65,rows=17;var v=new Vector3[columns*rows];var tris=new List<int>();
                for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
                {
                    var p=new Vector3(-2500+x*80,0,-1000+tile*1280+z*80);
                    float d=Project(p,out float offset);
                    // Distance to the finite centreline also handles the land behind the starting city.
                    float edge=Mathf.Abs(offset);
                    float near=4+16*Mathf.PerlinNoise(d*.003f,offset*.008f+25)+SideHeight(d,offset);
                    float outer=Mathf.SmoothStep(0,1,Mathf.InverseLerp(310,950,edge));
                    float hill=75+160*Mathf.PerlinNoise(p.x*.0017f+31,p.z*.0013f+8);
                    p.y=RoadHeight(d)+Mathf.Lerp(near-8,hill,outer);
                    p.y-=55*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(180,310,edge)));
                    v[z*columns+x]=p;
                    if(z<rows-1&&x<columns-1){int a=z*columns+x;tris.AddRange(new[]{a,a+columns,a+1,a+1,a+columns,a+columns+1});}
                }
                var mesh=new Mesh{name="Horizon_"+tile};mesh.vertices=v;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Folder+"/Horizon_"+tile+".asset");
                var go=new GameObject(mesh.name);go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
            }
            // Sparse groups on the crests give scale without a wall of equally spaced trees.
            var random=new System.Random(926);
            for(float d=900;d<route.Length-200;d+=260)
            {
                route.Evaluate(d,out var p,out var q);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int n=0;n<5;n++)
                    {
                        var position=p+q*new Vector3(side*(205+(float)random.NextDouble()*65),0,(float)random.NextDouble()*80-40);
                        if(!Physics.Raycast(position+Vector3.up*400,Vector3.down,out var hit,650,~0,QueryTriggerInteraction.Ignore))continue;
                        JourneyLandscapeAuthoring.Place("Assets/Downloads/Low Poly Forest - Free Starter Pack/Prefabs/Trees/pine_large.prefab",root,hit.point,(float)random.NextDouble()*360,13+(float)random.NextDouble()*9,0);
                    }
                }
            }
        }

        [MenuItem("RogueDrive/Route/Blend Landscape Horizon")]
        public static void BlendEdges()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            var root=route.transform.Find("Afternoon_Landscape");
            if(root==null||root.Find("Horizon_Blended")!=null)throw new InvalidOperationException("Missing landscape or already blended.");
            var old=root.Find("Landscape_Seams");
            if(old!=null)
            {
                var paths=old.GetComponentsInChildren<MeshFilter>(true).Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)).ToArray();
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                foreach(var path in paths)if(path.StartsWith(Folder+"/")&&path.Contains("_Seam_"))AssetDatabase.DeleteAsset(path);
            }
            var horizon=root.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Horizon_")).ToArray();
            foreach(var mf in route.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Terrain_")&&m.sharedMesh!=null))
            {
                var mesh=mf.sharedMesh;var v=mesh.vertices;if(v.Length%61!=0)continue;
                for(int i=0;i<v.Length;i++)
                {
                    float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(190,300,Mathf.Abs(i%61-30)*10));
                    if(t==0)continue;
                    var p=mf.transform.TransformPoint(v[i]);var ray=new Ray(p+Vector3.up*500,Vector3.down);
                    foreach(var c in horizon)if(c.Raycast(ray,out var hit,1000)){p.y=Mathf.Lerp(p.y,hit.point.y+.025f,t);break;}
                    v[i]=mf.transform.InverseTransformPoint(p);
                }
                var collider=mf.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=null;
                // Inside bends can reverse the old ribbon's winding beyond the playable verge.
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    if(Vector3.Cross(v[triangles[i+1]]-v[triangles[i]],v[triangles[i+2]]-v[triangles[i]]).y>=0)continue;
                    int b=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=b;
                }
                mesh.vertices=v;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
                if(collider!=null)collider.sharedMesh=mesh;
                mf.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Physics.SyncTransforms();
            var grounds=route.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Terrain_")||c.name.StartsWith("Horizon_")).ToArray();
            int grounded=0;
            foreach(var t in route.GetComponentsInChildren<Transform>(true))
            {
                if(!(t.name.StartsWith("pine_")||t.name.StartsWith("birch_")||t.name.StartsWith("oak_")||t.name.StartsWith("Bush_")||t.name.StartsWith("stone_")))continue;
                var renderers=t.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                var ray=new Ray(new Vector3(b.center.x,b.min.y+400,b.center.z),Vector3.down);
                float height=float.NegativeInfinity;
                foreach(var ground in grounds)if(ground.Raycast(ray,out var hit,700))height=Mathf.Max(height,hit.point.y);
                if(float.IsNegativeInfinity(height))continue;
                t.position+=Vector3.up*(height-b.min.y-.08f);grounded++;
            }
            new GameObject("Horizon_Blended").transform.SetParent(root,false);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[Afternoon] Terrain edges blended, plants seated: "+grounded);
        }
    }
}
