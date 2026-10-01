using System;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class JourneyStationsAuthoring
    {
        const string Folder="Assets/Content/JourneyStations";
        static readonly string[] Scenes={"Stage1_Outskirts","Stage2_Wasteland","Stage3_Industrial","Stage4_Citadel"};
        static readonly string[] Names={"СЕВЕРНАЯ","КАНЬОН","РУБЕЖ"};
        static Material concrete,steel,roof,paint,accent;
        [MenuItem("RogueDrive/Route/Build Concept Service Stations")]
        public static void Build()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop play and save current edits first.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/JourneyItems");Directory.CreateDirectory("Temp/JourneyStations");AssetDatabase.Refresh();
            concrete=Mat("Concrete",new Color(.29f,.32f,.32f));steel=Mat("Steel",new Color(.14f,.20f,.23f));paint=Mat("Paint",new Color(.94f,.82f,.38f));
            for(int i=0;i<4;i++)
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+Scenes[i]+".unity");
                string backup="Temp/JourneyStations/"+Scenes[i]+".unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
                var world=scene.GetRootGameObjects().Single(g=>g.name==(i==0?"Stage_World":"Authored_Journey"));
                var route=world.GetComponentInChildren<StageRoute>(true);
                if(i<3&&world.transform.Find("Concept_STO_"+(i+1))==null)
                {
                    foreach(var old in world.GetComponentsInChildren<WorkshopServiceZone>(true))if(old.Safe){old.enabled=false;}
                    foreach(var stop in world.GetComponentsInChildren<JourneyStop>(true))if(stop.Title!=null&&stop.Title.Contains("СЕВЕРНАЯ"))stop.enabled=false;
                    roof=Mat("Roof_"+i,i==0?new Color(.21f,.33f,.30f):i==1?new Color(.52f,.30f,.17f):new Color(.23f,.29f,.42f));
                    accent=Mat("Accent_"+i,i==0?new Color(.34f,.61f,.50f):i==1?new Color(.85f,.52f,.18f):new Color(.39f,.57f,.79f));
                    Station(world.transform,route,i);
                }
                if(i>0)Roadside(world.transform,route,i);
                if(i==0)
                {
                    foreach(var old in world.GetComponentsInChildren<JourneyStop>(true))
                    {
                        if(old.Title==null||!old.Title.Contains("СЕВЕРНАЯ"))continue;
                        var data=new SerializedObject(old);
                        old.Configure("ЗАКРЫТЫЙ СЕРВИС","Действующая СТО дальше по шоссе","СТО №1 «Северная» находится дальше по дороге, на съезде перед пустошью.",data.FindProperty("roadDistance").floatValue,data.FindProperty("side").floatValue,false);
                        old.enabled=true;
                        foreach(var sign in old.GetComponentsInChildren<TextMesh>(true))if(sign.text.Contains("СТО"))sign.text="СЕРВИС ЗАКРЫТ\nСТО №1 — ДАЛЬШЕ ПО ШОССЕ";
                    }
                }
                if(i<3)
                {
                    route.Evaluate(route.Length,out var end,out var heading);
                    var authoredStation=world.transform.Find("Concept_STO_"+(i+1));
                    authoredStation.SetPositionAndRotation(end+heading*new Vector3(50,0,400),heading);
                    EnhanceStation(authoredStation,i);
                }
                BakeTemplates(scene.GetRootGameObjects(),Scenes[i]);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var garage=EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity");
            BakeTemplates(garage.GetRootGameObjects(),"Garage");EditorSceneManager.MarkSceneDirty(garage);EditorSceneManager.SaveScene(garage);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            SeamlessJourneyAuthoring.Build();
            Debug.Log("[JourneyStations] Three safe stations, four roadside services and checkpoint templates baked.");
        }
        static void Station(Transform world,StageRoute route,int index)
        {
            route.Evaluate(route.Length,out var p,out var q);
            var root=new GameObject("Concept_STO_"+(index+1)).transform;root.SetParent(world,false);root.SetPositionAndRotation(p+q*new Vector3(50,0,400),q);
            // The full apron sits on the existing straight connector, outside the traffic lane.
            Box(root,"Apron",new Vector3(0,-.16f,0),new Vector3(42,.4f,88),concrete);
            Ramp(root,"Entry_Ramp",new Vector3(-50,.055f,-110),new Vector3(0,.055f,-42),16);
            Ramp(root,"Exit_Ramp",new Vector3(0,.055f,42),new Vector3(-50,.055f,110),16);
            Box(root,"Hangar_Left",new Vector3(-15,4,0),new Vector3(.7f,8,66),roof);
            Box(root,"Hangar_Right",new Vector3(15,4,0),new Vector3(.7f,8,66),roof);
            Box(root,"Filtered_Roof",new Vector3(0,8.2f,0),new Vector3(31,.7f,68),roof);
            for(int z=-30;z<=30;z+=10)
            {
                Box(root,"Roof_Beam",new Vector3(0,7.7f,z),new Vector3(30,.5f,.5f),steel);
                foreach(int side in new[]{-1,1})Box(root,"Support",new Vector3(side*14.5f,4,z),new Vector3(.6f,8,.6f),accent);
            }
            Box(root,"Entrance_Lintel",new Vector3(0,7,-32),new Vector3(30,2,.8f),accent);
            var gate=Box(root,"Departure_Shutter",new Vector3(0,3,31),new Vector3(26,6,.4f),steel).transform;
            Box(root,"Filter_Housing",new Vector3(19,3,14),new Vector3(5,6,7),steel);
            for(int z=11;z<=17;z+=2)Box(root,"Filter_Louvre",new Vector3(21.6f,3,z),new Vector3(.1f,4,.5f),paint);
            Box(root,"Vent_Duct",new Vector3(16,7,14),new Vector3(8,1.4f,1.4f),steel);
            for(int z=-24;z<24;z+=6)foreach(int side in new[]{-1,1})Box(root,"Bay_Line",new Vector3(side*4,.06f,z),new Vector3(.15f,.015f,3.5f),paint,false);
            var console=Box(root,"Service_Console",new Vector3(-11,1.3f,-20),new Vector3(1.4f,2.6f,1),accent);
            var zone=root.gameObject.AddComponent<WorkshopServiceZone>();zone.Configure("СТО №"+(index+1)+" «"+Names[index]+"»",true,WorkshopEquipment.All,index+1,true,40);zone.ConfigureShelter(new Vector3(28,14,64));
            var station=root.gameObject.AddComponent<JourneyServiceStation>();
            var text=Label(root,"Station_Status",new Vector3(0,7,-32.5f),"СТО №"+(index+1)+"  "+Names[index]+"\nУКРЫТИЕ • РЕМОНТ • ТОРГОВЛЯ",.17f);
            station.Configure(index+1,gate,text);
            // Interaction is attached to the console; no invisible blocker across the driving bay.
            console.AddComponent<JourneyStationConsole>().Configure(station);
            foreach(int side in new[]{-1,1})
            {
                var lamp=new GameObject("Service_Light");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(side*9,6.8f,-10);var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=25;light.intensity=2;light.color=new Color(1,.84f,.58f);
            }
            Prop("WorkbenchFull",root,new Vector3(-11,0,0),0,4.5f);
            Prop("CarWheel",root,new Vector3(11,.1f,10),0,1.3f);
            Prop("WoodenPallet",root,new Vector3(11,0,-9),0,3);
            Box(root,"Engine_Crane_Base",new Vector3(10,.3f,22),new Vector3(4,.5f,4),accent);
            Box(root,"Engine_Crane_Mast",new Vector3(11,3.3f,22),new Vector3(.5f,6,.5f),accent);
            Box(root,"Engine_Crane_Arm",new Vector3(8.5f,6,22),new Vector3(5,.5f,.5f),accent);
            for(int side=-1;side<=1;side+=2)Box(root,"Lift_Post",new Vector3(side*5,2.5f,7),new Vector3(.6f,5,.7f),accent);
            Label(root,"Instructions",new Vector3(-10.9f,3,-20.6f),"[E] СЕРВИС\nПОДГОТОВИТЬ ВЫЕЗД",.085f);
            var sign=new GameObject("STO_Advance_Sign").transform;sign.SetParent(root,false);sign.localPosition=new Vector3(-39,0,-170);
            Box(sign,"Post",new Vector3(0,2,0),new Vector3(.3f,4,.3f),steel);
            Box(sign,"Board",new Vector3(0,4,0),new Vector3(10,2.2f,.2f),accent);
            Label(sign,"Text",new Vector3(0,4,-.12f),"СТО №"+(index+1)+"  →\nФИЛЬТРУЕМЫЙ АНГАР",.12f);
        }
        static void EnhanceStation(Transform root,int index)
        {
            roof=Mat("Roof_"+index,index==0?new Color(.21f,.33f,.30f):index==1?new Color(.52f,.30f,.17f):new Color(.23f,.29f,.42f));
            accent=Mat("Accent_"+index,index==0?new Color(.34f,.61f,.50f):index==1?new Color(.85f,.52f,.18f):new Color(.39f,.57f,.79f));
            var station=root.GetComponent<JourneyServiceStation>();
            if(root.Find("Arrival_Shutter")!=null)
            {
                PolishInterior(root);
                return;
            }
            var shutter=Box(root,"Arrival_Shutter",new Vector3(0,3,-31),new Vector3(26,6,.45f),steel).transform;
            Box(root,"Entrance_Left_Jamb",new Vector3(-14,3,-32),new Vector3(2,6,1.2f),accent);
            Box(root,"Entrance_Right_Jamb",new Vector3(14,3,-32),new Vector3(2,6,1.2f),accent);
            Box(root,"Entrance_Header",new Vector3(0,6.4f,-32),new Vector3(30,1.2f,1.5f),steel);
            Label(root,"Arrival_Banner",new Vector3(0,6.5f,-32.9f),"СТО №"+(index+1)+"  «"+Names[index]+"»\nВЪЕЗД В РЕМОНТНЫЙ АНГАР",.11f);
            for(int x=-1;x<=1;x+=2)
            {
                Box(root,"Light_Rail",new Vector3(x*8,7.5f,0),new Vector3(.25f,.12f,56),paint,false);
                for(int z=-22;z<=24;z+=8)Box(root,"Wall_Panel",new Vector3(x*14.85f,2.5f,z),new Vector3(.12f,4,5),steel,false);
                Box(root,"Floor_Lift_Rail",new Vector3(x*1.6f,.06f,3),new Vector3(.42f,.08f,8),accent,false);
                Box(root,"Lift_Arm",new Vector3(x*2.4f,.45f,3),new Vector3(1.4f,.2f,.32f),steel,false);
            }
            var lift=Box(root,"Lift_Platform",new Vector3(0,.06f,3),new Vector3(3.1f,.08f,8),steel,false).transform;
            Box(root,"Workbench_Backdrop",new Vector3(-14.3f,3.5f,0),new Vector3(.2f,3,9),accent,false);
            var bench=Box(root,"Workshop_Terminal",new Vector3(-11,1.35f,0),new Vector3(2.3f,2.7f,1.2f),steel);
            bench.AddComponent<JourneyStationPoint>().Configure(station,JourneyStationPoint.Kind.Workbench);
            Label(root,"Workshop_Label",new Vector3(-10.9f,3.2f,-.7f),"ВЕРСТАК\n[E] ОСМОТР МАШИНЫ",.10f).transform.localRotation=Quaternion.Euler(0,180,0);
            Box(root,"Tool_Shelf",new Vector3(-13,1.8f,12),new Vector3(2.4f,3.5f,5),roof);
            for(int n=0;n<4;n++)Box(root,"Tool_Tray",new Vector3(-11.7f,.6f+n*.75f,12),new Vector3(.4f,.1f,4.5f),paint,false);
            Box(root,"Receiving_Counter",new Vector3(11,1,-16),new Vector3(4,1.7f,4),roof);
            var trader=Box(root,"Receiving_Terminal",new Vector3(11,2.15f,-16),new Vector3(2.2f,.8f,.8f),accent);
            trader.AddComponent<JourneyStationPoint>().Configure(station,JourneyStationPoint.Kind.Trader);
            Label(root,"Receiving_Label",new Vector3(10.8f,3.1f,-17),"ПРИЁМКА ДОБЫЧИ\n[E] ПРОДАТЬ",.10f).transform.localRotation=Quaternion.Euler(0,180,0);
            for(int n=0;n<4;n++)Box(root,"Cargo_Crate",new Vector3(10.5f+n%2*2,.55f+n/2*1.1f,12+n%2*3),new Vector3(1.8f,1,2.2f),concrete);
            var arrival=root.gameObject.GetComponent<JourneyStationArrival>()??root.gameObject.AddComponent<JourneyStationArrival>();
            arrival.Configure(station,shutter,lift);
            var instructions=root.Find("Instructions")?.GetComponent<TextMesh>();if(instructions!=null)instructions.text="[E] ПУЛЬТ ВЫЕЗДА\nВЕРСТАК — У МАШИНЫ\nПРИЁМКА — СПРАВА";
            PolishInterior(root);
        }
        static void PolishInterior(Transform root)
        {
            var bench=root.Find("Workshop_Terminal");
            if(bench!=null)
            {
                bench.localPosition=new Vector3(-11,1.05f,0);
                bench.localScale=new Vector3(3,.25f,1.6f);
                bench.GetComponent<Renderer>().sharedMaterial=accent;
            }
            var backing=root.Find("Workbench_Backdrop");
            if(backing!=null){backing.localPosition=new Vector3(-14.35f,2.15f,0);backing.localScale=new Vector3(.16f,2.6f,5);}
            if(root.Find("Bench_Legs")==null)
            {
                var legs=new GameObject("Bench_Legs").transform;legs.SetParent(root,false);
                foreach(int x in new[]{-12,-10})foreach(int z in new[]{-1,1})
                    Box(legs,"Leg",new Vector3(x,.5f,z*.62f),new Vector3(.18f,1,.18f),steel);
                Box(legs,"Parts_Tray",new Vector3(-10.1f,1.25f,.35f),new Vector3(.85f,.1f,.6f),paint,false);
                Box(legs,"Tool_Rack",new Vector3(-14.15f,2.4f,0),new Vector3(.25f,.15f,4.2f),paint,false);
                for(int i=0;i<4;i++)Box(legs,"Hanging_Tool",new Vector3(-13.98f,2.05f,-1.4f+i*.9f),new Vector3(.16f,.75f,.12f),steel,false);
            }
            var work=root.Find("Workshop_Label")?.GetComponent<TextMesh>();
            if(work!=null)
            {work.text="ВЕРСТАК\n[E] МАШИНА";work.characterSize=.045f;work.transform.localPosition=new Vector3(-14.1f,3.05f,0);work.transform.localRotation=Quaternion.Euler(0,-90,0);}
            var trade=root.Find("Receiving_Label")?.GetComponent<TextMesh>();
            if(trade!=null)
            {trade.text="ПРИЁМКА\n[E] ПРОДАТЬ";trade.characterSize=.045f;trade.transform.localPosition=new Vector3(10.8f,3.1f,-16);trade.transform.localRotation=Quaternion.Euler(0,90,0);}
            var instructions=root.Find("Instructions")?.GetComponent<TextMesh>();
            if(instructions!=null)
            {instructions.text="ПУЛЬТ ВЫЕЗДА\n[E] СЕРВИС";instructions.characterSize=.045f;instructions.transform.localRotation=Quaternion.Euler(0,180,0);}
        }
        static void Roadside(Transform world,StageRoute route,int index)
        {
            if(world.Find("Concept_Roadside_Service")!=null)return;
            float d=index==1?4000:index==2?4700:4600;route.Evaluate(d,out var p,out var q);
            var root=new GameObject("Concept_Roadside_Service").transform;root.SetParent(world,false);root.SetPositionAndRotation(p+q*new Vector3(-20,0,0),q);
            root.gameObject.AddComponent<WorkshopServiceZone>().Configure("Придорожный ремонтный бокс",false,WorkshopEquipment.Tools|WorkshopEquipment.Lift|WorkshopEquipment.Bench,Mathf.Min(2,index),true,24);
            Prop("WorkbenchFull",root,new Vector3(-7,0,9),0,3);
            Label(root,"Service_Label",new Vector3(-4,3,-3),"РЕМОНТНЫЙ БОКС\nБУРЯ НЕ ОСТАНАВЛИВАЕТСЯ",.09f);
        }
        static void BakeTemplates(GameObject[] roots,string scene)
        {
            foreach(var prop in roots.SelectMany(g=>g.GetComponentsInChildren<PhysicsProp>(true)).ToArray())
            {
                if(prop.GetComponent<JourneyItemTemplate>()!=null)continue;
                string hierarchy=scene;for(var p=prop.transform;p!=null;p=p.parent)hierarchy+="/"+p.name+"#"+p.GetSiblingIndex();
                string key="JourneyItems/"+Hash128.Compute(hierarchy);
                prop.gameObject.AddComponent<JourneyItemTemplate>().Configure(key);
                var copy=Object.Instantiate(prop.gameObject);copy.transform.SetParent(null);copy.name=prop.name;
                PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Resources/"+key+".prefab");Object.DestroyImmediate(copy);
            }
        }
        static Material Mat(string name,Color color)
        {string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",.12f);return mat;}
        static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,bool collide=true)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static TextMesh Label(Transform p,string name,Vector3 position,string text,float size)
        {var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=position;var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.fontSize=64;t.characterSize=size;t.color=new Color(.97f,.93f,.79f);return t;}
        static void Ramp(Transform p,string name,Vector3 a,Vector3 b,float width)
        {var g=Box(p,name,(a+b)*.5f-new Vector3(0,.1f,0),new Vector3(width,.2f,Vector3.Distance(a,b)+1),concrete);g.transform.localRotation=Quaternion.LookRotation(b-a);}
        static void Prop(string name,Transform parent,Vector3 position,float yaw,float size)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/GarageAssetPack/Prefabs/"+name+".prefab");if(prefab==null)return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);go.transform.localScale*=size/Mathf.Max(b.size.x,b.size.y,b.size.z);
        }
    }
}
