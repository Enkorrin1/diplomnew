using System.Collections.Generic;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    [DefaultExecutionOrder(-200)]
    public sealed class InventoryWindowUI : MonoBehaviour
    {
        private static InventoryWindowUI instance;
        public static InventoryWindowUI Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<InventoryWindowUI>() ?? FindFirstObjectByType<InventoryWindowUI>(FindObjectsInactive.Include);
                return instance;
            }
            private set=>instance=value;
        }
        static int closeFrame = -1;
        public static bool BlockGameplayInput => (Instance != null && Instance.IsOpen) || (VehicleDashboardPanelsUI.Instance != null && VehicleDashboardPanelsUI.Instance.IsAnyPanelOpen) || closeFrame == Time.frameCount;
        public bool IsOpen => root != null && root.activeSelf;
        public VehicleCargoTrunk Trunk { get; private set; }
        PlayerPocketInventory pocket;
        PlayerHandsInventory Hands => PlayerHandsInventory.Instance;
        GameObject root;
        RectTransform panel;
        Text notice, heading, ghost;
        SurvivalStatusBar healthStatus, foodStatus, waterStatus;
        readonly List<InventoryCellUI> cells = new List<InventoryCellUI>();
        InventoryCellUI selected;
        int amount, openFrame;
        CursorLockMode priorLock;
        bool priorVisible, openedCargo;
        void Awake() { Instance = this; pocket = GetComponent<PlayerPocketInventory>(); }
        void OnEnable() { Instance=this; pocket=GetComponent<PlayerPocketInventory>(); }
        void OnDestroy() { Close(); if (Instance == this) Instance = null; }
        void OnDisable() { Close(); }
        void Update()
        {
            bool toggle = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
            if (IsOpen)
            {
                var controller = GetComponent<GaragePlayerController>();
                if ((Time.frameCount > openFrame && (toggle || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))) || (controller != null && controller.IsMovementLocked) || (openedCargo && (Trunk == null || Vector3.Distance(transform.position,Trunk.transform.position)>8f))) { Close(); return; }
                Refresh();
                if (ghost != null) ghost.transform.position = Input.mousePosition + new Vector3(20,-15,0);
            }
            else if (toggle)
            {
                var controller = GetComponent<GaragePlayerController>();
                if (controller == null || !controller.IsMovementLocked) OpenForTrunk(null);
            }
        }
        public void OpenForTrunk(VehicleCargoTrunk trunk)
        {
            if (IsOpen) Close();
            VehicleDashboardPanelsUI.Instance?.ClosePanel();
            priorLock = Cursor.lockState; priorVisible = Cursor.visible;
            Trunk = trunk; openedCargo = trunk != null; Build(); openFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Refresh();
        }
        public void Close()
        {
            if (!IsOpen) return;
            selected = null; amount = 0; root.SetActive(false); Trunk = null;
            Cursor.lockState = priorLock; Cursor.visible = priorVisible; closeFrame = Time.frameCount;
        }
        static Image Flat(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        { var image=LowPolyUi.Rect(parent,name,position,size).gameObject.AddComponent<Image>(); image.color=color; return image; }
        void Build()
        {
            if (root != null) { root.SetActive(false); Destroy(root); }
            cells.Clear(); selected = null; amount = 0;
            if (EventSystem.current == null) new GameObject("InventoryEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            root = new GameObject("InventoryWindow",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var shade=Flat(root.transform,"Shade",Vector2.zero,Vector2.zero,new Color(0.015f,.025f,.035f,.86f)).rectTransform;
            shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.sizeDelta=Vector2.zero;
            panel=Flat(shade,"Panel",Vector2.zero,new Vector2(1380,860),LowPolyUi.Ink).rectTransform;
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            heading=LowPolyUi.Label(panel,"Title",Trunk!=null?"БАГАЖНИК":"ИНВЕНТАРЬ",new Vector2(36,-20),new Vector2(980,65),42);
            LowPolyUi.Button(panel,"Close","ESC  /  ЗАКРЫТЬ",new Vector2(1110,-32),new Vector2(230,44),Close,false);
            healthStatus=SurvivalStatusBar.Create(panel,0,new Vector2(36,-88),420);
            foodStatus=SurvivalStatusBar.Create(panel,1,new Vector2(476,-88),420);
            waterStatus=SurvivalStatusBar.Create(panel,2,new Vector2(916,-88),420);
            LowPolyUi.Label(panel,"Backpack","РЮКЗАК",new Vector2(36,-184),new Vector2(550,30),23);
            for(int i=5;i<PlayerPocketInventory.SlotCount;i++) AddCell(0,i,new Vector2(36+(i-5)%5*116,-228-(i-5)/5*116));
            LowPolyUi.Label(panel,"QuickTitle","БЫСТРЫЙ ДОСТУП  /  1–5",new Vector2(36,-588),new Vector2(590,30),21);
            for(int i=0;i<5;i++) AddCell(0,i,new Vector2(36+i*116,-628));
            Flat(panel,"Divider",new Vector2(655,-184),new Vector2(2,553),LowPolyUi.Border).raycastTarget=false;
            LowPolyUi.Label(panel,"CargoTitle",Trunk!=null?"ГРУЗОВОЙ ОТСЕК":"В РУКАХ",new Vector2(700,-184),new Vector2(620,30),23);
            if(Trunk!=null)
            {
                // Up to 30 stable cargo slots fit without hiding occupied cells.
                float pitch=Trunk.MaxSlots>15?68:116;
                int columns=Trunk.MaxSlots>15?8:5;
                for(int i=0;i<Trunk.MaxSlots;i++) AddCell(1,i,new Vector2(700+i%columns*pitch,-228-i/columns*pitch),pitch-8);
                LowPolyUi.Label(panel,"HandsTitle","В РУКАХ",new Vector2(700,-588),new Vector2(520,30),21);
                AddCell(2,0,new Vector2(700,-628));
                LowPolyUi.Label(panel,"HandsHelp","Крупные предметы\nпереносите через руки",new Vector2(828,-647),new Vector2(460,70),20,LowPolyUi.Muted);
            }
            else
            {
                AddCell(2,0,new Vector2(700,-228));
                LowPolyUi.Label(panel,"Help","Мелкие предметы хранятся в рюкзаке.\nПеренесите нужные вещи в быстрые слоты.\n\nДля обмена с машиной откройте багажник:\nнаведите на него прицел и нажмите E.",new Vector2(700,-366),new Vector2(610,220),23,LowPolyUi.Muted);
            }
            notice=LowPolyUi.Label(panel,"Notice","",new Vector2(36,-749),new Vector2(1300,30),20,LowPolyUi.Amber);
            LowPolyUi.Label(panel,"Controls","ЛКМ / перетаскивание — перенести     Shift + ЛКМ — быстро     ПКМ — половина / по одному",new Vector2(36,-794),new Vector2(1310,30),18,LowPolyUi.Muted);
            ghost=LowPolyUi.Label(root.transform,"Carried","",Vector2.zero,new Vector2(350,80),22,LowPolyUi.Amber);
        }
        void AddCell(int area,int index,Vector2 position,float size=108)
        {
            var rt=Flat(panel,"Slot_"+area+"_"+index,position,new Vector2(size,size),LowPolyUi.Surface).rectTransform;
            var cell=rt.gameObject.AddComponent<InventoryCellUI>();cell.Initialize(this,area,index,size);cells.Add(cell);
        }
        public PocketSlotData Read(int area,int index)
        {
            if(area==0)return pocket.GetSlot(index);
            if(area==1)return Trunk!=null?Trunk.GetSlot(index):PocketSlotData.Empty;
            return Hands!=null&&Hands.HasItem?InventoryStackOps.FromObject(Hands.HeldGameObject,Hands.HeldWorldScale):PocketSlotData.Empty;
        }
        void Write(int area,int index,PocketSlotData value)
        {if(area==0)pocket.SetGridSlot(index,value);else Trunk.SetGridSlot(index,value);}
        bool Accepts(int area,PocketSlotData value)
        {return value.IsEmpty || (area==0?InventoryStackOps.FitsPocket(value):area==1 && (value.WorldObject!=null || value.legacyType!=BunkerAssemblyItemType.None));}
        public bool Transfer(int fromArea,int from,int toArea,int to,int requested,bool swap=true)
        {
            if(fromArea==toArea&&from==to)return false;
            var source=Read(fromArea,from);var target=Read(toArea,to);
            if(source.IsEmpty)return false;
            if(toArea==2)
            {
                if(fromArea!=1 || Hands==null || Hands.HasItem || InventoryStackOps.FitsPocket(source))return false;
                return Trunk.TakeToPlayer(from,Hands,pocket,out _);
            }
            if(!Accepts(toArea,source)) {notice.text="Крупный предмет — в руки или багажник. Ключи остаются у вас.";return false;}
            if(fromArea==2)
            {
                if(!target.IsEmpty)return false;
                Hands.DropItem(forStorage:true);Write(toArea,to,source);return true;
            }
            int moved=InventoryStackOps.Move(ref source,ref target,requested);
            if(moved==0)
            {
                source=Read(fromArea,from);
                if(!swap || requested<source.count || InventoryStackOps.Matches(source,target) || !Accepts(fromArea,target))return false;
                var temp=source;source=target;target=temp;
            }
            Write(fromArea,from,source);Write(toArea,to,target);return true;
        }
        public void QuickTransfer(InventoryCellUI cell)
        {
            selected=null;
            if(cell.Area==1&&!InventoryStackOps.FitsPocket(Read(1,cell.Index))) {Transfer(1,cell.Index,2,0,1);Refresh();return;}
            int destination=Trunk!=null?(cell.Area==1?0:1):0;
            int capacity=destination==1?Trunk.MaxSlots:PlayerPocketInventory.SlotCount;
            for(int pass=0;pass<2;pass++)for(int i=0;i<capacity;i++)
            {
                if(Trunk==null && cell.Area==0 && (cell.Index<5)==(i<5))continue;
                var source=Read(cell.Area,cell.Index);if(source.IsEmpty)break;
                var target=Read(destination,i);
                if(pass==0?!InventoryStackOps.Matches(source,target):!target.IsEmpty)continue;
                Transfer(cell.Area,cell.Index,destination,i,source.count,false);
            }
            Refresh();
        }
        public void Click(InventoryCellUI cell,bool right,bool shift)
        {
            if(shift){QuickTransfer(cell);return;}
            if(selected==null)
            {
                var data=Read(cell.Area,cell.Index);if(data.IsEmpty)return;
                selected=cell;amount=right?(data.count+1)/2:data.count;notice.text=data.displayName;
            }
            else if(selected==cell){selected=null;amount=0;}
            else
            {
                int before=Read(selected.Area,selected.Index).count;
                if(Transfer(selected.Area,selected.Index,cell.Area,cell.Index,right?1:amount))
                {
                    int remaining=Read(selected.Area,selected.Index).count;
                    int moved=before-remaining;amount-=moved>0?moved:amount;
                    if(amount<=0||remaining<=0){selected=null;amount=0;}
                }
            }
            Refresh();
        }
        public void BeginDrag(InventoryCellUI cell) { selected=null; Click(cell,false,false); }
        public void DropOn(InventoryCellUI cell) { if(selected!=null&&selected!=cell)Click(cell,false,false); }
        public void EndDrag() { selected=null;amount=0;Refresh(); }
        public void Hover(InventoryCellUI cell) { if(selected==null) { var s=Read(cell.Area,cell.Index);notice.text=s.IsEmpty?"":s.displayName+(s.count>1?" ×"+s.count:"")+(s.WorldObject!=null?"  "+(s.WorldObject.GetComponent<GarageItemFunction>()?.Status??""):""); } }
        void Refresh()
        {
            var needs=PlayerFieldNeeds.For(GetComponent<GaragePlayerController>());
            if(needs!=null){healthStatus?.Render(needs.Health);foodStatus?.Render(needs.Food);waterStatus?.Render(needs.Water);}
            foreach(var cell in cells)cell.Render(Read(cell.Area,cell.Index),cell==selected);
            if(ghost!=null)ghost.text=selected!=null?Read(selected.Area,selected.Index).displayName+" ×"+amount:"";
        }
    }
}


