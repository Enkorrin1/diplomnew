using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Track;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>A single driving HUD. Artwork is vector UI; every readout uses live vehicle state.</summary>
    public sealed class VehicleModularTacticalHud : MonoBehaviour
    {
        public static VehicleModularTacticalHud Instance { get; private set; }
        [SerializeField] ArcadeCarController car;
        [SerializeField] VehicleModularState modularState;
        [SerializeField] VehicleCargoTrunk trunk;
        [SerializeField] VehicleRadioSystem radio;

        static readonly Color Paper = new Color(.93f,.95f,.93f);
        static readonly Color Muted = new Color(.61f,.70f,.73f);
        static readonly Color Amber = new Color(.94f,.73f,.40f);
        static readonly Color Mint = new Color(.47f,.83f,.69f);
        static readonly Color Red = new Color(1f,.38f,.29f);
        static readonly Color Track = new Color(.24f,.31f,.34f);
        static readonly Color Background = new Color(.045f,.068f,.08f,.92f);

        GarageDriveOutVehicle hubVehicle;
        GameRunController activeRun;
        CreepingStormBarrier stormBarrier;
        PauseMenuUI pauseMenu;
        GarageInteractionUI interactionUI;
        float referenceTimer;
        RectTransform safeArea;
        GameObject hudRoot, routeRoot, stormRoot;
        Text speedText, directionText, brakeText, fuelText, coolantText, temperatureText, oilText, engineText;
        Text routeText, distanceText, progressText, coinsText, stormText, stormStatus;
        Text cargoText, bumperText, conditionText, radioText, alertText;
        Text[] tireTexts = new Text[4];
        Image[] tires = new Image[4];
        RectTransform fuelFill, coolantFill, heatFill, routeFill, oilFill;
        DrivingHudGraphic dial;
        Font bodyFont, numberFont;
        float refreshTimer;
        bool driving;
        public bool OwnsDrivingHud => isActiveAndEnabled && driving;

        void Awake()
        {
            if(Instance != null && Instance != this){ Destroy(gameObject); return; }
            Instance=this;
            bodyFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            numberFont=Resources.Load<Font>("UI/LowPoly/Fonts/Heading") ?? bodyFont;
            Build();
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        void OnDisable(){if(interactionUI!=null)interactionUI.SetCrosshairVisible(true);}
        void OnEnable(){ if(Instance==null)Instance=this; }
        void Start(){Instance=this;FindReferences();}
        void FindReferences()
        {
            if(car==null)car=FindFirstObjectByType<ArcadeCarController>();
            if(hubVehicle==null)hubVehicle=car!=null?car.GetComponent<GarageDriveOutVehicle>():FindFirstObjectByType<GarageDriveOutVehicle>();
            var owner=hubVehicle!=null?hubVehicle.gameObject:car!=null?car.gameObject:null;
            if(owner!=null){modularState=owner.GetComponent<VehicleModularState>();trunk=owner.GetComponent<VehicleCargoTrunk>();radio=owner.GetComponent<VehicleRadioSystem>();}
            if(activeRun==null)activeRun=FindFirstObjectByType<GameRunController>();
            if(stormBarrier==null)stormBarrier=FindFirstObjectByType<CreepingStormBarrier>();
            if(pauseMenu==null)pauseMenu=FindFirstObjectByType<PauseMenuUI>();
            if(interactionUI==null)interactionUI=FindFirstObjectByType<GarageInteractionUI>();
        }
        void Update()
        {
            if(Instance==null)Instance=this;
            referenceTimer-=Time.unscaledDeltaTime;
            if(referenceTimer<=0){FindReferences();referenceTimer=.5f;}
            bool garageDrive=hubVehicle!=null && hubVehicle.isActiveAndEnabled && hubVehicle.IsDrivingEnabled;
            driving=garageDrive || (car!=null && car.isActiveAndEnabled && !car.UsesGarageDriving);
            if(interactionUI!=null)interactionUI.SetCrosshairVisible(!driving);
            bool blocked=(VehicleDashboardPanelsUI.Instance?.IsAnyPanelOpen??false)
                || (LevelUpView.Instance?.IsVisible??false) || (BuffCasinoView.Instance?.IsVisible??false);
            bool visible=driving && modularState!=null && !blocked && Time.timeScale>0 && !(activeRun?.IsGameOver??false)
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MainMenuScene";
            hudRoot.SetActive(visible);
            var area=Screen.safeArea;
            safeArea.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            safeArea.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            if(!visible)return;
            refreshTimer+=Time.unscaledDeltaTime;
            if(refreshTimer<.05f)return;
            refreshTimer=0;
            RefreshReadouts(garageDrive);
        }
        void RefreshReadouts(bool garageDrive)
        {
            float signedSpeed=garageDrive?hubVehicle.SpeedKmh:car!=null?car.SpeedKmh:0;
            float speed=Mathf.Abs(signedSpeed);
            speedText.text=Mathf.RoundToInt(speed).ToString("000");
            directionText.text=signedSpeed < -1 ? "НАЗАД" : speed<1 ? "СТОП" : "ВПЕРЁД";
            dial.Value=Mathf.Clamp01(speed/160f);dial.SetVerticesDirty();
            bool braking=garageDrive?hubVehicle.IsHandbrakeActive:car!=null&&car.IsHandbrakeActive;
            brakeText.text=braking?"●  РУЧНИК ВКЛ":"РУЧНИК  /  SPACE";
            brakeText.color=braking?Red:Muted;
            float fuel=modularState.FuelLiters, maxFuel=Mathf.Max(1,modularState.MaxFuelLiters);
            fuelText.text=$"{fuel:0.0} <size=15>/ {maxFuel:0} л</size>";
            fuelText.color=fuel<5?Red:Paper;
            Fill(fuelFill,fuel/maxFuel,fuel<5?Red:Amber);
            coolantText.text=$"{modularState.RadiatorWater:0.0} <size=12>/ {modularState.MaxRadiatorWater:0} л</size>";
            Fill(coolantFill,modularState.RadiatorWater/Mathf.Max(1,modularState.MaxRadiatorWater),modularState.RadiatorWater<2?Red:Mint);
            temperatureText.text=$"{modularState.EngineTemperature:0} <size=12>°C</size>";
            temperatureText.color=modularState.IsOverheated?Red:Paper;
            Fill(heatFill,Mathf.InverseLerp(40,135,modularState.EngineTemperature),modularState.IsOverheated?Red:Amber);
            oilText.text=$"{modularState.EngineOil:0.0} <size=12>/ {modularState.MaxEngineOil:0} л</size>";
            oilText.color=modularState.OilPressure<.45f?Red:Paper;
            Fill(oilFill,modularState.EngineOil/modularState.MaxEngineOil,modularState.OilPressure<.45f?Red:Amber);
            engineText.text=$"ДВС  {modularState.EngineIntegrity*100:0}%";
            engineText.color=modularState.EngineIntegrity<.3f?Red:Muted;
            float worst=1;
            for(int i=0;i<4;i++)
            {
                float value=modularState.GetTireIntegrity(i); worst=Mathf.Min(worst,value);
                Color c=value<.3f?Red:value<.7f?Amber:Mint;
                tires[i].color=c; tireTexts[i].text=$"{value*100:0}";tireTexts[i].color=c;
            }
            bumperText.text=$"БАМПЕР  {modularState.BumperIntegrity*100:0}%";
            bumperText.color=modularState.BumperIntegrity<.3f?Red:Muted;
            conditionText.text=worst<.3f?"ПРОКОЛ ШИНЫ":worst<.7f?"ИЗНОС ШИН":"ШИНЫ / %";
            conditionText.color=worst<.7f?Amber:Muted;
            cargoText.text=trunk!=null?$"БАГАЖ  {trunk.ItemCount}<color=#82969C> / {trunk.MaxSlots}</color>":"";
            radioText.gameObject.SetActive(radio!=null);
            if(radio!=null)
                radioText.text=!radio.IsPlaying?"РАДИО  ВЫКЛ  [R]"
                    :radio.CurrentChannel==VehicleRadioSystem.RadioChannel.WastelandWave?"104.2 FM  /  [R]"
                    :radio.CurrentChannel==VehicleRadioSystem.RadioChannel.CitadelBeacon?"МАЯК 88.5  /  [R]":"РЕЙДЕРЫ 107.9  /  [R]";
            routeRoot.SetActive(activeRun!=null);
            if(activeRun!=null)
            {
                float target=Mathf.Max(1,activeRun.StageTargetDistance);
                float progress=Mathf.Clamp01(activeRun.Distance/target);
                string[] places={"ОКРАИНА ГОРОДА","ПУСТОШЬ","ПРОМЗОНА","ЦИТАДЕЛЬ"};
                int stage=activeRun.CurrentStageIndex;
                routeText.text=$"{stage:00}   /   {(stage>=1&&stage<=4?places[stage-1]:"МАРШРУТ")}";
                distanceText.text=$"{activeRun.Distance/1000f:0.0}<size=13> / {target/1000f:0.0} км</size>";
                progressText.text=$"{progress*100:0}%";
                coinsText.text=$"+{activeRun.CoinsCollected}  МОНЕТ";
                Fill(routeFill,progress,Mint);
            }
            var storm=stormBarrier;
            stormRoot.SetActive(storm!=null);
            if(storm!=null)
            {
                float distance=storm.DistanceToCar;
                stormText.text=distance<=0?"ВНУТРИ БУРИ":distance>=1000?$"{distance/1000f:0.00} км":$"{distance:0} м";
                stormText.color=distance<100?Red:Amber;
                stormStatus.text=distance<100?"ОПАСНОСТЬ РЯДОМ":"ДО ШТОРМА";
            }
            string warning=modularState.IsEngineSeized?"ДВИГАТЕЛЬ ЗАКЛИНИЛ  /  НУЖНА ЗАМЕНА"
                :modularState.OilPressure<.45f?"ДАВЛЕНИЕ МАСЛА  /  ЗАГЛУШИТЕ ДВИГАТЕЛЬ"
                :modularState.IsOverheated?"ПЕРЕГРЕВ  /  ОСТАНОВИТЕ ДВИГАТЕЛЬ"
                : !modularState.HasFuel?"БАК ПУСТ  /  НУЖНА ЗАПРАВКА"
                :modularState.WaterLeakPerSecond>.001f?"ТЕЧЬ РАДИАТОРА  /  НУЖЕН РЕМОНТ"
                :modularState.OilLeakPerSecond>.001f?"ТЕЧЬ МАСЛА  /  НУЖЕН РЕМОНТ"
                :modularState.RadiatorWater<2?"МАЛО ВОДЫ  /  ДОЛЕЙТЕ ПОСЛЕ ОСТЫВАНИЯ"
                :modularState.FuelLiters<5?"МАЛО ТОПЛИВА":worst<.3f?"ПРОКОЛ  /  ЗАМЕНИТЕ КОЛЕСО":"";
            alertText.text=warning;alertText.transform.parent.gameObject.SetActive(warning.Length>0);
        }
        void Build()
        {
            var canvasObject=new GameObject("DrivingHUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            safeArea=Rect(canvas.transform,"SafeArea",0,0,0,0);safeArea.anchorMin=Vector2.zero;safeArea.anchorMax=Vector2.one;safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
            var root=Rect(safeArea,"Instruments",0,0,0,0);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;hudRoot=root.gameObject;

            var route=Panel(root,"Route",new Vector2(0,1),new Vector2(24,-24),new Vector2(304,102));routeRoot=route.gameObject;
            Label(route,"RouteTitle","",18,10,272,24,14,Mint);
            routeText=route.Find("RouteTitle").GetComponent<Text>();
            distanceText=Label(route,"Distance","",18,36,176,30,24,Paper);
            progressText=Label(route,"Progress","",234,39,50,25,18,Paper,TextAnchor.MiddleRight);
            routeFill=Bar(route,"ProgressBar",18,76,268,3,Mint);
            coinsText=Label(route,"Coins","",18,81,268,18,11,Muted);

            var pause=Panel(root,"Pause",new Vector2(1,1),new Vector2(-24,-24),new Vector2(120,38));
            var pauseArt=pause.GetComponent<DrivingHudGraphic>();pauseArt.raycastTarget=true;
            var button=pause.gameObject.AddComponent<Button>();button.targetGraphic=pauseArt;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.3f,1.3f);colors.pressedColor=new Color(.7f,.8f,.8f);button.colors=colors;
            button.onClick.AddListener(()=>{if(pauseMenu==null)pauseMenu=FindFirstObjectByType<PauseMenuUI>();pauseMenu?.TogglePause();});
            Art(pause,"PauseIcon",DrivingHudGraphic.Shape.Pause,12,9,20,20,Paper);
            Label(pause,"PauseLabel","ESC  ПАУЗА",37,6,79,26,13,Paper);
            var threat=Panel(root,"Storm",new Vector2(1,1),new Vector2(-160,-24),new Vector2(188,62));stormRoot=threat.gameObject;
            Art(threat,"StormIcon",DrivingHudGraphic.Shape.Storm,14,15,28,28,Amber);
            stormStatus=Label(threat,"Status","ДО ШТОРМА",54,7,126,18,11,Muted);
            stormText=Label(threat,"Distance","",54,25,124,30,23,Amber);

            var resources=Panel(root,"Resources",Vector2.zero,new Vector2(24,58),new Vector2(300,224));
            Art(resources,"FuelIcon",DrivingHudGraphic.Shape.Fuel,18,19,23,27,Amber);
            Label(resources,"FuelLabel","ТОПЛИВО",54,12,132,19,11,Muted);
            fuelText=Label(resources,"FuelValue","",54,29,228,32,26,Paper);
            fuelFill=Bar(resources,"FuelBar",18,71,264,5,Amber);
            Label(resources,"CoolantLabel","ОХЛАЖДЕНИЕ",18,91,126,17,11,Muted);
            Label(resources,"TemperatureLabel","ДВИГАТЕЛЬ",164,91,118,17,11,Muted);
            coolantText=Label(resources,"CoolantValue","",18,109,126,26,20,Paper);
            temperatureText=Label(resources,"TemperatureValue","",164,109,118,26,20,Paper);
            coolantFill=Bar(resources,"CoolantBar",18,141,118,3,Mint);
            heatFill=Bar(resources,"HeatBar",164,141,118,3,Amber);
            Line(resources,"Divider",149,93,1,50,Track);
            Label(resources,"OilLabel","МАСЛО",18,158,126,18,13,Muted);
            oilText=Label(resources,"OilValue","",18,178,126,26,20,Paper);
            oilFill=Bar(resources,"OilBar",18,212,118,3,Amber);
            engineText=Label(resources,"EngineCondition","",164,175,118,26,17,Muted);
            Label(resources,"EngineHint","СОСТОЯНИЕ",164,201,118,17,13,Muted);

            var instruments=Panel(root,"Vehicle",new Vector2(1,0),new Vector2(-24,58),new Vector2(352,192));
            conditionText=Label(instruments,"Condition","СОСТОЯНИЕ",16,10,130,20,10,Muted);
            Art(instruments,"CarDiagram",DrivingHudGraphic.Shape.Car,44,39,64,98,Muted);
            for(int i=0;i<4;i++)
            {
                bool left=i%2==0;float y=i<2?53:102;
                tires[i]=Line(instruments,"Tire_"+i,left?48:96,y,8,18,Mint);
                tireTexts[i]=Label(instruments,"TireValue_"+i,"100",left?10:108,y-1,34,20,12,Mint,left?TextAnchor.MiddleRight:TextAnchor.MiddleLeft);
            }
            bumperText=Label(instruments,"Bumper","",12,140,132,19,10,Muted,TextAnchor.MiddleCenter);
            cargoText=Label(instruments,"Cargo","",12,163,132,20,12,Paper,TextAnchor.MiddleCenter);
            Line(instruments,"Divider",154,20,1,152,Track);
            dial=Art(instruments,"SpeedDial",DrivingHudGraphic.Shape.Dial,178,9,152,152,Paper);
            directionText=Label(instruments,"Direction","СТОП",210,35,88,24,11,Mint,TextAnchor.MiddleCenter);
            speedText=Label(instruments,"Speed","000",178,43,152,100,64,Paper,TextAnchor.MiddleCenter);speedText.font=numberFont;
            Label(instruments,"SpeedUnit","КМ / Ч",204,128,100,20,11,Muted,TextAnchor.MiddleCenter);
            brakeText=Label(instruments,"Handbrake","",169,162,170,21,11,Muted,TextAnchor.MiddleCenter);

            var controls=Rect(root,"Controls",24,0,680,30);controls.anchorMin=controls.anchorMax=Vector2.zero;controls.pivot=Vector2.zero;controls.anchoredPosition=new Vector2(24,17);
            Key(controls,"WASD","W A S D","УПРАВЛЕНИЕ",0,80,102);
            Key(controls,"Space","SPACE","РУЧНИК",196,54,70);
            Key(controls,"Exit","E","ВЫЙТИ",336,24,67);
            radioText=Label(root,"Radio","",0,0,240,20,11,Muted,TextAnchor.MiddleRight);
            var rr=radioText.rectTransform;rr.anchorMin=rr.anchorMax=new Vector2(1,0);rr.pivot=new Vector2(1,0);rr.anchoredPosition=new Vector2(-24,23);
            var warning=Panel(root,"Warning",new Vector2(.5f,1),new Vector2(0,-24),new Vector2(410,38));
            warning.GetComponent<DrivingHudGraphic>().color=new Color(.25f,.07f,.045f,.95f);
            alertText=Label(warning,"WarningText","",12,5,386,28,13,Red,TextAnchor.MiddleCenter);warning.gameObject.SetActive(false);
        }
        void Key(RectTransform parent,string name,string key,string label,float x,float width,float labelWidth)
        {
            var box=Rect(parent,name,x,1,width,24);var image=box.gameObject.AddComponent<Image>();image.color=new Color(.08f,.12f,.14f,.88f);image.raycastTarget=false;
            Label(box,"Key",key,0,0,width,24,11,Paper,TextAnchor.MiddleCenter);
            Label(parent,name+"Label",label,x+width+8,1,labelWidth,24,11,Paper);
        }
        RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rt=Rect(parent,name,0,0,size.x,size.y);rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=position;
            var art=rt.gameObject.AddComponent<DrivingHudGraphic>();art.Artwork=DrivingHudGraphic.Shape.Panel;art.color=Background;art.raycastTarget=false;return rt;
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var text=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();text.font=bodyFont;text.fontSize=Mathf.Max(13,size);text.text=value;text.color=color;
            text.alignment=alignment;text.supportRichText=true;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
        static Image Line(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        static DrivingHudGraphic Art(Transform parent,string name,DrivingHudGraphic.Shape shape,float x,float y,float w,float h,Color color)
        {
            var art=Rect(parent,name,x,y,w,h).gameObject.AddComponent<DrivingHudGraphic>();art.Artwork=shape;art.color=color;art.raycastTarget=false;return art;
        }
        static RectTransform Bar(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var track=Line(parent,name,x,y,w,h,Track).rectTransform;
            var fill=Line(track,"Fill",0,0,w,h,color).rectTransform;
            fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;return fill;
        }
        static void Fill(RectTransform fill,float value,Color color)
        {fill.anchorMax=new Vector2(Mathf.Clamp01(value),1);fill.GetComponent<Image>().color=color;}
    }
}
