using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Audio;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Контроллер боевой зоны придорожной остановки (Roadside Encounter):
    /// Отслеживает бродячих зомби в локации. При полном уничтожении всех врагов:
    /// 1. Активирует Безопасную Зону (WorkshopServiceZone: safe=true, radius=48м).
    /// 2. Воспроизводит торжественный джингл победы.
    /// 3. Выводит баннер «ЛОКАЦИЯ ЗАЧИЩЕНА: Безопасная зона для стоянки и ремонта».
    /// 4. Позволяет спокойно обслуживать автомобиль без риска нападения.
    /// </summary>
    public sealed class RoadsideEncounter : MonoBehaviour
    {
        [SerializeField] private string locationName = "Придорожная стоянка";
        [SerializeField] private float safeZoneRadius = 48f;
        [SerializeField] private bool isCleared = false;

        private List<EncounterZombie> encounterZombies = new List<EncounterZombie>();
        private float nextCheck;
        private bool hasInitialized;

        public bool IsCleared => isCleared;
        public string LocationName => locationName;

        private void Start()
        {
            InitializeEncounter();
        }

        private void InitializeEncounter()
        {
            if (hasInitialized) return;
            hasInitialized = true;

            // Определяем название локации по родительскому объекту
            if (string.IsNullOrEmpty(locationName) || locationName == "Придорожная стоянка")
            {
                if (transform.parent != null)
                {
                    string pName = transform.parent.name;
                    if (pName.Contains("Last_Gas_Station")) locationName = "АЗС «Последний привал»";
                    else if (pName.Contains("Birch_Camp")) locationName = "Берёзовый лагерь";
                    else if (pName.Contains("Suburban_Service")) locationName = "Пригородный автосервис";
                    else if (pName.Contains("Forestry_Camp")) locationName = "Лагерь лесорубов";
                    else if (pName.Contains("Freight_Yard")) locationName = "Грузовой терминал";
                    else if (pName.Contains("Rocky_Picnic")) locationName = "Скалистая смотровая";
                    else locationName = pName;
                }
            }

            // Находим всех прикрепленных зомби
            encounterZombies.Clear();
            var found = GetComponentsInChildren<EncounterZombie>(true);
            encounterZombies.AddRange(found);
        }

        private void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 1.2f;

            if (!hasInitialized) InitializeEncounter();

            // Гарантируем компонент препятствия на машине
            var car = FindFirstObjectByType<ArcadeCarController>();
            if (car != null && car.GetComponent<EncounterVehicleObstacle>() == null)
                car.gameObject.AddComponent<EncounterVehicleObstacle>();

            if (isCleared || encounterZombies.Count == 0) return;

            // Проверяем живых зомби
            int aliveCount = 0;
            for (int i = 0; i < encounterZombies.Count; i++)
            {
                var z = encounterZombies[i];
                if (z != null && z.gameObject.activeInHierarchy && !z.IsDead)
                {
                    aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                OnStopCleared();
            }
        }

        private void OnStopCleared()
        {
            if (isCleared) return;
            isCleared = true;

            // Активируем безопасную зону обслуживания
            var zone = GetComponent<WorkshopServiceZone>() ?? gameObject.AddComponent<WorkshopServiceZone>();
            zone.Configure(locationName, true, WorkshopEquipment.All, 1, false, safeZoneRadius);

            // Звук победы
            AudioManager.Instance?.PlayFanfare();

            // Оповещение на экране
            GarageInteractionUI.Instance?.ShowBanner($"✔ {locationName.ToUpper()} ЗАЧИЩЕНА!\nБезопасная зона: теперь здесь можно безопасно починить и заправить автомобиль.", 6.0f);
        }
    }
}
