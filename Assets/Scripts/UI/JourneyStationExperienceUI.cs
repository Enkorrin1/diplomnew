using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>World-facing station desk: live car on the left, contextual work on the right.</summary>
    public sealed class JourneyStationExperienceUI : MonoBehaviour
    {
        public enum Page { Service, Workshop, Trader }
        public static JourneyStationExperienceUI Instance { get; private set; }
        public bool IsOpen=>root!=null&&root.activeSelf;
        public int LastInteractionFrame {get;private set;}=-1;
        Canvas canvas;
        GameObject root;
        RectTransform right, content;
        Camera inspectionCamera;
        RenderTexture inspectionTexture;
        Text heading, status, feedback;
        JourneyServiceStation station;
        Page page;
        WorkshopSection section;
        WorkshopSlot selectedSlot;
        ServiceItemAddress? selectedItem;
        string selectedShop;
        bool showingShop;
        int itemPage,tradePage;
        CursorLockMode oldLock;
        bool oldCursor;
        float nextRefresh;

        public static void Open(JourneyServiceStation owner,Page destination)
        {
            if(owner==null||!owner.Visited)return;
            if(Instance==null)new GameObject("JourneyStationExperience").AddComponent<JourneyStationExperienceUI>();
            Instance.Show(owner,destination);
        }
        void Awake(){Instance=this;BuildShell();root.SetActive(false);}
        void OnDestroy(){if(Instance==this)Instance=null;DisposeCamera();}
        void Update()
        {
            if(!IsOpen)return;
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.E)){Close();return;}
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.15f;
            var w=VehicleWorkshop.For(VehicleModularState.Instance);
            if(w==null||w.GetComponent<ArcadeCarController>()?.Run?.IsGameOver==true){Close();return;}
            status.text=$"{station.Status}   ·   {w.Coins} МОНЕТ"+(w.Busy?$"   ·   РАБОТА {w.Remaining:0} С":"");
        }
        void BuildShell()
        {
            var go=new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=235;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            root=new GameObject("StationScreen",typeof(RectTransform),typeof(Image));root.transform.SetParent(go.transform,false);
            var full=(RectTransform)root.transform;full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=full.offsetMax=Vector2.zero;
            root.GetComponent<Image>().color=new Color(.035f,.045f,.05f,1);
            var image=LowPolyUi.Rect(full,"LiveCar",Vector2.zero,new Vector2(990,900)).gameObject.AddComponent<RawImage>();image.color=Color.white;image.raycastTarget=false;
            var lower=Block(full,"CarCaption",new Vector2(0,-746),new Vector2(990,154),new Color(.035f,.055f,.060f,.91f));
            LowPolyUi.Label(lower,"Caption","МАШИНА НА ПОДЪЁМНИКЕ",new Vector2(34,-16),new Vector2(720,42),28,LowPolyUi.Paper).fontStyle=FontStyle.Bold;
            LowPolyUi.Label(lower,"Description","Выберите узел справа. Детали для установки — в багажнике.",new Vector2(34,-67),new Vector2(900,48),19,LowPolyUi.Muted);
            right=Block(full,"Desk",new Vector2(990,0),new Vector2(610,900),new Color(.09f,.135f,.15f,1));
            Block(right,"Accent",new Vector2(0,0),new Vector2(5,900),LowPolyUi.Amber);
            heading=LowPolyUi.Label(right,"Heading","",new Vector2(30,-24),new Vector2(530,44),30,LowPolyUi.Paper);heading.fontStyle=FontStyle.Bold;
            status=LowPolyUi.Label(right,"Status","",new Vector2(30,-72),new Vector2(550,44),16,LowPolyUi.Healthy);
            var close=LowPolyUi.Button(right,"Close","X",new Vector2(546,-24),new Vector2(40,40),Close,false);close.GetComponentInChildren<Text>().fontSize=22;
            var tabs=new[]{"СЕРВИС","ВЕРСТАК","ПРИЁМКА"};
            for(int i=0;i<tabs.Length;i++){var index=i;LowPolyUi.Button(right,"Page_"+i,tabs[i],new Vector2(30+i*185,-126),new Vector2(176,44),()=>ShowPage((Page)index),false);}
            content=LowPolyUi.Rect(right,"Content",new Vector2(30,-188),new Vector2(550,640));
            feedback=LowPolyUi.Label(right,"Feedback","",new Vector2(30,-841),new Vector2(550,44),17,LowPolyUi.Amber);
            if(EventSystem.current==null)new GameObject("StationEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        }
        static RectTransform Block(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {var rect=LowPolyUi.Rect(parent,name,pos,size);rect.gameObject.AddComponent<Image>().color=color;return rect;}
        void Show(JourneyServiceStation owner,Page destination)
        {
            VehicleDashboardPanelsUI.Instance?.ClosePanel();
            if(!IsOpen){oldLock=Cursor.lockState;oldCursor=Cursor.visible;}
            station=owner;root.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(inspectionCamera==null)CreateCamera();
            ShowPage(destination);
        }
        void CreateCamera()
        {
            var car=VehicleModularState.Instance;if(car==null)return;
            inspectionTexture=new RenderTexture(1100,1000,24,RenderTextureFormat.ARGB32){name="StationCarPreview"};inspectionTexture.Create();
            var host=new GameObject("StationInspectionCamera");host.transform.SetParent(transform,false);
            inspectionCamera=host.AddComponent<Camera>();
            var source=GarageDriveOutController.Instance?.DrivingCamera??Camera.main;
            if(source!=null)inspectionCamera.CopyFrom(source);
            inspectionCamera.fieldOfView=46;inspectionCamera.depth=-20;inspectionCamera.targetTexture=inspectionTexture;
            inspectionCamera.clearFlags=CameraClearFlags.Skybox;
            inspectionCamera.cullingMask=~(1<<5);
            var focus=car.transform.position+Vector3.up*1.05f;
            host.transform.position=focus+car.transform.forward*1.5f-car.transform.right*8f+Vector3.up*2f;
            host.transform.LookAt(focus);
            root.transform.Find("LiveCar").GetComponent<RawImage>().texture=inspectionTexture;
        }
        void DisposeCamera()
        {
            if(inspectionCamera!=null){inspectionCamera.targetTexture=null;Destroy(inspectionCamera.gameObject);inspectionCamera=null;}
            if(inspectionTexture!=null){inspectionTexture.Release();Destroy(inspectionTexture);inspectionTexture=null;}
        }
        public void Close()
        {
            if(!IsOpen)return;
            LastInteractionFrame=Time.frameCount;
            root.SetActive(false);DisposeCamera();Cursor.lockState=oldLock;Cursor.visible=oldCursor;
        }
        void ShowPage(Page target)
        {
            page=target;for(int i=content.childCount-1;i>=0;i--){var child=content.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}
            heading.text=page==Page.Service?"СЕРВИС СТО":page==Page.Workshop?"ВЕРСТАК":"ПРИЁМКА ДОБЫЧИ";
            feedback.text="";
            if(page==Page.Service)ServicePage();else if(page==Page.Workshop)WorkshopPage();else TraderPage();
        }
        VehicleWorkshop Workshop()=>VehicleWorkshop.For(VehicleModularState.Instance);
        void Message(string message){feedback.text=message;}
        void ServicePage()
        {
            var w=Workshop();
            LowPolyUi.Label(content,"Intro","Машина под крышей. Фильтры держат фронт снаружи.",new Vector2(0,0),new Vector2(545,56),21,LowPolyUi.Paper);
            LowPolyUi.Label(content,"RepairQuote",$"ПОЛНЫЙ РЕМОНТ   {w.FullServicePrice} МОНЕТ   /   30 С",new Vector2(0,-76),new Vector2(545,42),20,LowPolyUi.Amber);
            LowPolyUi.Button(content,"Repair","ЗАПУСТИТЬ ПОЛНЫЙ РЕМОНТ",new Vector2(0,-126),new Vector2(545,52),()=>{w.BeginFullService(out var m);Message(m);},true);
            LowPolyUi.Label(content,"Supplies","ПРИПАСЫ В БАГАЖНИК",new Vector2(0,-207),new Vector2(545,32),19,LowPolyUi.Muted);
            SupplyButton("БЕНЗИН 15 Л · 20",BunkerAssemblyItemType.FuelCanister,new Vector2(0,-248));
            SupplyButton("ВОДА 10 Л · 10",BunkerAssemblyItemType.WaterCanister,new Vector2(278,-248));
            SupplyButton("МАСЛО 5 Л · 12",BunkerAssemblyItemType.OilCanister,new Vector2(0,-304));
            LowPolyUi.Button(content,"Ignition","ЗАЖИГАНИЕ",new Vector2(278,-304),new Vector2(267,48),()=>{w.ToggleEngine(out var m);Message(m);},false);
            LowPolyUi.Button(content,"Aid","АВАРИЙНАЯ ПОМОЩЬ",new Vector2(0,-382),new Vector2(545,52),()=>{w.EmergencyAssistance(out var m);Message(m);},false);
            LowPolyUi.Button(content,"Cancel","ОТМЕНИТЬ РАБОТУ",new Vector2(0,-446),new Vector2(545,48),()=>{w.CancelJob();Message(w.LastMessage);},false);
            LowPolyUi.Button(content,"Departure","ПОДГОТОВИТЬ ВЫЕЗД  →",new Vector2(0,-530),new Vector2(545,60),()=>{station.ArmDeparture(out var m);Message(m);},true);
        }
        void SupplyButton(string label,BunkerAssemblyItemType type,Vector2 pos)
        {LowPolyUi.Button(content,type.ToString(),label,pos,new Vector2(267,48),()=>{Workshop().BuySupply(type,out var m);Message(m);},false);}
        void WorkshopPage()
        {
            var w=Workshop();
            string[] tabs={"ДВИГАТЕЛЬ","ПОДВЕСКА","КУЗОВ"};
            for(int i=0;i<3;i++){int index=i;LowPolyUi.Button(content,"Section_"+i,tabs[i],new Vector2(i*184,0),new Vector2(178,40),()=>{section=(WorkshopSection)index;selectedSlot=Enum.GetValues(typeof(WorkshopSlot)).Cast<WorkshopSlot>().First(s=>WorkshopCatalog.Section(s)==section);itemPage=0;selectedItem=null;selectedShop=null;ShowPage(Page.Workshop);},i==(int)section);}
            var slots=Enum.GetValues(typeof(WorkshopSlot)).Cast<WorkshopSlot>().Where(s=>WorkshopCatalog.Section(s)==section).ToArray();
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];var installed=w.Installed(slot);
                string name=WorkshopCatalog.SlotName(slot).Replace("Колесо · ","").Replace("Амортизаторы","Аморт.");
                var button=LowPolyUi.Button(content,"Slot_"+slot,name,new Vector2(i%2*278,-52-i/2*44),new Vector2(267,38),()=>{selectedSlot=slot;selectedItem=null;selectedShop=null;itemPage=0;ShowPage(Page.Workshop);},slot==selectedSlot);
                button.GetComponentInChildren<Text>().fontSize=15;
            }
            int rows=(slots.Length+1)/2;float y=-66-rows*44;
            var fitted=w.Installed(selectedSlot);
            LowPolyUi.Label(content,"Selected",$"{WorkshopCatalog.SlotName(selectedSlot).ToUpperInvariant()}  ·  {(fitted==null?"ПУСТО":WorkshopCatalog.Get(fitted.definitionId).title)}",new Vector2(0,y),new Vector2(545,42),19,LowPolyUi.Paper);
            LowPolyUi.Label(content,"Condition",fitted==null?"Выберите подходящую деталь ниже.":$"Состояние установленной детали: {fitted.condition*100:0}%",new Vector2(0,y-39),new Vector2(545,32),16,LowPolyUi.Muted);
            float listY=Mathf.Min(-380,y-95);
            LowPolyUi.Button(content,"CargoTab","ИЗ БАГАЖНИКА",new Vector2(0,listY),new Vector2(267,38),()=>{showingShop=false;itemPage=0;selectedShop=null;selectedItem=null;ShowPage(Page.Workshop);},!showingShop);
            LowPolyUi.Button(content,"ShopTab","КУПИТЬ ДЕТАЛЬ",new Vector2(278,listY),new Vector2(267,38),()=>{showingShop=true;itemPage=0;selectedShop=null;selectedItem=null;ShowPage(Page.Workshop);},showingShop);
            var offers=Offers();
            for(int i=0;i<3;i++)
            {
                int index=itemPage*3+i;if(index>=offers.Count)break;
                var offer=offers[index];
                LowPolyUi.Button(content,"Offer_"+i,offer.label,new Vector2(0,listY-46-i*46),new Vector2(545,40),()=>{selectedItem=offer.address;selectedShop=offer.definition;Message(offer.label);},false).GetComponentInChildren<Text>().fontSize=16;
            }
            LowPolyUi.Button(content,"Prev","←",new Vector2(0,-567),new Vector2(55,38),()=>{itemPage=Mathf.Max(0,itemPage-1);ShowPage(Page.Workshop);},false);
            LowPolyUi.Label(content,"Page",$"{itemPage+1} / {Mathf.Max(1,Mathf.CeilToInt(offers.Count/3f))}",new Vector2(68,-567),new Vector2(130,38),17,LowPolyUi.Muted);
            LowPolyUi.Button(content,"Next","→",new Vector2(202,-567),new Vector2(55,38),()=>{itemPage=Mathf.Min(Mathf.Max(0,(offers.Count-1)/3),itemPage+1);ShowPage(Page.Workshop);},false);
            LowPolyUi.Button(content,"Install","УСТАНОВИТЬ",new Vector2(270,-567),new Vector2(275,38),()=>{string m;if(selectedItem.HasValue)w.BeginInstall(selectedItem.Value,selectedSlot,out m);else m="Выберите привезённую деталь.";Message(m);},true);
            LowPolyUi.Button(content,"Buy","КУПИТЬ",new Vector2(0,-613),new Vector2(267,38),()=>{string m;if(selectedShop!=null)w.Buy(selectedShop,out m);else m="Выберите деталь в каталоге.";Message(m);},false);
            LowPolyUi.Button(content,"RepairPart","РЕМОНТ ДЕТАЛИ",new Vector2(278,-613),new Vector2(267,38),()=>{string m;if(selectedItem.HasValue)w.BeginRepair(selectedItem.Value,out m);else m="Выберите деталь в багажнике.";Message(m);},false);
        }
        struct Offer {public string label,definition;public ServiceItemAddress? address;}
        List<Offer> Offers()
        {
            var offers=new List<Offer>();var w=Workshop();
            if(showingShop)
            {
                foreach(var part in WorkshopCatalog.All.Where(p=>p.kind==WorkshopCatalog.Kind(selectedSlot)&&p.tier<=(w.Zone?.StockTier??0)))
                    offers.Add(new Offer{label=$"{part.title}  ·  {part.price} МОНЕТ",definition=part.id});
            }
            else
            {
                var trunk=VehicleCargoTrunk.Instance;
                if(trunk!=null)for(int i=0;i<trunk.MaxSlots;i++)
                {
                    var a=new ServiceItemAddress(1,i);var item=a.Read();var part=WorkshopPartItem.Read(item);
                    if(part!=null&&WorkshopCatalog.Kind(selectedSlot)==WorkshopCatalog.Get(part.definitionId).kind)
                        offers.Add(new Offer{label=$"{item.displayName}  ·  {part.condition*100:0}%",address=a});
                }
            }
            return offers;
        }
        void TraderPage()
        {
            var w=Workshop();
            LowPolyUi.Label(content,"Intro","ПРИЁМКА ОПЛАЧИВАЕТ ДОБЫЧУ И СНЯТЫЕ ДЕТАЛИ",new Vector2(0,0),new Vector2(545,64),20,LowPolyUi.Paper);
            var sources=VehicleServiceInventory.Sources().Where(a=>!a.Read().IsEmpty).ToArray();
            int pages=Mathf.Max(1,Mathf.CeilToInt(sources.Length/7f));tradePage=Mathf.Clamp(tradePage,0,pages-1);
            for(int i=0;i<7;i++)
            {
                int index=tradePage*7+i;if(index>=sources.Length)break;
                var a=sources[index];var item=a.Read();int price=w.SellPrice(item);
                var prefix=a.Area==1?"БАГАЖНИК":a.Area==0?"РЮКЗАК":"РУКИ";
                LowPolyUi.Button(content,"Sell_"+index,$"{prefix} · {item.displayName}       +{price}",new Vector2(0,-78-i*64),new Vector2(545,54),()=>{w.Sell(a,out var m);ShowPage(Page.Trader);Message(m);},false).GetComponentInChildren<Text>().fontSize=17;
            }
            LowPolyUi.Button(content,"Prev","←",new Vector2(0,-560),new Vector2(70,46),()=>{tradePage--;ShowPage(Page.Trader);},false);
            LowPolyUi.Label(content,"Page",$"{tradePage+1} / {pages}",new Vector2(100,-560),new Vector2(120,46),18,LowPolyUi.Muted);
            LowPolyUi.Button(content,"Next","→",new Vector2(250,-560),new Vector2(70,46),()=>{tradePage++;ShowPage(Page.Trader);},false);
            LowPolyUi.Label(content,"Hint","Касание строки сразу продаёт один предмет. Содержимое контейнера сначала выгрузите.",new Vector2(0,-610),new Vector2(545,52),16,LowPolyUi.Muted);
        }
    }
}
