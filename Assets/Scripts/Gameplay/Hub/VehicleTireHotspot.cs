using RogueDrive.Audio;
using RogueDrive.Gameplay;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная точка колеса на автомобиле (Diegetic Tire Hotspot):
    /// Позволяет подойти к любому из 4 колес от первого лица на обочине шоссе:
    /// - Осмотреть состояние и износ шины;
    /// - Заменить пробитое колесо запасным колесом из рук или багажника.
    /// Реализует IGarageInteractable для взаимодействия с прицелом персонажа.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class VehicleTireHotspot : MonoBehaviour, IGarageInteractable
    {
        [Header("Tire Index (0: FL, 1: FR, 2: RL, 3: RR)")]
        [SerializeField, Range(0, 3)] private int tireIndex = 0;
        [SerializeField] private string tireLabel = "Переднее левое";

        public int TireIndex => tireIndex;

        public void Configure(int index, string label)
        {
            tireIndex = index;
            tireLabel = label;
        }

        public string GetPromptText()
        {
            var modular = GetComponentInParent<VehicleModularState>();
            if (modular == null) return $"[E] Колесо: {tireLabel}";

            float grip = modular.GetAverageTireGrip(); // общая сводка

            var inv = BunkerPlayerInventory.Instance;
            var trunk = VehicleCargoTrunk.Instance;
            bool hasSpare = (inv != null && inv.HeldItem == BunkerAssemblyItemType.Wheel) ||
                            (trunk != null && trunk.ContainsItem(BunkerAssemblyItemType.Wheel));

            string spareHint = hasSpare ? "<color=#55FF55>[Запаска готова]</color>" : "<color=#FFAA00>[Нужно колесо из багажника]</color>";
            return $"[E] {tireLabel} колесо: Заменить шину {spareHint}";
        }

        public bool CanInteract()
        {
            return true;
        }

        public void Interact(GaragePlayerController player)
        {
            var modular = GetComponentInParent<VehicleModularState>();
            var inv = BunkerPlayerInventory.Instance;
            var trunk = VehicleCargoTrunk.Instance;

            if(modular==null)return;
            if((modular.GetComponent<Rigidbody>()?.linearVelocity.magnitude??0)>.5f)
            {GarageInteractionUI.Instance?.ShowNotification("Сначала остановите автомобиль.",3);return;}
            if(modular.GetTireIntegrity(tireIndex)>=.999f)
            {GarageInteractionUI.Instance?.ShowNotification("Это колесо исправно. Замена не требуется.",3);return;}
            bool fromHands = inv != null && inv.HeldItem == BunkerAssemblyItemType.Wheel;
            bool fromTrunk = trunk != null && trunk.ContainsItem(BunkerAssemblyItemType.Wheel);

            if (!fromHands && !fromTrunk)
            {
                GarageInteractionUI.Instance?.ShowNotification("Нужно запасное колесо в руках или багажнике.",3);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayImpact();
                return;
            }

            // Потребляем запаску
            if (fromHands)
            {
                inv.ConsumeHeldItem();
            }
            else
            {
                trunk.RemoveFirst(BunkerAssemblyItemType.Wheel);
            }

            // Восстанавливаем шину
            modular?.RepairTire(tireIndex);

            GarageInteractionUI.Instance?.ShowNotification($"{tireLabel} колесо заменено. Сцепление восстановлено.",3.5f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUp();
            }
        }
    }
}
