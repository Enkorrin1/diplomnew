using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерфейс любого интерактивного объекта в убежище/гараже от первого лица.
    /// </summary>
    public interface IGarageInteractable
    {
        /// <summary>Текст подсказки для игрока (например: "[E] Запустить генератор")</summary>
        string GetPromptText();

        /// <summary>Доступно ли взаимодействие в текущем состоянии</summary>
        bool CanInteract();

        /// <summary>Вызывается при нажатии клавиши взаимодействия игроком</summary>
        void Interact(GaragePlayerController player);
    }
}
