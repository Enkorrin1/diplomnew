using System;
using System.Collections;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class JourneyStationsValidation
{
    public static string Geometry()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        string report="";Directory.CreateDirectory("Artifacts/JourneyStations");
        for(int n=1;n<=3;n++)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Journey/Route0"+n+"_World.unity");
            var descriptor=scene.GetRootGameObjects().Select(g=>g.GetComponent<JourneyStreamWorld>()).First(d=>d!=null);descriptor.World.SetActive(true);
            var station=descriptor.World.GetComponentInChildren<JourneyServiceStation>(true);station.transform.Find("Departure_Shutter").localPosition=new Vector3(0,9,31);station.transform.Find("Arrival_Shutter").localPosition=new Vector3(0,9,-31);Physics.SyncTransforms();
            int rays=0,miss=0,blocked=0;
            var points=new[]{new Vector3(-50,0,-110),new Vector3(0,0,-42),new Vector3(0,0,42),new Vector3(-50,0,110)};
            for(int part=0;part<3;part++)
            {
                Vector3 a=station.transform.TransformPoint(points[part]),b=station.transform.TransformPoint(points[part+1]);var q=Quaternion.LookRotation(b-a);int steps=Mathf.CeilToInt(Vector3.Distance(a,b));
                for(int k=0;k<=steps;k++)
                {
                    Vector3 p=Vector3.Lerp(a,b,(float)k/steps);
                    foreach(float lane in new[]{-3f,0,3f}){rays++;var hits=Physics.RaycastAll(p+q*Vector3.right*lane+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore);if(!hits.Any(h=>Mathf.Abs(h.point.y-p.y)<.3f))miss++;}
                    if(Physics.OverlapBox(p+Vector3.up*1.7f,new Vector3(1.2f,1,2.3f),q,~0,QueryTriggerInteraction.Ignore).Length>0)blocked++;
                }
            }
            report+=$"STO {n}: floor rays={rays}, missing={miss}, blocked driving samples={blocked}\n";
            if(miss>0||blocked>0){File.WriteAllText("Artifacts/JourneyStations/geometry.txt",report);throw new Exception(report);}
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");report+="PASS\n";File.WriteAllText("Artifacts/JourneyStations/geometry.txt",report);return report;
    }
    static JourneyStationsValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("StationTest",false)){SessionState.EraseBool("StationTest");new GameObject("Station_Validation").AddComponent<JourneyStationsRunner>();}};}
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts/JourneyStations");File.WriteAllText("Artifacts/JourneyStations/playmode.txt","START\n");
        EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");SessionState.SetBool("StationTest",true);EditorApplication.isPlaying=true;
    }
}
public sealed class JourneyStationsRunner:MonoBehaviour
{
    const string Report="Artifacts/JourneyStations/playmode.txt";
    ArcadeCarController car;VehicleModularState modules;VehicleWorkshop w;GarageDriveOutVehicle driver;Rigidbody rb;
    string carriedTemplate;
    void Check(bool value,string message){File.AppendAllText(Report,(value?"OK ":"FAIL ")+message+"\n");if(!value)throw new Exception(message);}
    void Bind(){car=Object.FindFirstObjectByType<ArcadeCarController>();modules=car.GetComponent<VehicleModularState>();w=VehicleWorkshop.For(modules);driver=car.GetComponent<GarageDriveOutVehicle>();rb=car.GetComponent<Rigidbody>();var pause=RogueDrive.UI.PauseMenuUI.Instance;if(pause!=null){pause.ResumeGame();pause.enabled=false;}foreach(var events in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))events.enabled=false;}
    void Funds(int value){var data=JsonUtility.FromJson<VehicleWorkshop.SaveData>(w.Capture());data.coins=value;w.Restore(JsonUtility.ToJson(data));}
    void Hold(Vector3 position,Quaternion rotation){driver.CinematicControl=true;rb.isKinematic=true;car.PlaceAtStart(position,rotation);rb.isKinematic=true;JsonUtility.FromJsonOverwrite("{\"engineStopped\":true}",w);Physics.SyncTransforms();}
    IEnumerator WaitFor(Func<bool> test,string label,float timeout=90)
    {float end=Time.realtimeSinceStartup+timeout;while(!test()&&Time.realtimeSinceStartup<end)yield return null;Check(test(),label);}
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);yield return new WaitForSeconds(2);Bind();
        int audioId=RogueDrive.Audio.AudioManager.Instance.GetInstanceID();
        Check(RogueDrive.Audio.AudioManager.Instance.transform.parent==null&&RogueDrive.Audio.AudioManager.Instance.gameObject.scene.name=="DontDestroyOnLoad","Audio service is a persistent root");
        var firearm=Object.FindFirstObjectByType<FirearmWeapon>(FindObjectsInactive.Include);
        Check(firearm!=null,"Starter pistol available");firearm.SetAmmo(2);
        var pocket=PlayerPocketInventory.Instance;int reserve=pocket.GetItemCount("ammo_9mm");
        if(reserve>0)pocket.RemoveItem("ammo_9mm",reserve);
        var combat=pocket.GetComponent<PlayerFirearmCombat>();combat.enabled=false;combat.enabled=true;combat.EnsureStarterFirearm();
        Check(pocket.GetItemCount("ammo_9mm")==0&&Object.FindObjectsByType<FirearmWeapon>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Re-enabling player cannot refill ammo or duplicate pistol");
        Check(Object.FindObjectsByType<FirearmWeapon>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(f=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(f.gameObject)==0),"Pistol has no missing components");
        File.AppendAllText(Report,ExpeditionReliabilityValidation.CheckRestoreCleanup(car)+"\n");
        var loot=Object.FindObjectsByType<JourneyItemTemplate>(FindObjectsSortMode.None).First(t=>t.GetComponent<FluidContainer>()!=null&&t.GetComponent<PhysicsProp>()!=null);
        carriedTemplate=loot.ResourceKey;
        var initialCargo=VehicleCargoTrunk.Instance;int initialFree=Enumerable.Range(0,initialCargo.MaxSlots).First(i=>initialCargo.GetSlot(i).IsEmpty);
        initialCargo.SetGridSlot(initialFree,InventoryStackOps.FromObject(loot.gameObject,loot.transform.lossyScale));
        for(int n=1;n<=3;n++)
        {
            var stream=SeamlessJourneyStream.Instance;float distance=stream.Catalog.segments[n-1].roadEndDistance+400;
            stream.Route.Evaluate(distance,out var p,out var q);Hold(p+q*new Vector3(50,.25f,0),q);Funds(500);
            yield return WaitFor(()=>stream.IsWorldLoaded(n-1),"Station world "+n+" loaded");yield return new WaitForSeconds(.5f);
            var station=Object.FindObjectsByType<JourneyServiceStation>(FindObjectsSortMode.None).Single(s=>s.Number==n);
            Check(w.Zone==station.GetComponent<WorkshopServiceZone>()&&w.Sheltered,"Safe hangar "+n+" connected");
            Check(w.Zone.StockTier==n&&w.Zone.Equipment==WorkshopEquipment.All,"Station "+n+" stock/equipment");
            Check(!w.Zone.Contains(p),"Main road outside safe shelter");
            float front=CreepingStormBarrier.Instance.RouteDistance;
            yield return WaitFor(()=>station.Ready,"Filtration and arrival checkpoint "+n,120);
            Check(Mathf.Abs(front-CreepingStormBarrier.Instance.RouteDistance)<1,"Shelter protects during service; timeScale="+Time.timeScale);
            var trunk=VehicleCargoTrunk.Instance;
            Check(w.BuySupply(BunkerAssemblyItemType.FuelCanister,out var message),"Physical fuel purchase: "+message);
            int free=Enumerable.Range(0,trunk.MaxSlots).Where(i=>trunk.GetSlot(i).IsEmpty).DefaultIfEmpty(-1).First();
            Check(free>=0,"Cargo has room for install");trunk.SetGridSlot(free,WorkshopPartItem.Create(WorkshopCatalog.New("engine_economy")));
            Check(w.BeginInstall(new ServiceItemAddress(1,free),WorkshopSlot.Engine,out message),"Install brought engine: "+message);int before=w.Coins;w.Advance(100);
            Check(w.Installed(WorkshopSlot.Engine).definitionId=="engine_economy"&&w.Coins==before-WorkshopCatalog.Get("engine_economy").labor,"Pay labor only, original engine returned");
            Check(WorkshopPartItem.Read(trunk.GetSlot(free))!=null,"Removed engine remains cargo");
            Check(w.Sell(new ServiceItemAddress(1,free),out message),"Sale of returned engine");
            w.DamagePart(WorkshopSlot.Engine,.25f);Check(w.BeginFullService(out message),"Paid full repair starts");before=w.Coins;w.Advance(40);
            Check(modules.EngineIntegrity==1&&w.Coins<before&&!w.Busy,"Full repair commits once");
            Funds(0);modules.SetFuelForGaragePreparation(0);w.DamagePart(WorkshopSlot.Engine,.9f);
            Check(w.EmergencyAssistance(out message),"Emergency aid: "+message);
            Check(modules.FuelLiters>=8&&modules.EngineIntegrity>=.39f&&w.Coins==0,"Aid restores only minimum");
            Check(!w.EmergencyAssistance(out message),"Repeated emergency aid denied");Funds(500);
            RogueDrive.UI.VehicleDashboardPanelsUI.Instance.ShowStationPanel();yield return null;
            ScreenCapture.CaptureScreenshot("Artifacts/JourneyStations/sto"+n+"-service.png");yield return new WaitForEndOfFrame();yield return null;
            RogueDrive.UI.VehicleDashboardPanelsUI.Instance.ClosePanel();
            CaptureStation(station,n);
            Check(station.ArmDeparture(out message),"Departure allowed after filtration: "+message);yield return new WaitForSeconds(3);
            Hold(station.transform.TransformPoint(new Vector3(0,.3f,37)),station.transform.rotation);
            yield return new WaitForSeconds(.5f);
            Check(JourneyCheckpoint.OwnsCurrentRun,"Prepared departure checkpoint active");
            Check(Mathf.Abs(CreepingStormBarrier.Instance.DistanceToCar-900)<20,"Fresh wave after departure");
            if(n==1)
            {
                float fuel=modules.FuelLiters;int count=trunk.ItemCount;int money=w.Coins;
                modules.SetFuelForGaragePreparation(0);Funds(9999);
                car.Run.TakeChassisDamage(1000);Check(car.Run.IsGameOver,"Actual defeat before retry");yield return null;
                ScreenCapture.CaptureScreenshot("Artifacts/JourneyStations/defeat.png");yield return new WaitForEndOfFrame();yield return null;
                car.Run.Restart();Check(JourneyCheckpoint.HasPendingRestore,"Retry button uses station checkpoint");
                yield return WaitFor(()=>!JourneyCheckpoint.HasPendingRestore&&!JourneyCheckpoint.Restoring&&SeamlessJourneyStream.Instance!=null,"Prepared retry loaded");Bind();
                Check(Mathf.Abs(modules.FuelLiters-fuel)<.2f&&w.Coins==money&&VehicleCargoTrunk.Instance.ItemCount==count,"Retry restores exact supplies, money and cargo; no farming");
                var restoredGuns=Object.FindObjectsByType<FirearmWeapon>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                Check(restoredGuns.Length==1&&restoredGuns[0].CurrentAmmo==2&&PlayerPocketInventory.Instance.GetItemCount("ammo_9mm")==0,"Retry restores pistol and exact ammo without free refill");
                Check(RogueDrive.Audio.AudioManager.Instance.GetInstanceID()==audioId,"Same audio service survives checkpoint scene reload");
                Check(Object.FindObjectsByType<JourneyItemTemplate>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.ResourceKey==carriedTemplate)==1,"Authored physical loot restored once, world original suppressed");
                Check(Object.FindObjectsByType<JourneyServiceStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(s=>s.Number==1).AidUsed,"Prepared checkpoint retains used emergency aid");
                Check(JourneyCheckpoint.RequestRestore(false),"Change preparation accepted");
                yield return WaitFor(()=>!JourneyCheckpoint.HasPendingRestore&&!JourneyCheckpoint.Restoring&&SeamlessJourneyStream.Instance!=null,"Arrival state loaded");Bind();
                Check(w.Coins==500&&w.Installed(WorkshopSlot.Engine).definitionId=="engine_stock","Arrival rolls back purchases and engine fitting");
            }
        }
        yield return DriveGate();
        File.AppendAllText(Report,"PASS: service/checkpoint/physical-driving acceptance complete.\n");
        var mapObject=new GameObject("Validation_Map",typeof(BoxCollider));var map=mapObject.AddComponent<BunkerEvacuationMap>();map.OpenMap();yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/JourneyStations/map.png");yield return new WaitForEndOfFrame();yield return null;map.CloseMap();Object.Destroy(mapObject);
        File.AppendAllText(Report,"PASS: 3 service stations, economy, aid, shelter, departure and both checkpoints.\n");EditorApplication.isPaused=true;
    }
    IEnumerator DriveGate()
    {
        var station=Object.FindObjectsByType<JourneyServiceStation>(FindObjectsSortMode.None).First(s=>s.Number==3);
        car.PlaceAtStart(station.transform.TransformPoint(new Vector3(0,.3f,-40)),station.transform.rotation);
        rb.isKinematic=false;driver.CinematicControl=false;driver.ExternalInput=true;
        if(w.EngineStopped)w.ToggleEngine(out var ignored);rb.linearVelocity=station.transform.forward*10;driver.SetExternalInput(.6f,0,false);
        float deadline=Time.realtimeSinceStartup+60;bool crossed=false;
        while(Time.realtimeSinceStartup<deadline){yield return null;if(station.transform.InverseTransformPoint(car.transform.position).z>42){crossed=true;break;}}
        driver.SetExternalInput(0,0,true);driver.ExternalInput=false;
        Check(crossed,"Physical drive through hangar entrance and open exit gate");
    }
    static void CaptureStation(JourneyServiceStation station,int n)
    {
        var g=new GameObject("ValidationCamera");var cam=g.AddComponent<Camera>();cam.farClipPlane=500;cam.fieldOfView=55;
        cam.transform.position=station.transform.TransformPoint(new Vector3(-42,26,-62));cam.transform.LookAt(station.transform.TransformPoint(new Vector3(0,3,0)));
        var target=RenderTexture.GetTemporary(1280,720,24);cam.targetTexture=target;cam.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Artifacts/JourneyStations/sto"+n+".png",image.EncodeToPNG());
        RenderTexture.active=previous;cam.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(g);Object.Destroy(image);
    }
}
