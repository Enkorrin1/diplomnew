using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using RogueDrive.Gameplay.Track;

namespace RogueDrive.Gameplay
{
    /// <summary>Late afternoon cools with elapsed time and the approaching storm.</summary>
    public sealed class JourneyPresentation : MonoBehaviour
    {
        public static JourneyPresentation Instance { get; private set; }
        [SerializeField] GameObject[] districts;
        [SerializeField] Material journeySky;
        Material runtimeSky, previousSky;
        Light sun;
        float started, nextRefresh, atmosphere;
        int announced = -1;
        static readonly string[] Titles = { "ПОСЛЕДНИЕ КВАРТАЛЫ", "БЕРЁЗОВАЯ РОЩА", "ТИХИЙ ПРИГОРОД", "СОСНОВЫЙ БОР", "ПОЛЯ И ГРУЗОВОЙ ДВОР", "КАМЕНИСТЫЕ ХОЛМЫ", "СЕВЕРНЫЙ ВЫЕЗД" };
        /// <summary>0 — ясный вечер, 1 — буря рядом.</summary>
        public float Atmosphere => atmosphere;
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }
        public void Configure(GameObject[] authoredDistricts) => districts = authoredDistricts;
        public void ConfigureSky(Material sky) => journeySky = sky;
        IEnumerator Start()
        {
            yield return null;
            started = Time.time;
            previousSky = RenderSettings.skybox;
            if (journeySky != null) runtimeSky = new Material(journeySky);
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { sun = light; break; }
            ApplyAtmosphere(0, runtimeSky, sun);
        }
        public static void ApplyAtmosphere(float strength, Material sky, Light sunlight)
        {
            strength = Mathf.Clamp01(strength);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.58f,.64f,.70f), new Color(.35f,.43f,.51f), strength);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.48f,.47f,.41f), new Color(.35f,.39f,.40f), strength);
            RenderSettings.ambientGroundColor = new Color(.25f,.27f,.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(new Color(.64f,.68f,.67f), new Color(.43f,.51f,.55f), strength);
            RenderSettings.fogStartDistance = Mathf.Lerp(150,100,strength);
            RenderSettings.fogEndDistance = Mathf.Lerp(1150,680,strength);
            // Погодное событие подмешивается в атмосферу, а не перезаписывает её.
            var weather = TrackWeatherHazardManager.Instance;
            if (weather != null && weather.HazardBlend > 0)
            {
                float h = weather.HazardBlend;
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, weather.HazardFogColor, h * .75f);
                RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, 15, h);
                RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, weather.HazardFogEnd, h);
            }
            if (sunlight != null)
            {
                RenderSettings.sun = sunlight;
                sunlight.color = Color.Lerp(new Color(1f,.83f,.61f), new Color(.79f,.85f,.90f), strength);
                sunlight.intensity = Mathf.Lerp(1.05f,.63f,strength);
                sunlight.transform.rotation = Quaternion.Euler(Mathf.Lerp(24,19,strength),-48,0);
                sunlight.shadows = LightShadows.Soft;
                sunlight.shadowStrength = .8f;
            }
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                sky.SetFloat("_Storm",strength);
                sky.SetColor("_Horizon",RenderSettings.fogColor);
                if (sunlight != null) sky.SetVector("_SunDirection",-sunlight.transform.forward);
            }
            foreach (var camera in Camera.allCameras)
            {
                if (camera.cameraType != CameraType.Game || camera.targetTexture != null) continue;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = RenderSettings.fogColor;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane,1800);
            }
        }
        void Update()
        {
            if (StageRoute.Instance == null) return;
            if (Time.time >= nextRefresh && runtimeSky != null)
            {
                nextRefresh = Time.time + .25f;
                float elapsed = Mathf.Clamp01((Time.time-started)/900f)*.55f;
                var storm = CreepingStormBarrier.Instance;
                float approach = storm == null ? 0 : 1-Mathf.InverseLerp(80,750,storm.DistanceToCar);
                atmosphere = Mathf.MoveTowards(atmosphere,Mathf.Max(elapsed,approach*.9f),.005f);
                ApplyAtmosphere(atmosphere,runtimeSky,sun);
            }
            float d = StageRoute.Instance.Progress;
            var journey = SeamlessJourneyStream.Instance;
            if (journey != null && journey.CurrentSegmentIndex > 0)
            {
                int section = 100 + journey.CurrentSegmentIndex;
                if (announced != section)
                {
                    announced = section;
                    PrototypeHud.Instance?.ShowBiomeNotification(journey.Catalog.segments[journey.CurrentSegmentIndex].title,
                        "", new Color(.83f,.81f,.64f));
                }
                return;
            }
            int zone = d<1800?0:d<4200?1:d<6000?2:d<8500?3:d<10400?4:d<12000?5:6;
            if (zone == announced) return;
            announced = zone;
            PrototypeHud.Instance?.ShowBiomeNotification(Titles[zone],"",new Color(.83f,.81f,.64f));
        }
        void OnDestroy()
        {
            if (runtimeSky == null) return;
            if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = previousSky;
            Destroy(runtimeSky);
        }
    }
}
