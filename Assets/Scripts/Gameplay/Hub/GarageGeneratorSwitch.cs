using UnityEngine;

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

        private void Start()
        {
            UpdateVisuals();
            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.PowerStateChanged += _ => UpdateVisuals();
            }
        }

        public string GetPromptText()
        {
            bool isPowerOn = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn;
            return isPowerOn
                ? "[E] Выключить генератор"
                : "[E] Запустить аварийный генератор";
        }

        public bool CanInteract() => true;

        public void Interact(GaragePlayerController player)
        {
            if (GaragePrologueManager.Instance == null) return;

            bool newState = !GaragePrologueManager.Instance.IsPowerOn;
            GaragePrologueManager.Instance.SetPower(newState);
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (switchHandle == null) return;
            bool isPowerOn = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn;
            switchHandle.localRotation = Quaternion.Euler(isPowerOn ? onRotation : offRotation);
        }
    }
}
