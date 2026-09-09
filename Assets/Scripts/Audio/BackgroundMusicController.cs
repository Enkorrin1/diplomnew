using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Audio
{
    public enum MusicTrack
    {
        Menu,
        Run,
        Boss
    }

    /// <summary>
    /// Процедурный синтезатор динамической фоновой музыки (BGM).
    /// Генерирует в реальном времени полноценные музыкальные треки:
    /// 1. Menu/Garage: атмосферный неоновый Synthwave (105 BPM, суб-бас, арпеджио).
    /// 2. Run: скоростной драйвовый EBM/Industrial (135 BPM, бочка, 16-й бас, лид).
    /// 3. Boss: агрессивный боевой Dark Techno (145 BPM, тревожные риффы, сирена).
    /// Поддерживает плавный кросс-фейд между треками и реакцию на игровую обстановку.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class BackgroundMusicController : MonoBehaviour
    {
        public static BackgroundMusicController Instance { get; private set; }

        [Header("Volume Control")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.65f;

        private AudioSource audioSource;
        private MusicTrack currentTrack = MusicTrack.Menu;
        private MusicTrack targetTrack = MusicTrack.Menu;

        private float crossFadeAlpha = 1f;
        private const float CrossFadeSpeed = 1.8f;

        // Синтезаторные параметры времени
        private double sampleRate = 44100.0;
        private double phaseLead;
        private double phaseBass;
        private double phasePad;
        private double phaseNoise;

        private double beatTimer;
        private int currentStep = 0; // 0..15 (16 шагов в такте)
        private int currentBar = 0;

        // Ноты гаммы Ля-минор (A Minor) в Гц:
        // A1=55, C2=65.4, D2=73.4, E2=82.4, F2=87.3, G2=98.0
        // A2=110, C3=130.8, D3=146.8, E3=164.8, G3=196.0, A3=220.0, C4=261.6, E4=329.6
        private static readonly float[] ScaleNotes = { 110.0f, 130.81f, 146.83f, 164.81f, 174.61f, 196.0f, 220.0f, 261.63f, 329.63f, 392.0f, 440.0f };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            sampleRate = AudioSettings.outputSampleRate;

            SceneManager.sceneLoaded += OnSceneLoaded;
            RogueDrive.Gameplay.BossJuggernaut.BossSpawned += HandleBossSpawned;
            RogueDrive.Gameplay.BossJuggernaut.BossDefeated += HandleBossDefeated;

            UpdateTrackForCurrentScene(SceneManager.GetActiveScene().name);

            // Создаем тихий пустой клип, чтобы активировать OnAudioFilterRead
            AudioClip silentClip = AudioClip.Create("MusicSynthFeed", 44100, 1, 44100, false);
            audioSource.clip = silentClip;
            audioSource.Play();
        }

        private void HandleBossSpawned(RogueDrive.Gameplay.BossJuggernaut boss) => PlayTrack(MusicTrack.Boss);
        private void HandleBossDefeated(RogueDrive.Gameplay.BossJuggernaut boss) => PlayTrack(MusicTrack.Run);

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                RogueDrive.Gameplay.BossJuggernaut.BossSpawned -= HandleBossSpawned;
                RogueDrive.Gameplay.BossJuggernaut.BossDefeated -= HandleBossDefeated;
            }
        }

        private void Update()
        {
            // Обновление громкости из настроек
            float masterVol = PlayerPrefs.GetFloat("MasterVolume", 0.85f);
            float customMusicVol = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
            musicVolume = masterVol * customMusicVol * 0.6f;

            // Плавное переключение треков
            if (currentTrack != targetTrack)
            {
                crossFadeAlpha = Mathf.MoveTowards(crossFadeAlpha, 0f, Time.unscaledDeltaTime * CrossFadeSpeed);
                if (crossFadeAlpha <= 0.01f)
                {
                    currentTrack = targetTrack;
                    currentStep = 0;
                    currentBar = 0;
                }
            }
            else
            {
                crossFadeAlpha = Mathf.MoveTowards(crossFadeAlpha, 1f, Time.unscaledDeltaTime * CrossFadeSpeed);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateTrackForCurrentScene(scene.name);
        }

        public void PlayTrack(MusicTrack track)
        {
            targetTrack = track;
        }

        private void UpdateTrackForCurrentScene(string sceneName)
        {
            if (sceneName.Contains("Garage") || sceneName.Contains("Menu"))
            {
                PlayTrack(MusicTrack.Menu);
            }
            else
            {
                PlayTrack(MusicTrack.Run);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (musicVolume <= 0.001f)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            // Темп в зависимости от трека
            float bpm = currentTrack == MusicTrack.Menu ? 102f : (currentTrack == MusicTrack.Run ? 134f : 146f);
            double secondsPerStep = (60.0 / bpm) / 4.0; // 16-е доли
            double stepInc = 1.0 / sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                beatTimer += stepInc;
                if (beatTimer >= secondsPerStep)
                {
                    beatTimer -= secondsPerStep;
                    currentStep = (currentStep + 1) % 16;
                    if (currentStep == 0) currentBar = (currentBar + 1) % 4;
                }

                float sample = 0f;

                switch (currentTrack)
                {
                    case MusicTrack.Menu:
                        sample = SynthesizeMenuTrack(secondsPerStep);
                        break;
                    case MusicTrack.Run:
                        sample = SynthesizeRunTrack(secondsPerStep);
                        break;
                    case MusicTrack.Boss:
                        sample = SynthesizeBossTrack(secondsPerStep);
                        break;
                }

                // Масштабирование громкости и кросс-фейд
                sample *= musicVolume * crossFadeAlpha;

                // Лимитер (soft saturation), чтобы звук не хрипел
                sample = Mathf.Clamp(sample, -0.95f, 0.95f);

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }

        private float SynthesizeMenuTrack(double stepDuration)
        {
            // 1. Спокойный суб-бас (ноты A1, F1, C2, G1 по тактам)
            float[] bassPitches = { 55f, 43.65f, 65.41f, 49f };
            float bassFreq = bassPitches[currentBar];
            phaseBass += (2.0 * Math.PI * bassFreq) / sampleRate;
            float bass = (float)Math.Sin(phaseBass) * 0.45f;

            // 2. Мечтательное арпеджио Synthwave
            int[] arpHits = { 0, 4, 7, 11, 7, 4, 11, 7, 2, 6, 9, 6, 4, 7, 11, 9 };
            float leadFreq = ScaleNotes[arpHits[currentStep] % ScaleNotes.Length];
            phaseLead += (2.0 * Math.PI * leadFreq) / sampleRate;

            float stepProgress = (float)(beatTimer / stepDuration);
            float leadEnv = Mathf.Exp(-stepProgress * 4.5f); // Плавное затухание
            float lead = (float)Math.Sin(phaseLead) * leadEnv * 0.22f;

            // 3. Мягкий фон (Pad)
            phasePad += (2.0 * Math.PI * 220.0f) / sampleRate;
            float pad = (float)Math.Sin(phasePad) * 0.08f;

            return bass + lead + pad;
        }

        private float SynthesizeRunTrack(double stepDuration)
        {
            float stepProgress = (float)(beatTimer / stepDuration);

            // 1. Ударная бочка (Kick Drum) на шагах 0, 4, 8, 12 (четверти)
            float kick = 0f;
            if (currentStep % 4 == 0)
            {
                float kickFreq = Mathf.Lerp(125f, 38f, stepProgress);
                phaseNoise += (2.0 * Math.PI * kickFreq) / sampleRate;
                float kickEnv = Mathf.Exp(-stepProgress * 9.0f);
                kick = (float)Math.Sin(phaseNoise) * kickEnv * 0.65f;
            }

            // 2. Снэйр/Хлопок (Snare) на шагах 4 и 12
            float snare = 0f;
            if (currentStep == 4 || currentStep == 12)
            {
                float noise = (float)(UnityEngine.Random.value * 2.0 - 1.0);
                float snareEnv = Mathf.Exp(-stepProgress * 7.5f);
                snare = noise * snareEnv * 0.35f;
            }

            // 3. Хай-хэт (Hi-hat) на каждый 2-й шаг
            float hihat = 0f;
            if (currentStep % 2 == 1)
            {
                float noise = (float)(UnityEngine.Random.value * 2.0 - 1.0);
                float hhEnv = Mathf.Exp(-stepProgress * 18.0f);
                hihat = noise * hhEnv * 0.14f;
            }

            // 4. Пульсирующий 16-битный бас (Rolling Industrial Bass)
            float[] runBass = { 55f, 55f, 65.4f, 55f, 73.4f, 55f, 65.4f, 82.4f, 55f, 55f, 65.4f, 55f, 87.3f, 73.4f, 65.4f, 55f };
            float bassFreq = runBass[currentStep];
            phaseBass += (2.0 * Math.PI * bassFreq) / sampleRate;
            float bassEnv = Mathf.Exp(-stepProgress * 5.0f);
            // Пилообразная форма (sawtooth) для остроты
            float bassSaw = (float)((phaseBass % (2.0 * Math.PI)) / Math.PI - 1.0);
            float bass = bassSaw * bassEnv * 0.35f;

            // 5. Драйвовый синтезаторный мотив
            int[] melodySteps = { 6, -1, 8, -1, 9, -1, 8, 6, -1, 5, -1, 6, 8, -1, 6, -1 };
            float synthLead = 0f;
            int noteIndex = melodySteps[currentStep];
            if (noteIndex >= 0 && noteIndex < ScaleNotes.Length)
            {
                phaseLead += (2.0 * Math.PI * ScaleNotes[noteIndex]) / sampleRate;
                float leadEnv = Mathf.Exp(-stepProgress * 4.0f);
                synthLead = (float)Math.Sin(phaseLead) * leadEnv * 0.24f;
            }

            return kick + snare + hihat + bass + synthLead;
        }

        private float SynthesizeBossTrack(double stepDuration)
        {
            float stepProgress = (float)(beatTimer / stepDuration);

            // Тяжелая агрессивная бочка
            float kick = 0f;
            if (currentStep % 4 == 0 || currentStep == 14)
            {
                float kickFreq = Mathf.Lerp(150f, 32f, stepProgress);
                phaseNoise += (2.0 * Math.PI * kickFreq) / sampleRate;
                float kickEnv = Mathf.Exp(-stepProgress * 7.0f);
                kick = (float)Math.Sin(phaseNoise) * kickEnv * 0.75f;
            }

            // Дисторшн-бас
            float[] bossBassNotes = { 43.65f, 43.65f, 46.25f, 43.65f, 49.0f, 43.65f, 41.2f, 43.65f };
            float bassFreq = bossBassNotes[currentStep % bossBassNotes.Length];
            phaseBass += (2.0 * Math.PI * bassFreq) / sampleRate;
            float saw = (float)((phaseBass % (2.0 * Math.PI)) / Math.PI - 1.0);
            float distBass = Mathf.Clamp(saw * 2.2f, -0.6f, 0.6f) * 0.4f;

            // Тревожный сигнал сирены
            float sirenFreq = 440f + (float)Math.Sin(phasePad * 0.1) * 120f;
            phaseLead += (2.0 * Math.PI * sirenFreq) / sampleRate;
            phasePad += 1.0 / sampleRate;
            float siren = (float)Math.Sin(phaseLead) * 0.18f;

            return kick + distBass + siren;
        }
    }
}
