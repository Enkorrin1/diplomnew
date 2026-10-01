using RogueDrive.Audio;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>Low-poly service panels bound to the live vehicle and hands inventory.</summary>
    public sealed partial class VehicleDashboardPanelsUI : MonoBehaviour
    {
        public static VehicleDashboardPanelsUI Instance { get; private set; }
        public enum ActivePanelType { None, Engine, Trunk, FuelInlet }
        [SerializeField] ActivePanelType currentPanel;
        Canvas canvas;
        GameObject modal;
        RectTransform body;
        Text title, subtitle, notice, temperature, volume, battery, reason;
        Image fluidFill;
        RectTransform temperatureNeedle;
        Button fluidButton;
        int selectedSlot, openFrame, lastCargo = -1;
        BunkerAssemblyItemType lastHeld;
        CursorLockMode priorLock;
        bool priorVisible;
        float nextRefresh;
        public bool IsAnyPanelOpen => currentPanel != ActivePanelType.None;
        public int LastInteractionFrame {get;private set;}=-1;
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; EnsureUI();
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Update()
        {
            if (!IsAnyPanelOpen) return;
            if (Time.frameCount > openFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))) { ClosePanel(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            if (currentPanel == ActivePanelType.Trunk)
            {
                int count = VehicleCargoTrunk.Instance != null ? VehicleCargoTrunk.Instance.ItemCount : 0;
                var held = BunkerPlayerInventory.Instance != null ? BunkerPlayerInventory.Instance.HeldItem : BunkerAssemblyItemType.None;
                if (count != lastCargo || held != lastHeld) BuildTrunk();
            }
            else RefreshStats();
        }
        public void ShowEnginePanel()
        {
            Open(ActivePanelType.Engine, "ДВИГАТЕЛЬ", "ПОЛЕВОЕ ОБСЛУЖИВАНИЕ / ДВС • РАДИАТОР • АКБ");
            if (BunkerStarterCarAssembly.Instance != null && !BunkerStarterCarAssembly.Instance.IsBatteryInstalled)
            {
                sourceArea = PlayerHandsInventory.Instance != null && PlayerHandsInventory.Instance.HasItem ? 2 : 0;
                sourcePage = 0;
                BuildFieldService();
            }
            else BuildEngine();
        }
        public void ShowTrunkPanel() { InventoryWindowUI.Instance?.OpenForTrunk(VehicleCargoTrunk.Instance); }
        public void ShowFuelInletPanel() { Open(ActivePanelType.FuelInlet, "ЗАПРАВКА", "ТОПЛИВНАЯ СИСТЕМА / БЕНЗИН"); BuildFuel(); }
        void Open(ActivePanelType panel, string heading, string description)
        {
            EnsureUI();
            if (!IsAnyPanelOpen) { priorLock = Cursor.lockState; priorVisible = Cursor.visible; }
            currentPanel = panel; openFrame = Time.frameCount;
            InventoryWindowUI.Instance?.Close();
            modal.SetActive(true); title.text = heading; subtitle.text = description; notice.text = "";
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            AudioManager.Instance?.PlaySwitchClick();
        }
        public void ClosePanel()
        {
            LastInteractionFrame=Time.frameCount;
            CancelServiceDrag();
            bool wasOpen = IsAnyPanelOpen; currentPanel = ActivePanelType.None;
            if (modal != null) modal.SetActive(false);
            if (wasOpen) { Cursor.lockState = priorLock; Cursor.visible = priorVisible; AudioManager.Instance?.PlaySwitchClick(); }
        }
        void EnsureUI()
        {
            if (canvas != null) return;
            var go = new GameObject("LowPoly_ServiceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 220;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            modal = new GameObject("ServiceModal", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(go.transform, false);
            var overlay = (RectTransform)modal.transform; overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.sizeDelta = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(.025f,.04f,.05f,.70f);
            var panel = LowPolyUi.Panel(overlay, "ServicePanel", Vector2.zero, new Vector2(1380,740), LowPolyUi.Ink);
            var rt = panel.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f); rt.anchoredPosition = Vector2.zero;
            title = LowPolyUi.Label(rt,"Title","",new Vector2(36,-2),new Vector2(1080,88),52); title.fontStyle = FontStyle.Bold;
            subtitle = LowPolyUi.Label(rt,"Subtitle","",new Vector2(38,-88),new Vector2(1160,26),16,LowPolyUi.Muted);
            var rule=LowPolyUi.Panel(rt,"HeaderRule",new Vector2(38,-120),new Vector2(1300,2),LowPolyUi.Border);rule.sprite=LowPolyUi.Sprite("bar");rule.color=LowPolyUi.Border;rule.raycastTarget=false;
            LowPolyUi.Button(rt,"Close","ESC / ЗАКРЫТЬ",new Vector2(1130,-36),new Vector2(210,48),ClosePanel,false);
            body = LowPolyUi.Rect(rt,"Content",new Vector2(32,-132),new Vector2(1316,510));
            notice = LowPolyUi.Label(rt,"Notice","",new Vector2(36,-659),new Vector2(1300,52),20,LowPolyUi.Amber);
            modal.SetActive(false);
        }
        void Clear()
        {
            stationScreen=false;
            for (int i=body.childCount-1;i>=0;i--) { var child=body.GetChild(i); child.gameObject.SetActive(false); Destroy(child.gameObject); }
            ClearService(); WorkshopFrame(false);
            temperature=volume=battery=reason=null; fluidFill=null; fluidButton=null; temperatureNeedle=null;
        }
        static string ItemName(BunkerAssemblyItemType type)
        {
            switch(type) { case BunkerAssemblyItemType.FuelCanister:return "БЕНЗИН";case BunkerAssemblyItemType.WaterCanister:return "ОХЛАЖДАЙКА";case BunkerAssemblyItemType.Wheel:return "ЗАПАСНОЕ КОЛЕСО";case BunkerAssemblyItemType.Battery:return "АККУМУЛЯТОР";case BunkerAssemblyItemType.Axe:return "ТОПОР";default:return "ПУСТО"; }
        }
        public static string ItemIcon(BunkerAssemblyItemType type)
        {
            switch(type) { case BunkerAssemblyItemType.OilCanister:return "icon_fuel_canister";case BunkerAssemblyItemType.Engine:return "engine_bay";case BunkerAssemblyItemType.Radiator:return "engine_bay";case BunkerAssemblyItemType.FuelCanister:return "icon_fuel_canister";case BunkerAssemblyItemType.WaterCanister:return "icon_water_canister";case BunkerAssemblyItemType.Wheel:return "icon_wheel";case BunkerAssemblyItemType.Battery:return "icon_battery";case BunkerAssemblyItemType.Axe:return "icon_axe";default:return "icon_scrap"; }
        }
        void BuildTrunk()
        {
            Clear(); var trunk=VehicleCargoTrunk.Instance; var hands=BunkerPlayerInventory.Instance; var pocket=PlayerPocketInventory.Instance;
            var pocketItem=pocket!=null?pocket.GetActiveItem():PocketSlotData.Empty;
            bool carrying=hands!=null && hands.HasItem;
            var source=carrying?hands.HeldGameObject:pocketItem.worldItem!=null?pocketItem.worldItem.gameObject:null;
            var sourceIcon=source!=null?source.GetComponent<PhysicsProp>()?.InventoryIcon:null;
            lastCargo=trunk!=null?trunk.ItemCount:0; lastHeld=hands!=null?hands.HeldItem:BunkerAssemblyItemType.None;
            int capacity=trunk!=null?trunk.MaxSlots:0;
            var left=LowPolyUi.Panel(body,"HandsPanel",Vector2.zero,new Vector2(240,510),LowPolyUi.Surface).transform;
            LowPolyUi.Label(left,"Heading","У ВАС",new Vector2(20,-14),new Vector2(200,36),23).fontStyle=FontStyle.Bold;
            if(sourceIcon!=null) DrawItemIcon(left,sourceIcon,new Vector2(24,-70),new Vector2(192,192));
            else if(lastHeld!=BunkerAssemblyItemType.None) LowPolyUi.Icon(left,ItemIcon(lastHeld),new Vector2(24,-70),new Vector2(192,192));
            else
            {
                var empty=LowPolyUi.Panel(left,"EmptyHandSlot",new Vector2(20,-70),new Vector2(200,192),LowPolyUi.Ink);
                var mark=LowPolyUi.Label(empty.transform,"EmptyMark","—",new Vector2(0,-45),new Vector2(200,90),54,LowPolyUi.Border);mark.alignment=TextAnchor.MiddleCenter;
            }
            LowPolyUi.Label(left,"HeldName",carrying?hands.GetHeldItemDisplayName():pocketItem.IsEmpty?"Выберите слот ниже":pocketItem.displayName+ (pocketItem.count>1?" ×"+pocketItem.count:""),new Vector2(20,-280),new Vector2(200,70),20);
            for(int n=0;n<5;n++){int slot=n;var choose=LowPolyUi.Button(left,"Pocket_"+n,(n+1).ToString(),new Vector2(20+n*40,-345),new Vector2(36,34),()=>{pocket?.SelectSlot(slot);BuildTrunk();},false);choose.interactable=pocket!=null&&!pocket.GetSlot(n).IsEmpty;}
            var deposit=LowPolyUi.Button(left,"StoreItem","ПОЛОЖИТЬ",new Vector2(20,-390),new Vector2(200,48),StoreItem);
            deposit.interactable=source!=null&&trunk!=null&&lastCargo<capacity;
            LowPolyUi.Label(left,"Hint",lastCargo>=capacity&&capacity>0?"НЕТ МЕСТА":"Из рук или слота,\nпо одному предмету",new Vector2(20,-450),new Vector2(200,48),16,LowPolyUi.Muted);
            var center=LowPolyUi.Panel(body,"TrunkPanel",new Vector2(258,0),new Vector2(676,510),LowPolyUi.Surface).transform;
            LowPolyUi.Label(center,"Heading",$"ХРАНИЛИЩЕ     {lastCargo} / {capacity}",new Vector2(20,-14),new Vector2(635,40),23).fontStyle=FontStyle.Bold;
            int columns=capacity>6?4:3; int rows=Mathf.Max(1,Mathf.CeilToInt(capacity/(float)columns));
            float width=(636-(columns-1)*12)/columns, height=Mathf.Min(202,(418-(rows-1)*12)/rows);
            selectedSlot=Mathf.Clamp(selectedSlot,0,Mathf.Max(0,lastCargo-1));
            for(int i=0;i<capacity;i++)
            {
                int slot=i; bool occupied=i<lastCargo; var item=occupied?trunk.GetItem(i):BunkerAssemblyItemType.None;
                var button=LowPolyUi.Button(center,"Slot_"+i,"",new Vector2(20+(i%columns)*(width+12),-68-(i/columns)*(height+12)),new Vector2(width,height),()=> {selectedSlot=slot;if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))TakeItem();else BuildTrunk();},false);
                button.GetComponent<Image>().sprite=LowPolyUi.Sprite("panel");
                var slotColors=button.colors;slotColors.normalColor=Color.white;slotColors.highlightedColor=new Color32(218,228,228,255);slotColors.selectedColor=Color.white;slotColors.pressedColor=LowPolyUi.Muted;button.colors=slotColors;
                if(occupied&&i==selectedSlot){var accent=LowPolyUi.Panel(button.transform,"SelectedAccent",new Vector2(13,-9),new Vector2(width-26,3),LowPolyUi.Amber);accent.sprite=LowPolyUi.Sprite("bar");accent.color=LowPolyUi.Amber;accent.raycastTarget=false;}
                LowPolyUi.Label(button.transform,"Number",$"{i+1:00}",new Vector2(12,-6),new Vector2(45,26),16,occupied?LowPolyUi.Amber:LowPolyUi.Muted);
                if(occupied){var icon=trunk.GetIcon(i);if(icon!=null)DrawItemIcon(button.transform,icon,new Vector2((width-154)/2,-18),new Vector2(154,Mathf.Min(154,height-48)));else LowPolyUi.Icon(button.transform,ItemIcon(item),new Vector2((width-154)/2,-18),new Vector2(154,Mathf.Min(154,height-48)));}
                var label=LowPolyUi.Label(button.transform,"ItemName",occupied?trunk.GetDisplayName(i):"ПУСТО",new Vector2(8,-height+48),new Vector2(width-16,40),18,occupied?LowPolyUi.Paper:LowPolyUi.Muted);label.alignment=TextAnchor.MiddleCenter;label.font=LowPolyUi.HeadingFont;
            }
            var detail=LowPolyUi.Panel(body,"ItemDetail",new Vector2(952,0),new Vector2(364,510),LowPolyUi.Surface).transform;
            var selected=trunk!=null?trunk.GetItem(selectedSlot):BunkerAssemblyItemType.None;
            bool hasSelected=trunk!=null && selectedSlot<trunk.ItemCount;
            LowPolyUi.Label(detail,"Name",hasSelected?trunk.GetDisplayName(selectedSlot):"ПУСТО",new Vector2(24,-20),new Vector2(316,58),24).fontStyle=FontStyle.Bold;
            if(hasSelected){var icon=trunk.GetIcon(selectedSlot);if(icon!=null)DrawItemIcon(detail,icon,new Vector2(24,-60),new Vector2(316,280));else LowPolyUi.Icon(detail,ItemIcon(selected),new Vector2(24,-60),new Vector2(316,280));}
            LowPolyUi.Label(detail,"Description",!hasSelected?"Выберите предмет в хранилище.":"Мелочь → в слоты. Крупный предмет → в руки.",new Vector2(24,-330),new Vector2(316,62),18,LowPolyUi.Muted);
            var take=LowPolyUi.Button(detail,"TakeItem","ЗАБРАТЬ",new Vector2(24,-402),new Vector2(316,50),TakeItem);
            take.interactable=hasSelected&&hands!=null&&!hands.HasItem;
            LowPolyUi.Label(detail,"TakeHint",hands!=null&&hands.HasItem?"Сначала освободите руки":"Shift + клик — быстрый перенос",new Vector2(24,-462),new Vector2(316,34),16,LowPolyUi.Muted);
            if(trunk==null) Notify("Хранилище автомобиля недоступно.");
        }
        static void DrawItemIcon(Transform parent,Sprite sprite,Vector2 position,Vector2 size)
        {
            var rect=LowPolyUi.Rect(parent,"ItemIcon",position,size);var image=rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
        }
        void StoreItem()
        {
            var trunk=VehicleCargoTrunk.Instance;if(trunk==null)return;
            bool success=trunk.StoreFromPlayer(PlayerHandsInventory.Instance,PlayerPocketInventory.Instance,out var message);
            BuildTrunk();Notify(success?"Предмет уложен в багажник.":message);
        }
        void TakeItem()
        {
            var trunk=VehicleCargoTrunk.Instance;if(trunk==null)return;
            bool success=trunk.TakeToPlayer(selectedSlot,PlayerHandsInventory.Instance,PlayerPocketInventory.Instance,out var message);
            BuildTrunk();Notify(success?"Предмет перенесён.":message);
        }
        static void BayTag(Transform parent,string value,Vector2 position,Vector2 size)
        {
            var tag=LowPolyUi.Panel(parent,"PartTag",position,size,LowPolyUi.Ink);
            var label=LowPolyUi.Label(tag.transform,"Text",value,new Vector2(8,0),size-new Vector2(16,0),18,LowPolyUi.Paper);
            label.font=LowPolyUi.HeadingFont;label.alignment=TextAnchor.MiddleCenter;
        }
        static void BayLeader(Transform parent,Vector2 from,Vector2 to)
        {
            var line=LowPolyUi.Icon(parent,"bar",(from+to)*.5f,new Vector2(Vector2.Distance(from,to),1.5f));
            line.preserveAspect=false;line.color=LowPolyUi.Paper;line.rectTransform.pivot=new Vector2(.5f,.5f);
            line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
            var dot=LowPolyUi.Icon(parent,"bar",to-new Vector2(3,-3),new Vector2(6,6));dot.color=LowPolyUi.Paper;
        }
        void BuildFuel()
        {
            Clear();
            var left=LowPolyUi.Panel(body,"FuelIllustration",Vector2.zero,new Vector2(530,510),LowPolyUi.Surface).transform;
            LowPolyUi.Icon(left,"icon_fuel_canister",new Vector2(90,-48),new Vector2(350,350));
            LowPolyUi.Label(left,"Title","БЕНЗИН / ЗАПАС ХОДА",new Vector2(50,-420),new Vector2(430,45),25);
            var right=LowPolyUi.Panel(body,"FuelCard",new Vector2(548,0),new Vector2(768,510),LowPolyUi.Surface).transform;
            LowPolyUi.Label(right,"Title","ТОПЛИВНЫЙ БАК",new Vector2(32,-26),new Vector2(700,42),28);
            volume=LowPolyUi.Label(right,"FuelVolume","",new Vector2(32,-100),new Vector2(700,70),44);
            fluidFill=MakeBar(right,new Vector2(32,-205),new Vector2(700,18),LowPolyUi.Amber);
            reason=LowPolyUi.Label(right,"Source","",new Vector2(32,-265),new Vector2(700,90),23,LowPolyUi.Muted);
            fluidButton=LowPolyUi.Button(right,"Refuel","ЗАПРАВИТЬ",new Vector2(32,-410),new Vector2(700,58),()=>Pour(true));
            RefreshStats();
        }
        static Image MakeBar(Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var bg=LowPolyUi.Panel(parent,"BarBackground",position,size,LowPolyUi.Ink);
            var fill=LowPolyUi.Panel(bg.transform,"Fill",Vector2.zero,size,color);
            fill.sprite=LowPolyUi.Sprite("bar");fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillAmount=0;return fill;
        }
        void RefreshStats()
        {
            if(stationScreen){RefreshStation();return;}
            if (currentPanel == ActivePanelType.Engine) { RefreshService(); return; }
            var state=VehicleModularState.Instance;
            if(state==null){if(fluidButton!=null)fluidButton.interactable=false;Notify("Диагностика автомобиля недоступна.");return;}
            bool isFuel=currentPanel==ActivePanelType.FuelInlet;
            float current=isFuel?state.FuelLiters:state.RadiatorWater,max=isFuel?state.MaxFuelLiters:state.MaxRadiatorWater;
            if(temperature!=null){temperature.text=$"{state.EngineTemperature:0} °C";temperature.color=state.IsOverheated?LowPolyUi.Danger:LowPolyUi.Paper;}
            if(temperatureNeedle!=null)temperatureNeedle.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(135,-135,Mathf.InverseLerp(40,130,state.EngineTemperature)));
            if(volume!=null)volume.text=$"{current:0.0} / {max:0.#} л";
            if(fluidFill!=null)fluidFill.fillAmount=max>0?current/max:0;
            bool available=CanPour(isFuel,out string message);
            if(fluidButton!=null)fluidButton.interactable=available;
            if(reason!=null){reason.text=isFuel?message:state.IsOverheated?"ПЕРЕГРЕВ\n\nПроверьте уровень охлаждающей жидкости.":"ТЕМПЕРАТУРА В НОРМЕ\n\nСледите за уровнем жидкости.";reason.color=state.IsOverheated&&!isFuel?LowPolyUi.Danger:LowPolyUi.Muted;}
            if(battery!=null){var assembly=BunkerStarterCarAssembly.Instance;battery.text=assembly==null?"Осмотр через гнездо АКБ":assembly.IsBatteryInstalled?"УСТАНОВЛЕН":"НЕ УСТАНОВЛЕН";battery.color=assembly!=null&&assembly.IsBatteryInstalled?LowPolyUi.Healthy:LowPolyUi.Amber;}
            if(!isFuel&&notice!=null)notice.text=message;
        }
        bool CanPour(bool gasoline,out string message)
        {
            var type = gasoline ? BunkerFluidType.Gasoline : BunkerFluidType.Water;
            if (VehicleServiceInventory.Space(VehicleModularState.Instance, type) <= .001f) { message = "Ёмкость заполнена"; return false; }
            if (!VehicleServiceInventory.FindFluid(type, out var source)) { message = gasoline ? "Нужна канистра бензина" : "Нужна канистра воды"; return false; }
            message = $"Доступно {source.Fluid.CurrentLiters:0.##} л · остаток сохранится";
            return true;
        }
        void Pour(bool gasoline)
        {
            var type = gasoline ? BunkerFluidType.Gasoline : BunkerFluidType.Water;
            if (!VehicleServiceInventory.FindFluid(type, out var source)) { Notify("Нужна подходящая канистра."); return; }
            VehicleServiceInventory.Pour(VehicleModularState.Instance, source.Fluid, type, out var message);
            RefreshStats(); Notify(message);
        }
        void Notify(string message) { if(notice!=null)notice.text=message; }
    }
}

