using System;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GaragePolishAuthoring
{
    [MenuItem("RogueDrive/Bunker/Polish Garage Presentation")]
    public static void Author()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "GarageScene") throw new InvalidOperationException("Open GarageScene first.");
        if (GameObject.Find("Garage_Presentation") != null) throw new InvalidOperationException("Already authored; adjust saved presentation objects.");
        var root = new GameObject("Garage_Presentation").transform;
        var amber = Material("Workshop_Amber", new Color(.86f,.58f,.22f), true);
        var pale = Material("Workshop_Lamp", new Color(.75f,.84f,.87f), true);
        var dark = Material("Workshop_Sign", new Color(.045f,.07f,.085f), false);
        var paint = Material("Workshop_FloorPaint", new Color(.72f,.53f,.25f), false);
        var manager = Object.FindFirstObjectByType<GaragePrologueManager>();
        var so = new SerializedObject(manager);
        var lights = so.FindProperty("mainWorkshopLights");
        foreach (var position in new[] { new Vector3(-5,3.7f,2.8f), new Vector3(4,4,2), new Vector3(0,4.7f,10) })
        {
            var go = new GameObject("Workshop_Fill"); go.transform.SetParent(root); go.transform.position = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 10; light.intensity = 2.1f;
            light.color = new Color(1,.82f,.59f); light.shadows = LightShadows.None;
            lights.InsertArrayElementAtIndex(lights.arraySize); lights.GetArrayElementAtIndex(lights.arraySize-1).objectReferenceValue = light;
            Box(root,"Ceiling_Luminaire", position+Vector3.up*.3f,new Vector3(1.6f,.08f,.28f),pale);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        // Low-level independent emergency supply keeps the first objective legible.
        var emergency = new GameObject("Emergency_GuideLight"); emergency.transform.SetParent(root);
        emergency.transform.position = new Vector3(4,3,-5);
        var guide = emergency.AddComponent<Light>(); guide.type=LightType.Point; guide.color=new Color(.42f,.60f,.8f);
        guide.intensity=1.2f; guide.range=16; guide.shadows=LightShadows.None;
        Sign(root,"ВЫЕЗД  /  01",new Vector3(0,4.8f,13.3f),Quaternion.identity,4.8f,amber,dark);
        Sign(root,"ПИТАНИЕ",new Vector3(11.5f,3.3f,0),Quaternion.Euler(0,90,0),2.5f,amber,dark);
        Sign(root,"МАСТЕРСКАЯ",new Vector3(-5.1f,2.8f,3.5f),Quaternion.identity,3.1f,pale,dark);
        for(int i=0;i<7;i++)
            foreach(float side in new[]{-2.4f,2.4f})
                Box(root,"Exit_Lane",new Vector3(side,.027f,4+i*1.3f),new Vector3(.12f,.012f,.8f),paint);
        Box(root,"Workshop_Boundary",new Vector3(-5.3f,.027f,1.8f),new Vector3(3.4f,.012f,.08f),paint);

        // Reuse the project's authored canister model, rather than a runtime cube.
        if (GameObject.Find("Pickup_WaterCanister") == null)
        {
            var source=GameObject.Find("JerrycanLarge");
            if(source!=null)
            {
                var water=Object.Instantiate(source,root); water.name="Pickup_WaterCanister";
                water.transform.position=new Vector3(5.2f,.85f,3.2f);
                foreach(var b in water.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(b);
                (water.GetComponent<BunkerAssemblyItem>() ?? water.AddComponent<BunkerAssemblyItem>()).Configure(BunkerAssemblyItemType.WaterCanister,"Вода для радиатора");
                (water.GetComponent<BunkerFluidContainer>() ?? water.AddComponent<BunkerFluidContainer>()).Configure(BunkerFluidType.Water,10,10);
            }
        }
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();
        var introSO=new SerializedObject(intro);
        introSO.FindProperty("bedLyingOffset").vector3Value=new Vector3(0,.7f,0);
        introSO.FindProperty("radioStaticDuration").floatValue=1.4f;
        introSO.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    static Material Material(string name,Color color,bool emissive)
    {
        const string folder="Assets/Materials/GaragePresentation";
        System.IO.Directory.CreateDirectory(folder);
        var path=folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) {mat=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path);}
        mat.color=color; mat.SetFloat("_Glossiness",.12f);
        if(emissive) {mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",color*.7f);}
        EditorUtility.SetDirty(mat); return mat;
    }
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);
        go.transform.position=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Sign(Transform parent,string title,Vector3 pos,Quaternion rotation,float width,Material ink,Material backing)
    {
        var sign=new GameObject("Sign_"+title).transform;sign.SetParent(parent);sign.position=pos;sign.rotation=rotation;
        var board=Box(sign,"Backing",pos,new Vector3(width,.62f,.06f),backing);board.transform.rotation=rotation;
        var text=new GameObject("Label").AddComponent<TextMesh>();text.transform.SetParent(sign,false);text.transform.localPosition=new Vector3(0,0,-.045f);
        text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=80;
        text.characterSize=.043f;text.color=ink.color;
    }
}
