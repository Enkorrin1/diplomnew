using RogueDrive.Audio;
using RogueDrive.Gameplay;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная точка придорожного лутинга (Roadside Scavenging Point):
    /// Размещается на заброшенных автомобилях, сейфах АЗС и бочках на обочине.
    /// Позволяет пешему игроку в режиме 1-го лица [E] добывать ценные ресурсы в заезде:
    /// - Слив бензина из чужих бензобаков;
    /// - Обыск багажников брошенной техники (запаски, канистры воды);
    /// - Взлом сейфов монтировкой (золотые жетоны для Казино «Фортуна Пустоши»).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RoadsideScavengePoint : MonoBehaviour, IGarageInteractable
    {
        public enum ScavengeType
        {
            AbandonedFuelTank,
            AbandonedCarTrunk,
            RoadsideSafeBox,
            WaterBarrel,
            Backpack,
            CardboardBox,
            ToolCabinet
        }

        [Header("Settings")]
        [SerializeField] private ScavengeType type = ScavengeType.AbandonedFuelTank;
        [SerializeField] private string targetName = "Остов легковушки";
        [SerializeField] private bool isLooted = false;
        [SerializeField] private int claimedSupplies;
        [SerializeField] private int trunkRewardRoll = -1;

        public ScavengeType Type => type;
        public bool IsLooted => isLooted;

        public void Configure(ScavengeType scavengeType, string customName = "")
        {
            type = scavengeType;
            if (!string.IsNullOrEmpty(customName)) targetName = customName;
        }

        public string GetPromptText()
        {
            if (isLooted) return $"[Обыскано] {targetName}";

            var inv = BunkerPlayerInventory.Instance;

            switch (type)
            {
                case ScavengeType.AbandonedFuelTank:
                    return $"[E] Слить бензин из {targetName} (+15 л)";
                case ScavengeType.AbandonedCarTrunk:
                    return $"[E] Обыскать багажник {targetName}";
                case ScavengeType.Backpack:
                    return $"[E] Обыскать рюкзак {targetName} (Медикаменты, припасы)";
                case ScavengeType.CardboardBox:
                    return $"[E] Обыскать коробку {targetName} (Запчасти, расходники)";
                case ScavengeType.ToolCabinet:
                    return $"[E] Обыскать верстак / инструменты {targetName}";
                case ScavengeType.RoadsideSafeBox:
                    bool hasCrowbar = inv != null && inv.HeldItem == BunkerAssemblyItemType.Crowbar;
                    string crowbarHint = hasCrowbar ? "<color=#55FF55>[Монтировка наготове]</color>" : "<color=#FFAA00>[Нужна монтировка]</color>";
                    return $"[E] Вскрыть сейф АЗС (Золотые жетоны Казино) {crowbarHint}";
                case ScavengeType.WaterBarrel:
                    return $"[E] Набрать воду из {targetName} (+5 л в радиатор)";
                default:
                    return $"[E] Осмотреть {targetName}";
            }
        }

        public bool CanInteract()
        {
            return !isLooted;
        }

        public void Interact(GaragePlayerController player)
        {
            if (isLooted) return;

            var inv = BunkerPlayerInventory.Instance;
            var trunk = VehicleCargoTrunk.Instance;
            var modular = VehicleModularState.Instance;

            switch (type)
            {
                case ScavengeType.AbandonedFuelTank:
                    if (modular == null) return;
                    isLooted = true;
                    if (modular != null) modular.AddFuel(15f);
                    ShowNotify("✔ Слито 15 л бензина прямо в бензобак вашего Седана!");
                    break;

                case ScavengeType.AbandonedCarTrunk:
                    // Случайный лут: канистра воды, бензин или запасное колесо
                    if (trunkRewardRoll < 0) trunkRewardRoll = Random.Range(0, 3);
                    int roll = trunkRewardRoll;
                    BunkerAssemblyItemType lootedItem = roll == 0 ? BunkerAssemblyItemType.WaterCanister :
                                                       (roll == 1 ? BunkerAssemblyItemType.FuelCanister : BunkerAssemblyItemType.Wheel);
                    string itemName = BunkerPlayerInventory.GetItemDisplayName(lootedItem);

                    if (trunk != null && trunk.TryStoreItem(lootedItem, out _))
                    {
                        isLooted = true;
                        ShowNotify($"✔ Найдено: {itemName}! Уложено в багажник вашего авто.");
                    }
                    else if (inv != null && !inv.HasItem && inv.TryHoldItem(lootedItem, out _))
                    {
                        isLooted = true;
                        ShowNotify($"✔ Найдено: {itemName}! Взято в руки.");
                    }
                    else
                    {
                        ShowNotify($"✔ В багажнике обнаружено: {itemName}. Освободите место в багажнике!");
                    }
                    break;

                case ScavengeType.Backpack:
                    GivePocketSupplies(Supply("first_aid_medkit", "Аптечка", 1),
                        Supply("ration_food", "Походный паёк", 2), Supply("battery_small", "Батарейка", 1),
                        Supply("ammo_9mm", "Патроны 9мм", 14));
                    break;

                case ScavengeType.CardboardBox:
                    GivePocketSupplies(Supply("bolt_repair", "Ремонтные болты", 4), Supply("ammo_9mm", "Патроны 9мм", 7));
                    break;

                case ScavengeType.ToolCabinet:
                    GivePocketSupplies(Supply("wrench_tool", "Гаечный ключ", 1), Supply("ammo_9mm", "Патроны 9мм", 14));
                    break;

                case ScavengeType.RoadsideSafeBox:
                    if (inv == null || inv.HeldItem != BunkerAssemblyItemType.Crowbar)
                    {
                        ShowNotify("Сейф заперт! Найдите стальную монтировку на СТО или в багажнике.");
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayImpact();
                        return;
                    }

                    isLooted = true;
                    // Взлом сейфа: получаем золотые жетоны для Казино «Фортуна Пустоши»!
                    var coordinator = FindFirstObjectByType<GameSessionCoordinator>();
                    if (coordinator != null)
                    {
                        // Добавляем 2 жетона
                        var run = FindFirstObjectByType<GameRunController>();
                        run?.AddCoins(10);
                    }

                    ShowNotify("✔ СЕЙФ ВСКРЫТ! Получены: 2 Золотых Жетона Казино «Фортуна» и 10 монет!");
                    break;

                case ScavengeType.WaterBarrel:
                    if(modular==null)return;
                    if(!modular.CanService(true,out var serviceMessage)){ShowNotify(serviceMessage);return;}
                    float water=Mathf.Min(5,modular.MaxRadiatorWater-modular.RadiatorWater);
                    if(water<=.001f){ShowNotify("Радиатор заполнен.");return;}
                    isLooted = true;
                    modular.AddRadiatorWater(water);
                    ShowNotify($"Залито {water:0.0} л воды. Повреждённый радиатор нужно отремонтировать.");
                    break;
            }

            if (isLooted && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUp();
            }
        }

        static RoadsideSupplyCache.Supply Supply(string id, string title, int count) =>
            new RoadsideSupplyCache.Supply { id = id, title = title, count = count };

        void GivePocketSupplies(params RoadsideSupplyCache.Supply[] supplies)
        {
            var inventory = PlayerPocketInventory.Instance;
            var taken = new System.Collections.Generic.List<string>();
            for (int i = 0; i < supplies.Length; i++)
            {
                int bit = 1 << i;
                if ((claimedSupplies & bit) != 0 || inventory == null ||
                    !inventory.TryAddItem(supplies[i].id, supplies[i].title, supplies[i].count)) continue;
                claimedSupplies |= bit; taken.Add(supplies[i].title + " ×" + supplies[i].count);
            }
            isLooted = claimedSupplies == (1 << supplies.Length) - 1;
            if (taken.Count > 0 && !isLooted) AudioManager.Instance?.PlayLevelUp();
            ShowNotify((taken.Count > 0 ? "Получено: " + string.Join(", ", taken) : "Карманы заполнены.") +
                (isLooted ? ". Всё забрано." : "\nОстаток сохранён здесь. Освободите карманы и повторите обыск."));
        }

        private void ShowNotify(string msg)
        {
            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(msg, 3.5f);
            }
        }
    }
}
