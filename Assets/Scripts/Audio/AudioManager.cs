using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Audio
{
    /// <summary>
    /// Центральная аудио-подсистема игры.
    /// Поддерживает процедурный синтез звуков мотора, нитро, выстрелов, взрывов, ударов и сбора монет
    /// без обязательной зависимости от внешних аудио-файлов, а также позволяет назначать кастомные клипы.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;

        [Header("Optional Custom Clips")]
        [SerializeField] private AudioClip customEngineClip;
        [SerializeField] private AudioClip customNitroClip;
        [SerializeField] private AudioClip customShootClip;
        [SerializeField] private AudioClip customExplosionClip;
        [SerializeField] private AudioClip customCrashClip;
        [SerializeField] private AudioClip customCoinClip;

        AudioSource engineSource;
        AudioSource nitroSource;
        AudioSource[] sfxPool;
        int nextPoolIndex;

        AudioClip engineClip;
        AudioClip nitroClip;
        AudioClip shootClip;
        AudioClip hitClip;
        AudioClip explosionClip;
        AudioClip crashClip;
        AudioClip coinClip;
        AudioClip crateClip;
        AudioClip sirenClip;
        AudioClip mineBeepClip;
        AudioClip fanfareClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioClips();
            SetupAudioSources();

            if (FindFirstObjectByType<BackgroundMusicController>() == null)
            {
                GameObject bgmObj = new GameObject("BackgroundMusicController");
                bgmObj.AddComponent<BackgroundMusicController>();
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureAudioListenerInActiveScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureAudioListenerInActiveScene();
        }

        private void EnsureAudioListenerInActiveScene()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners == null || listeners.Length == 0)
            {
                Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                }
                else
                {
                    if (GetComponent<AudioListener>() == null)
                    {
                        gameObject.AddComponent<AudioListener>();
                    }
                }
            }
            else if (listeners.Length > 1)
            {
                AudioListener myListener = GetComponent<AudioListener>();
                if (myListener != null)
                {
                    Destroy(myListener);
                }
            }
        }

        void SetupAudioSources()
        {
            // 1. Источник звука двигателя
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = engineClip;
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.volume = engineVolume * masterVolume;
            engineSource.pitch = 0.85f;
            engineSource.spatialBlend = 0f;
            engineSource.Play();

            // 2. Источник звука нитро
            nitroSource = gameObject.AddComponent<AudioSource>();
            nitroSource.clip = nitroClip;
            nitroSource.loop = true;
            nitroSource.playOnAwake = false;
            nitroSource.volume = 0f;
            nitroSource.pitch = 1.15f;
            nitroSource.spatialBlend = 0f;
            nitroSource.Play();

            // 3. Пул источников для звуковых эффектов (SFX)
            const int poolSize = 12;
            sfxPool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                sfxPool[i] = src;
            }
        }

        AudioSource GetAvailableSource()
        {
            AudioSource src = sfxPool[nextPoolIndex];
            nextPoolIndex = (nextPoolIndex + 1) % sfxPool.Length;
            return src;
        }

        public void UpdateEngineSound(float speedKmh, float maxSpeedKmh, bool isNitro)
        {
            if (engineSource == null) return;
            sfxVolume = PlayerPrefs.GetFloat("SfxVolume", sfxVolume);

            float speedRatio = maxSpeedKmh > 0f ? Mathf.Clamp01(speedKmh / maxSpeedKmh) : 0f;

            // Изменение тональности двигателя от холостых (0.75) до максимальных (2.2)
            float targetPitch = Mathf.Lerp(0.75f, 2.1f, speedRatio);
            if (isNitro) targetPitch *= 1.18f;

            engineSource.pitch = Mathf.MoveTowards(engineSource.pitch, targetPitch, Time.deltaTime * 3.5f);
            engineSource.volume = Mathf.Lerp(engineVolume * 0.7f, engineVolume, speedRatio) * sfxVolume;

            // Гул нитро-ускорения
            if (nitroSource != null)
            {
                float targetNitroVol = isNitro ? (0.8f * sfxVolume) : 0f;
                nitroSource.volume = Mathf.MoveTowards(nitroSource.volume, targetNitroVol, Time.deltaTime * 6f);
            }
        }

        public void PlayShoot(float pitchVariation = 0.12f)
        {
            PlaySfx(shootClip, 0.75f, 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation));
        }

        public void PlayHit(float volume = 0.65f)
        {
            PlaySfx(hitClip, volume, UnityEngine.Random.Range(0.95f, 1.25f));
        }

        public void PlayRicochet()
        {
            PlaySfx(hitClip, 0.7f, UnityEngine.Random.Range(1.6f, 2.2f));
        }

        public void PlayExplosion(float volume = 1f)
        {
            PlaySfx(explosionClip, volume, UnityEngine.Random.Range(0.9f, 1.1f));
        }

        public void PlayCrash(float impactForce = 1f)
        {
            float vol = Mathf.Clamp01(impactForce * 0.85f);
            PlaySfx(crashClip, vol, UnityEngine.Random.Range(0.85f, 1.15f));
        }

        public void PlayCoin()
        {
            PlaySfx(coinClip, 0.8f, UnityEngine.Random.Range(0.98f, 1.08f));
        }

        public void PlayCrateBreak()
        {
            PlaySfx(crateClip, 0.85f, UnityEngine.Random.Range(0.9f, 1.1f));
        }

        public void PlaySiren(float volume = 0.9f)
        {
            PlaySfx(sirenClip, volume, 1f);
        }

        public void PlayMineBeep(float pitch = 1f)
        {
            PlaySfx(mineBeepClip, 0.75f, pitch);
        }

        public void PlayFanfare()
        {
            PlaySfx(fanfareClip, 0.95f, 1f);
        }

        void PlaySfx(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;

            AudioSource src = GetAvailableSource();
            src.pitch = pitch;
            src.PlayOneShot(clip, volume * PlayerPrefs.GetFloat("SfxVolume", sfxVolume));
        }

        #region Procedural Audio Synthesizers

        void InitializeAudioClips()
        {
            engineClip = customEngineClip != null ? customEngineClip : SynthesizeEngineClip();
            nitroClip = customNitroClip != null ? customNitroClip : SynthesizeNitroClip();
            shootClip = customShootClip != null ? customShootClip : SynthesizeShootClip();
            hitClip = SynthesizeHitClip();
            explosionClip = customExplosionClip != null ? customExplosionClip : SynthesizeExplosionClip();
            crashClip = customCrashClip != null ? customCrashClip : SynthesizeCrashClip();
            coinClip = customCoinClip != null ? customCoinClip : SynthesizeCoinClip();
            crateClip = SynthesizeCrateClip();
            sirenClip = SynthesizeSirenClip();
            mineBeepClip = SynthesizeMineBeepClip();
            fanfareClip = SynthesizeFanfareClip();
        }

        AudioClip SynthesizeEngineClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.5f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            const float fundamentalFreq = 58f; // Рокот V8 на холостых

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float phase = t * fundamentalFreq * Mathf.PI * 2f;

                // Смешение основной гармоники, суббаса и металлического обертона
                float val = Mathf.Sin(phase) * 0.5f
                          + Mathf.Sin(phase * 2f) * 0.3f
                          + Mathf.Sin(phase * 3f) * 0.15f
                          + Mathf.Sin(phase * 0.5f) * 0.35f
                          + (UnityEngine.Random.value * 2f - 1f) * 0.08f; // легкий шум трения поршней

                samples[i] = Mathf.Clamp(val * 0.7f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Engine", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeNitroClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.6f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float lastNoise = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float noise = UnityEngine.Random.value * 2f - 1f;
                lastNoise = Mathf.Lerp(lastNoise, noise, 0.45f); // High-pass filtering

                // Свист турбины (2.4 кГц) + реактивный рев
                float turbine = Mathf.Sin(t * 2400f * Mathf.PI * 2f) * 0.25f;
                samples[i] = Mathf.Clamp((lastNoise * 0.65f + turbine) * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Nitro", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeShootClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.14f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 38f); // Быстрое затухание выстрела

                // Нисходящий частотный щелчок от 450 Гц до 70 Гц + ударный шум
                float freq = Mathf.Lerp(450f, 70f, t / duration);
                float tone = Mathf.Sin(t * freq * Mathf.PI * 2f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.6f;

                samples[i] = Mathf.Clamp((tone * 0.65f + noise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Shoot", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeHitClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.12f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 45f);
                // Металлический звон 1600 Гц и 3100 Гц
                float ping = Mathf.Sin(t * 1600f * Mathf.PI * 2f) * 0.6f
                           + Mathf.Sin(t * 3100f * Mathf.PI * 2f) * 0.4f;

                samples[i] = ping * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Proc_Hit", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeExplosionClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.65f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float lowPassNoise = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6.5f); // Тяжелое низкочастотное затухание

                float rawNoise = UnityEngine.Random.value * 2f - 1f;
                lowPassNoise = Mathf.Lerp(lowPassNoise, rawNoise, 0.15f); // Низкочастотный фильтр

                // Суббас волна 55 Гц
                float subBass = Mathf.Sin(t * 55f * Mathf.PI * 2f) * 0.75f;

                samples[i] = Mathf.Clamp((lowPassNoise * 0.8f + subBass) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Explosion", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeCrashClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.35f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 12f);
                float noise = UnityEngine.Random.value * 2f - 1f;
                float crunch = Mathf.Sin(t * 180f * Mathf.PI * 2f) * 0.5f;

                samples[i] = Mathf.Clamp((noise * 0.7f + crunch) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Crash", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeCoinClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.28f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);

                // Двухтоновый кристальный перезвон: нота B5 (987 Гц) -> E6 (1318 Гц)
                float freq = t < 0.05f ? 987.7f : 1318.5f;
                float tone = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.8f
                           + Mathf.Sin(t * freq * 2f * Mathf.PI * 2f) * 0.25f;

                samples[i] = tone * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Proc_Coin", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeCrateClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.25f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float snap = Mathf.Sin(t * 260f * Mathf.PI * 2f) * 0.6f;
                float thud = (UnityEngine.Random.value * 2f - 1f) * 0.5f;

                samples[i] = (snap + thud) * env * 0.75f;
            }

            AudioClip clip = AudioClip.Create("Proc_Crate", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeSirenClip()
        {
            const int sampleRate = 44100;
            const float duration = 1.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Синусоидальная модуляция частоты от 550 Гц до 900 Гц с частотой цикла 2.5 Гц
                float modFreq = 725f + Mathf.Sin(t * 2.5f * Mathf.PI * 2f) * 175f;
                float tone = Mathf.Sin(t * modFreq * Mathf.PI * 2f);
                samples[i] = tone * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Proc_Siren", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeMineBeepClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI); // Плавная огибающая без щелчков
                float tone = Mathf.Sin(t * 1850f * Mathf.PI * 2f);
                samples[i] = tone * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Proc_MineBeep", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        AudioClip SynthesizeFanfareClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.8f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Мажорный аккорд/арпеджио: C5 (523Hz), E5 (659Hz), G5 (784Hz), C6 (1046Hz)
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float noteLen = 0.16f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIdx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                float noteT = t - noteIdx * noteLen;

                float env = Mathf.Exp(-noteT * 5f);
                if (noteIdx == notes.Length - 1)
                {
                    // Последняя нота тянется дольше
                    env = Mathf.Exp(-noteT * 2.5f);
                }

                float tone = Mathf.Sin(t * notes[noteIdx] * Mathf.PI * 2f) * 0.7f
                           + Mathf.Sin(t * notes[noteIdx] * 2f * Mathf.PI * 2f) * 0.25f;

                samples[i] = Mathf.Clamp(tone * env * 0.65f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Proc_Fanfare", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
