using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    public sealed partial class VehicleModularState
    {
        [Header("Damage and lubrication")]
        [SerializeField, Range(0,1)] private float oilPanIntegrity=1;
        public float EngineIntegrity => Workshop?.Installed(WorkshopSlot.Engine)?.condition ?? 1;
        public float RadiatorIntegrity => Workshop?.Installed(WorkshopSlot.Radiator)?.condition ?? 1;
        public float OilPanIntegrity => oilPanIntegrity;
        public bool IsEngineSeized => EngineIntegrity<=.03f;
        public bool CanProvidePower => HasFuel && !IsEngineSeized && !(Workshop?.DriveBlocked??false);
        public float OilPressure => Mathf.Clamp01(currentEngineOil/(maxEngineOil*.4f))
            * Mathf.Lerp(1,.65f,Mathf.InverseLerp(110,135,engineTemperature));
        public float WaterLeakPerSecond => Mathf.Max(0,.75f-RadiatorIntegrity)/.75f*.065f;
        public float OilLeakPerSecond => Mathf.Max(0,.75f-oilPanIntegrity)/.75f*.025f;
        public float WaterUsePerSecond {get;private set;}
        public float OilUsePerSecond {get;private set;}
        public float EnginePowerMultiplier => IsEngineSeized?0:
            Mathf.Lerp(.35f,1,Mathf.Clamp01(EngineIntegrity/.6f))
            * Mathf.Lerp(.35f,1,Mathf.Clamp01(OilPressure/.5f))
            * Mathf.Lerp(1,.4f,Mathf.InverseLerp(105,maxEngineTemp,engineTemperature));

        // Small integration steps keep starvation/boiling consistent across frame rates.
        // Public for deterministic balance verification; no input or wall-clock dependency.
        public void SimulateSystems(float dt,bool engineRunning,float load,float speed)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            if(Workshop==null)Workshop=VehicleWorkshop.For(this);
            load=Mathf.Clamp01(load);
            var stats=Workshop.Stats;
            float climate=StageRoute.Instance!=null?StageRoute.Instance.CoolantMultiplier:1;
            while(dt>0)
            {
                float step=Mathf.Min(dt,.05f);dt-=step;
                bool running=engineRunning && HasFuel && !IsEngineSeized;
                float cooling=Mathf.Clamp01(currentRadiatorWater/(maxRadiatorWater*.5f))
                    * Mathf.Lerp(.25f,1,RadiatorIntegrity)*Mathf.Max(.2f,stats.cooling);
                float target=running?88+Mathf.Max(0,stats.heat-cooling)*38
                    +load*12*(1-Mathf.Clamp01(cooling))+Mathf.Max(0,.5f-OilPressure)*35:25;
                target=Mathf.Clamp(target,25,maxEngineTemp);
                engineTemperature=Mathf.MoveTowards(engineTemperature,target,step*(running?1.2f+load*1.8f:1+Mathf.Min(speed,25)*.035f));
                // Closed healthy cooling system loses little; damaged tanks leak even parked.
                float boiling=Mathf.InverseLerp(103,130,engineTemperature)*.055f;
                WaterUsePerSecond=WaterLeakPerSecond+boiling+(running?(.0005f+load*.0015f)*climate:0);
                OilUsePerSecond=OilLeakPerSecond+(running?(.00015f+load*.00045f)
                    *(1+(1-EngineIntegrity)*5+Mathf.InverseLerp(105,135,engineTemperature)*3):0);
                currentRadiatorWater=Mathf.Max(0,currentRadiatorWater-WaterUsePerSecond*step);
                currentEngineOil=Mathf.Max(0,currentEngineOil-OilUsePerSecond*step);
                if(running)
                {
                    float starvation=Mathf.InverseLerp(.45f,0,OilPressure);
                    float heatDamage=Mathf.InverseLerp(overheatThreshold,maxEngineTemp,engineTemperature);
                    Workshop.DamagePart(WorkshopSlot.Engine,step*(.000002f+load*.000008f
                        +starvation*(.004f+load*.012f)+heatDamage*(.001f+load*.005f)));
                }
            }
            RadiatorChanged?.Invoke(currentRadiatorWater,maxRadiatorWater);
            EngineTempChanged?.Invoke(engineTemperature);
        }

        public void RestoreOilPan()=>oilPanIntegrity=1;

        public bool CanService(bool openingRadiator,out string message)
        {
            if(Workshop==null)Workshop=VehicleWorkshop.For(this);
            if((GetComponent<Rigidbody>()?.linearVelocity.magnitude??0)>.5f)
            {message="Сначала остановите автомобиль.";return false;}
            if(Workshop.Busy){message="Дождитесь завершения ремонта.";return false;}
            if(!Workshop.EngineStopped&&!IsEngineSeized&&HasFuel)
            {message="Сначала заглушите двигатель в мастерской.";return false;}
            if(openingRadiator&&engineTemperature>95)
            {message="Радиатор горячий. Дождитесь температуры ниже 95 °C.";return false;}
            message="";return true;
        }

        public bool TryFieldRepair(out string message)
        {
            if(!CanService(true,out message))return false;
            bool repair=RadiatorIntegrity<.75f || oilPanIntegrity<.75f || bumperIntegrity<.99f;
            if(!repair){message=IsEngineSeized?"Двигатель заклинил: замените его на СТО.":"Течей и повреждений бампера нет. Износ деталей устраняется на СТО.";return false;}
            Workshop.RepairPart(WorkshopSlot.Radiator,1,.75f);
            oilPanIntegrity=Mathf.Max(oilPanIntegrity,.75f);
            RepairBumper(.6f);
            message="Течи заделаны, бампер восстановлен. Долейте жидкости. Изношенные детали замените на СТО.";
            return true;
        }

        // Damage amount uses the same 0..100 scale as chassis HP. Local point selects a zone.
        public void ApplyComponentDamage(float amount,Vector3 localPoint)
        {
            if(amount<=0)return;
            if(Workshop==null)Workshop=VehicleWorkshop.For(this);
            amount*=1-Mathf.Clamp(Workshop.Stats.protection,0,.7f);
            bool front=localPoint.z>.3f;
            if(front)
            {
                float absorbed=Mathf.Min(amount,bumperIntegrity*(hasCowcatcher?80:25));
                bumperIntegrity=Mathf.Clamp01(bumperIntegrity-amount/(hasCowcatcher?80:25));
                amount-=absorbed*(hasCowcatcher?.8f:.35f);
                Workshop.DamagePart(WorkshopSlot.Radiator,amount*.012f);
                BumperChanged?.Invoke(bumperIntegrity);
            }
            if(localPoint.y<-.1f)oilPanIntegrity=Mathf.Clamp01(oilPanIntegrity-amount*.014f);
            if(Mathf.Abs(localPoint.x)>.65f)
                DamageTire((localPoint.z>=0?0:2)+(localPoint.x>0?1:0),amount*.012f);
            Workshop.DamagePart(WorkshopSlot.Engine,amount*(front?.0035f:.0015f));
        }

        public float ApplyCollisionDamage(Vector3 localPoint,float normalSpeed,float massRatio=1)
        {
            if(normalSpeed<=3)return 0;
            float amount=Mathf.Min(65,(normalSpeed-3)*(normalSpeed-3)*.24f)*Mathf.Clamp01(massRatio);
            ApplyComponentDamage(amount,localPoint);
            GetComponent<ArcadeCarController>()?.Run?.TakeChassisDamage(amount);
            return amount;
        }

        private int lastImpactBody;
        private float lastImpactTime=-1;
        private void OnCollisionEnter(Collision collision)
        {
            if(collision.contactCount==0||GetComponent<ArcadeCarController>()?.Run?.IsGameOver==true)return;
            // These actors already deliver their damage through GameRunController.
            var other=collision.collider;
            if(other.GetComponentInParent<EnemyBase>()!=null||other.GetComponentInParent<TrackObstacle>()!=null
                ||other.GetComponentInParent<Combat.RaiderVehicleAI>()!=null||other.GetComponentInParent<BossJuggernaut>()!=null)return;
            int id=collision.rigidbody!=null?collision.rigidbody.GetInstanceID():other.transform.root.GetInstanceID();
            if(id==lastImpactBody&&Time.time-lastImpactTime<.25f)return;
            float speed=0;Vector3 point=Vector3.zero;
            for(int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                float closing=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal));
                if(closing>speed){speed=closing;point=contact.point;}
            }
            float ratio=collision.rigidbody==null?1:Mathf.Clamp01(collision.rigidbody.mass/Mathf.Max(1,GetComponent<Rigidbody>()?.mass??1200));
            if(ApplyCollisionDamage(transform.InverseTransformPoint(point),speed,ratio)>0)
            {lastImpactBody=id;lastImpactTime=Time.time;}
        }
    }
}
