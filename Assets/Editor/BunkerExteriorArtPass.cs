using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay.Hub;

public static class BunkerExteriorArtPass
{
    [MenuItem("RogueDrive/Bunker/Replace Garage Exterior Billboard")]
    public static void IntegrateGarageExterior()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var garage = SceneManager.GetSceneByName("GarageScene");
        if (!garage.isLoaded) throw new InvalidOperationException("Open GarageScene first.");
        SceneManager.SetActiveScene(garage);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/JourneyAtmosphere/AfternoonSky.mat");
        foreach (var camera in garage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            camera.clearFlags = CameraClearFlags.Skybox;
        if (garage.GetRootGameObjects().Any(g => g.name == "Garage_Exterior_3D"))
        {
            ClearGarageInterior();
            return;
        }
        var stage = SceneManager.GetSceneByName("Stage1_Outskirts");
        bool opened = !stage.isLoaded;
        if (opened) stage = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity", OpenSceneMode.Additive);
        var world = stage.GetRootGameObjects().Single(g => g.name == "Stage_World").transform;
        var root = new GameObject("Garage_Exterior_3D");
        SceneManager.MoveGameObjectToScene(root, garage);
        foreach (var name in new[] { "Bunker_Launch_Set", "Bunker_Exterior_Art" })
        {
            var copy = UnityEngine.Object.Instantiate(world.Find(name).gameObject);
            copy.name = name;
            SceneManager.MoveGameObjectToScene(copy, garage);
            copy.transform.SetParent(root.transform, false);
            copy.transform.position += new Vector3(0, 1.8f, 13.8f);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name == "Tunnel_Shadow_End" || t.name.StartsWith("Tunnel_Rib") || t.name.StartsWith("Tunnel_Light") || t.name == "Tunnel_Interior_Glow")
                    t.gameObject.SetActive(false);
        }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Exterior_Ground";
        SceneManager.MoveGameObjectToScene(ground, garage);
        ground.transform.SetParent(root.transform, false);
        ground.transform.position = new Vector3(0, 1.55f, 93.8f);
        ground.transform.localScale = new Vector3(220, .5f, 160);
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BunkerLaunch/Art_Earth.mat");
        foreach (var t in garage.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            if (t.name == "Exit_Horizon" || t.name == "Exit_Apron") t.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(garage);
        EditorSceneManager.SaveScene(garage);
        if (opened) EditorSceneManager.CloseScene(stage, true);
        SceneManager.SetActiveScene(garage);
        ClearGarageInterior();
    }

    public static void ClearGarageInterior()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var root = GameObject.Find("Garage_Exterior_3D");
        if (root == null) throw new InvalidOperationException("Garage exterior is missing.");
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Faceted_Earth_Embankment") continue;
            // The road scene has a narrow tunnel; the garage has a full-width workshop.
            // Retain only the mesh in front of the gate instead of copying earth into it.
            var source = filter.sharedMesh;
            var vertices = source.vertices;
            var triangles = source.triangles;
            var kept = new System.Collections.Generic.List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                if (vertices[triangles[i]].z >= 0 && vertices[triangles[i + 1]].z >= 0 && vertices[triangles[i + 2]].z >= 0)
                    kept.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
            string path = "Assets/Materials/BunkerLaunch/Garage_Exterior_Earth_" + (vertices[0].x < 0 ? "Left" : "Right") + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = kept.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            filter.GetComponent<MeshCollider>().sharedMesh = mesh;
            EditorUtility.SetDirty(mesh);
        }
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (renderer.name == "Rock_Embankment" && renderer.bounds.min.z < 13.8f)
                renderer.gameObject.SetActive(false);
            if (renderer.name == "Recessed_Concrete_Jamb" && renderer.bounds.min.z < 13.8f)
                renderer.transform.position += Vector3.forward * (13.8f - renderer.bounds.min.z);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(root.scene);
    }

    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = SceneManager.GetSceneByName("Stage1_Outskirts");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity", OpenSceneMode.Additive);
        var world = scene.GetRootGameObjects().Single(g => g.name == "Stage_World").transform;
        var launch = world.Find("Bunker_Launch_Set");
        var old = world.Find("Bunker_Exterior");
        var previous = world.Find("Bunker_Exterior_Art");
        if (previous != null) throw new InvalidOperationException("Art pass already saved; edit the objects.");
        foreach (Transform t in launch)
            if (t.name.StartsWith("Portal_") || t.name == "Bunker_07_Sign") t.gameObject.SetActive(false);
        foreach (Transform t in old)
            if (t.name == "Left_Wall" || t.name == "Right_Wall" || t.name == "Lintel") t.gameObject.SetActive(false);
        var root = new GameObject("Bunker_Exterior_Art").transform; root.SetParent(world, false);
        var concrete = Mat("Art_Concrete", new Color(.36f,.36f,.31f));
        var edge = Mat("Art_Concrete_Edge", new Color(.22f,.25f,.23f));
        var steel = Mat("Art_Steel", new Color(.10f,.14f,.15f));
        var dirt = Mat("Art_Earth", new Color(.27f,.28f,.20f));
        var amber = Mat("Art_Safety", new Color(.78f,.46f,.13f));
        var black = Mat("Art_Tunnel_Dark", new Color(.025f,.033f,.035f));
        var light = Mat("Art_Lamp", new Color(.95f,.69f,.35f));
        light.EnableKeyword("_EMISSION"); light.SetColor("_EmissionColor", new Color(1,.55f,.18f) * 1.7f);
        for (int side = -1; side <= 1; side += 2)
        {
            Cube("Recessed_Concrete_Jamb", root, new Vector3(side*4.5f,2.55f,-.9f),new Vector3(2.0f,5.1f,2.8f),concrete);
            Cube("Steel_Door_Frame",root,new Vector3(side*3.6f,2.15f,.56f),new Vector3(.25f,4.3f,.28f),steel);
            Cube("Corner_Reinforcement",root,new Vector3(side*5.5f,2.7f,-.3f),new Vector3(.32f,5.4f,1.8f),edge);
            for (int row = 0; row < 4; row++)
            {
                Cube("Concrete_Form_Joint",root,new Vector3(side*4.5f,.65f+row*1.15f,.52f),new Vector3(1.85f,.025f,.035f),edge,false);
                for(int bolt=0;bolt<2;bolt++) Cube("Anchor_Plate",root,new Vector3(side*(3.95f+bolt*1.0f),.65f+row*1.15f,.56f),new Vector3(.12f,.12f,.07f),steel,false);
            }
            Cube("Hazard_Backplate",root,new Vector3(side*3.93f,1.2f,.61f),new Vector3(.33f,1.7f,.06f),steel,false);
            for(int stripe=0;stripe<5;stripe++)
            {
                var band=Cube("Hazard_Stripe",root,new Vector3(side*3.93f,.5f+stripe*.30f,.66f),new Vector3(.30f,.13f,.025f),amber,false);
                band.rotation=Quaternion.Euler(0,0,-25);
            }
            Cube("Portal_Lamp_Casing",root,new Vector3(side*4.45f,4.55f,.7f),new Vector3(.85f,.25f,.35f),steel,false);
            Cube("Portal_Lamp_Lens",root,new Vector3(side*4.45f,4.49f,.91f),new Vector3(.62f,.1f,.05f),light,false);
            for(int i=0;i<3;i++)
            {
                Cube("Tunnel_Rib",root,new Vector3(side*3.47f,2.3f,-2-i*3),new Vector3(.13f,4.6f,.23f),steel);
                Cube("Tunnel_Light",root,new Vector3(side*3.38f,3.1f,-2-i*3),new Vector3(.06f,.12f,1.1f),light,false);
            }
            Embankment(root,side,dirt);
        }
        Cube("Recessed_Lintel",root,new Vector3(0,4.9f,-.8f),new Vector3(11,1.2f,3),concrete);
        Cube("Steel_Lintel",root,new Vector3(0,4.28f,.56f),new Vector3(7.3f,.25f,.28f),steel);
        Cube("Tunnel_Shadow_End",root,new Vector3(0,2.4f,-10.8f),new Vector3(7,4.8f,.15f),black,false);
        Cube("Earth_Roof_Cap",root,new Vector3(0,6.25f,-7),new Vector3(15,1.5f,14),dirt);
        foreach(int side in new[]{-1,1})
        {
            var pool=new GameObject("Portal_Warm_Pool").AddComponent<Light>();pool.transform.SetParent(root,false);
            pool.transform.position=new Vector3(side*4.45f,4.35f,1.1f);pool.type=LightType.Point;pool.range=6;pool.intensity=1.4f;
            pool.color=new Color(1,.74f,.46f);pool.shadows=LightShadows.None;
        }
        var tunnelGlow=new GameObject("Tunnel_Interior_Glow").AddComponent<Light>();tunnelGlow.transform.SetParent(root,false);
        tunnelGlow.transform.position=new Vector3(0,2.8f,-4);tunnelGlow.type=LightType.Point;tunnelGlow.range=7;tunnelGlow.intensity=.85f;
        tunnelGlow.color=new Color(.72f,.78f,.83f);tunnelGlow.shadows=LightShadows.None;
        Cube("Sector_Plaque",root,new Vector3(4.53f,2.3f,.65f),new Vector3(1.35f,1.1f,.07f),steel,false);
        var text=new GameObject("Sector_07").AddComponent<TextMesh>(); text.transform.SetParent(root,false);
        text.transform.position=new Vector3(4.53f,2.35f,.71f); text.transform.rotation=Quaternion.Euler(0,180,0);
        text.text="07";text.anchor=TextAnchor.MiddleCenter;text.fontSize=64;text.characterSize=.17f;text.color=new Color(.83f,.76f,.58f);
        var subtitle=new GameObject("Sector_Label").AddComponent<TextMesh>(); subtitle.transform.SetParent(root,false);
        subtitle.transform.position=new Vector3(4.53f,1.95f,.71f); subtitle.transform.rotation=Quaternion.Euler(0,180,0);
        subtitle.text="NORTH ACCESS";subtitle.anchor=TextAnchor.MiddleCenter;subtitle.fontSize=48;subtitle.characterSize=.028f;subtitle.color=new Color(.63f,.69f,.66f);
        var pine=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Environment/Pine_01.prefab");
        var grass=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Grass_1_1.prefab");
        for(int i=0;i<24;i++)
        {
            int side=i%2==0?-1:1;
            var tree=(GameObject)PrefabUtility.InstantiatePrefab(pine,root);
            tree.name="Pine_Embankment";tree.transform.position=new Vector3(side*(20+i%4*4),0,-16+i/2*5);
            tree.transform.rotation=Quaternion.Euler(0,i*137,0);tree.transform.localScale=Vector3.one*(.8f+i%3*.18f);
            for(int j=0;j<2;j++)
            {
                var clump=(GameObject)PrefabUtility.InstantiatePrefab(grass,root);clump.name="Roadside_Dry_Grass";
                clump.transform.position=new Vector3(side*(6.5f+j*2.7f),0,23+i*1.45f);clump.transform.localScale=Vector3.one*(1.3f+i%3*.3f);
            }
        }
        foreach(var r in launch.GetComponentsInChildren<Renderer>(true))
            if(r.name=="Concrete_Barrier")r.sharedMaterial=concrete;
        var arrival=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageGarageArrival>(true)).Single();
        arrival.ExteriorShot.position=new Vector3(3.6f,4f,27);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static Material Mat(string name,Color color)
    {
        const string dir="Assets/Materials/BunkerLaunch";
        string path=dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Glossiness",.04f);EditorUtility.SetDirty(m);return m;
    }
    static Transform Cube(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
        g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
    }
    static void Embankment(Transform parent,int side,Material mat)
    {
        float[] zs={-24,-12,0,7,17,36};float[] xs={5.55f,10,19,31};float[] hs={5.7f,7.2f,3.1f,0};
        var vertices=new System.Collections.Generic.List<Vector3>();var tris=new System.Collections.Generic.List<int>();
        for(int z=0;z<zs.Length-1;z++)for(int x=0;x<xs.Length-1;x++)
        {
            int k=vertices.Count;
            for(int dz=0;dz<2;dz++)for(int dx=0;dx<2;dx++)
            {
                float fade=zs[z+dz]<=0?1:Mathf.Clamp01(1-zs[z+dz]/36);
                vertices.Add(new Vector3(side*xs[x+dx],hs[x+dx]*fade,zs[z+dz]));
            }
            int[] face=side==1?new[]{k,k+2,k+1,k+1,k+2,k+3}:new[]{k,k+1,k+2,k+1,k+3,k+2};tris.AddRange(face);
        }
        var edges=new System.Collections.Generic.Dictionary<string,Tuple<Vector3,Vector3,int>>();
        for(int i=0;i<tris.Count;i+=3)for(int e=0;e<3;e++)
        {
            var a=vertices[tris[i+e]];var b=vertices[tris[i+(e+1)%3]];
            var sa=a.ToString("F3");var sb=b.ToString("F3");var key=string.CompareOrdinal(sa,sb)<0?sa+sb:sb+sa;
            Tuple<Vector3,Vector3,int> old;
            edges[key]=edges.TryGetValue(key,out old)?Tuple.Create(old.Item1,old.Item2,old.Item3+1):Tuple.Create(a,b,1);
        }
        foreach(var boundary in edges.Values.Where(e=>e.Item3==1))
        {
            var a=boundary.Item1;var b=boundary.Item2;if(a.y<.01f&&b.y<.01f)continue;
            int k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(new Vector3(a.x,-.1f,a.z));vertices.Add(new Vector3(b.x,-.1f,b.z));
            tris.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
        }
        var mesh=new Mesh();mesh.name="Bunker_Earth_"+side;mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
        string path="Assets/Materials/BunkerLaunch/Bunker_Earth_"+side+".asset";AssetDatabase.CreateAsset(mesh,path);
        var g=new GameObject("Faceted_Earth_Embankment");g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
}
