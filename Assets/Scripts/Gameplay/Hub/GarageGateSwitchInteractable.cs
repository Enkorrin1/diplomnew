using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Простой триггер-рубильник / терминал открытия распашных ворот на стене бункера.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageGateSwitchInteractable : MonoBehaviour, IGarageInteractable
    {
        [SerializeField] private GarageSwingGateController gate;
        public void Configure(GarageSwingGateController targetGate)
        {
            gate = targetGate;
        }

        private void Awake()
        {
            if (gate == null)
            {
                gate = FindFirstObjectByType<GarageSwingGateController>();
            }
        }

        public string GetPromptText()
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready)
                return "Подготовка к дороге: карта маршрута, бензин и ремкомплект в багажнике";
            if (gate != null && gate.IsOpen) return "✓ Гермоворота открыты (Путь свободен)";
            bool hasPower = GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsPowerOn;
            if (!hasPower)
            {
                return "[!] Пульт ворот обесточен (Сначала запустите дизель-генератор)";
            }
            return "[E] Нажать пульт: Открыть распашные гермоворота";
        }

        public bool CanInteract()
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready) return false;
            bool hasPower = GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsPowerOn;
            return hasPower && gate != null && !gate.IsOpen;
        }

        public void Interact(GaragePlayerController player)
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready)
            {
                GaragePrologueManager.Instance?.ShowNotification("Сначала изучите маршрут и загрузите припасы.");
                return;
            }
            bool hasPower = GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsPowerOn;
            if (!hasPower)
            {
                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification("ВНИМАНИЕ: Нет питания! Запустите дизель-генератор на стене бункера.", 4.0f);
                }
                return;
            }

            if (gate != null)
            {
                gate.OpenGates();
                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.OpenGate();
                }
            }
        }
    }
}
