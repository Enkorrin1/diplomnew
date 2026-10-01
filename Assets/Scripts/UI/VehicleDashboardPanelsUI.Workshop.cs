using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Track;
using UnityEngine;
using UnityEngine.EventSystems;
using Text=UnityEngine.UI.Text;
using Image=UnityEngine.UI.Image;
using Button=UnityEngine.UI.Button;

namespace RogueDrive.UI
{
    public sealed partial class VehicleDashboardPanelsUI
    {
        bool workshopScreen, shopSource;
        WorkshopSection workshopSection;
        WorkshopSlot workshopSlot;
        ServiceItemAddress? previewSource;
        WorkshopPartData previewPart;
        string shopSelection;
        Text workshopContext, workshopQuote, workshopComparison, workshopBalance, workshopPager;
        Image workProgress;
        Button installButton,buyButton,sellButton,repairButton;
        readonly Dictionary<WorkshopSlot,Text> workshopLabels=new Dictionary<WorkshopSlot,Text>();
        readonly List<Text> workshopItems=new List<Text>();
        readonly List<Button> workshopItemButtons=new List<Button>();
        const int WorkshopPageSize=9;
        readonly Dictionary<WorkshopSlot,Image> slotAccents=new Dictionary<WorkshopSlot,Image>();
        readonly Dictionary<WorkshopSlot,Image> slotLeaders=new Dictionary<WorkshopSlot,Image>();
        readonly List<Image> itemPictures=new List<Image>();
        readonly List<Text> itemDetails=new List<Text>();
        readonly List<Button> sourceButtons=new List<Button>();
        void WorkshopFrame(bool expanded)
        {
            var panel=(RectTransform)body.parent;
            panel.sizeDelta=new Vector2(expanded?1500:1380,expanded?848:740);
            body.sizeDelta=new Vector2(expanded?1436:1316,expanded?640:510);
            notice.rectTransform.anchoredPosition=new Vector2(36,expanded?-786:-659);
            notice.rectTransform.sizeDelta=new Vector2(expanded?1428:1300,44);
            panel.Find("Close").GetComponent<RectTransform>().anchoredPosition=new Vector2(expanded?1250:1130,-36);
            panel.Find("HeaderRule").GetComponent<RectTransform>().sizeDelta=new Vector2(expanded?1420:1300,2);
        }
        static Image Flat(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var r=LowPolyUi.Rect(parent,name,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        static Image Art(Transform parent,Sprite sprite,Vector2 pos,Vector2 size)
        {
            var image=Flat(parent,"Illustration",pos,size,Color.white);image.sprite=sprite;image.preserveAspect=true;return image;
        }
        void BuildEngine()
        {
            Clear();WorkshopFrame(true);workshopScreen=true;workshopLabels.Clear();workshopItems.Clear();workshopItemButtons.Clear();slotAccents.Clear();slotLeaders.Clear();itemPictures.Clear();itemDetails.Clear();sourceButtons.Clear();
            title.text="МАСТЕРСКАЯ";subtitle.text="01  ВЫБЕРИТЕ УЗЕЛ     /     02  СРАВНИТЕ ДЕТАЛЬ     /     03  ОПЛАТИТЕ УСТАНОВКУ";
            string[] tabs={"ДВИГАТЕЛЬ","ПОДВЕСКА","КУЗОВ"};int[] tabIcons={0,5,8};
            for(int i=0;i<3;i++)
            {
                int tab=i;
                var b=LowPolyUi.Button(body,"WorkshopTab_"+i,"",new Vector2(i*322,0),new Vector2(310,60),()=>
                {workshopSection=(WorkshopSection)tab;workshopSlot=Slots().First();previewPart=null;previewSource=null;shopSelection=null;sourcePage=0;BuildEngine();},i==(int)workshopSection);
                Art(b.transform,WorkshopArt.Part(tabIcons[i]),new Vector2(8,-5),new Vector2(50,50));
                LowPolyUi.Label(b.transform,"Section",tabs[i],new Vector2(70,0),new Vector2(230,60),25,i==(int)workshopSection?LowPolyUi.Ink:LowPolyUi.Paper).alignment=TextAnchor.MiddleLeft;
            }
            workshopContext=LowPolyUi.Label(body,"WorkshopContext","",new Vector2(0,-66),new Vector2(950,30),18,LowPolyUi.Muted);
            LowPolyUi.Button(body,"StationServices","СЕРВИС СТО",new Vector2(742,-66),new Vector2(212,30),ShowStationPanel,false);
            workshopContext.rectTransform.sizeDelta=new Vector2(730,30);
            var diagram=Flat(body,"AssemblyDiagram",new Vector2(228,-103),new Vector2(498,322),new Color32(24,39,45,255));
            Art(diagram.transform,WorkshopArt.Section((int)workshopSection),new Vector2(58,0),new Vector2(382,322));
            LowPolyUi.Label(diagram.transform,"Caption","СХЕМА УЗЛОВ · ВЫБЕРИТЕ МЕТКУ",new Vector2(40,-300),new Vector2(420,22),14,LowPolyUi.Muted).alignment=TextAnchor.MiddleCenter;
            var slots=Slots();
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];bool left=i<(slots.Length+1)/2;int row=left?i:i-(slots.Length+1)/2;
                float x=left?0:740,y=-103-row*81;
                var card=Flat(body,"WorkshopSlot_"+slot,new Vector2(x,y),new Vector2(214,73),LowPolyUi.Ink);card.raycastTarget=true;
                var target=card.gameObject.AddComponent<WorkshopSlotUI>();target.owner=this;target.slot=slot;
                slotAccents[slot]=Flat(card.transform,"Selection",Vector2.zero,new Vector2(3,73),LowPolyUi.Border);
                Art(card.transform,WorkshopArt.Part((int)WorkshopCatalog.Kind(slot)),new Vector2(9,-10),new Vector2(52,52));
                string name=WorkshopCatalog.SlotName(slot).ToUpperInvariant().Replace("ПЕРЕДНИЕ","ПЕРЕД.").Replace("ЗАДНИЕ","ЗАД.").Replace("АМОРТИЗАТОРЫ","АМОРТИЗ.").Replace("БАГАЖНИК НА КРЫШЕ","БАГАЖНИК / КРЫША");
                if(slot==WorkshopSlot.Filter)name="ФИЛЬТР";if(slot==WorkshopSlot.Pistons)name="ПОРШНИ";
                var heading=LowPolyUi.Label(card.transform,"SlotName",$"{i+1:00}  {name}",new Vector2(68,-3),new Vector2(140,38),14);
                heading.font=LowPolyUi.HeadingFont;
                workshopLabels[slot]=LowPolyUi.Label(card.transform,"Part","",new Vector2(68,-42),new Vector2(140,26),14,LowPolyUi.Muted);
                // Numbered callouts link the schematic to the matching drag target.
                Vector2 point=DiagramPoint(i);
                var from=new Vector2(left?214:740,y-36);
                var leader=Flat(body,"SelectedLeader_"+slot,(from+point)*.5f,new Vector2(Vector2.Distance(from,point),2),LowPolyUi.Amber);
                leader.rectTransform.pivot=new Vector2(.5f,.5f);leader.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(point.y-from.y,point.x-from.x)*Mathf.Rad2Deg);slotLeaders[slot]=leader;
                var marker=LowPolyUi.Button(body,"DiagramSlot_"+slot,(i+1).ToString("00"),point+new Vector2(-16,16),new Vector2(32,32),()=>PreviewWorkshopSlot(slot),false);
                var number=marker.GetComponentInChildren<Text>();number.rectTransform.anchoredPosition=new Vector2(2,-2);number.rectTransform.sizeDelta=new Vector2(28,28);number.fontSize=16;
                var drop=marker.gameObject.AddComponent<WorkshopSlotUI>();drop.owner=this;drop.slot=slot;
            }
            var compare=Flat(body,"Comparison",new Vector2(0,-436),new Vector2(954,104),LowPolyUi.Ink);
            workshopComparison=LowPolyUi.Label(compare.transform,"Stats","",new Vector2(16,-8),new Vector2(922,90),18);
            workshopQuote=LowPolyUi.Label(body,"Quote","",new Vector2(0,-546),new Vector2(954,48),17,LowPolyUi.Amber);
            installButton=LowPolyUi.Button(body,"Install","УСТАНОВИТЬ",new Vector2(0,-600),new Vector2(252,42),StartWorkshopJob,true);
            LowPolyUi.Button(body,"EngineSwitch","ЗАЖИГАНИЕ",new Vector2(264,-600),new Vector2(200,42),()=>{var w=Workshop();if(w!=null){w.ToggleEngine(out var msg);ServiceNotice(msg);}RefreshWorkshop();},false);
            LowPolyUi.Button(body,"Fluids","ЖИДКОСТИ / АКБ",new Vector2(476,-600),new Vector2(238,42),()=>{workshopScreen=false;BuildFieldService();},false);
            LowPolyUi.Button(body,"CancelJob","ОТМЕНА РАБОТЫ",new Vector2(726,-600),new Vector2(228,42),()=>{Workshop()?.CancelJob();RefreshWorkshop();},false);
            workProgress=MakeBar(body,new Vector2(0,-648),new Vector2(954,4),LowPolyUi.Amber);
            var inventory=Flat(body,"WorkshopInventory",new Vector2(976,0),new Vector2(460,650),LowPolyUi.Ink).transform;
            workshopBalance=LowPolyUi.Label(inventory,"Balance","",new Vector2(14,-6),new Vector2(434,38),26,LowPolyUi.Amber);
            string[] sources={"РЮКЗАК","БАГАЖНИК","РУКИ","МАГАЗИН"};
            for(int i=0;i<4;i++)
            {
                int area=i;
                sourceButtons.Add(LowPolyUi.Button(inventory,"WorkshopSource_"+i,sources[i],new Vector2(10+i*111,-51),new Vector2(108,38),()=>{shopSource=area==3;sourceArea=Mathf.Min(area,2);sourcePage=0;previewSource=null;previewPart=null;shopSelection=null;CancelServiceDrag();RefreshWorkshop();},false));
            }
            for(int i=0;i<WorkshopPageSize;i++)
            {
                int index=i;
                var button=LowPolyUi.Button(inventory,"WorkshopItem_"+i,"",new Vector2(10+i%3*149,-102-i/3*146),new Vector2(142,138),()=>ChooseWorkshopItem(index),false);
                var drag=button.gameObject.AddComponent<VehicleServiceCellUI>();drag.Owner=this;drag.IsSource=true;
                var label=button.GetComponentInChildren<Text>();label.fontSize=16;label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(0,1);label.rectTransform.anchoredPosition=new Vector2(6,-68);label.rectTransform.sizeDelta=new Vector2(130,43);
                itemPictures.Add(Art(button.transform,null,new Vector2(38,-5),new Vector2(66,62)));
                var detail=LowPolyUi.Label(button.transform,"PriceCondition","",new Vector2(6,-112),new Vector2(130,21),14,LowPolyUi.Amber);detail.alignment=TextAnchor.MiddleCenter;itemDetails.Add(detail);
                workshopItems.Add(label);workshopItemButtons.Add(button);
            }
            LowPolyUi.Button(inventory,"Prev","←",new Vector2(10,-552),new Vector2(48,34),()=>{sourcePage=Mathf.Max(0,sourcePage-1);RefreshWorkshop();},false);
            workshopPager=LowPolyUi.Label(inventory,"Page","",new Vector2(64,-552),new Vector2(330,32),16,LowPolyUi.Muted);workshopPager.alignment=TextAnchor.MiddleCenter;
            LowPolyUi.Button(inventory,"Next","→",new Vector2(402,-552),new Vector2(48,34),()=>{sourcePage++;RefreshWorkshop();},false);
            buyButton=LowPolyUi.Button(inventory,"Buy","КУПИТЬ",new Vector2(10,-600),new Vector2(142,42),()=>{var w=Workshop();if(w!=null){w.Buy(shopSelection,out var msg);ServiceNotice(msg);}RefreshWorkshop();},true);
            sellButton=LowPolyUi.Button(inventory,"Sell","ПРОДАТЬ",new Vector2(159,-600),new Vector2(142,42),()=>{if(previewSource.HasValue){Workshop().Sell(previewSource.Value,out var msg);ServiceNotice(msg);previewPart=null;previewSource=null;}RefreshWorkshop();},false);
            repairButton=LowPolyUi.Button(inventory,"Repair","РЕМОНТ",new Vector2(308,-600),new Vector2(142,42),()=>{if(previewSource.HasValue){Workshop().BeginRepair(previewSource.Value,out var msg);ServiceNotice(msg);}RefreshWorkshop();},false);
            serviceGhost=LowPolyUi.Label(canvas.transform,"ServiceDragGhost","",Vector2.zero,new Vector2(390,70),22,LowPolyUi.Amber);serviceGhost.raycastTarget=false;
            if(EventSystem.current==null)new GameObject("ServiceEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            RefreshWorkshop();
        }
        Vector2 DiagramPoint(int index)
        {
            Vector2[][] points={
                new[]{new Vector2(500,-240),new Vector2(430,-178),new Vector2(528,-201),new Vector2(572,-262),new Vector2(408,-305)},
                new[]{new Vector2(410,-280),new Vector2(525,-210),new Vector2(438,-310),new Vector2(580,-190),new Vector2(330,-315),new Vector2(475,-350),new Vector2(443,-190),new Vector2(562,-235)},
                new[]{new Vector2(401,-306),new Vector2(471,-323),new Vector2(540,-272),new Vector2(484,-195),new Vector2(421,-212)}
            };
            return points[(int)workshopSection][index];
        }
        VehicleWorkshop Workshop()=>VehicleWorkshop.For(VehicleModularState.Instance);
        WorkshopSlot[] Slots()=>Enum.GetValues(typeof(WorkshopSlot)).Cast<WorkshopSlot>().Where(s=>WorkshopCatalog.Section(s)==workshopSection).ToArray();
        WorkshopPartDefinition[] ShopItems()=>WorkshopCatalog.All.Where(d=>Slots().Any(s=>WorkshopCatalog.Kind(s)==d.kind)&&d.tier<=(Workshop()?.Zone?.StockTier??0)).ToArray();
        void ChooseWorkshopItem(int cell)
        {
            if(shopSource)
            {
                var stock=ShopItems();int index=sourcePage*WorkshopPageSize+cell;if(index>=stock.Length)return;
                shopSelection=stock[index].id;previewPart=WorkshopCatalog.New(shopSelection);previewSource=null;CancelServiceDrag();
            }
            else
            {
                previewSource=new ServiceItemAddress(sourceArea,sourcePage*WorkshopPageSize+cell);previewPart=WorkshopPartItem.Read(previewSource.Value.Read());shopSelection=null;
            }
            if(previewPart!=null)
            {
                var kind=WorkshopCatalog.Get(previewPart.definitionId).kind;
                if(WorkshopCatalog.Kind(workshopSlot)!=kind)
                {
                    workshopSlot=Enum.GetValues(typeof(WorkshopSlot)).Cast<WorkshopSlot>().First(s=>WorkshopCatalog.Kind(s)==kind);
                    var section=WorkshopCatalog.Section(workshopSlot);
                    if(section!=workshopSection){workshopSection=section;BuildEngine();return;}
                }
            }
            RefreshWorkshop();
        }
        public void PreviewWorkshopSlot(WorkshopSlot slot)
        {
            workshopSlot=slot;
            if(selectedService.HasValue){previewSource=selectedService;previewPart=WorkshopPartItem.Read(selectedService.Value.Read());shopSelection=null;CancelServiceDrag();}
            RefreshWorkshop();
        }
        void StartWorkshopJob()
        {
            if(previewSource.HasValue){Workshop().BeginInstall(previewSource.Value,workshopSlot,out var msg);ServiceNotice(msg);}RefreshWorkshop();
        }
        void RefreshWorkshop()
        {
            var w=Workshop();if(w==null||workshopContext==null)return;
            if(VehicleModularState.Instance.GetComponent<ArcadeCarController>()?.Run?.IsGameOver??false){ClosePanel();return;}
            var station=w.Zone;
            var storm=CreepingStormBarrier.Instance;
            string danger=station!=null&&station.Safe?"БЕЗОПАСНО":storm!=null?$"БУРЯ: {Mathf.Max(0,storm.DistanceToCar):0} М":"БУРЯ НЕ ОСТАНАВЛИВАЕТСЯ";
            workshopContext.text=$"{(station!=null?station.DisplayName:"ПОЛЕВОЙ СЕРВИС")} · {danger}";
            workshopContext.color=station!=null&&station.Safe?LowPolyUi.Healthy:LowPolyUi.Amber;
            workshopBalance.text=$"{w.Coins} МОНЕТ  /  {(shopSource?"КАТАЛОГ":"ПРЕДМЕТЫ")}";
            foreach(var pair in workshopLabels)
            {
                var p=w.Installed(pair.Key);var d=WorkshopCatalog.Get(p?.definitionId);
                pair.Value.text=d==null?"+ УСТАНОВИТЬ":$"УР. {d.tier}  ·  {p.condition:P0}";
                pair.Value.resizeTextForBestFit=true;pair.Value.resizeTextMinSize=12;pair.Value.resizeTextMaxSize=15;
                pair.Value.color=pair.Key==workshopSlot?LowPolyUi.Amber:LowPolyUi.Muted; slotAccents[pair.Key].color=pair.Key==workshopSlot?LowPolyUi.Amber:LowPolyUi.Border;
                slotLeaders[pair.Key].enabled=pair.Key==workshopSlot;
                var marker=body.Find("DiagramSlot_"+pair.Key)?.GetComponent<Button>();
                if(marker!=null){var colors=marker.colors;colors.normalColor=pair.Key==workshopSlot?LowPolyUi.Amber:LowPolyUi.Surface;marker.colors=colors;marker.GetComponentInChildren<Text>().color=pair.Key==workshopSlot?LowPolyUi.Ink:LowPolyUi.Paper;}
            }
            var stock=shopSource?ShopItems():Array.Empty<WorkshopPartDefinition>();
            int capacity=shopSource?stock.Length:sourceArea==0?PlayerPocketInventory.SlotCount:sourceArea==1?(VehicleCargoTrunk.Instance?.MaxSlots??0):1;
            int pages=Mathf.Max(1,Mathf.CeilToInt(capacity/(float)WorkshopPageSize));sourcePage=Mathf.Clamp(sourcePage,0,pages-1);workshopPager.text=$"{sourcePage+1} / {pages}";
            for(int i=0;i<workshopItems.Count;i++)
            {
                int index=sourcePage*WorkshopPageSize+i;var button=workshopItemButtons[i];button.gameObject.SetActive(index<capacity);
                var drag=button.GetComponent<VehicleServiceCellUI>();drag.enabled=!shopSource;drag.Address=new ServiceItemAddress(sourceArea,index);
                if(index>=capacity)continue; var data=shopSource?WorkshopCatalog.New(stock[index].id):WorkshopPartItem.Read(drag.Address.Read()); var def=WorkshopCatalog.Get(data?.definitionId); itemPictures[i].sprite=def!=null?WorkshopArt.Part((int)def.kind):null; itemPictures[i].enabled=def!=null;
                var tint=button.colors;
                bool chosen=shopSource?stock[index].id==shopSelection:previewSource.HasValue&&previewSource.Value.Equals(drag.Address);
                tint.normalColor=chosen?LowPolyUi.Border:LowPolyUi.Surface;button.colors=tint;
                if(shopSource){workshopItems[i].text=stock[index].title;itemDetails[i].text=$"{stock[index].price} монет";}
                else {var item=drag.Address.Read();var part=WorkshopPartItem.Read(item);workshopItems[i].text=item.IsEmpty?"—":item.displayName;itemDetails[i].text=part!=null?$"УР. {def.tier} · {part.condition:P0}":item.count>1?$"×{item.count}":"";}
            }
            if(previewSource.HasValue)previewPart=WorkshopPartItem.Read(previewSource.Value.Read());
            var definition=WorkshopCatalog.Get(previewPart?.definitionId);
            bool compatible=definition!=null&&definition.kind==WorkshopCatalog.Kind(workshopSlot);
            var after=compatible?w.Preview(workshopSlot,previewPart):w.Stats;
            var now=w.Stats;
            string comparison=workshopSection==WorkshopSection.Engine?
                $"Тяга {now.power:P0} → {after.power:P0}    Расход {now.fuel:P0} → {after.fuel:P0}\nОхлаждение {now.cooling:P0} → {after.cooling:P0}    Нагрев {now.heat:P0} → {after.heat:P0}":
                workshopSection==WorkshopSection.Suspension?
                $"Сцепление {now.grip:P0} → {after.grip:P0}    Клиренс +{now.clearance*100:0} → +{after.clearance*100:0} см\nПружины {now.spring:P0} → {after.spring:P0}    Демпфирование {now.damping:P0} → {after.damping:P0}":
                $"Защита {now.protection:P0} → {after.protection:P0}    Груз +{now.cargo} → +{after.cargo} мест\nСвет {now.light:P0} → {after.light:P0}";
            workshopComparison.text=$"{WorkshopCatalog.SlotName(workshopSlot).ToUpperInvariant()}  /  {(definition!=null?definition.title:"выберите деталь")}\n"+comparison+$"\nМасса +{now.mass:0} → +{after.mass:0} кг";
            bool available=w.CanInstall(workshopSlot,previewPart,out var reason);
            workshopQuote.text=w.Busy?$"РАБОТА · осталось {w.Remaining:0} с · можно закрыть меню":definition==null?"Перетащите деталь на слот. Простое обслуживание — «Жидкости / АКБ».":
                $"{definition.role} · работа {definition.labor} монет / {definition.seconds:0} с\n"+(shopSelection!=null?"Сначала купите деталь в багажник.":available?"Снятая деталь вернётся в ту же ячейку багажника.":reason);
            installButton.interactable=available&&previewSource.HasValue;
            buyButton.interactable=!w.Busy&&shopSelection!=null&&station!=null&&station.Trader;
            var selected=previewSource.HasValue?previewSource.Value.Read():PocketSlotData.Empty;
            sellButton.interactable=!w.Busy&&!selected.IsEmpty&&station!=null&&station.Trader;
            sellButton.GetComponentInChildren<Text>().text=$"ПРОДАТЬ · {w.SellPrice(selected)}";
            repairButton.interactable=!w.Busy&&previewPart!=null&&previewPart.condition<1&&previewSource.HasValue&&station!=null&&station.Safe;
            repairButton.GetComponentInChildren<Text>().text=$"РЕМОНТ · {w.RepairPrice(previewPart)}";
            workProgress.fillAmount=w.Progress; for(int i=0;i<sourceButtons.Count;i++){bool active=shopSource?i==3:i==sourceArea;var colors=sourceButtons[i].colors;colors.normalColor=active?LowPolyUi.Amber:LowPolyUi.Surface;sourceButtons[i].colors=colors;sourceButtons[i].GetComponentInChildren<Text>().color=active?LowPolyUi.Ink:LowPolyUi.Paper;}
            if(Time.unscaledTime>noticeUntil)Notify(w.Busy?"Мир продолжает жить. Закрытие меню не прерывает работу.":w.LastMessage??"Выберите найденную деталь или откройте магазин. Установка оплачивается отдельно.");
        }
    }
}
