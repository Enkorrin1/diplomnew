using System.Collections.Generic;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;
namespace RogueDrive.UI
{
    public sealed partial class VehicleDashboardPanelsUI
    {
        readonly List<VehicleServiceCellUI> serviceSources = new List<VehicleServiceCellUI>();
        readonly Dictionary<VehicleServiceSlot, Text> serviceStatus = new Dictionary<VehicleServiceSlot, Text>();
        ServiceItemAddress? selectedService, armedScrewdriver;
        GameObject armedTool;
        Text serviceGhost, serviceTemperature, sourcePageLabel;
        Image waterBar, oilBar;
        int sourceArea, sourcePage;
        float noticeUntil;
        void ClearService()
        {
            workshopScreen=false;
            serviceSources.Clear(); serviceStatus.Clear(); selectedService = null; armedScrewdriver = null; armedTool = null;
            if (serviceGhost != null) Destroy(serviceGhost.gameObject);
            serviceGhost = null; serviceTemperature = null;
        }
        void BuildFieldService()
        {
            Clear();
            subtitle.text = "ОБСЛУЖИВАНИЕ / ДЕТАЛЬ В СЛОТ → ИНСТРУМЕНТ → ЛКМ";
            serviceTemperature = LowPolyUi.Label(body, "ServiceTemperature", "", Vector2.zero, new Vector2(804,36), 21, LowPolyUi.Muted);
            AddServiceSlot(VehicleServiceSlot.Engine, "ДВИГАТЕЛЬ", new Vector2(0,-48), new Vector2(252,226), "engine_bay");
            AddServiceSlot(VehicleServiceSlot.Radiator, "РАДИАТОР", new Vector2(270,-48), new Vector2(252,226), null);
            AddServiceSlot(VehicleServiceSlot.Battery, "АККУМУЛЯТОР", new Vector2(540,-48), new Vector2(252,226), "icon_battery");
            var water = AddServiceSlot(VehicleServiceSlot.Water, "ВОДА / ОХЛАЖДЕНИЕ", new Vector2(0,-292), new Vector2(387,164), null);
            waterBar = MakeBar(water.transform,new Vector2(20,-83),new Vector2(347,8),LowPolyUi.Water);
            var oil = AddServiceSlot(VehicleServiceSlot.Oil, "МОТОРНОЕ МАСЛО", new Vector2(405,-292), new Vector2(387,164), null);
            oilBar = MakeBar(oil.transform,new Vector2(20,-83),new Vector2(347,8),LowPolyUi.Amber);
            LowPolyUi.Label(body,"Hint","Детали и жидкости — перетащить из ячеек справа",new Vector2(0,-478),new Vector2(796,32),18,LowPolyUi.Muted);
            var inventory = LowPolyUi.Panel(body,"ServiceInventory",new Vector2(812,0),new Vector2(504,510),LowPolyUi.Ink).transform;
            LowPolyUi.Label(inventory,"Heading","ПРЕДМЕТЫ",new Vector2(16,0),new Vector2(460,34),24);
            string[] names = { "РЮКЗАК", "БАГАЖНИК", "РУКИ" };
            for(int i=0;i<3;i++)
            {
                int area=i;
                LowPolyUi.Button(inventory,"Source_"+i,names[i],new Vector2(16+i*160,-44),new Vector2(152,36),()=>{sourceArea=area;sourcePage=0;RefreshService();},false);
            }
            for(int i=0;i<12;i++)
            {
                var rect=LowPolyUi.Panel(inventory,"ServiceItem_"+i,new Vector2(16+i%4*120,-94-i/4*114),new Vector2(110,104),LowPolyUi.Surface);
                rect.raycastTarget=true;
                var cell=rect.gameObject.AddComponent<VehicleServiceCellUI>();cell.Owner=this;cell.IsSource=true;serviceSources.Add(cell);
                LowPolyUi.Icon(rect.transform,"icon_scrap",new Vector2(28,-6),new Vector2(54,54));
                var label=LowPolyUi.Label(rect.transform,"ItemName","",new Vector2(6,-61),new Vector2(98,38),13);label.alignment=TextAnchor.MiddleCenter;
            }
            LowPolyUi.Button(inventory,"Previous","←",new Vector2(16,-453),new Vector2(60,40),()=>{sourcePage=Mathf.Max(0,sourcePage-1);RefreshService();},false);
            sourcePageLabel=LowPolyUi.Label(inventory,"Page","",new Vector2(90,-456),new Vector2(302,34),18,LowPolyUi.Muted);
            sourcePageLabel.alignment=TextAnchor.MiddleCenter;
            LowPolyUi.Button(inventory,"Next","→",new Vector2(428,-453),new Vector2(60,40),()=>{sourcePage++;RefreshService();},false);
            serviceGhost=LowPolyUi.Label(canvas.transform,"ServiceDragGhost","",Vector2.zero,new Vector2(390,70),22,LowPolyUi.Amber);
            serviceGhost.raycastTarget=false;
            if(EventSystem.current==null)new GameObject("ServiceEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            noticeUntil=0; RefreshService();
            LowPolyUi.Button(body,"BackToWorkshop","← МАСТЕРСКАЯ",new Vector2(0,0),new Vector2(300,36),BuildEngine,false);
            serviceTemperature.rectTransform.anchoredPosition=new Vector2(310,0);
            serviceTemperature.rectTransform.sizeDelta=new Vector2(480,36);
        }
        Image AddServiceSlot(VehicleServiceSlot slot,string heading,Vector2 position,Vector2 size,string icon)
        {
            var panel=LowPolyUi.Panel(body,"ServiceSlot_"+slot,position,size,LowPolyUi.Surface);
            panel.raycastTarget=true;
            var cell=panel.gameObject.AddComponent<VehicleServiceCellUI>();cell.Owner=this;cell.Slot=slot;
            LowPolyUi.Label(panel.transform,"Heading",heading,new Vector2(18,-12),new Vector2(size.x-36,32),21);
            bool fluid=slot==VehicleServiceSlot.Water||slot==VehicleServiceSlot.Oil;
            if(icon!=null)LowPolyUi.Icon(panel.transform,icon,new Vector2((size.x-126)/2,-42),new Vector2(126,112));
            if(slot==VehicleServiceSlot.Radiator)
            {
                // Simple radiator silhouette instead of reusing the whole engine illustration.
                for(int i=0;i<12;i++)LowPolyUi.Panel(panel.transform,"Fin_"+i,new Vector2(52+i*12,-67),new Vector2(6,73),LowPolyUi.Muted).raycastTarget=false;
                LowPolyUi.Panel(panel.transform,"TopTank",new Vector2(46,-56),new Vector2(158,12),LowPolyUi.Water).raycastTarget=false;
                LowPolyUi.Panel(panel.transform,"BottomTank",new Vector2(46,-138),new Vector2(158,12),LowPolyUi.Muted).raycastTarget=false;
            }
            var status=LowPolyUi.Label(panel.transform,"Status","",new Vector2(18,fluid?-48:-164),new Vector2(size.x-36,fluid?36:56),fluid?25:17,LowPolyUi.Amber);
            serviceStatus[slot]=status;
            if(fluid)LowPolyUi.Label(panel.transform,"DropHint","Перетащите канистру сюда",new Vector2(18,-111),new Vector2(size.x-36,35),18,LowPolyUi.Muted);
            return panel;
        }
        void LateUpdate()
        {
            if(serviceGhost!=null){serviceGhost.transform.position=Input.mousePosition+new Vector3(18,-18);serviceGhost.text=selectedService.HasValue?selectedService.Value.Read().displayName:"";}
        }
        public void SelectServiceItem(ServiceItemAddress address)
        {
            if(address.Read().IsEmpty)return;
            selectedService=address;
        }
        public void CancelServiceDrag() { selectedService=null; if(serviceGhost!=null)serviceGhost.text=""; }
        public void DropServiceItem(VehicleServiceSlot slot)
        {
            if(!selectedService.HasValue)return;
            var address=selectedService.Value;
            var obj=address.Read().WorldObject;
            var state=VehicleServiceState.For(VehicleModularState.Instance);
            if(state==null)return;
            if(slot==VehicleServiceSlot.Battery && obj!=null && obj.GetComponent<GarageItemFunction>()?.Kind==GarageItemFunction.ItemKind.Screwdriver)
            {
                if(state.Battery==null||!state.Battery.HasPlacedBattery)ServiceNotice("Сначала установите аккумулятор.");
                else if(state.Battery.IsInstalled)ServiceNotice("Клеммы уже закреплены.");
                else {armedScrewdriver=address;armedTool=obj;ServiceNotice("Отвёртка выбрана. Нажмите ЛКМ на аккумулятор, чтобы затянуть клеммы.");}
            }
            else {state.TryApply(address,slot,out var message);ServiceNotice(message);}
            CancelServiceDrag();RefreshService();
        }
        public void ClickServiceSlot(VehicleServiceSlot slot)
        {
            if(selectedService.HasValue){DropServiceItem(slot);return;}
            if(slot==VehicleServiceSlot.Battery && armedScrewdriver.HasValue)
            {
                var item=armedScrewdriver.Value.Read().WorldObject;
                if(item==null||item!=armedTool||item.GetComponent<GarageItemFunction>()?.Kind!=GarageItemFunction.ItemKind.Screwdriver)
                    ServiceNotice("Отвёртка больше не доступна. Выберите её снова.");
                else
                {
                    bool secured=VehicleServiceState.For(VehicleModularState.Instance)?.Battery?.SecureBattery(item.GetComponent<GarageItemFunction>())??false;
                    ServiceNotice(secured?"Клеммы затянуты. Аккумулятор подключён.":"Аккумулятор уже закреплён или отсутствует.");
                }
                armedScrewdriver=null;armedTool=null;RefreshService();return;
            }
            ServiceNotice(slot==VehicleServiceSlot.Engine||slot==VehicleServiceSlot.Radiator?"Перетащите запчасть из багажника. Замена — на СТО.":"Перетащите нужный предмет из ячеек справа.");
        }
        void ServiceNotice(string message){noticeUntil=Time.unscaledTime+6f;Notify(message);}
        void RefreshService()
        {
            if(workshopScreen){RefreshWorkshop();return;}
            var car=VehicleModularState.Instance;if(car==null||serviceTemperature==null)return;
            var state=VehicleServiceState.For(car);
            serviceTemperature.text=$"{car.EngineTemperature:0} °C  /  "+(car.IsEngineSeized?"ДВИГАТЕЛЬ ЗАКЛИНИЛ":car.IsOverheated?"ПЕРЕГРЕВ":car.OilPressure<.45f?"НЕТ ДАВЛЕНИЯ МАСЛА":"ОБСЛУЖИВАНИЕ");
            serviceTemperature.color=car.IsEngineSeized||car.IsOverheated||car.OilPressure<.45f?LowPolyUi.Danger:LowPolyUi.Muted;
            serviceStatus[VehicleServiceSlot.Engine].text=$"СОСТОЯНИЕ {car.EngineIntegrity:P0}\n"+(car.IsEngineSeized?"ЗАКЛИНИЛ / ЗАМЕНА НА СТО":"Замена / ремонт на СТО");
            serviceStatus[VehicleServiceSlot.Radiator].text=$"СОСТОЯНИЕ {car.RadiatorIntegrity:P0}\n"+(car.WaterLeakPerSecond>0?$"Течь: {car.WaterLeakPerSecond*60:0.00} л/мин":"Герметичен");
            var batterySlot=state.Battery;
            serviceStatus[VehicleServiceSlot.Battery].text=batterySlot==null?"ГНЕЗДО НЕДОСТУПНО":batterySlot.IsInstalled?"ЗАКРЕПЛЁН / 12 V":batterySlot.HasPlacedBattery?(armedScrewdriver.HasValue?"ЛКМ — ЗАТЯНУТЬ КЛЕММЫ":"НЕ ЗАКРЕПЛЁН\nПеретащите отвёртку"):"ПУСТО\nПеретащите аккумулятор";
            serviceStatus[VehicleServiceSlot.Battery].color=batterySlot!=null&&batterySlot.IsInstalled?LowPolyUi.Healthy:LowPolyUi.Amber;
            serviceStatus[VehicleServiceSlot.Water].text=$"{car.RadiatorWater:0.0} / {car.MaxRadiatorWater:0.#} л";
            serviceStatus[VehicleServiceSlot.Oil].text=$"{car.EngineOil:0.0} / {car.MaxEngineOil:0.#} л";
            serviceStatus[VehicleServiceSlot.Oil].transform.parent.Find("DropHint").GetComponent<Text>().text=
                car.OilLeakPerSecond>0?$"Течь поддона: {car.OilLeakPerSecond*60:0.00} л/мин":$"Расход: {car.OilUsePerSecond*60:0.00} л/мин · долив из канистры";
            waterBar.fillAmount=car.RadiatorWater/car.MaxRadiatorWater;oilBar.fillAmount=car.EngineOil/car.MaxEngineOil;
            int capacity=sourceArea==0?PlayerPocketInventory.SlotCount:sourceArea==1?(VehicleCargoTrunk.Instance?.MaxSlots??0):1;
            int pages=Mathf.Max(1,Mathf.CeilToInt(capacity/12f));sourcePage=Mathf.Clamp(sourcePage,0,pages-1);
            sourcePageLabel.text=(sourceArea==0?"РЮКЗАК":sourceArea==1?"БАГАЖНИК":"В РУКАХ")+$"  {sourcePage+1} / {pages}";
            for(int i=0;i<serviceSources.Count;i++)
            {
                var cell=serviceSources[i];int index=sourcePage*12+i;cell.gameObject.SetActive(index<capacity);cell.Address=new ServiceItemAddress(sourceArea,index);
                var item=cell.Address.Read();
                var itemIcon=cell.transform.GetChild(0).GetComponent<Image>();
                itemIcon.enabled=!item.IsEmpty;itemIcon.sprite=item.icon!=null?item.icon:LowPolyUi.Sprite(ItemIcon(item.legacyType));
                var label=cell.GetComponentInChildren<Text>();var fluid=cell.Address.Fluid;
                itemIcon.color=fluid!=null&&fluid.FluidType==BunkerFluidType.EngineOil?LowPolyUi.Amber:Color.white;
                label.text=item.IsEmpty?"—":fluid!=null?fluid.GetFluidName()+$" {fluid.CurrentLiters:0.#} л":item.displayName;
            }
            if(Time.unscaledTime>noticeUntil)Notify("Деталь → гнездо · Отвёртка → аккумулятор → ЛКМ · Канистра → вода или масло");
        }
    }
}
