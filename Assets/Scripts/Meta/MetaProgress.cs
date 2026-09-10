using System;
using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Мета-прогресс: покупки в гараже и их влияние на заезд.
    ///
    /// Ключевое положение работы — гараж управляет не силой игрока напрямую,
    /// а пространством доступных ему сессионных стратегий. Поэтому класс
    /// реализует сразу два интерфейса: поставляет базовые характеристики
    /// и определяет состав пула модификаторов.
    /// </summary>
    public sealed class MetaProgress : IBaseStatsProvider, IUnlockProvider
    {
        public event Action Changed;

        readonly MetaProgressData _data;
        readonly IReadOnlyList<UpgradeTrack> _tracks;
        readonly IReadOnlyList<CarDefinition> _cars;

        readonly List<StatValue> _statBuffer = new List<StatValue>();

        public MetaProgress(MetaProgressData data,
                            IReadOnlyList<UpgradeTrack> tracks,
                            IReadOnlyList<CarDefinition> cars)
        {
            _data = data ?? new MetaProgressData();
            _tracks = tracks ?? Array.Empty<UpgradeTrack>();
            _cars = cars ?? Array.Empty<CarDefinition>();
        }

        public MetaProgressData Data => _data;
        public int Coins => _data.Coins;
        public string StartingModifierId => _data.StartingModifierId;

        public CarDefinition SelectedCar
        {
            get
            {
                for (int i = 0; i < _cars.Count; i++)
                    if (_cars[i] != null && _cars[i].Id == _data.SelectedCarId)
                        return _cars[i];

                return _cars.Count > 0 ? _cars[0] : null;
            }
        }

        // --- Базовые характеристики -------------------------------------------------

        /// <summary>
        /// Характеристики выбранной машины с наложенными улучшениями.
        /// Сессионные модификаторы применяются уже поверх этого результата.
        /// </summary>
        public IEnumerable<StatValue> GetBaseStats()
        {
            _statBuffer.Clear();

            CarDefinition car = SelectedCar;

            if (car != null && car.BaseStats != null)
                _statBuffer.AddRange(car.BaseStats);

            string currentCarId = car != null ? car.Id : "light";
            for (int i = 0; i < _tracks.Count; i++)
            {
                UpgradeTrack track = _tracks[i];

                if (track == null)
                    continue;

                int level = _data.GetUpgradeLevel(currentCarId, track.Id);

                if (level <= 0)
                    continue;

                AddBonus(track.Target, track.GetBonus(level));
            }

            return _statBuffer;
        }

        void AddBonus(StatId target, float bonus)
        {
            for (int i = 0; i < _statBuffer.Count; i++)
            {
                if (_statBuffer[i].Id != target)
                    continue;

                _statBuffer[i] = new StatValue(target, _statBuffer[i].Value + bonus);
                return;
            }

            _statBuffer.Add(new StatValue(target, bonus));
        }

        // --- Пул модификаторов ------------------------------------------------------

        public bool IsUnlocked(string modifierId)
        {
            return !string.IsNullOrEmpty(modifierId)
                && _data.UnlockedModifierIds.Contains(modifierId);
        }

        public bool UnlockModifier(string modifierId, int cost)
        {
            if (string.IsNullOrEmpty(modifierId) || IsUnlocked(modifierId) || !TrySpend(cost))
                return false;

            _data.UnlockedModifierIds.Add(modifierId);
            Changed?.Invoke();
            return true;
        }

        public bool SetStartingModifier(string modifierId)
        {
            if (!IsUnlocked(modifierId))
                return false;

            _data.StartingModifierId = modifierId;
            Changed?.Invoke();
            return true;
        }

        // --- Покупки ----------------------------------------------------------------

        public int GetUpgradeLevel(UpgradeTrack track) =>
            track == null ? 0 : _data.GetUpgradeLevel(SelectedCar != null ? SelectedCar.Id : "light", track.Id);

        public int GetUpgradeLevel(string carId, UpgradeTrack track) =>
            track == null ? 0 : _data.GetUpgradeLevel(carId, track.Id);

        public bool CanBuyUpgrade(UpgradeTrack track) =>
            CanBuyUpgrade(SelectedCar != null ? SelectedCar.Id : "light", track);

        public bool CanBuyUpgrade(string carId, UpgradeTrack track)
        {
            if (track == null)
                return false;

            int level = _data.GetUpgradeLevel(carId, track.Id);
            return level < track.MaxLevel && _data.Coins >= track.GetCost(level);
        }

        public bool BuyUpgrade(UpgradeTrack track) =>
            BuyUpgrade(SelectedCar != null ? SelectedCar.Id : "light", track);

        public bool BuyUpgrade(string carId, UpgradeTrack track)
        {
            if (string.IsNullOrEmpty(carId)) carId = "light";
            if (!CanBuyUpgrade(carId, track))
                return false;

            int level = _data.GetUpgradeLevel(carId, track.Id);

            if (!TrySpend(track.GetCost(level)))
                return false;

            _data.SetUpgradeLevel(carId, track.Id, level + 1);
            Changed?.Invoke();
            return true;
        }

        public bool OwnsCar(string carId) => _data.OwnedCarIds.Contains(carId);

        public bool IsCarUnlocked(CarDefinition car)
        {
            if (car == null) return false;
            if (OwnsCar(car.Id)) return true;
            int req = car.GetRequiredCampaignLevel();
            return _data.HighestCampaignLevel >= req;
        }

        public bool BuyCar(CarDefinition car, int cost)
        {
            if (car == null || OwnsCar(car.Id) || !IsCarUnlocked(car) || !TrySpend(cost))
                return false;

            _data.OwnedCarIds.Add(car.Id);
            Changed?.Invoke();
            return true;
        }

        public bool SelectCar(string carId)
        {
            if (!OwnsCar(carId))
                return false;

            _data.SelectedCarId = carId;
            Changed?.Invoke();
            return true;
        }

        // --- Валюта -----------------------------------------------------------------

        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;

            _data.Coins += amount;
            Changed?.Invoke();
        }

        public void RegisterRunResult(int campaignLevel, float distance)
        {
            _data.TotalRuns++;

            if (campaignLevel > _data.HighestCampaignLevel)
                _data.HighestCampaignLevel = campaignLevel;

            if (distance > _data.BestEndlessDistance)
                _data.BestEndlessDistance = distance;

            Changed?.Invoke();
        }

        bool TrySpend(int cost)
        {
            if (cost < 0 || _data.Coins < cost)
                return false;

            _data.Coins -= cost;
            return true;
        }

        /// <summary>Начальное состояние: стартовая машина и базовый набор модификаторов.</summary>
        public static MetaProgressData CreateInitialData(CarDefinition starterCar,
                                                         IEnumerable<string> starterModifiers)
        {
            var data = new MetaProgressData();

            if (starterCar != null)
            {
                data.OwnedCarIds.Add(starterCar.Id);
                data.SelectedCarId = starterCar.Id;
            }

            if (starterModifiers != null)
                foreach (string id in starterModifiers)
                    if (!string.IsNullOrEmpty(id) && !data.UnlockedModifierIds.Contains(id))
                        data.UnlockedModifierIds.Add(id);

            return data;
        }

        /// <summary>Полный доступ к контенту для демонстрации и отладки.</summary>
        public void UnlockEverything(ModifierCatalog catalog)
        {
            if (catalog == null)
                return;

            for (int i = 0; i < catalog.All.Count; i++)
            {
                ModifierDefinition definition = catalog.All[i];

                if (definition != null && !_data.UnlockedModifierIds.Contains(definition.Id))
                    _data.UnlockedModifierIds.Add(definition.Id);
            }

            for (int i = 0; i < _cars.Count; i++)
                if (_cars[i] != null && !_data.OwnedCarIds.Contains(_cars[i].Id))
                    _data.OwnedCarIds.Add(_cars[i].Id);

            for (int i = 0; i < _tracks.Count; i++)
                if (_tracks[i] != null)
                    _data.SetUpgradeLevel(_tracks[i].Id, _tracks[i].MaxLevel);

            Debug.Log("Мета-прогресс открыт полностью (отладочный режим).");
            Changed?.Invoke();
        }
    }
}
