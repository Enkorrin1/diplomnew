using System.Collections;
using RogueDrive.Gameplay;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>The arrival shot lives in the streamed world; the car and player never change scenes.</summary>
    public sealed class JourneyStationArrival : MonoBehaviour
    {
        [SerializeField] JourneyServiceStation station;
        [SerializeField] Transform entranceShutter;
        [SerializeField] Transform liftPlatform;
        bool running;
        public void Configure(JourneyServiceStation owner, Transform shutter, Transform lift)
        { station=owner; entranceShutter=shutter; liftPlatform=lift; }

        void Update()
        {
            if(station==null||entranceShutter==null)return;
            var car=VehicleModularState.Instance;
            if(car==null)return;
            var p=transform.InverseTransformPoint(car.transform.position);
            var driving=GarageDriveOutController.Instance;
            if(!running&&!station.Visited&&!JourneyCheckpoint.Restoring&&!JourneyCheckpoint.HasPendingRestore
                &&driving!=null&&driving.IsDriving&&p.z>-62&&p.z<-32&&Mathf.Abs(p.x)<9)
                StartCoroutine(Enter(car,driving));
            if(!running)
            {
                bool open=station.Visited||p.z>-32&&p.z<30;
                var target=entranceShutter.localPosition;
                target.y=open?9:3;
                entranceShutter.localPosition=Vector3.MoveTowards(entranceShutter.localPosition,target,Time.deltaTime*4);
            }
        }

        IEnumerator Enter(VehicleModularState car,GarageDriveOutController driving)
        {
            running=true;
            var vehicle=car.GetComponent<GarageDriveOutVehicle>();
            var body=car.GetComponent<Rigidbody>();
            var camera=driving.DrivingCamera;
            var follow=camera!=null?camera.GetComponent<ArcadeCameraFollow>():null;
            bool followEnabled=follow!=null&&follow.enabled;
            var interpolation=body!=null?body.interpolation:RigidbodyInterpolation.None;
            driving.CinematicControl=true;
            if(vehicle!=null){vehicle.CinematicControl=true;vehicle.SetExternalInput(0,0,true);}
            if(body!=null){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;}
            if(follow!=null)follow.enabled=false;
            var from=car.transform.position;
            var to=transform.TransformPoint(new Vector3(0,1,3));
            var shot=transform.TransformPoint(new Vector3(17,8,-43));
            float elapsed=0;
            while(elapsed<4.6f)
            {
                elapsed+=Time.deltaTime;
                float gate=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/1.7f));
                var gatePos=entranceShutter.localPosition;gatePos.y=Mathf.Lerp(3,9,gate);entranceShutter.localPosition=gatePos;
                if(elapsed>1.5f)
                {
                    float drive=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-1.5f)/3.1f));
                    var pose=Vector3.Lerp(from,to,drive);
                    car.transform.SetPositionAndRotation(pose,transform.rotation);
                    if(body!=null){body.position=pose;body.rotation=transform.rotation;}
                }
                if(camera!=null)
                {
                    camera.transform.position=Vector3.Lerp(camera.transform.position,shot,Time.deltaTime*2.3f);
                    var look=car.transform.position+Vector3.up*1.7f;
                    camera.transform.rotation=Quaternion.Slerp(camera.transform.rotation,Quaternion.LookRotation(look-camera.transform.position),Time.deltaTime*3);
                }
                yield return null;
            }
            car.transform.SetPositionAndRotation(to,transform.rotation);
            if(body!=null){body.position=to;body.rotation=transform.rotation;body.isKinematic=false;body.interpolation=interpolation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            if(vehicle!=null)vehicle.CinematicControl=false;
            if(follow!=null)follow.enabled=followEnabled;
            driving.CinematicControl=false;
            driving.ExitCar();
            if(liftPlatform!=null)liftPlatform.localPosition=new Vector3(0,.06f,3);
            GarageInteractionUI.Instance?.ShowNotification("МАШИНА В РЕМОНТНОМ БОКСЕ\nВерстак — улучшения. Приёмка — продажа добычи. Казино находится на трассе.",7);
            running=false;
        }
    }
}
