using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный верстак слесаря в бункере.
    /// Направляет игрока на диегетическую 3D-прокачку автомобиля (наведение на узлы машины).
    /// Полностью очищен от устаревшего OnGUI.
    /// </summary>
    public sealed class GarageWorkbenchInteractable : MonoBehaviour, IGarageInteractable
    {
        public string GetPromptText()
        {
            return "[E] Верстак: инструкция по модернизации автомобиля";
        }

        public bool CanInteract() => true;

        public void Interact(GaragePlayerController player)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(
                    "МОДЕРНИЗАЦИЯ: Подойдите к автомобилю и наведите прицел на капот, турель, колеса или бак!", 
                    5.5f
                );
            }
        }
    }
}

