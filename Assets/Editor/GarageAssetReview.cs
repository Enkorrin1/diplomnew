using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GarageAssetReview
{
    public static string[] Paths()=>AssetDatabase.GetAllAssetPaths().Where(p=>p.EndsWith(".prefab") &&
        (p.Contains("GarageAssetPack/Prefabs") || p.Contains("Low-Poly 3D Lockers") ||
        p.Contains("HouseholdPropsPack/Prefabs/Decoration") || p.Contains("Cosmic_Retro") ||
        (p.Contains("Apocalypse_Free/Prefabs/Props") && !p.Contains("Apocalypse_Car") && !p.Contains("Pistol")))).OrderBy(p=>p).ToArray();
    public static void Preview(int start,int count)
    {
        Directory.CreateDirectory("Temp/GarageDressing/Previews");
        var paths=Paths(); File.WriteAllLines("Temp/GarageDressing/review.txt",paths);
        var preview=new PreviewRenderUtility();
        try
        {
            for(int i=start;i<Mathf.Min(start+count,paths.Length);i++)
            {
                var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
                preview.AddSingleGO(instance);
                var renderers=instance.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0) {Object.DestroyImmediate(instance);continue;}
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                var camera=preview.camera;camera.fieldOfView=35;camera.nearClipPlane=.01f;camera.farClipPlane=1000;
                camera.transform.position=bounds.center+new Vector3(1,.7f,-1).normalized*bounds.size.magnitude*1.9f;
                camera.transform.LookAt(bounds.center);camera.clearFlags=CameraClearFlags.Color;camera.backgroundColor=new Color(.18f,.21f,.24f);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);
                preview.lights[1].intensity=.9f;preview.ambientColor=new Color(.4f,.4f,.4f);
                preview.BeginStaticPreview(new Rect(0,0,240,190));preview.Render();
                var tex=preview.EndStaticPreview();File.WriteAllBytes($"Temp/GarageDressing/Previews/{i:D3}.png",tex.EncodeToPNG());
                Object.DestroyImmediate(tex);Object.DestroyImmediate(instance);
            }
        }
        finally {preview.Cleanup();}
    }
}
