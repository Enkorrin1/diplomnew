using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    [DisallowMultipleComponent]
    public sealed partial class VehicleWorkshop : MonoBehaviour
    {
        [Serializable] public sealed class FittedPart { public WorkshopSlot slot; public WorkshopPartData part; }
        [Serializable] public sealed class SaveData { public int coins; public List<FittedPart> fitted=new List<FittedPart>(); }
        [SerializeField] SaveData state=new SaveData();
        [SerializeField] bool engineStopped;
        VehicleModularState car;
        Rigidbody body;
        WorkshopServiceZone zone;
        float nextZoneCheck, remaining, duration;
        [SerializeField] float originalMass;
        ServiceItemAddress jobSource;
        string sourceId;
        WorkshopPartData replacement;
        WorkshopSlot jobSlot;
        int jobCost;
        bool repairJob;
        WorkshopServiceZone jobZone;
        public string LastMessage { get; private set; }
        public int Coins=>state.coins;
        public bool Busy=>replacement!=null||fullService;
        public bool EngineStopped=>engineStopped||(GetComponent<GarageDriveOutVehicle>() is GarageDriveOutVehicle driver
            ?!driver.IsDrivingEnabled:!(GetComponent<ArcadeCarController>()?.isActiveAndEnabled??false));
        public bool DriveBlocked=>Busy||engineStopped;
        public bool Sheltered=>Zone!=null&&Zone.Safe;
        public float Remaining=>remaining;
        public float Progress=>Busy?1-remaining/duration:0;
        public WorkshopStats Stats { get; private set; }
        public void WearEngine(float loadSeconds)
        {
            var engine=Installed(WorkshopSlot.Engine);if(engine==null||loadSeconds<=0)return;
            engine.condition=Mathf.Max(0,engine.condition-loadSeconds*.00001f);Stats=Calculate();
        }
        public void DamagePart(WorkshopSlot slot,float amount)
        {
            var part=Installed(slot);if(part==null||amount<=0)return;
            part.condition=Mathf.Clamp01(part.condition-amount);Stats=Calculate();
        }
        public void RepairPart(WorkshopSlot slot,float amount,float ceiling)
        {
            var part=Installed(slot);if(part==null||amount<=0||part.condition>=ceiling)return;
            part.condition=Mathf.Min(ceiling,part.condition+amount);Stats=Calculate();
        }
        public void SetTireCondition(int index,float condition)
        {
            if(index<0||index>3)return;
            var tire=Installed(WorkshopSlot.FrontLeftWheel+index);if(tire!=null)tire.condition=Mathf.Clamp01(condition);Stats=Calculate();
        }
        public WorkshopServiceZone Zone { get { RefreshZone();return zone; } }
        public static VehicleWorkshop For(VehicleModularState vehicle)=>vehicle==null?null:vehicle.GetComponent<VehicleWorkshop>()??vehicle.gameObject.AddComponent<VehicleWorkshop>();
        void Awake()
        {
            car=GetComponent<VehicleModularState>();body=GetComponent<Rigidbody>();if(originalMass<=0)originalMass=body!=null?body.mass:1200;
            if(state.fitted.Count==0)
            {
                state.fitted.Add(new FittedPart{slot=WorkshopSlot.Engine,part=WorkshopCatalog.New("engine_stock")});
                state.fitted.Add(new FittedPart{slot=WorkshopSlot.Radiator,part=WorkshopCatalog.New("radiator_stock")});
                for(int i=0;i<4;i++)state.fitted.Add(new FittedPart{slot=WorkshopSlot.FrontLeftWheel+i,part=WorkshopCatalog.New("wheel_road")});
            }
            Recalculate();
        }
        void Update()
        {
            RefreshZone();
            if(GetComponent<ArcadeCarController>()?.Run?.IsGameOver??false){if(Busy)CancelJob();return;}
            if(fullService)AdvanceService(Time.deltaTime);else if(Busy)Advance(Time.deltaTime);
        }
        void RefreshZone()
        {
            if(Time.unscaledTime<nextZoneCheck&&zone!=null)return;
            nextZoneCheck=Time.unscaledTime+.25f;zone=WorkshopServiceZone.Find(transform.position);
        }
        public bool ToggleEngine(out string message)
        {
            if(Busy){message="Дождитесь завершения работы.";return false;}
            if(body!=null&&body.linearVelocity.magnitude>.5f){message="Сначала остановите автомобиль.";return false;}
            if(engineStopped&&car!=null&&car.IsEngineSeized){message="Двигатель заклинил. Нужна замена или капитальный ремонт на СТО.";return false;}
            engineStopped=!engineStopped;message=engineStopped?"Двигатель заглушён.":"Зажигание включено.";return true;
        }
        public WorkshopPartData Installed(WorkshopSlot slot)
        {
            if(WorkshopCatalog.Internal(slot))return Installed(WorkshopSlot.Engine)?.Internals.FirstOrDefault(p=>WorkshopCatalog.Get(p.definitionId)?.kind==WorkshopCatalog.Kind(slot));
            return state.fitted.FirstOrDefault(p=>p.slot==slot)?.part;
        }
        void SetPart(WorkshopSlot slot,WorkshopPartData part)
        {
            if(WorkshopCatalog.Internal(slot))
            {
                var engine=Installed(WorkshopSlot.Engine);
                engine.Internals.RemoveAll(p=>WorkshopCatalog.Get(p.definitionId)?.kind==WorkshopCatalog.Kind(slot));
                if(part!=null)engine.Internals.Add(part);
            }
            else {state.fitted.RemoveAll(p=>p.slot==slot);if(part!=null)state.fitted.Add(new FittedPart{slot=slot,part=part});}
        }
        public WorkshopStats Preview(WorkshopSlot slot,WorkshopPartData part)
        {
            var before=Installed(slot);SetPart(slot,part);var result=Calculate();SetPart(slot,before);return result;
        }
        WorkshopStats Calculate(){var result=WorkshopStats.Standard;foreach(var entry in state.fitted)result.Add(entry.part);return result;}
        public void Recalculate()
        {
            Stats=Calculate();
            if(body!=null)body.mass=originalMass+Stats.mass;
            var trunk=GetComponent<VehicleCargoTrunk>();if(trunk!=null)trunk.SetWorkshopCapacity(Stats.cargo);
            var visuals=GetComponent<WorkshopVehicleVisuals>()??gameObject.AddComponent<WorkshopVehicleVisuals>();visuals.Apply(this);
        }
        public bool CanInstall(WorkshopSlot slot,WorkshopPartData part,out string message)
        {
            message="";
            var d=WorkshopCatalog.Get(part?.definitionId);
            if(d==null||d.kind!=WorkshopCatalog.Kind(slot)){message="Эта деталь не подходит к выбранному слоту.";return false;}
            if(part.condition<.35f){message="Деталь повреждена. Сначала отремонтируйте её.";return false;}
            if(WorkshopCatalog.Internal(slot)&&Installed(WorkshopSlot.Engine)==null){message="Сначала установите двигатель.";return false;}
            if(Busy){message="Уже выполняется работа.";return false;}
            if(body!=null&&body.linearVelocity.magnitude>.5f){message="Остановите автомобиль.";return false;}
            if(!EngineStopped){message="Заглушите двигатель.";return false;}
            var station=Zone;
            if(station!=null&&!station.ServicesAvailable){message="Остановитесь в ангаре: сохраняется прибытие на СТО.";return false;}
            if(d.kind!=WorkshopPartKind.Wheel&&station==null){message="Нужен ремонтный бокс или СТО.";return false;}
            if((d.kind==WorkshopPartKind.Engine||d.kind==WorkshopPartKind.Pistons)&&station!=null&&!station.Safe){message="Эта работа доступна только на безопасной СТО.";return false;}
            if(station==null)
            {
                bool wrench=VehicleServiceInventory.Sources().Any(a=>a.Read().WorldObject?.GetComponent<GarageItemFunction>()?.Kind==GarageItemFunction.ItemKind.Wrench);
                if(!wrench){message="Для полевой замены колеса нужен гаечный ключ.";return false;}
            }
            else if((station.Equipment&d.equipment)!=d.equipment)
            {
                var missing=d.equipment&~station.Equipment;
                message=(missing&WorkshopEquipment.Crane)!=0?"Нужен кран для двигателя.":(missing&WorkshopEquipment.Lift)!=0?"Нужен подъёмник.":(missing&WorkshopEquipment.Bench)!=0?"Нужен верстак.":"Нужны инструменты.";return false;
            }
            if(slot<=WorkshopSlot.Radiator&&car!=null&&car.EngineTemperature>100){message="Двигатель должен остыть ниже 100 °C.";return false;}
            if(slot==WorkshopSlot.RoofRack&&GetComponent<VehicleCargoTrunk>() is VehicleCargoTrunk cargo&&!cargo.CanSetWorkshopCapacity(d.cargo)){message="Освободите дополнительные места багажника.";return false;}
            if(Coins<d.labor){message=$"Не хватает монет: работа стоит {d.labor}.";return false;}
            return true;
        }
        public bool BeginInstall(ServiceItemAddress source,WorkshopSlot slot,out string message)
        {
            var item=source.Read();var part=WorkshopPartItem.Read(item);
            if(!CanInstall(slot,part,out message))return false;
            if(source.Area!=1||item.count!=1){message="Положите одну запчасть в отдельную ячейку багажника: сюда вернётся снятая.";return false;}
            replacement=part.Copy();jobSlot=slot;jobSource=source;sourceId=item.id;jobCost=WorkshopCatalog.Get(part.definitionId).labor;
            duration=remaining=Mathf.Max(.1f,WorkshopCatalog.Get(part.definitionId).seconds);jobZone=Zone;repairJob=false;
            LastMessage=message="Работа началась. Автомобиль заблокирован до завершения или отмены.";return true;
        }
        public bool BeginRepair(ServiceItemAddress source,out string message)
        {
            var item=source.Read();var part=WorkshopPartItem.Read(item);message="Ремонт детали доступен на безопасной СТО.";
            if(Busy||Zone==null||!Zone.Safe||!Zone.ServicesAvailable||!EngineStopped||source.Area!=1||part==null||part.condition>=1||item.count!=1)return false;
            if(body!=null&&body.linearVelocity.magnitude>.5f){message="Остановите автомобиль.";return false;}
            int cost=Mathf.CeilToInt(WorkshopCatalog.Get(part.definitionId).price*(1-part.condition)*.35f);
            if(Coins<cost){message=$"Ремонт стоит {cost} монет.";return false;}
            replacement=part.Copy();replacement.condition=1;jobSource=source;sourceId=item.id;jobCost=cost;
            duration=remaining=15;jobZone=Zone;repairJob=true;LastMessage=message="Ремонт начался.";return true;
        }
        public int RepairPrice(WorkshopPartData part)=>part==null?0:Mathf.CeilToInt(WorkshopCatalog.Get(part.definitionId).price*(1-part.condition)*.35f);
        public void CancelJob()
        {
            bool hadJob=Busy;replacement=null;fullService=false;remaining=0;if(hadJob)LastMessage="Работа отменена. Старая деталь закреплена; монеты не списаны.";
        }
        public void Advance(float dt)
        {
            if(fullService){if(dt>0)AdvanceService(dt);return;}
            if(!Busy||dt<=0)return;
            var item=jobSource.Read();
            var livePart=WorkshopPartItem.Read(item);
            if(item.IsEmpty||item.id!=sourceId||livePart==null||livePart.instanceId!=replacement.instanceId||Zone!=jobZone||Coins<jobCost||!EngineStopped||(body!=null&&body.linearVelocity.magnitude>.5f))
            {CancelJob();LastMessage="Работа отменена: предмет или сервис больше не доступны.";return;}
            remaining=Mathf.Max(0,remaining-dt);if(remaining>0)return;
            // Commit only at completion. The original assembly and source item remain intact until this point.
            if(repairJob)
            {
                var component=item.WorldObject!=null?item.WorldObject.GetComponent<WorkshopPartItem>():null;
                if(component==null){CancelJob();return;}component.data=replacement;
            }
            else
            {
                var previous=Installed(jobSlot);
                var returned=previous==null?PocketSlotData.Empty:WorkshopPartItem.Create(previous);
                var consumed=jobSource.Extract();SetPart(jobSlot,replacement);jobSource.Write(returned);
                WorkshopPartItem.Retire(consumed);InventoryStackOps.DestroyObjects(consumed);
                if(jobSlot>=WorkshopSlot.FrontLeftWheel&&jobSlot<=WorkshopSlot.RearRightWheel)
                {
                    int index=(int)jobSlot-(int)WorkshopSlot.FrontLeftWheel;float condition=replacement.condition;
                    car?.RepairTire(index);car?.DamageTire(index,1-condition);
                }
                if(jobSlot==WorkshopSlot.Engine)car?.RestoreOilPan();
                if(jobSlot==WorkshopSlot.Bumper)car?.InstallCowcatcher();
            }
            state.coins-=jobCost;replacement=null;remaining=0;Recalculate();LastMessage="Готово. Снятая деталь возвращена в багажник.";
        }
        public int SellPrice(PocketSlotData item)
        {
            if(item.IsEmpty)return 0;
            var p=WorkshopPartItem.Read(item);
            if(p==null)return 10;
            int price=Mathf.Max(1,Mathf.FloorToInt(WorkshopCatalog.Get(p.definitionId).price*.45f*p.condition));
            foreach(var child in p.Internals)price+=Mathf.FloorToInt(WorkshopCatalog.Get(child.definitionId).price*.45f*child.condition);
            return price;
        }
        public bool Sell(ServiceItemAddress source,out string message)
        {
            message="Торговля недоступна.";
            if(Busy||Zone==null||!Zone.Trader||!Zone.ServicesAvailable)return false;
            var item=source.Read();if(item.IsEmpty)return false;
            var container=item.WorldObject?.GetComponent<GaragePortableContainer>();
            if(container!=null&&container.CheckpointContents().Any()){message="Сначала освободите содержимое контейнера.";return false;}
            int price=SellPrice(item);var sold=source.Extract();WorkshopPartItem.Retire(sold);InventoryStackOps.DestroyObjects(sold);state.coins+=price;
            LastMessage=message=$"Продано за {price} монет.";return true;
        }
        public bool Buy(string definitionId,out string message)
        {
            var d=WorkshopCatalog.Get(definitionId);message="Товар недоступен.";
            if(Busy||d==null||Zone==null||!Zone.Trader||!Zone.ServicesAvailable||d.tier>Zone.StockTier)return false;
            if(Coins<d.price){message=$"Не хватает монет: цена {d.price}.";return false;}
            var trunk=GetComponent<VehicleCargoTrunk>();
            int free=trunk==null?-1:Enumerable.Range(0,trunk.MaxSlots).Where(i=>trunk.GetSlot(i).IsEmpty).DefaultIfEmpty(-1).First();
            if(free<0){message="Нет свободного места в багажнике.";return false;}
            trunk.SetGridSlot(free,WorkshopPartItem.Create(WorkshopCatalog.New(d.id)));state.coins-=d.price;
            LastMessage=message="Деталь в багажнике. Установка оплачивается отдельно.";return true;
        }
        public string Capture()=>JsonUtility.ToJson(state);
        public void Restore(string json)
        {
            CancelJob();LastMessage=null;state=string.IsNullOrEmpty(json)?new SaveData():JsonUtility.FromJson<SaveData>(json);
            if(state.fitted.Count==0){state.fitted.Add(new FittedPart{slot=WorkshopSlot.Engine,part=WorkshopCatalog.New("engine_stock")});state.fitted.Add(new FittedPart{slot=WorkshopSlot.Radiator,part=WorkshopCatalog.New("radiator_stock")});}
            Recalculate();
        }
    }
}
