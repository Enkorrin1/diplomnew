using RogueDrive.Gameplay.Track;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    [RequireComponent(typeof(WorkshopServiceZone))]
    public sealed class JourneyServiceStation : MonoBehaviour, IGarageInteractable
    {
        [SerializeField] int stationNumber;
        [SerializeField] Transform exitGate;
        [SerializeField] float filtrationSeconds=12;
        [SerializeField] TextMesh statusBoard;
        WorkshopServiceZone zone;
        ArcadeCarController car;
        float stoppedSeconds, remaining;
        bool admitted, released, departureArmed, aidUsed;
        public int Number=>stationNumber;
        public bool AidUsed=>aidUsed;
        public bool Visited=>admitted;
        public bool Departed=>released;
        public bool Ready=>admitted&&remaining<=0;
        public bool DepartureArmed=>departureArmed;
        public string Status=>!admitted?"ЗАЕЗЖАЙТЕ В АНГАР И ОСТАНОВИТЕСЬ":remaining>0?$"ФИЛЬТРАЦИЯ • {Mathf.CeilToInt(remaining)} С":departureArmed?"ВЫЕЗД ГОТОВ • НОВАЯ ВОЛНА ПОСЛЕ ВЫЕЗДА":"БЕЗОПАСНО • РЕМОНТ И ТОРГОВЛЯ";
        public void Configure(int number,Transform gate,TextMesh board){stationNumber=number;exitGate=gate;statusBoard=board;}
        public void UseAid()=>aidUsed=true;
        public void RestoreVisit(bool prepared,bool used){admitted=true;remaining=0;released=prepared;departureArmed=false;aidUsed=used;}
        void Awake()=>zone=GetComponent<WorkshopServiceZone>();
        void Update()
        {
            if(zone==null)zone=GetComponent<WorkshopServiceZone>();
            if(car==null)car=FindFirstObjectByType<ArcadeCarController>();
            if(car==null||car.Run==null||car.Run.IsGameOver||JourneyCheckpoint.Restoring||JourneyCheckpoint.HasPendingRestore)return;
            bool inside=zone.Contains(car.transform.position);
            if(!admitted&&inside)
            {
                stoppedSeconds=car.GetComponent<Rigidbody>().linearVelocity.magnitude<.5f?stoppedSeconds+Time.deltaTime:0;
                if(stoppedSeconds>=1.5f)
                {
                    if(JourneyCheckpoint.CaptureArrival(this))
                    {admitted=true;remaining=filtrationSeconds;GarageInteractionUI.Instance?.ShowNotification("СТО №"+stationNumber+": прибытие сохранено.\nФильтры укрывают от фронта. Обслуживание — через капот или пульт.",7);}
                }
            }
            if(admitted&&remaining>0&&inside)remaining=Mathf.Max(0,remaining-Time.deltaTime);
            if(exitGate!=null)
            {
                var target=new Vector3(0,departureArmed||released?9:3,31);
                exitGate.localPosition=Vector3.MoveTowards(exitGate.localPosition,target,Time.deltaTime*3);
            }
            if(statusBoard!=null)statusBoard.text="СТО №"+stationNumber+"\n"+Status;
            if(departureArmed&&!released&&!inside&&transform.InverseTransformPoint(car.transform.position).z>32)
            {
                // Save the actual loaded car immediately before the next road attempt.
                if(JourneyCheckpoint.CapturePrepared(this))
                {
                    released=true;departureArmed=false;
                    CreepingStormBarrier.Instance?.BeginNextWave(StageRoute.Instance.ProjectDistance(car.transform.position));
                    GarageInteractionUI.Instance?.ShowNotification("Выезд сохранён. Следующая волна приближается.\nПри поражении можно повторить выезд или изменить подготовку на СТО.",7);
                }
            }
        }
        public bool ArmDeparture(out string message)
        {
            message="Дождитесь завершения фильтрации в ангаре.";
            var workshop=VehicleWorkshop.For(VehicleModularState.Instance);
            if(!Ready||workshop==null||workshop.Zone!=zone)return false;
            if(released){message="Эта СТО уже пройдена. Ворота открыты; следующая контрольная точка — на следующей СТО.";return false;}
            if(workshop.Busy){message="Завершите обслуживание.";return false;}
            departureArmed=true;message="Ворота открываются. Подготовленный выезд сохранится при выезде вперёд.";return true;
        }
        public string GetPromptText()=>"[E] СТО №"+stationNumber+" — сервис и выезд";
        public bool CanInteract()=>true;
        public void Interact(GaragePlayerController player)=>RogueDrive.UI.JourneyStationExperienceUI.Open(this,RogueDrive.UI.JourneyStationExperienceUI.Page.Service);
    }
}
