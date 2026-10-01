using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.UI
{
    public sealed partial class VehicleDashboardPanelsUI
    {
        bool stationScreen;
        UnityEngine.UI.Text stationStatus,stationPrice;
        public void ShowStationPanel()
        {
            var active=Workshop()?.Zone?.GetComponent<JourneyServiceStation>();
            if(active!=null&&active.Visited){ClosePanel();JourneyStationExperienceUI.Open(active,JourneyStationExperienceUI.Page.Service);return;}
            Open(ActivePanelType.Engine,"СЕРВИС СТО","РЕМОНТ • ПРИПАСЫ • ПОДГОТОВКА К ВЫЕЗДУ");Clear();stationScreen=true;
            stationStatus=LowPolyUi.Label(body,"StationStatus","",new Vector2(12,-12),new Vector2(1280,100),26,LowPolyUi.Healthy);
            stationPrice=LowPolyUi.Label(body,"StationQuote","",new Vector2(12,-120),new Vector2(1280,70),22,LowPolyUi.Paper);
            LowPolyUi.Button(body,"FullRepair","ПОЛНЫЙ РЕМОНТ • 30 С",new Vector2(12,-216),new Vector2(410,56),()=>{Workshop().BeginFullService(out var m);Notify(m);},true);
            LowPolyUi.Button(body,"StationIgnition","ЗАЖИГАНИЕ",new Vector2(448,-216),new Vector2(410,56),()=>{Workshop().ToggleEngine(out var m);Notify(m);},false);
            LowPolyUi.Button(body,"StationWorkshop","ДЕТАЛИ / ТОРГОВЛЯ",new Vector2(884,-216),new Vector2(410,56),ShowEnginePanel,false);
            LowPolyUi.Button(body,"SupplyFuel","БЕНЗИН 15 Л • 20 МОНЕТ",new Vector2(12,-288),new Vector2(410,56),()=>Supply(BunkerAssemblyItemType.FuelCanister),false);
            LowPolyUi.Button(body,"SupplyWater","ВОДА 10 Л • 10 МОНЕТ",new Vector2(448,-288),new Vector2(410,56),()=>Supply(BunkerAssemblyItemType.WaterCanister),false);
            LowPolyUi.Button(body,"SupplyOil","МАСЛО 5 Л • 12 МОНЕТ",new Vector2(884,-288),new Vector2(410,56),()=>Supply(BunkerAssemblyItemType.OilCanister),false);
            LowPolyUi.Button(body,"EmergencyAid","АВАРИЙНАЯ ПОМОЩЬ • 0",new Vector2(12,-360),new Vector2(410,56),()=>{Workshop().EmergencyAssistance(out var m);Notify(m);},false);
            LowPolyUi.Button(body,"CancelStationJob","ОТМЕНИТЬ РАБОТУ",new Vector2(448,-360),new Vector2(410,56),()=>{Workshop().CancelJob();Notify(Workshop().LastMessage);},false);
            LowPolyUi.Button(body,"DepartStation","ПОДГОТОВИТЬ ВЫЕЗД →",new Vector2(884,-360),new Vector2(410,56),()=>{var s=Workshop()?.Zone?.GetComponent<JourneyServiceStation>();if(s!=null){s.ArmDeparture(out var m);Notify(m);}else Notify("Нужна безопасная СТО.");},true);
            LowPolyUi.Label(body,"StationHelp","Припасы поступают в багажник. Установка купленных деталей оплачивается отдельно.\nАварийная помощь — один раз на СТО при нехватке денег, до минимальной работоспособности.",new Vector2(12,-438),new Vector2(1280,64),19,LowPolyUi.Muted);
            RefreshStation();
        }
        void Supply(BunkerAssemblyItemType type){Workshop().BuySupply(type,out var m);Notify(m);}
        void RefreshStation()
        {
            var w=Workshop();if(w==null)return;
            if(VehicleModularState.Instance.GetComponent<ArcadeCarController>()?.Run?.IsGameOver??false){ClosePanel();return;}
            var s=w.Zone?.GetComponent<JourneyServiceStation>();
            stationStatus.text=(w.Zone!=null?w.Zone.DisplayName:"ВНЕ СТО")+"   /   "+w.Coins+" МОНЕТ\n"+(s!=null?s.Status:"Придорожный сервис не защищает от бури.");
            stationPrice.text=w.Busy?$"Работа: осталось {w.Remaining:0} с. Оплата по завершении.":$"Полный ремонт: {w.FullServicePrice} монет / 30 с. Жидкости отдельно.\nДвигатель: {(w.EngineStopped?"ЗАГЛУШЁН":"РАБОТАЕТ")}";
        }
    }
}
