using System;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Координатор интерактивного пролога и состояния гаража.
    /// Управляет освещением (аварийный полумрак -> полный рабочий свет),
    /// наличием ключей от машины, состоянием ворот и подсказками текущей цели.
    /// </summary>
    public sealed class GaragePrologueManager : MonoBehaviour
    {
        public static GaragePrologueManager Instance { get; private set; }

        public event Action<bool> PowerStateChanged;
        public event Action KeysPickedUp;
        public event Action GateOpened;

        [Header("State")]
        [SerializeField] private bool isPowerOn = false;
        [SerializeField] private bool hasCarKeys = false;
        [SerializeField] private bool isGateOpen = false;

        [Header("Lighting References")]
        [SerializeField] private Light emergencyRedLight;
        [SerializeField] private Light[] mainWorkshopLights;
        [SerializeField] private GameObject neonPodiumRing;

        [Header("Audio")]
        [SerializeField] private AudioSource ambientSource;

        private float bannerTimer;
        private string bannerMessage = string.Empty;
        private GUIStyle bannerStyle;
        private Texture2D bannerBgTex;

        public bool IsPowerOn => isPowerOn;
        public bool HasCarKeys => hasCarKeys;
        public bool IsGateOpen => isGateOpen;

        private void Awake()
        {
            Instance = this;

            // Если игрок уже совершал заезды (есть сохранения), свет включен сразу
            int completedRuns = PlayerPrefs.GetInt("GaragePrologueDone", 0);
            if (completedRuns > 0)
            {
                isPowerOn = true;
                hasCarKeys = true;
                isGateOpen = true;
            }

            ApplyLightingState(immediate: true);
        }

        private void Start()
        {
            if (!isPowerOn)
            {
                ShowNotification("ЦЕЛЬ: Включите питание на стене (найдите рубильник)");
            }
            else if (!hasCarKeys)
            {
                ShowNotification("ЦЕЛЬ: Заберите ключи от машины с верстака");
            }
            else
            {
                ShowNotification("ЦЕЛЬ: Откройте гермоворота и садитесь в автомобиль");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (bannerBgTex != null) Destroy(bannerBgTex);
        }

        public void ShowNotification(string message, float duration = 4.5f)
        {
            bannerMessage = message;
            bannerTimer = duration;
        }

        public void SetPower(bool enabled)
        {
            if (isPowerOn == enabled) return;
            isPowerOn = enabled;
            ApplyLightingState(immediate: false);
            PowerStateChanged?.Invoke(isPowerOn);

            if (isPowerOn)
            {
                ShowNotification("ПИТАНИЕ ПОДАНО! Найдите ключи зажигания на верстаке");
                if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayLevelUp();
            }
        }

        public void PickUpKeys()
        {
            if (hasCarKeys) return;
            hasCarKeys = true;
            KeysPickedUp?.Invoke();
            ShowNotification("КЛЮЧИ ЗАЖИГАНИЯ ПОЛУЧЕНЫ! Откройте ворота и садитесь в машину");
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayLevelUp();
        }

        public void OpenGate()
        {
            if (isGateOpen) return;
            isGateOpen = true;
            GateOpened?.Invoke();
            ShowNotification("ВОРОТА ОТКРЫТЫ! Путь свободен — садитесь за руль");
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayImpact();
        }

        public void MarkPrologueCompleted()
        {
            PlayerPrefs.SetInt("GaragePrologueDone", 1);
            PlayerPrefs.Save();
        }

        private void ApplyLightingState(bool immediate)
        {
            if (emergencyRedLight != null)
            {
                emergencyRedLight.enabled = !isPowerOn;
            }

            if (mainWorkshopLights != null)
            {
                for (int i = 0; i < mainWorkshopLights.Length; i++)
                {
                    if (mainWorkshopLights[i] != null)
                    {
                        mainWorkshopLights[i].enabled = isPowerOn;
                    }
                }
            }

            if (neonPodiumRing != null)
            {
                neonPodiumRing.SetActive(isPowerOn);
            }

            // Общий фоновый свет ангара
            RenderSettings.ambientLight = isPowerOn
                ? new Color(0.24f, 0.26f, 0.32f)
                : new Color(0.04f, 0.02f, 0.03f);
        }

        private void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (bannerTimer <= 0f || string.IsNullOrEmpty(bannerMessage)) return;

            EnsureStyles();

            float w = Mathf.Min(600f, Screen.width - 40f);
            float h = 42f;
            float x = (Screen.width - w) * 0.5f;
            float y = 28f;

            GUI.Box(new Rect(x, y, w, h), bannerMessage, bannerStyle);
        }

        private void EnsureStyles()
        {
            if (bannerBgTex == null)
            {
                bannerBgTex = new Texture2D(1, 1);
                bannerBgTex.SetPixel(0, 0, new Color(0.08f, 0.12f, 0.18f, 0.92f));
                bannerBgTex.Apply();
            }

            if (bannerStyle == null)
            {
                bannerStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = bannerBgTex, textColor = new Color(0.35f, 0.95f, 1f) },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}
