using System;
using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Автомобильная радиостанция на клавишу [R] (Road Trip Radio):
    /// Переключает между частотами:
    /// 1. «Волна Пустоши» (104.2 FM) — атмосферная музыка дорожного путешествия.
    /// 2. «Маяк Цитадели» (88.5 FM) — диспетчер, предупреждения о пылевом шторме и аномалиях.
    /// 3. «Перехват Рейдеров» (107.9 FM) — радиопереговоры бандитов Пустоши, координаты засад.
    /// 4. Радио выключено.
    /// </summary>
    public sealed class VehicleRadioSystem : MonoBehaviour
    {
        public static VehicleRadioSystem Instance { get; private set; }

        public enum RadioChannel
        {
            WastelandWave = 0,
            CitadelBeacon = 1,
            RaiderIntercept = 2,
            Off = 3
        }

        [Header("State")]
        [SerializeField] private RadioChannel currentChannel = RadioChannel.WastelandWave;
        [SerializeField, Range(0f, 1f)] private float radioVolume = 0.65f;

        private AudioSource audioSource;
        private float speechTimer;
        private int speechPhraseIndex;

        private static readonly string[] CitadelAlerts = new[]
        {
            "«Внимание всем экспедициям: Пылевой Шторм ускоряется в Секторе 01. Держите скорость выше 60 км/ч!»",
            "«Цитадель сообщает: обнаружена сейсмическая активность в районе тоннелей. Остерегайтесь обвалов.»",
            "«Диспетчер Маяка: на заправках Сектора 02 зафиксированы следы рейдеров. Подготовьте вооружение.»",
            "«Метеосводка: радиационный фон в норме. Зарядите аккумуляторы на солнечных участках.»"
        };

        private static readonly string[] RaiderChatter = new[]
        {
            "«База, видим одиночку на седане! Готовьте ежи и шипы на 42-м километре!»",
            "«Ха-ха, у него полный багажник лута! Забираем колеса и топливо, остальное на металлолом!»",
            "«Главарь сказал не стрелять по бензобаку — горючее нужно нам самим!»",
            "«Внимание шакалы, песчаная буря на хвосте! Быстро потрошим тачку и уходим в каньон!»"
        };

        public RadioChannel CurrentChannel => currentChannel;
        public bool IsPlaying => currentChannel != RadioChannel.Off;

        private void Awake()
        {
            Instance = this;
            EnsureAudioSource();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Переключение радиостанции на клавишу [R]
            if (Input.GetKeyDown(KeyCode.R))
            {
                CycleChannel();
            }

            // Динамическое вещание переговоров на каналах диспетчера и рейдеров
            if (currentChannel == RadioChannel.CitadelBeacon || currentChannel == RadioChannel.RaiderIntercept)
            {
                speechTimer += Time.deltaTime;
                if (speechTimer >= 14f)
                {
                    speechTimer = 0f;
                    BroadcastRadioDialogue();
                }
            }
        }

        public void CycleChannel()
        {
            int next = ((int)currentChannel + 1) % 4;
            currentChannel = (RadioChannel)next;
            speechTimer = 8f; // Быстрая первая реплика при настройке на канал

            PlaySwitchSound();
            ShowStationBanner();
        }

        private void BroadcastRadioDialogue()
        {
            string message = string.Empty;

            if (currentChannel == RadioChannel.CitadelBeacon)
            {
                message = CitadelAlerts[speechPhraseIndex % CitadelAlerts.Length];
                speechPhraseIndex++;
            }
            else if (currentChannel == RadioChannel.RaiderIntercept)
            {
                message = RaiderChatter[speechPhraseIndex % RaiderChatter.Length];
                speechPhraseIndex++;
            }

            if (!string.IsNullOrEmpty(message) && GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(message, 4.5f);
            }
        }

        private void ShowStationBanner()
        {
            string stationName;
            switch (currentChannel)
            {
                case RadioChannel.WastelandWave:
                    stationName = "📻 РАДИО: «Волна Пустоши» [104.2 FM] — Synth & Rock";
                    break;
                case RadioChannel.CitadelBeacon:
                    stationName = "📻 РАДИО: «Маяк Цитадели» [88.5 FM] — Сводка Шторма";
                    break;
                case RadioChannel.RaiderIntercept:
                    stationName = "📻 РАДИО: «Перехват Рейдеров» [107.9 FM] — Радиоэфир Бандитов";
                    break;
                case RadioChannel.Off:
                default:
                    stationName = "📻 РАДИО: ВЫКЛЮЧЕНО [Нажмите R для включения]";
                    break;
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(stationName, 3.0f);
            }
        }

        private void PlaySwitchSound()
        {
            if (audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
                audioSource.PlayOneShot(CreateStaticClickClip());
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.0f; // 2D в салоне
                audioSource.volume = radioVolume;
            }
        }

        private AudioClip CreateStaticClickClip()
        {
            // Процедурный щелчок переключателя радиоприемника
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.08f);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float env = 1f - ((float)i / length);
                data[i] = UnityEngine.Random.Range(-0.4f, 0.4f) * env;
            }

            AudioClip clip = AudioClip.Create("RadioClick", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
