using System;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    public static class JourneyContentAuthoring
    {
        const string Folder="Assets/Content/JourneyEncounters";
        const string City="Assets/Downloads/SimplePoly City - Low Poly Assets/Prefab/";
        const string Garage="Assets/Downloads/GarageAssetPack/Prefabs/";
        const string Forest="Assets/Downloads/Low Poly Forest - Free Starter Pack/Prefabs/";
        static StageRoute route;
        static MeshCollider[] ground;
        static Material sign,ink;
        static int lootCount;
        static readonly float[] Stops={1400,3200,5400,7600,9600,11600,13040};
        static readonly string[] Titles={"ПОСЛЕДНЯЯ АЗС","ЛЕСНОЙ ЛАГЕРЬ","РЕМОНТНЫЙ БОКС","ЛЕСОПИЛКА","ГРУЗОВОЙ ДВОР","СМОТРОВАЯ","СТО СЕВЕРНАЯ"};
        static readonly string[] Supplies={"Бензин · масло","Вода · аптечка · еда","Запасное колесо · инструмент · ремонт","Инструменты · запчасти","Колёса · детали на продажу","Вода · аптечка","Укрытие · ремонт · торговля"};
        [MenuItem("RogueDrive/Route/Author Journey Stops And Sound")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="Stage1_Outskirts")throw new InvalidOperationException("Open Stage1_Outskirts.");
            route=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageRoute>(true)).Single();
            if(route.transform.Find("Journey_Encounters")!=null)throw new InvalidOperationException("Content already authored.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Content","JourneyEncounters");
            ground=route.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Terrain_")||c.name.StartsWith("BrokenVector_Road_")).ToArray();
            sign=Material("RoadsideSign",new Color(.12f,.22f,.23f));ink=Material("SignPost",new Color(.24f,.27f,.26f));
            var root=new GameObject("Journey_Encounters").transform;root.SetParent(route.transform,false);
            lootCount=0;
            for(int i=0;i<Stops.Length;i++)Stop(root,i);
            Landmarks(root);
            var sound=root.gameObject.AddComponent<JourneySoundscape>();
            sound.Configure(Clip("ca12_forest_birds_small-stream"),Clip("ca8_ambience_farm_calm_birds_02"),Clip("ca1_ambience_city_rain_distantthunder"),DustMaterial());
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[JourneyContent] Seven stops, "+lootCount+" physical supplies, four landmark groups and contextual sound authored.");
        }
        static Vector3 At(float d,float x)
        {
            route.Evaluate(d,out var p,out var q);p+=q*Vector3.right*x;
            var ray=new Ray(p+Vector3.up*180,Vector3.down);float highest=float.NegativeInfinity;
            foreach(var c in ground)if(c.Raycast(ray,out var hit,350))highest=Mathf.Max(highest,hit.point.y);
            if(!float.IsNegativeInfinity(highest))p.y=highest;
            return p;
        }
        static float Yaw(float d){route.Evaluate(d,out _,out var q);return q.eulerAngles.y;}
        static GameObject Place(string path,Transform parent,float d,float x,float size,float yawOffset=0,int collision=1)
        {
            int before=parent.childCount;
            JourneyLandscapeAuthoring.Place(path,parent,At(d,x),Yaw(d)+yawOffset,size,collision);
            return parent.GetChild(before).gameObject;
        }
        static void Stop(Transform root,int i)
        {
            float d=Stops[i],side=i==6?1:i%2==0?1:-1,x=side*91;
            var site=new GameObject("Stop_"+(i+1)+"_"+Titles[i]).transform;site.SetParent(root,false);
            // Replace the two identical prototype cans at every stop with the authored supply sets.
            var landscape=route.transform.Find("Journey_Landscape");
            var old=landscape.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("Detour_"+(i+1)+"_"));
            if(old!=null)foreach(var item in old.GetComponentsInChildren<BunkerAssemblyItem>(true))item.gameObject.SetActive(false);
            var notice=Board(site,d-17,x,Titles[i],Supplies[i],3.4f);
            var stop=notice.AddComponent<JourneyStop>();
            string instructions=i==6?"Безопасная площадка. Осмотрите капот для ремонта и торговли. После подготовки возвращайтесь на основную дорогу.":i==2?"Инструменты, подъёмник и верстак доступны через осмотр капота. Услуги платные; буря продолжает двигаться. Двигатель меняют на безопасной СТО.":Supplies[i]+". Заберите нужное в руки или инвентарь и погрузите в багажник. Выезд возвращает на основную дорогу.";
            stop.Configure(Titles[i],Supplies[i],instructions,d,side,i==6);
            Board(site,d-310,side*12.7f,Titles[i],(side>0?"300 м  →":"←  300 м"),4.2f);
            Board(site,d-210,side*12.7f,i==6?"СТО":"СЪЕЗД",(side>0?"→":"←"),2.5f);
            if(i==2)
            {
                var zone=new GameObject("Roadside_Service_Zone");zone.transform.SetParent(site,false);zone.transform.position=At(d,side*86);
                zone.AddComponent<WorkshopServiceZone>().Configure("Пригородный ремонтный бокс",false,WorkshopEquipment.Tools|WorkshopEquipment.Lift|WorkshopEquipment.Bench,1,true);
                var so=new SerializedObject(zone.GetComponent<WorkshopServiceZone>());so.FindProperty("radius").floatValue=27;so.ApplyModifiedPropertiesWithoutUndo();
                Place(Garage+"WorkbenchFull.prefab",site,d+1,side*100,3,-side*90);
            }
            if(i==6)return;
            Place(Garage+"WoodenPallet.prefab",site,d-9,x,2.4f);
            Place(City+"Props/Props_Bench_1.prefab",site,d+6,side*106,2.4f,-side*90);
            if(i==0){Fluid(site,d-8,x,"FuelCanister",BunkerFluidType.Gasoline,10);Fluid(site,d-6,x,"FuelCanister",BunkerFluidType.Gasoline,6);Fluid(site,d-4,x,"OilCanister",BunkerFluidType.EngineOil,2);Tool(site,d-2,x);}
            if(i==1){Fluid(site,d-8,x,"WaterCanister",BunkerFluidType.Water,8);Medkit(site,d-6,x);Food(site,d-4,x);}
            if(i==2){Part(site,d-8,x,"wheel_road",.82f);Tool(site,d-6,x);Fluid(site,d-4,x,"OilCanister",BunkerFluidType.EngineOil,2);Medkit(site,d-2,x);}
            if(i==3){Part(site,d-8,x,"wheel_offroad",.62f);Tool(site,d-6,x);Fluid(site,d-4,x,"FuelCanister",BunkerFluidType.Gasoline,5);Food(site,d-2,x);}
            if(i==4){Part(site,d-8,x,"wheel_cargo",.70f);Part(site,d-6,x,"wheel_road",.45f);Fluid(site,d-4,x,"OilCanister",BunkerFluidType.EngineOil,2);Fluid(site,d-2,x,"FuelCanister",BunkerFluidType.Gasoline,7);}
            if(i==5){Fluid(site,d-8,x,"WaterCanister",BunkerFluidType.Water,6);Medkit(site,d-6,x);Food(site,d-4,x);}
        }
        static GameObject Board(Transform parent,float d,float x,string title,string subtitle,float width)
        {
            var go=new GameObject("Notice_"+title);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(At(d,x),Quaternion.Euler(0,Yaw(d),0));
            Cube(go.transform,"Post",new Vector3(0,1.1f,0),new Vector3(.12f,2.2f,.12f),ink);
            Cube(go.transform,"Panel",new Vector3(0,2.3f,0),new Vector3(width,1.25f,.12f),sign);
            var text=new GameObject("Lettering").AddComponent<TextMesh>();text.transform.SetParent(go.transform,false);text.transform.localPosition=new Vector3(0,2.3f,-.075f);
            text.text=title+"\n"+subtitle;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.89f,.87f,.73f);
            text.GetComponent<Renderer>().sharedMaterial=text.font.material;
            var heading=go.transform.rotation;go.transform.rotation=Quaternion.identity;
            var bounds=text.GetComponent<Renderer>().bounds;float scale=Mathf.Min((width-.35f)/Mathf.Max(.1f,bounds.size.x),.95f/Mathf.Max(.1f,bounds.size.y));text.transform.localScale=Vector3.one*scale;go.transform.rotation=heading;
            return go;
        }
        static void Cube(Transform root,string name,Vector3 p,Vector3 size,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;}
        static void Fluid(Transform parent,float d,float x,string prefab,BunkerFluidType type,float liters)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VehicleService/"+prefab+".prefab"),parent);
            go.transform.position=At(d,x)+Vector3.up*.5f;go.transform.rotation=Quaternion.Euler(0,Yaw(d),0);
            go.GetComponent<FluidContainer>().Configure(type,type==BunkerFluidType.EngineOil?5:10,liters);lootCount++;
        }
        static GameObject Portable(string path,Transform parent,float d,float x,float size,string title)
        {
            var go=Place(path,parent,d,x,size,0,0);go.transform.position+=Vector3.up*.3f;
            var bounds=go.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            var collider=go.AddComponent<BoxCollider>();collider.center=go.transform.InverseTransformPoint(bounds.center);collider.size=new Vector3(bounds.size.x/go.transform.lossyScale.x,bounds.size.y/go.transform.lossyScale.y,bounds.size.z/go.transform.lossyScale.z);
            go.AddComponent<Rigidbody>().mass=2;go.AddComponent<PhysicsProp>().Configure(title);lootCount++;return go;
        }
        static void Tool(Transform p,float d,float x)
        {var go=Portable(Garage+"WrenchLarge.prefab",p,d,x,.45f,"Гаечный ключ");go.GetComponent<PhysicsProp>().SetPocketSized(true);go.AddComponent<GarageItemFunction>().Configure(GarageItemFunction.ItemKind.Wrench);go.AddComponent<GarageItemUse>();}
        static void Medkit(Transform p,float d,float x)
        {var go=Portable("Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Props/First_Aid.prefab",p,d,x,.48f,"Аптечка");go.GetComponent<PhysicsProp>().SetPocketSized(true);go.AddComponent<GarageItemFunction>().Configure(GarageItemFunction.ItemKind.Medkit);go.AddComponent<GarageItemUse>();}
        static void Food(Transform p,float d,float x)
        {var go=Portable("Assets/Downloads/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Food_3D_Icon_01.prefab",p,d,x,.32f,"Походная еда");go.GetComponent<PhysicsProp>().SetPocketSized(true);go.AddComponent<GarageItemFunction>().Configure(GarageItemFunction.ItemKind.Food);go.AddComponent<GarageItemUse>();}
        static void Part(Transform p,float d,float x,string id,float condition)
        {var def=WorkshopCatalog.Get(id);var go=Portable(Garage+"CarWheel.prefab",p,d,x,.7f,def.title);var data=WorkshopCatalog.New(id);data.condition=condition;go.AddComponent<WorkshopPartItem>().data=data;}
        static void Landmarks(Transform parent)
        {
            var post=new GameObject("Abandoned_Checkpoint").transform;post.SetParent(parent,false);
            foreach(int side in new[]{-1,1})
            {
                Place("Assets/Downloads/FastMesh/Prefabs/Road/RoadBarrier-1.prefab",post,1080,side*14,4,side*15);
                Place(City+"Props/Props_Traffic Sign_stop.prefab",post,1055,side*12,2.6f);
            }
            Place("Assets/Downloads/FREE Low Poly Shipping Container/Prefabs/Low Poly Shipping Container.prefab",post,1095,23,8,90);
            var fallen=new GameObject("Woodland_Fallen_Tree").transform;fallen.SetParent(parent,false);
            Place(Forest+"Props/fallen_log_small_2.prefab",fallen,2530,15,8,35);
            Place(Forest+"Props/tree_stump_medium.prefab",fallen,2527,19,1.4f);
            var lumber=new GameObject("Forestry_Log_Yard").transform;lumber.SetParent(parent,false);
            for(int i=0;i<8;i++)Place(Forest+"Props/fallen_log_small_2.prefab",lumber,7590+i*3,-115,9,90);
            Place(Garage+"WorkbenchFull.prefab",lumber,7605,-105,3.2f,90);
            Place(City+"Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",lumber,7620,-109,5.4f);
            var overlook=new GameObject("Hilltop_Overlook").transform;overlook.SetParent(parent,false);
            for(int i=0;i<3;i++)Place(City+"Props/Props_Bench_1.prefab",overlook,11585+i*7,-111,2.7f,-90);
        }
        static Material Material(string name,Color color)
        {var mat=new Material(Shader.Find("Standard")){name=name,color=color,enableInstancing=true};mat.SetFloat("_Glossiness",.08f);AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");return mat;}
        static AudioClip Clip(string name)
        {
            string path="Assets/Libraries/Soundbits_freeSFX_2025/Sounds/"+name+".wav";
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.6f;importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static Material DustMaterial()
        {
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.name="SoftDust";
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),2)));}texture.Apply();
            AssetDatabase.CreateAsset(texture,Folder+"/SoftDust.asset");var mat=new Material(Shader.Find("Sprites/Default")){name="WindblownDust",mainTexture=texture};AssetDatabase.CreateAsset(mat,Folder+"/WindblownDust.mat");return mat;
        }

        [MenuItem("RogueDrive/Route/Refine Stop Props")]
        public static void Refine()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            route=UnityEngine.Object.FindFirstObjectByType<StageRoute>(FindObjectsInactive.Include);
            var root=route.transform.Find("Journey_Encounters");
            if(root==null||root.Find("Stop_Detail")!=null)throw new InvalidOperationException("Missing content or already refined.");
            ground=route.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Terrain_")||c.name.StartsWith("BrokenVector_Road_")).ToArray();
            var detail=new GameObject("Stop_Detail").transform;detail.SetParent(root,false);
            // Fit signs in their own plane, irrespective of the route heading.
            foreach(var text in root.GetComponentsInChildren<TextMesh>())
            {
                var parent=text.transform.parent;var q=parent.rotation;parent.rotation=Quaternion.identity;text.transform.localScale=Vector3.one;
                float width=parent.Find("Panel").localScale.x;var b=text.GetComponent<Renderer>().bounds;
                text.transform.localScale=Vector3.one*Mathf.Min((width-.35f)/Mathf.Max(.1f,b.size.x),.95f/Mathf.Max(.1f,b.size.y));parent.rotation=q;
            }
            foreach(float d in new[]{1400f,5400f})
            {
                Place(Garage+"Barrelfbx.prefab",detail,d+12,112,1.2f);
                Place(Garage+"Barrelfbx.prefab",detail,d+14,112,1.2f);
                Place(Garage+"StorageShelfFull.prefab",detail,d+6,108,2.4f,-90);
                Place(City+"Props/Props_Dustbin.prefab",detail,d-18,105,1.1f);
                Place(City+"Props/Props_Street Light.prefab",detail,d-23,104,6);
                var kit=Portable("Assets/Downloads/Low-Poly 3D Lockers/Low-Poly 3D Lockers/Prefabs/metal box.prefab",detail,d-1,91,.55f,"Ремкомплект");
                kit.name="RepairKit_Journey";kit.GetComponent<PhysicsProp>().SetPocketSized(true);kit.AddComponent<GarageItemFunction>().Configure(GarageItemFunction.ItemKind.RepairKit);kit.AddComponent<GarageItemUse>();
            }
            foreach(float d in new[]{3200f,7600f,11600f})
            {
                Place("Assets/Downloads/JeffamazedDev/HouseholdPropsPack/Prefabs/Decoration/DiningRoom/DEC_DiningTable.prefab",detail,d-8,-102,2.2f,90);
                for(int i=0;i<7;i++)
                {float a=i*Mathf.PI*2/7;Place(Forest+"Rocks/stone_small_2.prefab",detail,d+Mathf.Sin(a)*1.4f,-107+Mathf.Cos(a)*1.4f,.55f,i*51);}
                Place(Forest+"Props/fallen_log_small_2.prefab",detail,d,-107,1.4f,35);
                Place(Forest+"Props/fallen_log_small_2.prefab",detail,d,-107,1.1f,-45);
                Place("Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Characters/Adult_Survivor/Backpack.prefab",detail,d-7,-104,.65f,90);
            }
            for(int i=0;i<5;i++)
            {
                Place(Garage+"WoodenPallet.prefab",detail,9608+i*3,112,2.5f,i%2*15);
                Place(Garage+"Barrelfbx.prefab",detail,9608+i*3,108,1.1f);
            }
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(route.gameObject.scene);EditorSceneManager.SaveScene(route.gameObject.scene);
            Debug.Log("[JourneyContent] Stop props refined; two usable repair kits added.");
        }
    }
}
