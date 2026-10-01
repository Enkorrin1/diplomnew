using System.Collections;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Track;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Environmental mix follows biome, actual movement and storm distance; never changes vehicle physics.</summary>
    public sealed class JourneySoundscape : MonoBehaviour
    {
        [SerializeField] AudioClip forestClip, fieldClip, rainClip;
        [SerializeField] Material dustMaterial;
        AudioSource forest, field, rain, wind, asphalt, gravel;
        AudioSource[] sources;
        AudioClip[] generated;
        GarageDriveOutVehicle driver;
        VehicleModularState vehicle;
        Rigidbody body;
        ParticleSystem dust;
        float nextSurfaceCheck, nextWarning;
        bool onRoad=true, paused, shelteredLast;
        int warningStage;
        public float StormIntensity { get; private set; }
        public bool OnRoad => onRoad;
        public void Configure(AudioClip woods, AudioClip fields, AudioClip storm, Material particles)
        {forestClip=woods;fieldClip=fields;rainClip=storm;dustMaterial=particles;}
        IEnumerator Start()
        {
            yield return null;
            driver=FindFirstObjectByType<GarageDriveOutVehicle>();
            if(driver==null){enabled=false;yield break;}
            vehicle=driver.GetComponent<VehicleModularState>();body=driver.GetComponent<Rigidbody>();
            generated=new[]{Noise("Journey Wind",0),Noise("Asphalt Roll",1),Noise("Gravel Roll",2)};
            forest=Loop("Woodland ambience",forestClip);field=Loop("Field ambience",fieldClip);rain=Loop("Storm rain and thunder",rainClip);
            wind=Loop("Wind",generated[0]);asphalt=Loop("Tyres asphalt",generated[1]);gravel=Loop("Tyres dirt",generated[2]);
            sources=new[]{forest,field,rain,wind,asphalt,gravel};
            var go=new GameObject("Windblown Dust");go.transform.SetParent(transform,false);dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main;main.loop=true;main.startLifetime=2.8f;main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.07f,.22f);
            main.startColor=new Color(.56f,.52f,.42f,.20f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=160;
            var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(28,6,28);
            var velocity=dust.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=7;velocity.z=3;
            var emission=dust.emission;emission.rateOverTime=0;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dustMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            dust.Play();
        }
        AudioSource Loop(string title,AudioClip clip)
        {
            var child=new GameObject(title);child.transform.SetParent(transform,false);var source=child.AddComponent<AudioSource>();
            source.clip=clip;source.loop=true;source.playOnAwake=false;source.spatialBlend=0;source.volume=0;source.priority=180;
            if(clip!=null)source.Play();return source;
        }
        void Update()
        {
            if(sources==null||driver==null||StageRoute.Instance==null)return;
            bool shouldPause=Time.timeScale<=0;
            if(shouldPause!=paused){paused=shouldPause;foreach(var source in sources)if(paused)source.Pause();else source.UnPause();}
            if(paused)return;
            float dt=Time.deltaTime;
            bool sheltered=vehicle!=null&&vehicle.Workshop!=null&&vehicle.Workshop.Sheltered;
            var storm=CreepingStormBarrier.Instance;
            float gap=storm!=null?storm.DistanceToCar:900;
            float target=sheltered?0:1-Mathf.InverseLerp(90,850,gap);
            StormIntensity=Mathf.MoveTowards(StormIntensity,target,dt*.12f);
            float d=StageRoute.Instance.Progress;
            float woods=(d>1750&&d<4250||d>5950&&d<8550)?1:0;
            var journey = SeamlessJourneyStream.Instance;
            if (journey != null && journey.CurrentSegmentIndex > 0)
                woods = journey.CurrentSegmentIndex == 2 ? 1 : 0;
            float speed=body!=null?body.linearVelocity.magnitude:0;
            bool driving=driver.isActiveAndEnabled&&driver.IsDrivingEnabled;
            RogueDrive.Audio.AudioManager.Instance?.UpdateJourneyEngineSound(speed,driver.ThrottleInput,driving&&!(vehicle?.Workshop?.DriveBlocked??false));
            float rolling=Mathf.Clamp01(speed/20)*(driver.IsGrounded?1:0);
            float sfx=Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume",.9f));
            float outdoors=driving?.62f:1;
            Fade(forest,woods*.23f*(1-StormIntensity)*outdoors*sfx,dt);
            Fade(field,(1-woods)*.14f*(1-StormIntensity)*outdoors*sfx,dt);
            Fade(rain,StormIntensity*.36f*sfx,dt);
            Fade(wind,(.025f+rolling*.075f+StormIntensity*.24f)*sfx,dt);
            if(Time.time>=nextSurfaceCheck)
            {
                nextSurfaceCheck=Time.time+.25f;
                onRoad=false;
                foreach(var hit in Physics.RaycastAll(driver.transform.position+Vector3.up*.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(driver.transform)&&(hit.collider.name.StartsWith("BrokenVector_Road_")||hit.collider.name=="Service_Return_Road"||hit.collider.name=="Asphalt"||hit.collider.name=="Seamless_Connector_Road")){onRoad=true;break;}
            }
            Fade(asphalt,onRoad?rolling*.16f*sfx:0,dt);Fade(gravel,onRoad?0:rolling*.24f*sfx,dt);
            asphalt.pitch=Mathf.Lerp(.7f,1.3f,rolling);gravel.pitch=Mathf.Lerp(.75f,1.15f,rolling);
            dust.transform.position=driver.transform.position+Vector3.up*3;
            var emission=dust.emission;emission.rateOverTime=StormIntensity*42;
            if(sheltered&&!shelteredLast)GarageInteractionUI.Instance?.ShowNotification("Укрытие СТО: здесь можно спокойно подготовить машину.\nЗа пределами площадки буря снова опасна.",6f);
            shelteredLast=sheltered;
            int stage=sheltered?0:gap<180?3:gap<400?2:gap<700?1:0;
            if(stage>warningStage&&Time.time>nextWarning)
            {
                warningStage=stage;nextWarning=Time.time+20;
                string message=stage==3?"Фронт совсем близко. Возвращайтесь к машине!":stage==2?"Ветер усиливается. Сократите остановку — буря рядом.":"Сзади надвигается буря. Следите за отрывом перед остановками.";
                GarageInteractionUI.Instance?.ShowNotification(message,6f);
            }
            if(gap>950||sheltered)warningStage=0;
        }
        static void Fade(AudioSource source,float target,float dt)=>source.volume=Mathf.MoveTowards(source.volume,target,dt*.12f);
        static AudioClip Noise(string title,int type)
        {
            const int frequency=22050;int count=frequency*8;var samples=new float[count];var random=new System.Random(931+type);
            float filtered=0,slow=0;
            for(int i=0;i<count;i++)
            {
                float white=(float)random.NextDouble()*2-1;
                filtered=Mathf.Lerp(filtered,white,type==0?.035f:type==1?.18f:.6f);slow=Mathf.Lerp(slow,white,.006f);
                float pulse=type==2?(.6f+.4f*Mathf.Sin(i*.011f)*Mathf.Sin(i*.003f)):1;
                samples[i]=(filtered*(type==0?3:1.7f)+slow)*pulse*.45f;
            }
            // Short overlap at the loop boundary prevents clicks without fading the loop to silence.
            int blend=frequency/10;
            for(int i=0;i<blend;i++)samples[count-blend+i]=Mathf.Lerp(samples[count-blend+i],samples[i],i/(float)blend);
            var clip=AudioClip.Create(title,count-blend,1,frequency,false);var trimmed=new float[count-blend];System.Array.Copy(samples,blend,trimmed,0,trimmed.Length);clip.SetData(trimmed,0);return clip;
        }
        void OnDestroy(){if(generated!=null)foreach(var clip in generated)if(clip!=null)Destroy(clip);}
    }
}
