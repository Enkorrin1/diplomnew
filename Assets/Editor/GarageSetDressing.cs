using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GarageSetDressing
{
    static Transform root;
    static string[] assets;
    static int placed;
    public static void Author()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="GarageScene")throw new InvalidOperationException("Open GarageScene");
        if(GameObject.Find("Garage_SetDressing")!=null)throw new InvalidOperationException("Already dressed");
        Directory.CreateDirectory("Temp/GarageDressing");
        File.Copy("Assets/Scenes/GarageScene.unity","Temp/GarageDressing/GarageScene.before.unity",true);
        assets=GarageAssetReview.Paths();
        root=new GameObject("Garage_SetDressing").transform;
        // Remove only the previous pass's floating labels, retaining its useful lights.
        var old=GameObject.Find("Garage_Presentation");
        if(old!=null)foreach(Transform t in old.transform.Cast<Transform>().ToArray())
            if(t.name.StartsWith("Sign_") || t.name=="Exit_Lane" || t.name=="Workshop_Boundary")Object.DestroyImmediate(t.gameObject);

        var workshop=Zone("01_Workshop");
        Place(25,workshop,-10.5f,0,4.5f,90,1.9f);
        Place(25,workshop,-10.5f,0,6.4f,90,1.9f);
        Place(21,workshop,-10.8f,0,8.6f,90,2.5f);
        Place(21,workshop,-10.8f,0,10.2f,90,2.5f);
        Place(23,workshop,-7.7f,0,6.7f,8,.16f);
        for(int i=0;i<4;i++)Place(15,workshop,-7.7f,.16f+i*.22f,6.7f,i*23,.22f);
        Place(15,workshop,-8.8f,0,5.9f,10,.78f,new Vector3(0,0,90));
        Place(22,workshop,-9,0,3.9f,0,.7f);
        Place(82,workshop,-10.4f,0,2.1f,12,.6f);
        Place(83,workshop,-10.4f,.6f,2.1f,-7,.45f);
        Place(14,workshop,-8.2f,0,4.8f,-15,.34f);

        var storage=Zone("02_Stores");
        for(int i=0;i<4;i++)
        {
            float x=-9.8f+i*1.6f;
            Place(23,storage,x,0,10.8f,0,.16f);
            Place(82,storage,x-.22f,.16f,10.8f,i*7,.7f);
            Place(82,storage,x+.25f,.86f,10.8f,3+i*8,.5f);
        }
        for(int i=0;i<3;i++)Place(10,storage,-6.7f+i*.85f,0,8.7f,15*i,1.05f);
        Place(83,storage,-5.5f,0,10.5f,20,.6f);

        var supplies=Zone("03_Supplies");
        Place(21,supplies,9.8f,0,9.9f,180,2.5f);
        Place(20,supplies,7.9f,0,9.9f,180,2.5f);
        for(int i=0;i<3;i++)Place(82,supplies,7.5f+i*.35f,.9f,9.9f,0,.32f);
        Place(31,supplies,7.5f,1.55f,9.9f,0,.32f);
        Place(34,supplies,8.15f,.15f,9.9f,0,.45f);
        Place(23,supplies,6.2f,0,8.6f,0,.16f);
        Place(82,supplies,6.2f,.16f,8.6f,-12,.65f);
        Place(83,supplies,6.1f,.81f,8.6f,8,.38f);
        Place(43,supplies,9.8f,0,5.9f,90,.86f);
        Place(41,supplies,8.4f,0,6,-90,.95f);
        Place(40,supplies,9.5f,.87f,5.4f,-20,.22f);
        Place(32,supplies,10,.87f,6.3f,50,.18f,new Vector3(0,0,90));
        Place(53,supplies,9.6f,.87f,6.2f,0,.11f);

        var power=Zone("04_Utility");
        for(int i=0;i<3;i++)Place(10,power,10.6f,0,1.9f+i*.86f,i*21,1.0f);
        Place(23,power,9.2f,0,2.9f,0,.15f);
        Place(18,power,8.9f,.15f,2.6f,6,.48f);
        Place(18,power,9.45f,.15f,2.6f,-12,.48f);
        Place(18,power,9.15f,.15f,3.2f,0,.48f);
        Place(85,power,11,0,-3.8f,90,2.1f);
        Place(85,power,11,0,-4.5f,90,2.1f);

        var living=Zone("05_Living");
        Place(72,living,2.5f,0,-10.3f,0,1.7f);
        Place(60,living,1.1f,0,-10.3f,0,.88f);
        Place(71,living,.85f,.89f,-10.3f,0,.34f);
        Place(53,living,1.7f,.89f,-10.2f,12,.11f);
        Place(47,living,.2f,.89f,-10.3f,10,.25f);
        Place(43,living,4.2f,0,-6.6f,0,.78f);
        Place(41,living,4.2f,0,-5.5f,180,.95f);
        Place(41,living,3.3f,0,-6.7f,90,.95f);
        Place(46,living,4.05f,.79f,-6.5f,0,.025f);
        Place(53,living,4.5f,.79f,-6.8f,22,.12f);
        Place(40,living,3.7f,.79f,-6.8f,30,.2f);
        Place(83,living,2.2f,0,-8.4f,10,.46f);
        Place(82,living,2.25f,.46f,-8.4f,-8,.36f);
        Place(52,living,2.3f,0,-9,0,.58f);

        // Tighten the existing scene rather than duplicate its interactive objects.
        var bench=GameObject.Find("Assembly_Workbench");
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(r.gameObject.scene!=scene || r.transform.IsChildOf(root))continue;
            if(r.name=="Gas_Burner" || r.name=="First_Aid")
                r.transform.position+=Vector3.up*Mathf.Max(0,.035f-r.bounds.min.y);
        }
        // Put essential pickups on the front work surface, preserving component references.
        var battery=GameObject.Find("Pickup_Battery");if(battery!=null)battery.transform.position=new Vector3(-4.5f,1.05f,2.9f);

        var architecture=Zone("06_Structure");
        var steel=Material("Steel",new Color(.11f,.14f,.15f));
        var wall=Material("LowerWall",new Color(.18f,.24f,.23f));
        foreach(float z in new[]{-7f,0f,7f})
        {
            Box(architecture,"Roof_Beam",new Vector3(0,5.7f,z),new Vector3(24,.35f,.22f),steel);
            foreach(float x in new[]{-11.85f,11.85f})Box(architecture,"Wall_Rib",new Vector3(x,2.8f,z),new Vector3(.18f,5.6f,.24f),steel);
        }
        foreach(float x in new[]{-11.96f,11.96f})Box(architecture,"Lower_Wall_Panel",new Vector3(x,.8f,0),new Vector3(.03f,1.6f,23),wall);
        foreach(float x in new[]{-11.6f,11.6f})
            Box(architecture,"Cable_Tray",new Vector3(x,4.2f,0),new Vector3(.22f,.12f,23),steel);
        // Warm practical lighting belongs to the generator circuit.
        var manager=Object.FindFirstObjectByType<RogueDrive.Gameplay.Hub.GaragePrologueManager>();
        var settings=new SerializedObject(manager);var circuit=settings.FindProperty("mainWorkshopLights");
        foreach(var p in new[]{new Vector3(-8,3.4f,5),new Vector3(-8,3.4f,10),new Vector3(8,3.4f,8),new Vector3(4,3,-7)})
        {
            var light=new GameObject("Zone_Light").AddComponent<Light>();light.transform.SetParent(root);light.transform.position=p;
            light.type=LightType.Point;light.color=new Color(1,.82f,.62f);light.intensity=2.6f;light.range=9;
            circuit.InsertArrayElementAtIndex(circuit.arraySize);circuit.GetArrayElementAtIndex(circuit.arraySize-1).objectReferenceValue=light;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/GarageDressing/placed.txt",placed+" prefab instances");
    }
    static Transform Zone(string name){var go=new GameObject(name);go.transform.SetParent(root);return go.transform;}
    public static void Refine()
    {
        root=GameObject.Find("Garage_SetDressing").transform;assets=GarageAssetReview.Paths();
        var workshop=root.Find("01_Workshop");
        var benches=workshop.Cast<Transform>().Where(t=>t.name=="WorkbenchFull").ToArray();
        for(int i=0;i<benches.Length;i++)Seat(benches[i].gameObject,new Vector3(-10.5f,0,4.5f+i*1.9f),1.9f);
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if(t.name=="WorkbenchFull" && !t.IsChildOf(root) && Bounds(t.gameObject).center.x < -15)
                Seat(t.gameObject,new Vector3(-10.5f,0,1.6f),1.9f);
        }
        var sofa=GameObject.Find("sofa");if(sofa!=null)Seat(sofa,new Vector3(8.6f,0,-8.4f),1.05f);
        var arcade=GameObject.Find("CRR_ArcadeMachine_02");if(arcade!=null)Seat(arcade,new Vector3(5.2f,0,-10.1f),2.05f);
        var living=root.Find("05_Living");
        Place(8,living,7,0,-8.4f,0,.6f);
        Place(9,living,7,.61f,-8.4f,0,.2f);
        Place(53,living,8.3f,.58f,-6.2f,0,.12f);
        Place(42,living,4.2f,3.7f,-6.6f,0,.75f);
        var work=Place(24,workshop,-6.2f,0,4.8f,180,1.8f);
        Place(16,workshop,-6.35f,.89f,4.7f,35,.08f,new Vector3(90,0,0));
        Place(27,workshop,-6f,.89f,4.75f,55,.035f);
        Place(19,workshop,-6.1f,.89f,4.5f,15,.04f,new Vector3(90,0,0));
        var paint=Material("Bay_Edge",new Color(.5f,.39f,.2f));
        var neon=GameObject.Find("NeonRing");if(neon!=null)foreach(var r in neon.GetComponentsInChildren<Renderer>())r.sharedMaterial=paint;
        // Add solid bounds only to large scenery; tiny clutter must not snag walking.
        foreach(Transform zone in root)
        foreach(Transform item in zone)
        {
            if(item.GetComponentsInChildren<Renderer>().Length==0 || item.GetComponentInChildren<Collider>()!=null)continue;
            var b=Bounds(item.gameObject);if(b.size.y < .55f || b.size.x*b.size.z<.25f)continue;
            var collider=item.gameObject.AddComponent<BoxCollider>();
            var corners=new[]{b.min,b.max,new Vector3(b.min.x,b.min.y,b.max.z),new Vector3(b.max.x,b.max.y,b.min.z)};
            var local=new Bounds(item.InverseTransformPoint(corners[0]),Vector3.zero);
            foreach(var point in corners)local.Encapsulate(item.InverseTransformPoint(point));
            collider.center=local.center;collider.size=local.size;
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static void Seat(GameObject go,Vector3 bottom,float height)
    {
        var b=Bounds(go);go.transform.localScale*=height/b.size.y;b=Bounds(go);
        go.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
    }
    static Bounds Bounds(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static GameObject Place(int index,Transform parent,float x,float y,float z,float yaw,float height,Vector3 tilt=default)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(assets[index]);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=prefab.name;
        go.transform.rotation=Quaternion.Euler(tilt.x,yaw+tilt.y,tilt.z);
        var bounds=Bounds(go);go.transform.localScale*=height/Mathf.Max(.001f,bounds.size.y);
        bounds=Bounds(go);go.transform.position+=new Vector3(x-bounds.center.x,y-bounds.min.y,z-bounds.center.z);
        foreach(var rb in go.GetComponentsInChildren<Rigidbody>())rb.isKinematic=true;
        placed++;return go;
    }
    static Material Material(string name,Color color)
    {
        const string directory="Assets/Materials/GaragePresentation";
        var path=directory+"/Dressing_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;mat.SetFloat("_Glossiness",.12f);return mat;
    }
    static void Box(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=pos;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    public static void Capture(string name,Vector3 position,Vector3 target)
    {
        var go=new GameObject("ReviewCamera");var camera=go.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);
        camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
        var rt=RenderTexture.GetTemporary(1600,900,24);var old=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
        File.WriteAllBytes("Temp/GarageDressing/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;
        RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);
    }
}
