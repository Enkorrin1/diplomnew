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

        public string GetPromptText()
        {
            if (gate != null && gate.IsOpen) return "✓ Гермоворота открыты (Путь свободен)";
            return "[E] Нажать пульт: Открыть распашные гермоворота";
        }

        public bool CanInteract()
        {
            return gate != null && !gate.IsOpen;
        }

        public void Interact(GaragePlayerController player)
        {
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
