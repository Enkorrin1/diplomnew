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

        private bool isTransitioning;

        public string GetPromptText()
        {
            var prologue = GaragePrologueManager.Instance;
            if (prologue == null) return "[E] Сесть в машину";

            if (!prologue.IsPowerOn)
            {
                return "[E] В ангаре темно! (Сначала включите генератор)";
            }
            if (!prologue.HasCarKeys)
            {
                return "[E] Машина заперта! (Заберите ключи с верстака)";
            }
            if (!prologue.IsGateOpen)
            {
                return "[E] Гермоворота закрыты! (Откройте ворота на стене)";
            }

            return "[E] Сесть за руль и выехать на трассу [Space]";
        }

        public bool CanInteract() => !isTransitioning;

        public void Interact(GaragePlayerController player)
        {
            var prologue = GaragePrologueManager.Instance;
            if (prologue != null)
            {
                if (!prologue.IsPowerOn)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Сначала запустите дизель-генератор на стене!");
                    return;
                }
                if (!prologue.HasCarKeys)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Без ключей зажигания машина не заведется! Осмотрите верстак.");
                    return;
                }
                if (!prologue.IsGateOpen)
                {
                    prologue.ShowNotification("ВНИМАНИЕ: Ворота закрыты! Потяните рычаг привода ворот.");
                    return;
                }
            }

            StartCoroutine(BoardAndLaunchRoutine(player));
        }

        private IEnumerator BoardAndLaunchRoutine(GaragePlayerController player)
        {
            isTransitioning = true;
            if (player != null)
            {
                player.SetMovementLocked(true);
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.MarkPrologueCompleted();
                GaragePrologueManager.Instance.ShowNotification("ЗАЖИГАНИЕ ВКЛЮЧЕНО... ВЫЕЗД НА ТРАССУ!", 3.0f);
            }

            // Звук зажигания и рева мотора
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUp();
            }

            yield return new WaitForSeconds(1.2f);

            // Определяем целевую сцену
            string sceneToLoad = targetSceneName;
            if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
            {
                sceneToLoad = "RogueDrivePrototype";
            }

            SceneTransitionManager.SwitchScene(sceneToLoad);
        }
    }
}
