using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная посадка в автомобиль на подиуме.
    /// Проверяет наличие ключей и открытых ворот, запускает кинематографичную
    /// анимацию посадки и переходит в заезд на трассе.
    /// </summary>
    public sealed class GarageVehicleBoarding : MonoBehaviour, IGarageInteractable
    {
        [Header("Target Stage")]
        [SerializeField] private string targetSceneName = "Stage1_Outskirts";

        public string TargetSceneName => targetSceneName;

        private bool isTransitioning;

        public string GetPromptText()
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready)
                return "[E] Сначала изучите маршрут и загрузите бензин с ремкомплектом";
            var prologue = GaragePrologueManager.Instance;
            if (prologue == null) return "[E] Сесть в машину";

            if (!prologue.IsPowerOn)
            {
                return "[E] В ангаре темно! (Сначала включите генератор)";
            }

            var assembly = BunkerStarterCarAssembly.Instance;
            if (assembly != null && !assembly.IsAssemblyComplete)
            {
                if (!assembly.IsWheelInstalled) return "[E] Машина на домкрате! (Установите колесо со стеллажа)";
                if (!assembly.IsBatteryInstalled) return "[E] Нет питания! (Установите аккумулятор под капот)";
                if (!assembly.IsFuelFilled) return "[E] Бак пуст! (Залейте канистру бензина)";
            }

            if (!prologue.HasCarKeys)
            {
                return "[E] Машина заперта! (Заберите ключи с верстака)";
            }
            if (!prologue.IsGateOpen)
            {
                return "[E] Гермоворота закрыты! (Откройте ворота на пульте у пандуса)";
            }

            return "[E] Завести мотор и выехать из бункера";
        }

        public bool CanInteract()
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready) return false;
            if (isTransitioning) return false;
            var prologue = GaragePrologueManager.Instance;
            if (prologue == null) return true;
            if (!prologue.IsPowerOn) return false;
            var assembly = BunkerStarterCarAssembly.Instance;
            if (assembly != null && !assembly.IsAssemblyComplete) return false;
            if (!prologue.HasCarKeys) return false;
            if (!prologue.IsGateOpen) return false;
            return true;
        }

        public void Interact(GaragePlayerController player)
        {
            if (GarageDeparturePreparation.Instance != null && !GarageDeparturePreparation.Instance.Ready)
            {
                GaragePrologueManager.Instance?.ShowNotification("Изучите маршрут и положите запас бензина и ремкомплект в багажник.");
                return;
            }
            var prologue = GaragePrologueManager.Instance;
            if (prologue != null)
            {
                if (!prologue.IsPowerOn)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Сначала запустите дизель-генератор на стене!");
                    return;
                }

                var assembly = BunkerStarterCarAssembly.Instance;
                if (assembly != null && !assembly.IsAssemblyComplete)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Седан еще не готов к выезду! Завершите базовую сборку машины.");
                    return;
                }

                if (!prologue.HasCarKeys)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Без ключей зажигания машина не заведется! Осмотрите верстак.");
                    return;
                }

                if (!prologue.IsGateOpen)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Гермоворота заперты! Откройте их с пульта у пандуса.");
                    return;
                }
            }

            if (GarageDeparturePreparation.Instance != null && !GarageDepartureCheckpoint.CapturePrepared())
            {
                GaragePrologueManager.Instance?.ShowNotification("Не удалось сохранить подготовку. Припасы остались на месте.");
                return;
            }
            isTransitioning = true;

            GameObject carToDrive = ResolveCarObject();

            var driveController = GarageDriveOutController.Instance;
            if (driveController == null)
            {
                driveController = (carToDrive != null ? carToDrive : gameObject).AddComponent<GarageDriveOutController>();
            }

            driveController.StartDriveOut(player, carToDrive);
        }

        public void ResetBoardingState()
        {
            isTransitioning = false;
        }

        private GameObject ResolveCarObject()
        {
            if (gameObject.name.Contains("Classic Car") || gameObject.name.Contains("Car_9"))
            {
                return gameObject;
            }

            Transform carChild = transform.Find("Classic Car_9");
            if (carChild != null) return carChild.gameObject;

            if (transform.parent != null)
            {
                carChild = transform.parent.Find("Classic Car_9");
                if (carChild != null) return carChild.gameObject;
            }

            GameObject podium = GameObject.Find("PodiumAnchor");
            if (podium != null)
            {
                carChild = podium.transform.Find("Classic Car_9");
                if (carChild != null) return carChild.gameObject;
            }

            var assembly = BunkerStarterCarAssembly.Instance ?? FindFirstObjectByType<BunkerStarterCarAssembly>();
            if (assembly != null)
            {
                carChild = assembly.transform.Find("Classic Car_9");
                if (carChild != null) return carChild.gameObject;
            }

            return GameObject.Find("Classic Car_9");
        }
    }
}
