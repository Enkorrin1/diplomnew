using System;
using System.Collections;
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
        [Tooltip("Объекты сцены, видимые только при работающем генераторе (свечение ламп, световые конусы)")]
        [SerializeField] private GameObject[] poweredVisuals;
        [SerializeField] private Color poweredAmbient = new Color(0.24f, 0.26f, 0.32f);
        [SerializeField] private Color unpoweredAmbient = new Color(0.04f, 0.02f, 0.03f);

        [Header("Audio")]
        [SerializeField] private AudioSource ambientSource;

        [Header("Editor & Testing")]
        [Tooltip("Тестировать новый пролог без удаления сохранения игрока")]
        [SerializeField] private bool forceFreshPrologueInEditor = true;

        public static bool ForcePrologueAwakening = false;
        private Coroutine lightingSequence;

        public bool IsPowerOn => isPowerOn;
        public bool HasCarKeys => hasCarKeys;
        public bool IsGateOpen => isGateOpen;
        public bool IsPreviewRun
        {
            get
            {
#if UNITY_EDITOR
                return forceFreshPrologueInEditor;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            Instance = this;

            // Если игрок уже совершал заезды (есть сохранения), свет включен сразу (если не форсирован пролог)
            int completedRuns = PlayerPrefs.GetInt("GaragePrologueDone", 0);
            if (completedRuns > 0 && !ForcePrologueAwakening && !IsPreviewRun)
            {
                isPowerOn = true;
                hasCarKeys = true;
                isGateOpen = true;
            }
            else
            {
                isPowerOn = false;
                hasCarKeys = false;
                isGateOpen = false;
            }

            ApplyLightingState(immediate: true);
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Быстрый сброс сцены бункера для тестирования по клавише F8
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ForcePrologueAwakening = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
#endif
        }

        private void Start()
        {
            RefreshObjective();

            if (!isPowerOn)
            {
                ShowNotification("ЦЕЛЬ: Включите дизель-генератор у правой стены", 5.0f);
            }
            else if (!hasCarKeys)
            {
                ShowNotification("ЦЕЛЬ: Подготовьте Седан и заберите ключи зажигания", 5.0f);
            }
            else
            {
                ShowNotification("ЦЕЛЬ: Откройте гермоворота и садитесь за руль Седана", 5.0f);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowNotification(string message, float duration = 5.0f)
        {
            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.ShowBanner(message, duration);
            }
        }

        public void RefreshObjective()
        {
            if (GarageInteractionUI.Instance == null) return;

            if (!isPowerOn)
            {
                GarageInteractionUI.Instance.SetObjective(
                    "<b><color=#55CCFF>[!] ТЕКУЩАЯ ЗАДАЧА:</color></b>\n" +
                    "• Включите дизель-генератор у правой стены");
                return;
            }

            var assembly = BunkerStarterCarAssembly.Instance;
            if (assembly != null && !assembly.IsAssemblyComplete)
            {
                var inv = BunkerPlayerInventory.Instance;
                var held = inv != null && inv.HasItem ? inv.HeldItem : (BunkerAssemblyItemType)(-1);

                string wheelState = assembly.IsWheelInstalled
                    ? "<color=#55FF88>✓ Установлено</color>"
                    : (held == BunkerAssemblyItemType.Wheel ? "<color=#55FFFF>▶ В руках (отнесите к Седану!)</color>" : "<color=#FFAA33>✗ На стеллаже</color>");

                string batteryState = assembly.IsBatteryInstalled
                    ? "<color=#55FF88>✓ Установлен</color>"
                    : (held == BunkerAssemblyItemType.Battery ? "<color=#55FFFF>▶ В руках (отнесите к Седану!)</color>" : "<color=#FFAA33>✗ На верстаке</color>");

                string fuelState = assembly.IsFuelFilled
                    ? "<color=#55FF88>✓ Залито</color>"
                    : (held == BunkerAssemblyItemType.FuelCanister ? "<color=#55FFFF>▶ В руках (отнесите к Седану!)</color>" : "<color=#FFAA33>✗ Канистра у бочек</color>");

                GarageInteractionUI.Instance.SetObjective(
                    "<b><color=#55CCFF>[!] СБОРКА СЕДАНА К ВЫЕЗДУ:</color></b>\n" +
                    $"• Колесо: {wheelState}\n" +
                    $"• Аккумулятор: {batteryState}\n" +
                    $"• Топливо: {fuelState}");
                return;
            }

            var preparation = GarageDeparturePreparation.Instance;
            if (preparation != null && !preparation.Ready)
            {
                GarageInteractionUI.Instance.SetObjective(preparation.Objective);
                return;
            }

            if (!hasCarKeys)
            {
                GarageInteractionUI.Instance.SetObjective(
                    "<b><color=#55CCFF>[!] ТЕКУЩАЯ ЗАДАЧА:</color></b>\n" +
                    "• Заберите ключи зажигания с верстака");
                return;
            }

            if (!isGateOpen)
            {
                GarageInteractionUI.Instance.SetObjective(
                    "<b><color=#55CCFF>[!] ТЕКУЩАЯ ЗАДАЧА:</color></b>\n" +
                    "• Откройте гермоворота на пульте у пандуса");
                return;
            }

            GarageInteractionUI.Instance.SetObjective(
                "<b><color=#55FF88>[!] ПУТЬ СВОБОДЕН:</color></b>\n" +
                "• Садитесь за руль Седана [E] и выезжайте из бункера!");
        }

        public void SetPower(bool enabled)
        {
            if (isPowerOn == enabled) return;
            isPowerOn = enabled;
            ApplyLightingState(immediate: false);
            PowerStateChanged?.Invoke(isPowerOn);
            RefreshObjective();

            if (isPowerOn)
            {
                ShowNotification("ПИТАНИЕ ПОДАНО! Соберите Седан: колесо, аккумулятор и канистра бензина!", 6.0f);
                RefreshObjective();
                if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayLevelUp();
                RogueDrive.Gameplay.Narrative.RadioTransmissionSystem.Instance?.PlayGeneratorOnline();
            }
        }

        public void PickUpKeys()
        {
            if (hasCarKeys) return;
            hasCarKeys = true;
            KeysPickedUp?.Invoke();
            RefreshObjective();
            ShowNotification(BunkerStarterCarAssembly.Instance != null && !BunkerStarterCarAssembly.Instance.IsAssemblyComplete
                ? "Ключи у вас. Завершите подготовку автомобиля." : "Ключи у вас. Откройте ворота на пульте у выезда.", 4f);
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayLevelUp();
        }

        public void RestorePreparation(bool power, bool keys)
        {
            isPowerOn = power; hasCarKeys = keys; isGateOpen = false;
            ApplyLightingState(true); PowerStateChanged?.Invoke(power); RefreshObjective();
        }

        public void OpenGate()
        {
            if (isGateOpen) return;
            isGateOpen = true;
            GateOpened?.Invoke();
            RefreshObjective();
            ShowNotification("ГЕРМОВОРОТА ОТКРЫТЫ! Садитесь за руль [E] и жмите газ!", 6.0f);
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayImpact();
        }

        public void MarkPrologueCompleted()
        {
            if (IsPreviewRun) return;
            PlayerPrefs.SetInt("GaragePrologueDone", 1);
            PlayerPrefs.Save();
        }

        [ContextMenu("Сбросить сохранение бункера (Reset Bunker State)")]
        public static void ResetProloguePlayerPrefs()
        {
            PlayerPrefs.DeleteKey("GaragePrologueDone");
            PlayerPrefs.DeleteKey("BunkerDataSaved");
            PlayerPrefs.DeleteKey("BunkerSaved_Fuel");
            PlayerPrefs.DeleteKey("BunkerSaved_Water");
            PlayerPrefs.DeleteKey("BunkerSaved_Trunk");
            PlayerPrefs.DeleteKey("BunkerSaved_Pocket");
            PlayerPrefs.DeleteKey("BunkerPrologueSeen_V1");
            PlayerPrefs.Save();
            Debug.Log("[GaragePrologueManager] Прогресс бункера сброшен в исходное состояние!");
        }

        private void ApplyLightingState(bool immediate)
        {
            if (lightingSequence != null) { StopCoroutine(lightingSequence); lightingSequence = null; }
            if (!immediate && isPowerOn && isActiveAndEnabled)
            { lightingSequence = StartCoroutine(BringWorkshopOnline()); return; }
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
            SetPoweredVisuals(isPowerOn);

            // Общий фоновый свет ангара
            RenderSettings.ambientLight = isPowerOn ? poweredAmbient : unpoweredAmbient;
        }

        private void SetPoweredVisuals(bool on)
        {
            if (poweredVisuals == null) return;
            foreach (var visual in poweredVisuals) if (visual != null) visual.SetActive(on);
        }

        private IEnumerator BringWorkshopOnline()
        {
            if (mainWorkshopLights != null)
            {
                foreach (var lamp in mainWorkshopLights) if (lamp != null) lamp.enabled = false;
                foreach (var lamp in mainWorkshopLights)
                {
                    if (lamp == null) continue;
                    lamp.enabled = true;
                    yield return new WaitForSeconds(.07f);
                    lamp.enabled = false;
                    yield return new WaitForSeconds(.09f);
                    lamp.enabled = true;
                    yield return new WaitForSeconds(.10f);
                }
            }
            if (emergencyRedLight != null) emergencyRedLight.enabled = false;
            if (neonPodiumRing != null) neonPodiumRing.SetActive(true);
            SetPoweredVisuals(true);
            Color start = RenderSettings.ambientLight;
            for(float t=0;t<.8f;t+=Time.deltaTime)
            { RenderSettings.ambientLight = Color.Lerp(start, poweredAmbient,t/.8f); yield return null; }
            RenderSettings.ambientLight = poweredAmbient;
            lightingSequence = null;
        }
    }
}
