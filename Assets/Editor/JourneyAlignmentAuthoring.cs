using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>Distance-preserving re-alignment of the saved road and its surrounding world.</summary>
    public static class JourneyAlignmentAuthoring
    {
        static readonly float[] Distances={0,350,1000,1450,2100,2450,3100,3850,4300,4900,5500,6100,6800,7500,8200,8900,9600,10200,10800,11400,12000,12500,13400,14000,14600,16000};
        static readonly float[] Headings={0,0,-35,-35,30,30,70,-10,-10,-50,15,15,65,-5,-55,10,10,45,-15,-50,10,0,0,-35,25,25};
        static Vector3[] oldPoints,newPoints;
        static float[] oldLengths;
        static readonly HashSet<Mesh> editedMeshes=new HashSet<Mesh>();
        static int moved,warped;

        [MenuItem("RogueDrive/Route/Author Varied Route Alignment")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="Stage1_Outskirts")throw new InvalidOperationException("Open Stage1_Outskirts.");
            var route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            if(route.transform.Find("Alignment_V2")!=null)throw new InvalidOperationException("Alignment already applied.");
            var property=new SerializedObject(route).FindProperty("points");oldPoints=new Vector3[property.arraySize];oldLengths=new float[property.arraySize];
            for(int i=0;i<oldPoints.Length;i++)
            {oldPoints[i]=property.GetArrayElementAtIndex(i).vector3Value;if(i>0)oldLengths[i]=oldLengths[i-1]+Vector3.Distance(oldPoints[i],oldPoints[i-1]);}
            float length=route.Length;
            var generated=new List<Vector3>{oldPoints[0]};
            for(float d=0;d<length;d+=10)
            {
                float step=Mathf.Min(10,length-d);
                generated.Add(generated[generated.Count-1]+Quaternion.Euler(0,Heading(d+step/2),0)*Vector3.forward*step);
            }
            newPoints=generated.ToArray();moved=warped=0;editedMeshes.Clear();
            // Rigid asset roots retain their shape; terrain and road surfaces follow the new curve.
            foreach(Transform child in route.transform)
            {
                if(child.name=="Journey_Landscape")foreach(Transform group in child)ProcessChildren(group);
                else if(child.name=="Asset_Road_And_Service"||child.name=="World_Activity_Details")ProcessChildren(child);
                else if(child.GetComponent<TrackChunk>()!=null)
                {
                    MoveRigid(child);
                    foreach(var t in child.GetComponentsInChildren<Transform>(true))
                        if(t.name=="DividerWall")t.gameObject.SetActive(false);
                }
            }
            route.Configure(newPoints);
            JourneyRoadUpgrade.RebuildRoadMeshes();
            var marker=new GameObject("Alignment_V2");marker.transform.SetParent(route.transform,false);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Alignment] {route.Length:F1} m, {moved} asset roots moved, {warped} meshes deformed. Heading -55 to +70 degrees.");
        }

        static float Heading(float d)
        {
            int i=1;while(i<Distances.Length-1&&Distances[i]<d)i++;
            return Mathf.Lerp(Headings[i-1],Headings[i],Mathf.SmoothStep(0,1,Mathf.InverseLerp(Distances[i-1],Distances[i],d)));
        }
        static void EvaluateNew(float d,out Vector3 p,out Quaternion q)
        {
            int i=Mathf.Clamp(Mathf.FloorToInt(d/10),0,newPoints.Length-2);
            Vector3 edge=newPoints[i+1]-newPoints[i];
            p=newPoints[i]+edge*((d-i*10)/Mathf.Max(.001f,edge.magnitude));q=Quaternion.LookRotation(edge);
        }
        static float ProjectOld(Vector3 p,out Vector3 centre,out Quaternion rotation)
        {
            float best=float.PositiveInfinity,distance=0;centre=oldPoints[0];Vector3 bestEdge=Vector3.forward;
            for(int i=1;i<oldPoints.Length;i++)
            {
                var a=oldPoints[i-1];var edge=oldPoints[i]-a;
                float t=Vector3.Dot(new Vector3(p.x-a.x,0,p.z-a.z),edge)/Mathf.Max(.001f,edge.sqrMagnitude);
                t=i==1?Mathf.Min(1,t):i==oldPoints.Length-1?Mathf.Max(0,t):Mathf.Clamp01(t);
                var c=a+edge*t;float error=(p.x-c.x)*(p.x-c.x)+(p.z-c.z)*(p.z-c.z);
                if(error>=best)continue;best=error;centre=c;bestEdge=edge;
                distance=oldLengths[i-1]+edge.magnitude*t;
            }
            rotation=Quaternion.LookRotation(bestEdge);return distance;
        }
        static Vector3 Map(Vector3 position)
        {
            float d=ProjectOld(position,out var centre,out var oldRotation);
            EvaluateNew(d,out var newCentre,out var newRotation);
            return newCentre+newRotation*Quaternion.Inverse(oldRotation)*(position-centre);
        }
        static void MoveRigid(Transform t)
        {
            float d=ProjectOld(t.position,out var centre,out var oldRotation);EvaluateNew(d,out var p,out var q);
            t.SetPositionAndRotation(p+q*Quaternion.Inverse(oldRotation)*(t.position-centre),q*Quaternion.Inverse(oldRotation)*t.rotation);moved++;
        }
        static void ProcessChildren(Transform group)
        {
            foreach(Transform t in group)
            {
                var mf=t.GetComponent<MeshFilter>();
                string path=mf!=null&&mf.sharedMesh!=null?AssetDatabase.GetAssetPath(mf.sharedMesh):"";
                if(path.StartsWith("Assets/Content/JourneyLandscape/")||path.StartsWith("Assets/Content/JourneyRoad/"))
                {
                    var mesh=mf.sharedMesh;if(!editedMeshes.Add(mesh))continue;
                    var vertices=mesh.vertices;if(vertices.Length==0)throw new InvalidOperationException("Unreadable authored mesh: "+path);
                    for(int i=0;i<vertices.Length;i++)vertices[i]=t.InverseTransformPoint(Map(t.TransformPoint(vertices[i])));
                    var collider=t.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=null;
                    mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
                    if(collider!=null)collider.sharedMesh=mesh;warped++;
                }
                else MoveRigid(t);
            }
        }
    }
}
