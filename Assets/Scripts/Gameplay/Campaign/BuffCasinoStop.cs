using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Промежуточный пункт на трассе между локациями — придорожное казино.
    /// Опыт с зомби копится в жетоны (по одному за уровень), а здесь жетоны
    /// обмениваются на случайные бафы: игрок ничего не выбирает, слот-машина
    /// сама выдаёт модификатор. На каждой трассе этапа стоит 1–2 таких пункта.
    ///
    /// Объект и его декорации размещаются в сцене (меню «RogueDrive/Казино»),
    /// компонент в рантайме ничего не строит — только реагирует на въезд машины.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuffCasinoStop : MonoBehaviour
    {
        /// <summary>Поднимается при въезде машины игрока в любой пункт-казино.</summary>
        public static event Action<BuffCasinoStop, ArcadeCarController> StopReached;

        [Header("Stop Info")]
        [SerializeField, Min(1)] private int stopIndex = 1;
        [SerializeField] private string stopName = "Казино «Фортуна»";

        [Header("Pit-stop Bonuses")]
        [SerializeField, Range(0f, 0.5f)] private float fuelBonusRatio = 0.20f;
        [SerializeField, Range(0f, 0.5f)] private float healthRepairRatio = 0.10f;

        [Header("Scene Visuals (authored)")]
        [SerializeField] private Light[] neonLights;
        [SerializeField] private Renderer[] neonBanners;
        [SerializeField] private Color idleNeon = new Color(1f, 0.25f, 0.85f);
        [SerializeField] private Color visitedNeon = new Color(1f, 0.85f, 0.2f);

        bool isPassed;
        float blinkTimer;

        public int StopIndex => stopIndex;
        public string StopName => stopName;
        public bool IsPassed => isPassed;

        public void Configure(int index, string name)
        {
            stopIndex = index;
            stopName = name;
        }

        /// <summary>Привязка декораций сцены (вызывается редакторской командой размещения).</summary>
        public void BindVisuals(Light[] lights, Renderer[] banners)
        {
            neonLights = lights;
            neonBanners = banners;
        }

        private void Awake()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            if (col.size == Vector3.one)
            {
                col.size = new Vector3(24f, 8f, 5f);
                col.center = new Vector3(0f, 4f, 0f);
            }

            SetNeon(idleNeon, 2.5f);
        }

        private void Update()
        {
            // Мигание неона до посещения; после — ровное свечение
            if (neonLights == null || isPassed) return;

            blinkTimer += Time.deltaTime;
            float pulse = 0.65f + 0.35f * Mathf.Sin(blinkTimer * 6f);
            for (int i = 0; i < neonLights.Length; i++)
            {
                if (neonLights[i] != null)
                    neonLights[i].intensity = 2.5f * pulse;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPassed) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car == null) return;

            Pass(car);
        }

        void Pass(ArcadeCarController car)
        {
            isPassed = true;

            GameRunController run = car.Run != null ? car.Run : FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                if (fuelBonusRatio > 0f) run.AddFuel(run.MaxFuel * fuelBonusRatio);
                if (healthRepairRatio > 0f) run.Heal(run.MaxHealth * healthRepairRatio);
            }

            RogueDrive.Audio.AudioManager.Instance?.PlayGateOpen();
            ArcadeCameraFollow.Instance?.TriggerShake(0.3f, 0.25f);

            SetNeon(visitedNeon, 3f);

            StopReached?.Invoke(this, car);
        }

        void SetNeon(Color c, float intensity)
        {
            if (neonBanners != null)
            {
                foreach (Renderer r in neonBanners)
                {
                    if (r != null) r.material.color = c;
                }
            }

            if (neonLights != null)
            {
                foreach (Light l in neonLights)
                {
                    if (l == null) continue;
                    l.color = c;
                    l.intensity = intensity;
                }
            }
        }
    }
}
