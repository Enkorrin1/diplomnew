using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Optional camp visit. Tracks physical supplies without granting or respawning rewards.</summary>
    public sealed class ForestCampEpisode : MonoBehaviour
    {
        [SerializeField] GameObject[] supplies;
        [SerializeField] string[] supplyNames;
        [SerializeField] Vector3 parking;
        [SerializeField] float returnDistance=3390;
        bool[] loaded;
        float nextCheck;
        AudioSource sound;
        AudioClip thump;
        public bool Entered { get; private set; }
        public bool ReturnedToRoad { get; private set; }
        public int LoadedCount { get; private set; }
        public void Configure(GameObject[] items,string[] names,Vector3 parkingPosition)
        {supplies=items;supplyNames=names;parking=parkingPosition;}
        void Start()
        {
            loaded=new bool[supplies.Length];sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;
            var samples=new float[4410];var random=new System.Random(921);
            for(int i=0;i<samples.Length;i++){float t=i/22050f;samples[i]=(Mathf.Sin(t*480)*.32f+((float)random.NextDouble()*2-1)*.09f)*Mathf.Exp(-t*32)*Mathf.Clamp01(t*400);}
            thump=AudioClip.Create("Cargo set down",samples.Length,1,22050,false);thump.SetData(samples,0);
        }
        void Update()
        {
            if(loaded==null||Time.time<nextCheck||StageRoute.Instance==null)return;
            nextCheck=Time.time+.3f;
            var cargo=VehicleCargoTrunk.Instance;if(cargo==null)return;
            var driver=cargo.GetComponent<GarageDriveOutVehicle>();
            bool near=(cargo.transform.position-parking).sqrMagnitude<65*65;
            if(!Entered&&near&&driver!=null&&!driver.isActiveAndEnabled)
            {Entered=true;GarageInteractionUI.Instance?.ShowNotification("Вода у стола, аптечка в палатке.\nТропа за лагерем ведёт к запасному колесу.",6f);}
            LoadedCount=0;
            for(int i=0;i<supplies.Length;i++)
            {
                bool now=supplies[i]!=null&&supplies[i].transform.IsChildOf(cargo.transform);
                if(now)LoadedCount++;
                if(now&&!loaded[i])
                {
                    sound.PlayOneShot(thump,.65f*Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume",.9f)));
                    GarageInteractionUI.Instance?.ShowNotification($"Погружено: {supplyNames[i]}\nБагажник: {cargo.ItemCount}/{cargo.MaxSlots}",3f);
                }
                loaded[i]=now;
            }
            StageRoute.Instance.Evaluate(StageRoute.Instance.Progress,out var roadPosition,out var roadRotation);
            float lateral=Vector3.Dot(cargo.transform.position-roadPosition,roadRotation*Vector3.right);
            if(Entered&&!ReturnedToRoad&&StageRoute.Instance.Progress>returnDistance&&Mathf.Abs(lateral)<8f&&driver!=null&&driver.isActiveAndEnabled)
            {ReturnedToRoad=true;GarageInteractionUI.Instance?.ShowNotification(LoadedCount>0?"Припасы погружены. Вы вернулись на трассу.":"Вы вернулись на трассу. Следите за отрывом от бури.",4f);}
        }
        void OnDestroy(){if(thump!=null)Destroy(thump);}
    }
}
