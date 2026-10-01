using System.IO;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class GarageInventoryIcons
{
    public static string Generate()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        const string folder="Assets/Textures/UI/InventoryProps";Directory.CreateDirectory(folder);
        var groups=Object.FindObjectsByType<PhysicsProp>(FindObjectsSortMode.None).GroupBy(p=>p.PropName).OrderBy(g=>g.Key).ToArray();
        var preview=new PreviewRenderUtility();
        try
        {
            for(int i=0;i<groups.Length;i++)
            {
                var clone=Object.Instantiate(groups[i].First().gameObject);clone.transform.position=Vector3.zero;clone.transform.rotation=Quaternion.identity;
                foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(script);
                foreach(var collider in clone.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                foreach(var rb in clone.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(rb);
                foreach(var light in clone.GetComponentsInChildren<Light>())Object.DestroyImmediate(light);
                preview.AddSingleGO(clone);
                var renderers=clone.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                var camera=preview.camera;camera.orthographic=true;camera.orthographicSize=b.extents.magnitude*1.12f;
                camera.nearClipPlane=.01f;camera.farClipPlane=1000;
                camera.transform.position=b.center+new Vector3(1,.6f,-1).normalized*(b.size.magnitude*3+1);camera.transform.LookAt(b.center);
                camera.clearFlags=CameraClearFlags.Color;camera.backgroundColor=new Color(0,0,0,0);
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);
                preview.lights[1].intensity=1;preview.ambientColor=new Color(.6f,.6f,.6f);
                preview.BeginStaticPreview(new Rect(0,0,192,192));preview.Render();
                var previous=RenderTexture.active;RenderTexture.active=camera.targetTexture;
                var tex=new Texture2D(192,192,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,192,192),0,0);tex.Apply();
                RenderTexture.active=previous;var rgbPreview=preview.EndStaticPreview();Object.DestroyImmediate(rgbPreview);
                string path=$"{folder}/prop_{i:00}.png";File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);Object.DestroyImmediate(clone);
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                foreach(var prop in groups[i]){prop.SetInventoryIcon(sprite);EditorUtility.SetDirty(prop);}
            }
        }
        finally{preview.Cleanup();}
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return $"Generated {groups.Length} item icons";
    }
}
