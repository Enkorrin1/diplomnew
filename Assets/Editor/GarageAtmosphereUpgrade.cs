using System;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public static class GarageAtmosphereUpgrade
{
    const string Dir = "Assets/Art/GarageUpgrade/";
    const string Sounds = "Assets/Libraries/Soundbits_freeSFX_2025/Sounds/";
    static Transform root;
    static Material steel, black, amber, ivory;

    [MenuItem("RogueDrive/Bunker/Upgrade Atmosphere And Props")]
    public static void Author()
    {
        if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GarageScene")
            throw new InvalidOperationException("Open GarageScene in Edit mode.");
        var old=GameObject.Find("Garage_AtmosphereUpgrade");
        if(old!=null) throw new InvalidOperationException("Upgrade already authored.");
        root=new GameObject("Garage_AtmosphereUpgrade").transform;
        steel=Mat("Steel",new Color(.19f,.23f,.23f),.45f);
        black=Mat("Rubber",new Color(.035f,.044f,.043f));
        amber=Mat("Ochre",new Color(.65f,.39f,.12f));
        ivory=Mat("Ivory",new Color(.78f,.76f,.65f));
        Map(); Supplies(); Generator(); Floor();
        SetupDirector();
        RefinePlacement();
        AddExitHorizon();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene); AssetDatabase.SaveAssets();
    }
    public static void RefinePlacement()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var board=GameObject.Find("Route_Board").transform;
        board.position=new Vector3(-4,1.95f,-8.1f);board.localScale=Vector3.one*.8f;
        var prep=board.GetComponent<GarageDeparturePreparation>();
        for(int i=0;i<2;i++)
        {
            prep.briefingShots[i].position=board.position+board.rotation*new Vector3(i==0?0:.15f,0,-2.65f);
            prep.briefingShots[i].LookAt(board.position);
        }
        var metal=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Steel.mat");
        if(board.Find("Map_Stand")==null)foreach(float x in new[]{-1.05f,1.05f})
        {
            Box(board,"Map_Stand",new Vector3(x,-1.65f,.05f),new Vector3(.055f,1.5f,.055f),metal);
            Box(board,"Map_Foot",new Vector3(x,-2.39f,0),new Vector3(.3f,.045f,.55f),metal);
        }
        foreach(Transform t in GameObject.Find("Floor_Patina").transform)
        {
            var p=t.position;p.y=Mathf.Abs(p.x)<3.25f && Mathf.Abs(p.z)<3.25f?.206f:.028f;t.position=p;
        }
        EditorSceneManager.MarkSceneDirty(board.gameObject.scene);EditorSceneManager.SaveScene(board.gameObject.scene);
    }
    public static void AddExitHorizon()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var parent=GameObject.Find("Garage_AtmosphereUpgrade").transform;
        if(parent.Find("Exit_Horizon")!=null)return;
        var mat=new Material(Shader.Find("Unlit/Texture"));mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"ExitHorizon.png");
        AssetDatabase.CreateAsset(mat,Dir+"ExitHorizon.mat");
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Exit_Horizon";quad.transform.SetParent(parent,false);
        quad.transform.position=new Vector3(0,6.4f,24);quad.transform.localScale=new Vector3(19.2f,12.8f,1);quad.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(quad.GetComponent<Collider>());
        var asphalt=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Rubber.mat");
        Box(parent,"Exit_Apron",new Vector3(0,1.75f,18.9f),new Vector3(8,.06f,10.2f),asphalt);
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);EditorSceneManager.SaveScene(parent.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static Material Mat(string name,Color color,float metal=0)
    {
        string path=Dir+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Glossiness",.23f);EditorUtility.SetDirty(mat);return mat;
    }
    static Transform Group(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    static GameObject Box(Transform parent,string name,Vector3 p,Vector3 s,Material m,bool solid=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=s;
        g.GetComponent<Renderer>().sharedMaterial=m;if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static TextMesh Label(Transform parent,string text,Vector3 pos,float width,Color color)
    {
        var t=Group("Label_"+text,parent,pos).gameObject.AddComponent<TextMesh>();t.text=text;t.fontSize=72;t.characterSize=.08f;
        t.fontStyle=FontStyle.Bold;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        float actual=t.GetComponent<Renderer>().localBounds.size.x;
        if(actual>.001f)t.transform.localScale=Vector3.one*(width/actual);
        return t;
    }
    static void Map()
    {
        var board=GameObject.Find("Route_Board").transform;
        foreach(Transform c in board.Cast<Transform>().ToArray())c.gameObject.SetActive(false);
        board.position=new Vector3(-4f,2.15f,-11.7f);board.rotation=Quaternion.Euler(0,180,0);
        var col=board.GetComponent<BoxCollider>();col.size=new Vector3(2.7f,1.9f,.16f);col.center=Vector3.zero;
        Box(board,"New_Map_Backing",Vector3.zero,new Vector3(2.75f,1.92f,.12f),black);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"EvacuationRoute.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Dir+"EvacuationRoute.png");
        importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.anisoLevel=4;importer.SaveAndReimport();
        var paper=Mat("EvacuationPrint",Color.white);paper.mainTexture=texture;
        paper.EnableKeyword("_EMISSION");paper.SetTexture("_EmissionMap",texture);paper.SetColor("_EmissionColor",Color.white*.12f);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Evacuation_Map_Print";quad.transform.SetParent(board,false);
        quad.transform.localPosition=new Vector3(0,0,-.071f);quad.transform.localScale=new Vector3(2.6f,1.734f,1);quad.GetComponent<Renderer>().sharedMaterial=paper;Object.DestroyImmediate(quad.GetComponent<Collider>());
        foreach(float x in new[]{-1.35f,1.35f})Box(board,"Frame_Side",new Vector3(x,0,-.09f),new Vector3(.045f,1.9f,.055f),steel);
        foreach(float y in new[]{-.93f,.93f})Box(board,"Frame_Rail",new Vector3(0,y,-.09f),new Vector3(2.7f,.045f,.055f),steel);
        foreach(float x in new[]{-1.32f,1.32f})foreach(float y in new[]{-.9f,.9f})Box(board,"Frame_Fastener",new Vector3(x,y,-.122f),Vector3.one*.025f,ivory);
        var light=LightAt(board,"Map_Practical",new Vector3(0,.8f,-.65f),new Color(1,.87f,.67f),1.5f,4);
        Box(board,"Map_Lamp_Housing",new Vector3(0,1.07f,-.24f),new Vector3(.75f,.09f,.22f),steel);
        var prep=board.GetComponent<GarageDeparturePreparation>();
        if(prep.briefingShots.Length>=2)
        {
            for(int i=0;i<2;i++)
            {
                var shot=prep.briefingShots[i];shot.position=board.TransformPoint(new Vector3(i==0?0:.2f,0,-3.3f));
                shot.LookAt(board.position);
            }
        }
    }
    static void Supplies()
    {
        var table=GameObject.Find("Supplies_Table").transform;
        foreach(Transform t in table.Cast<Transform>().ToArray())
            if(t.GetComponent<TextMesh>()!=null || t.name=="Supply_Sign"||t.name=="Supply_Instructions"||t.name=="Sign_Support")t.gameObject.SetActive(false);
        var rail=Group("Supply_Label_Rail",table,new Vector3(0,.72f,-.598f));
        Box(rail,"Mounting_Rail",Vector3.zero,new Vector3(3.7f,.24f,.055f),black);
        string[] labels={"БЕНЗИН","РЕМКОМПЛЕКТ","АПТЕЧКА","ВОДА"};float[] xs={-1.35f,-.48f,.4f,1.35f};
        for(int i=0;i<4;i++)
        {
            Box(rail,"Enamel_Plate",new Vector3(xs[i],0,-.034f),new Vector3(.78f,.18f,.012f),ivory);
            Label(rail,labels[i],new Vector3(xs[i],.02f,-.043f),i==1?.67f:.5f,new Color(.08f,.12f,.12f));
            Box(rail,i<2?"Required_Amber_Marker":"Optional_Green_Marker",new Vector3(xs[i],-.055f,-.044f),new Vector3(.65f,.018f,.008f),i<2?amber:steel);
        }
        var tag=Group("Cargo_Instructions",table,new Vector3(0,.43f,-.6f));
        Box(tag,"Plate",Vector3.zero,new Vector3(1.9f,.18f,.025f),steel);
        Label(tag,"ЗАПАС В БАГАЖНИК",new Vector3(0,0,-.018f),1.65f,ivory.color);
        table.Find("Top").GetComponent<Renderer>().sharedMaterial=Mat("Workbench",new Color(.25f,.29f,.26f));
    }
    static void Generator()
    {
        GameObject.Find("Placeholder_DieselGenerator")?.SetActive(false);
        var gen=Group("Diesel_Generator",root,new Vector3(10.75f,.06f,-.1f));gen.rotation=Quaternion.Euler(0,90,0);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Generator/DieselGenerator.obj");
        if(prefab==null)throw new InvalidOperationException("Generator OBJ not imported.");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,gen);model.name="Imported_Diesel_Model";
        var bounds=new Bounds();bool first=true;
        foreach(var r in model.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        model.transform.localScale*=1.1f/Mathf.Max(.01f,bounds.size.y);
        foreach(var r in model.GetComponentsInChildren<Renderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=mats[i].name.Contains("paint")?Mat("Generator_Paint",new Color(.32f,.38f,.26f),.35f):mats[i].name.Contains("steel")?steel:black;
            r.sharedMaterials=mats;
        }
        Box(gen,"Concrete_Plinth",new Vector3(0,0,0),new Vector3(1.6f,.12f,1.12f),Mat("Concrete",new Color(.32f,.32f,.29f)),true);
        var solid=gen.gameObject.AddComponent<BoxCollider>();solid.center=new Vector3(0,.59f,0);solid.size=new Vector3(1.4f,1.12f,.96f);
        var panel=Group("Control_Panel",gen,new Vector3(0,.87f,-.47f));
        Box(panel,"Panel",Vector3.zero,new Vector3(.66f,.35f,.075f),black);
        Label(panel,"ДИЗЕЛЬ / 07",new Vector3(0,.11f,-.041f),.48f,ivory.color);
        for(int i=0;i<2;i++)
        {
            var dial=GameObject.CreatePrimitive(PrimitiveType.Cylinder);dial.name="Analog_Gauge";dial.transform.SetParent(panel,false);
            dial.transform.localPosition=new Vector3(-.19f+i*.2f,-.045f,-.06f);dial.transform.localRotation=Quaternion.Euler(90,0,0);dial.transform.localScale=new Vector3(.12f,.012f,.12f);
            dial.GetComponent<Renderer>().sharedMaterial=ivory;Object.DestroyImmediate(dial.GetComponent<Collider>());
            var needle=Box(panel,"Gauge_Needle",new Vector3(-.19f+i*.2f,-.045f,-.077f),new Vector3(.007f,.044f,.004f),black);needle.transform.localRotation=Quaternion.Euler(0,0,-25);
        }
        Box(panel,"Start_Button",new Vector3(.22f,-.045f,-.06f),new Vector3(.07f,.07f,.04f),amber);
        for(int i=0;i<6;i++)Box(gen,"Cooling_Fin",new Vector3(-.38f+i*.15f,.43f,-.49f),new Vector3(.018f,.27f,.026f),black);
        Pipe(gen,"Exhaust_Riser",new Vector3(.52f,1.4f,.21f),.075f,1.2f,steel);
        Pipe(gen,"Exhaust_Silencer",new Vector3(.52f,1.27f,.21f),.14f,.4f,black);
        Box(gen,"Exhaust_Cap",new Vector3(.52f,2.02f,.21f),new Vector3(.19f,.04f,.19f),steel);
        var sw=GameObject.Find("GeneratorSwitchBox");sw.transform.position=gen.TransformPoint(new Vector3(.20f,.87f,-.53f));
        sw.transform.localScale=new Vector3(.13f,.18f,.13f);sw.GetComponent<Renderer>().enabled=false;
        foreach(var r in sw.GetComponentsInChildren<Renderer>())r.enabled=false;
        sw.GetComponent<BoxCollider>().size=new Vector3(4,3,3);
        var cable=Group("Generator_Cables",root,Vector3.zero);
        var wireMat=Mat("Cable",new Color(.045f,.05f,.043f));
        for(int i=0;i<2;i++)
        {
            var line=Group("Power_Conduit",cable,Vector3.zero).gameObject.AddComponent<LineRenderer>();line.sharedMaterial=wireMat;line.startWidth=line.endWidth=.035f;line.numCornerVertices=4;
            line.positionCount=5;line.SetPositions(new[]{new Vector3(10.8f,.16f,-.4f-i*.1f),new Vector3(11.6f,.16f,-.4f-i*.1f),new Vector3(11.72f,.3f,-.4f-i*.1f),new Vector3(11.72f,3.7f,-.4f-i*.1f),new Vector3(11.72f,3.7f,5)});
        }
        var sign=Group("Power_Wall_Plate",root,new Vector3(11.72f,2.8f,-.2f));sign.rotation=Quaternion.Euler(0,90,0);
        Box(sign,"Plate",Vector3.zero,new Vector3(1.05f,.3f,.035f),black);Label(sign,"ПИТАНИЕ / 07",new Vector3(0,0,-.03f),.88f,ivory.color);
        LightAt(root,"Generator_Worklight",new Vector3(9.8f,2.45f,-.8f),new Color(1,.78f,.47f),1.4f,4.5f);
    }
    static void Pipe(Transform parent,string name,Vector3 pos,float diameter,float height,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=new Vector3(diameter,height/2,diameter);
        go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Floor()
    {
        var wear=Group("Floor_Patina",root,Vector3.zero);var oil=Mat("Oil_Stain",new Color(.058f,.062f,.058f));
        for(int j=0;j<7;j++)
        {
            var go=Group("Oil_Patch",wear,new Vector3(-.7f+(j%3)*.67f,.016f,-.7f+(j/3)*1.13f)).gameObject;
            var vertices=new Vector3[18];var triangles=new int[48];vertices[0]=Vector3.zero;
            for(int i=0;i<17;i++){float a=i*Mathf.PI*2/16,r=.24f+.06f*Mathf.Sin(i*3.7f+j);vertices[i+1]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.65f);}
            for(int i=0;i<16;i++){triangles[i*3]=0;triangles[i*3+1]=i+2;triangles[i*3+2]=i+1;}
            var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh,Dir+"OilPatch"+j+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=oil;
        }
        var tire=Mat("Tire_Wear",new Color(.12f,.13f,.125f));
        for(int i=0;i<22;i++)foreach(float x in new[]{-.78f,.78f})
        {
            var tread=Box(wear,"Tire_Tread",new Vector3(x,.013f,2.4f+i*.34f),new Vector3(.18f,.003f,.19f),tire);
            tread.transform.localRotation=Quaternion.Euler(0,12,0);
        }
        var paint=Mat("Worn_Bay_Marking",new Color(.43f,.34f,.18f));
        foreach(float x in new[]{-2.35f,2.35f})for(int i=0;i<5;i++)Box(wear,"Bay_Paint",new Vector3(x,.016f,-1.5f+i),new Vector3(.045f,.003f,.72f),paint);
    }
    static Light LightAt(Transform parent,string name,Vector3 p,Color color,float intensity,float range)
    {var l=Group(name,parent,p).gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;}
    static AudioClip Clip(string file)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds+file+".wav");
    static AudioSource Source(string name,Vector3 position,AudioClip clip,float volume,bool loop)
    {
        var s=Group(name,root,position).gameObject.AddComponent<AudioSource>();s.clip=clip;s.volume=volume;s.loop=loop;s.playOnAwake=false;s.spatialBlend=1;s.minDistance=3;s.maxDistance=30;s.rolloffMode=AudioRolloffMode.Logarithmic;
        // Follow the same mixer routing as the existing scene's ambience when available.
        var existing=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).FirstOrDefault(a=>a!=s&&a.outputAudioMixerGroup!=null);
        if(existing!=null)s.outputAudioMixerGroup=existing.outputAudioMixerGroup;
        return s;
    }
    static void SetupDirector()
    {
        var d=root.gameObject.AddComponent<GaragePresentationDirector>();
        d.generatorVisual=root.Find("Diesel_Generator/Imported_Diesel_Model");
        d.generatorLoop=Source("Diesel_Idle",new Vector3(10.7f,1,0),Clip("crs-oa_idling-mixture_engine_04"),.23f,true);d.generatorLoop.pitch=.72f;
        d.generatorEffects=Source("Diesel_Starter",new Vector3(10.7f,1,0),null,.7f,false);
        d.exteriorWind=Source("Outside_Storm",new Vector3(0,3,14),Clip("ca1_ambience_city_rain_distantthunder"),.1f,true);
        var low=d.exteriorWind.gameObject.AddComponent<AudioLowPassFilter>();low.cutoffFrequency=900;
        d.gateEffects=Source("Gate_Mechanism",new Vector3(0,3,13.5f),null,.8f,false);
        d.workshopEffects=Source("Workshop_Foley",Vector3.up,null,.8f,false);
        d.starter=Clip("jw4_engines-motors-024");d.metalRattle=Clip("ucns_metal_creak_08");d.gateMotor=Clip("ji-e2_metal_jolt_rattle_scrape-054");
        d.installation=Clip("blacksmith_metal_pickup_putdown_07");d.pour=AssetDatabase.LoadAssetAtPath<AudioClip>(Dir+"FuelPour.wav");d.ignition=Clip("crs-oa_driving_short_engine_01_04");
        d.gateDaylight=LightAt(root,"Gate_Outside_Spill",new Vector3(0,4.5f,13),new Color(1,.81f,.55f),0,17);d.gateDaylight.enabled=false;
        var car=GameObject.Find("Classic Car_9");
        d.dashboardGlow=LightAt(car.transform,"Instrument_Glow",new Vector3(0,.9f,.45f),new Color(.48f,.77f,.59f),.35f,1.6f);d.dashboardGlow.enabled=false;
        var fan=Group("Ventilation_Fan",root,new Vector3(-11.7f,3.6f,-4));fan.rotation=Quaternion.Euler(0,-90,0);d.ventilationFan=fan;
        for(int i=0;i<4;i++){var blade=Box(fan,"Fan_Blade",Vector3.zero,new Vector3(.55f,.065f,.025f),steel);blade.transform.localRotation=Quaternion.Euler(0,0,i*45);}
        d.gateDust=Dust("Gate_Dust",new Vector3(0,3,13.4f),new Vector3(6,3,.15f),false);
        Dust("Workshop_Motes",new Vector3(0,2.6f,0),new Vector3(12,3,14),true);
        EditorUtility.SetDirty(d);
    }
    static ParticleSystem Dust(string name,Vector3 pos,Vector3 size,bool loop)
    {
        var ps=Group(name,root,pos).gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=ps.main;m.loop=loop;m.playOnAwake=loop;m.duration=loop?10:1.5f;m.startLifetime=loop?9:3;m.startSpeed=.09f;m.startSize=loop?.017f:.055f;m.maxParticles=120;m.startColor=new Color(.7f,.64f,.52f,.22f);m.simulationSpace=ParticleSystemSimulationSpace.World;
        var em=ps.emission;em.rateOverTime=loop?8:35;var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Box;sh.scale=size;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        var col=ps.colorOverLifetime;col.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.6f,.2f),new GradientAlphaKey(0,1)});col.color=gradient;
        return ps;
    }
}
