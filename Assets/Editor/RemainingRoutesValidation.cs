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
    public static class RemainingRoutesValidation
    {
        public static string ValidateActive()
        {
            var route=Object.FindFirstObjectByType<StageRoute>();
            if(route==null)throw new InvalidOperationException("Open an authored journey.");
            Physics.SyncTransforms();
            int rays=0,missing=0,blocked=0;float maxGrade=0,minY=float.MaxValue,maxY=float.MinValue;
            var failures=new List<string>();
            for(float d=5;d<route.Length-110;d+=5)
            {
                route.Evaluate(d,out var p,out var q);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
                Vector3 f=q*Vector3.forward;maxGrade=Mathf.Max(maxGrade,Mathf.Abs(f.y)/Mathf.Max(.001f,new Vector2(f.x,f.z).magnitude));
                foreach(float side in new[]{-5f,0,5f})
                {
                    Vector3 point=p+q*Vector3.right*side; rays++;
                    var hits=Physics.RaycastAll(point+Vector3.up*1.5f,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.collider.GetComponentInParent<ArcadeCarController>()==null).OrderBy(h=>h.distance).ToArray();
                    var hit=hits.Length>0?hits[0]:default(RaycastHit);
                    if(hits.Length==0||Mathf.Abs(hit.point.y-point.y)>.35f)
                    {missing++;if(failures.Count<25)failures.Add("Surface "+d+" lane "+side+" "+(hit.collider!=null?hit.collider.name:"missing"));}
                }
                var overlaps=Physics.OverlapBox(p+Vector3.up*1.9f,new Vector3(2.7f,1.35f,1.3f),q,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.GetComponentInParent<ArcadeCarController>()==null).ToArray();
                if(overlaps.Length>0){blocked++;if(failures.Count<25)failures.Add("Clearance "+d+": "+string.Join(",",overlaps.Select(c=>c.name)));}
            }
            string result=$"{SceneManager.GetActiveScene().name}: length={route.Length:F1}m, height={minY:F1}..{maxY:F1}m, maxGrade={maxGrade:P1}, rays={rays}, missing={missing}, blocked={blocked}\n"+string.Join("\n",failures);
            Directory.CreateDirectory("Temp/RemainingRoutes");File.WriteAllText("Temp/RemainingRoutes/"+SceneManager.GetActiveScene().name+"_validation.txt",result);
            Debug.Log(result);return result;
        }

        public static string Capture(float distance,string name,bool aerial=false)
        {
            var route=StageRoute.Instance??Object.FindFirstObjectByType<StageRoute>();if(route==null)throw new InvalidOperationException("Route missing.");
            route.Evaluate(distance,out var p,out var q);
            var go=new GameObject("Temporary_Review_Camera");var camera=go.AddComponent<Camera>();
            camera.fieldOfView=aerial?62:68;camera.farClipPlane=1800;camera.nearClipPlane=.2f;
            camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=RenderSettings.fogColor;
            go.transform.position=p+q*new Vector3(aerial?100:0,aerial?95:4,aerial?-120:-18);
            go.transform.LookAt(p+q*Vector3.forward*(aerial?70:50)+Vector3.up*(aerial?0:2));
            var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            try
            {
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
                Directory.CreateDirectory("Temp/RemainingRoutes");string path="Temp/RemainingRoutes/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);return Path.GetFullPath(path);
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
