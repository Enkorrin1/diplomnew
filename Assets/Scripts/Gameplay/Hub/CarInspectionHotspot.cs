using RogueDrive.Audio;
using RogueDrive.Gameplay.VFX;
using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная 3D-точка осмотра и прокачки узла машины (Diegetic Physical Hotspot).
    /// Игрок наводит прицел от первого лица на капот, колеса, турель или бак —
    /// в прицеле появляется текущий уровень, прирост параметров и стоимость.
    /// По нажатию [E] улучшение мгновенно покупается и визуально монтируется на машину.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CarInspectionHotspot : MonoBehaviour, IGarageInteractable
    {
        [Header("Upgrade Configuration")]
        [SerializeField] private string trackKeyword = "engine"; // engine, tires, armament, tank, hull
        [SerializeField] private string partDisplayName = "ДВИГАТЕЛЬ V8";
        [SerializeField] private string effectDescription = "+Макс. скорость и разгон";

        private GarageCatalog catalog;
        private MetaProgress metaProgress;
        private UpgradeTrack cachedTrack;

        private void Start()
        {
            InitData();
        }

        public void Configure(string keyword, string displayName, string effect)
        {
            trackKeyword = keyword;
            partDisplayName = displayName;
            effectDescription = effect;
            InitData();
        }

        private void InitData()
        {
            if (catalog == null)
            {
                catalog = Resources.Load<GarageCatalog>("GarageCatalog");
                if (catalog == null)
                {
#if UNITY_EDITOR
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
#endif
                }
            }

            if (catalog != null)
            {
                metaProgress = SaveService.GetActiveProgress(catalog.Upgrades, catalog.Cars);
                FindTrack();
            }
        }

        private void FindTrack()
        {
            if (catalog == null || catalog.Upgrades == null) return;

            string key = trackKeyword.ToLowerInvariant();
            for (int i = 0; i < catalog.Upgrades.Count; i++)
            {
                var tr = catalog.Upgrades[i];
                if (tr != null && tr.Id != null && tr.Id.ToLowerInvariant().Contains(key))
                {
                    cachedTrack = tr;
                    break;
                }
            }
        }

        private string GetActiveCarId()
        {
            if (metaProgress != null && metaProgress.SelectedCar != null)
            {
                return metaProgress.SelectedCar.Id;
            }
            return "light";
        }

        public string GetPromptText()
        {
            if (metaProgress == null) InitData();
            if (cachedTrack == null) FindTrack();

            if (cachedTrack == null)
            {
                return $"[E] {partDisplayName}: модуль не найден";
            }

            string carId = GetActiveCarId();
            int currentLvl = metaProgress.GetUpgradeLevel(carId, cachedTrack);
            int maxLvl = cachedTrack.MaxLevel;

            if (currentLvl >= maxLvl)
            {
                return $"★ {partDisplayName} [МАКС. УРОВЕНЬ {maxLvl}/{maxLvl}]";
            }

            int cost = cachedTrack.GetCost(currentLvl);
            int playerCoins = metaProgress.Coins;
            bool canAfford = playerCoins >= cost;

            string costColor = canAfford ? "#55FF55" : "#FF5555";
            return $"[E] {partDisplayName} (Ур.{currentLvl}/{maxLvl}) → Ур.{currentLvl + 1} | <color={costColor}>{cost} монет</color> ({effectDescription})";
        }

        public bool CanInteract()
        {
            if (metaProgress == null) InitData();
            if (cachedTrack == null) FindTrack();
            if (cachedTrack == null) return false;

            string carId = GetActiveCarId();
            int currentLvl = metaProgress.GetUpgradeLevel(carId, cachedTrack);
            return currentLvl < cachedTrack.MaxLevel;
        }

        public void Interact(GaragePlayerController player)
        {
            if (metaProgress == null) InitData();
            if (cachedTrack == null) FindTrack();
            if (cachedTrack == null) return;

            string carId = GetActiveCarId();
            int currentLvl = metaProgress.GetUpgradeLevel(carId, cachedTrack);
            if (currentLvl >= cachedTrack.MaxLevel)
            {
                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification($"{partDisplayName} уже прокачан до максимума!", 3f);
                }
                return;
            }

            int cost = cachedTrack.GetCost(currentLvl);
            if (metaProgress.Coins < cost)
            {
                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification($"Не хватает монет! Нужно: {cost}, у вас: {metaProgress.Coins}", 3.5f);
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySwitchClick();
                }
                return;
            }

            // Покупка улучшения
            if (metaProgress.BuyUpgrade(carId, cachedTrack))
            {
                SaveService.Save(metaProgress.Data);

                int newLvl = metaProgress.GetUpgradeLevel(carId, cachedTrack);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayLevelUp();
                }

                // Обновляем 3D-тюнинг на машине
                var tuning = FindFirstObjectByType<CarVisualTuning>();
                if (tuning != null)
                {
                    tuning.RefreshTuning(carId, metaProgress);
                }

                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification($"✔ {partDisplayName} улучшен до Ур. {newLvl}! [Осталось: {metaProgress.Coins} монет]", 4f);
                }
            }
        }
    }
}
