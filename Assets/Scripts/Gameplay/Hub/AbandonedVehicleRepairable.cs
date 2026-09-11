using RogueDrive.Audio;
using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный найденный автомобиль на промежуточной базе (СТО, Лагерь рейдеров, Ангар).
    /// Игрок поэтапно ремонтирует его:
    /// 1. Подключение электропроводки и аккумулятора.
    /// 2. Заливка масла и топлива.
    /// 3. Запуск стартера двигателя.
    /// После завершения починки машина разблокируется в автопарке игрока навсегда!
    /// </summary>
    public sealed class AbandonedVehicleRepairable : MonoBehaviour, IGarageInteractable
    {
        [Header("Car Data")]
        [SerializeField] private string unlockCarId = "suv"; // suv, armored, sport
        [SerializeField] private string carDisplayName = "Пикап «Следопыт»";

        [Header("Visual Elements")]
        [SerializeField] private GameObject smokeParticles;
        [SerializeField] private Light[] headlights;

        [Header("Repair State")]
        [SerializeField] private int currentRepairStep = 0; // 0, 1, 2, 3 (3 = готово)
        [SerializeField] private bool isRepaired = false;

        public bool IsRepaired => isRepaired;
        public int CurrentRepairStep => currentRepairStep;

        private MetaProgress metaProgress;
        private GarageCatalog catalog;

        public void Configure(string carId, string displayName)
        {
            unlockCarId = carId;
            carDisplayName = displayName;
        }

        public void AdvanceRepairStep()
        {
            Interact(null);
        }

        private void Start()
        {
            InitData();

            // Проверяем, может машина уже открыта
            if (metaProgress != null && metaProgress.OwnsCar(unlockCarId))
            {
                isRepaired = true;
                currentRepairStep = 3;
                if (smokeParticles != null) smokeParticles.SetActive(false);
                SetHeadlights(true);
            }
            else
            {
                SetHeadlights(false);
            }
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
            }
        }

        private void SetHeadlights(bool on)
        {
            if (headlights == null) return;
            for (int i = 0; i < headlights.Length; i++)
            {
                if (headlights[i] != null) headlights[i].enabled = on;
            }
        }

        public string GetPromptText()
        {
            if (isRepaired)
            {
                return $"★ {carDisplayName} [ПОЛНОСТЬЮ НА ХОДУ] (Выбран/доступен в автопарке)";
            }

            switch (currentRepairStep)
            {
                case 0:
                    return $"[E] Ремонт {carDisplayName}: [1/3] Подключить проводку и аккумулятор";
                case 1:
                    return $"[E] Ремонт {carDisplayName}: [2/3] Залить канистру масла и топлива";
                case 2:
                    return $"[E] Ремонт {carDisplayName}: [3/3] Запустить стартер и оживить двигатель";
                default:
                    return $"[E] Сесть в {carDisplayName}";
            }
        }

        public bool CanInteract()
        {
            return !isRepaired;
        }

        public void Interact(GaragePlayerController player)
        {
            if (isRepaired) return;
            if (metaProgress == null) InitData();

            currentRepairStep++;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }

            if (currentRepairStep < 3)
            {
                string stepMsg = currentRepairStep == 1
                    ? "✔ Проводка подключена! Теперь залейте технические жидкости."
                    : "✔ Топливо подано! Осталось провернуть стартер.";

                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification(stepMsg, 4f);
                }
            }
            else
            {
                // Финал починки: машина оживает!
                isRepaired = true;
                if (smokeParticles != null) smokeParticles.SetActive(false);
                SetHeadlights(true);

                if (metaProgress != null)
                {
                    if (!metaProgress.OwnsCar(unlockCarId))
                    {
                        metaProgress.Data.OwnedCarIds.Add(unlockCarId);
                        SaveService.Save(metaProgress.Data);
                    }
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayLevelUp();
                }

                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification($"🏆 МАШИНА НА ХОДУ! {carDisplayName} разблокирован в вашем гараже!", 6f);
                }
            }
        }
    }
}
