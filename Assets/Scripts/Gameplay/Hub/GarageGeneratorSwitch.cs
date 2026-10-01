using UnityEngine;
using System.Collections;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный рубильник генератора на стене гаража.
    /// Переключает освещение между аварийным полумраком и полным рабочим светом.
    /// </summary>
    public sealed class GarageGeneratorSwitch : MonoBehaviour, IGarageInteractable
    {
        [Header("Visuals")]
        [SerializeField] private Transform switchHandle;
        [SerializeField] private Vector3 offRotation = new Vector3(30f, 0f, 0f);
        [SerializeField] private Vector3 onRotation = new Vector3(-30f, 0f, 0f);

        private Light beaconLight;
        private bool starting;
        private GaragePrologueManager manager;
        public bool IsStarting => starting;

        private void Start()
        {
            UpdateVisuals();
            manager = GaragePrologueManager.Instance;
            if (manager != null)
            {
                manager.PowerStateChanged += OnPowerChanged;
            }

            var lightObj = new GameObject("GeneratorBeacon");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.35f, 0.25f);
            beaconLight = lightObj.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.range = 3.0f;
            beaconLight.color = new Color(1f, 0.65f, 0.1f);
            beaconLight.intensity = 1.4f;
        }

        private void Update()
        {
            if (beaconLight != null)
            {
                bool isPowerOn = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn;
                beaconLight.enabled = !isPowerOn;
                if (!isPowerOn)
                {
                    beaconLight.intensity = 0.8f + Mathf.PingPong(Time.time * 2.4f, 1.2f);
                }
            }
        }

        public string GetPromptText()
        {
            if (starting) return "Запуск дизеля…";
            bool isPowerOn = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn;
            return isPowerOn
                ? "[E] Выключить дизель-генератор"
                : "[E] Включить дизель-генератор (подать питание на бункер)";
        }

        public bool CanInteract() => !starting;

        public void Interact(GaragePlayerController player)
        {
            if (GaragePrologueManager.Instance == null || starting) return;

            bool newState = !GaragePrologueManager.Instance.IsPowerOn;
            if (newState) { StartCoroutine(StartDiesel()); return; }
            GaragePrologueManager.Instance.SetPower(false);
            UpdateVisuals();

            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlaySwitchClick();
            }
        }

        private IEnumerator StartDiesel()
        {
            starting = true;
            GaragePresentationDirector.Instance?.CrankGenerator();
            GaragePrologueManager.Instance?.ShowNotification("Запуск дизеля…", 2.5f);
            for (int i = 0; i < 3; i++)
            {
                if (switchHandle != null) switchHandle.localRotation = Quaternion.Euler(onRotation);
                yield return new WaitForSeconds(.45f);
                if (switchHandle != null) switchHandle.localRotation = Quaternion.Euler(offRotation * .35f);
                yield return new WaitForSeconds(.3f);
            }
            GaragePrologueManager.Instance?.SetPower(true);
            starting = false;
            UpdateVisuals();
        }
        private void OnPowerChanged(bool _) => UpdateVisuals();
        private void OnDisable() { StopAllCoroutines(); starting = false; UpdateVisuals(); }
        private void OnDestroy() { if (manager != null) manager.PowerStateChanged -= OnPowerChanged; }

        private void UpdateVisuals()
        {
            if (switchHandle == null) return;
            bool isPowerOn = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn;
            switchHandle.localRotation = Quaternion.Euler(isPowerOn ? onRotation : offRotation);
        }
    }
}
