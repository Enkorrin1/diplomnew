using System.Linq;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    public sealed partial class VehicleWorkshop
    {
        bool fullService;
        static float PartRepairPrice(WorkshopPartData part)=>(1-part.condition)*WorkshopCatalog.Get(part.definitionId).price*.35f+part.Internals.Sum(PartRepairPrice);
        public int FullServicePrice => Mathf.CeilToInt(state.fitted.Sum(p=>PartRepairPrice(p.part))
            + (1-car.OilPanIntegrity)*20+(1-car.BumperIntegrity)*20
            + Mathf.Max(0,100-(GetComponent<ArcadeCarController>()?.Run?.Health??100))*.5f);
        public bool BeginFullService(out string message)
        {
            message="Остановитесь в безопасной СТО и заглушите двигатель.";
            if(Busy||Zone==null||!Zone.Safe||!Zone.ServicesAvailable||!EngineStopped||(body!=null&&body.linearVelocity.magnitude>.5f))return false;
            int price=FullServicePrice;
            if(car.EngineTemperature>100){message="Дождитесь охлаждения двигателя ниже 100 °C.";return false;}
            if(price==0){message="Машина не требует ремонта.";return false;}
            if(Coins<price){message=$"Ремонт стоит {price} монет.";return false;}
            fullService=true;jobCost=price;duration=remaining=30;jobZone=Zone;
            LastMessage=message="Полный ремонт: 30 с. Оплата после завершения; жидкости приобретаются отдельно.";return true;
        }
        void AdvanceService(float dt)
        {
            if(Zone!=jobZone||!EngineStopped||Coins<jobCost||(body!=null&&body.linearVelocity.magnitude>.5f)){CancelJob();return;}
            remaining=Mathf.Max(0,remaining-dt);if(remaining>0)return;
            foreach(var fitted in state.fitted){fitted.part.condition=1;foreach(var part in fitted.part.Internals)part.condition=1;}
            car.RestoreOilPan();car.RepairBumper(1);for(int i=0;i<4;i++)car.RepairTire(i);
            GetComponent<ArcadeCarController>()?.Run?.Heal(100);
            state.coins-=jobCost;fullService=false;Recalculate();LastMessage="Полный ремонт завершён.";
        }
        public bool BuySupply(BunkerAssemblyItemType type,out string message)
        {
            int price=type==BunkerAssemblyItemType.FuelCanister?20:type==BunkerAssemblyItemType.WaterCanister?10:12;
            message="Покупка припасов доступна на СТО.";
            if(Busy||Zone==null||!Zone.Trader||!Zone.ServicesAvailable)return false;
            if(type!=BunkerAssemblyItemType.FuelCanister&&type!=BunkerAssemblyItemType.WaterCanister&&type!=BunkerAssemblyItemType.OilCanister)return false;
            if(Coins<price){message=$"Нужно {price} монет.";return false;}
            var trunk=GetComponent<VehicleCargoTrunk>();int free=trunk==null?-1:Enumerable.Range(0,trunk.MaxSlots).Where(i=>trunk.GetSlot(i).IsEmpty).DefaultIfEmpty(-1).First();
            if(free<0){message="Освободите место в багажнике.";return false;}
            var prefab=Resources.Load<GameObject>("VehicleService/"+type);if(prefab==null){message="Припас недоступен.";return false;}
            var obj=Instantiate(prefab);var identity=obj.GetComponent<GarageCheckpointItem>()??obj.AddComponent<GarageCheckpointItem>();
            identity.id="service_"+type+"_"+System.Guid.NewGuid().ToString("N");
            obj.GetComponent<FluidContainer>().Configure(type==BunkerAssemblyItemType.FuelCanister?BunkerFluidType.Gasoline:type==BunkerAssemblyItemType.WaterCanister?BunkerFluidType.Water:BunkerFluidType.EngineOil,type==BunkerAssemblyItemType.OilCanister?5:20,type==BunkerAssemblyItemType.FuelCanister?15:type==BunkerAssemblyItemType.WaterCanister?10:5);
            trunk.SetGridSlot(free,InventoryStackOps.FromObject(obj,obj.transform.localScale));state.coins-=price;
            message="Канистра в багажнике. Заправьте машину через её заливную горловину.";return true;
        }
        public bool EmergencyAssistance(out string message)
        {
            message="Аварийная помощь доступна без денег в безопасной СТО при остановленном двигателе.";
            float minimumCost=Mathf.Max(0,8-car.FuelLiters)*2+Mathf.Max(0,.4f-car.EngineIntegrity)*180*.35f
                +Mathf.Max(0,.75f-car.RadiatorIntegrity)*80*.35f+Mathf.Max(0,.75f-car.OilPanIntegrity)*20;
            if(Busy||Zone==null||!Zone.Safe||!Zone.ServicesAvailable||!EngineStopped||Coins>=Mathf.Max(20,Mathf.CeilToInt(minimumCost))||(body!=null&&body.linearVelocity.magnitude>.5f))return false;
            var station=Zone.GetComponent<JourneyServiceStation>();
            if(station==null||station.AidUsed){message="Аварийная помощь на этой СТО уже использована.";return false;}
            if(car.FuelLiters>=8&&car.EngineIntegrity>=.4f&&car.RadiatorIntegrity>=.75f&&car.OilPanIntegrity>=.75f&&car.RadiatorWater>=4&&car.EngineOil>=2&&Enumerable.Range(0,4).All(i=>car.GetTireIntegrity(i)>=.35f))
            {message="Машина уже способна доехать до ближайших припасов.";return false;}
            station.UseAid();car.AddFuel(Mathf.Max(0,8-car.FuelLiters));car.AddRadiatorWater(Mathf.Max(0,4-car.RadiatorWater));car.AddEngineOil(Mathf.Max(0,2-car.EngineOil));
            RepairPart(WorkshopSlot.Engine,1,.4f);RepairPart(WorkshopSlot.Radiator,1,.75f);car.RestoreOilPan();
            for(int i=0;i<4;i++)if(car.GetTireIntegrity(i)<.35f){car.RepairTire(i);car.DamageTire(i,.65f);}
            var run=GetComponent<ArcadeCarController>()?.Run;if(run!=null)run.Heal(Mathf.Max(0,35-run.Health));
            Recalculate();message="Минимальный ремонт и 8 л в баке обеспечены. Полный ремонт и улучшения остаются платными.";return true;
        }
    }
}
