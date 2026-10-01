using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Attach to an existing service point; does not construct any location.</summary>
    public sealed class WorkshopServiceZone : MonoBehaviour
    {
        static readonly List<WorkshopServiceZone> active=new List<WorkshopServiceZone>();
        [SerializeField] string displayName="СТО";
        [SerializeField] bool safe=true, trader=true;
        [SerializeField, Min(1)] float radius=12;
        [SerializeField, Range(1,3)] int stockTier=1;
        [SerializeField] WorkshopEquipment equipment=WorkshopEquipment.All;
        [SerializeField] Vector3 shelterSize;
        public bool Contains(Vector3 position)
        {
            if(shelterSize.sqrMagnitude<1)return Vector3.Distance(position,transform.position)<=radius;
            var p=transform.InverseTransformPoint(position);var h=shelterSize*.5f;
            return Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.y)<=h.y&&Mathf.Abs(p.z)<=h.z;
        }
        public void ConfigureShelter(Vector3 size)=>shelterSize=size;
        public string DisplayName=>displayName;
        public bool Safe=>safe;
        public bool Trader=>trader;
        public int StockTier=>stockTier;
        public WorkshopEquipment Equipment=>equipment;
        public bool ServicesAvailable=>GetComponent<JourneyServiceStation>() is JourneyServiceStation station?station.Visited:true;
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void OnDisable(){active.Remove(this);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry()=>active.Clear();
        public static WorkshopServiceZone Find(Vector3 position)
        {
            WorkshopServiceZone nearest=null;float distance=float.MaxValue;
            foreach(var zone in active)
            {
                if(zone==null||!zone.isActiveAndEnabled)continue;
                float d=Vector3.Distance(position,zone.transform.position);
                if(zone.Contains(position)&&d<distance){nearest=zone;distance=d;}
            }
            return nearest;
        }
        public void Configure(string title,bool isSafe,WorkshopEquipment tools,int tier=1,bool hasTrader=true,float customRadius=-1)
        {
            displayName=title;safe=isSafe;equipment=tools;stockTier=Mathf.Clamp(tier,1,3);trader=hasTrader;
            if(customRadius>0) radius=customRadius;
        }
    }
}
