using System;
using System.Collections.Generic;

namespace RogueDrive.Meta
{
    [Serializable]
    public struct UpgradeLevelEntry
    {
        public string CarId;
        public string TrackId;
        public int Level;
    }

    /// <summary>
    /// Сохраняемое состояние мета-прогресса. Простые поля и списки —
    /// требование JsonUtility, который не сериализует словари.
    ///
    /// Поле Version версионирует формат: при изменении структуры старые файлы
    /// мигрируются, а не отбрасываются.
    /// </summary>
    [Serializable]
    public class MetaProgressData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        public int Coins;
        public string SelectedCarId;
        public int HighestCampaignLevel;
        public float BestEndlessDistance;
        public int TotalRuns;

        public List<string> OwnedCarIds = new List<string>();
        public List<string> UnlockedModifierIds = new List<string>();
        public List<UpgradeLevelEntry> Upgrades = new List<UpgradeLevelEntry>();

        public string StartingModifierId;

        public int GetUpgradeLevel(string trackId)
        {
            string carId = !string.IsNullOrEmpty(SelectedCarId) ? SelectedCarId : "light";
            return GetUpgradeLevel(carId, trackId);
        }

        public int GetUpgradeLevel(string carId, string trackId)
        {
            if (string.IsNullOrEmpty(carId)) carId = "light";
            for (int i = 0; i < Upgrades.Count; i++)
            {
                string entryCar = string.IsNullOrEmpty(Upgrades[i].CarId) ? "light" : Upgrades[i].CarId;
                if (entryCar == carId && Upgrades[i].TrackId == trackId)
                    return Upgrades[i].Level;
            }

            return 0;
        }

        public void SetUpgradeLevel(string trackId, int level)
        {
            string carId = !string.IsNullOrEmpty(SelectedCarId) ? SelectedCarId : "light";
            SetUpgradeLevel(carId, trackId, level);
        }

        public void SetUpgradeLevel(string carId, string trackId, int level)
        {
            if (string.IsNullOrEmpty(carId)) carId = "light";
            for (int i = 0; i < Upgrades.Count; i++)
            {
                string entryCar = string.IsNullOrEmpty(Upgrades[i].CarId) ? "light" : Upgrades[i].CarId;
                if (entryCar == carId && Upgrades[i].TrackId == trackId)
                {
                    UpgradeLevelEntry entry = Upgrades[i];
                    entry.CarId = carId;
                    entry.Level = level;
                    Upgrades[i] = entry;
                    return;
                }
            }

            Upgrades.Add(new UpgradeLevelEntry { CarId = carId, TrackId = trackId, Level = level });
        }
    }
}
