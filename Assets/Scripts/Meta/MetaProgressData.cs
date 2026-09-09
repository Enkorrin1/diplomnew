using System;
using System.Collections.Generic;

namespace RogueDrive.Meta
{
    [Serializable]
    public struct UpgradeLevelEntry
    {
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
            for (int i = 0; i < Upgrades.Count; i++)
                if (Upgrades[i].TrackId == trackId)
                    return Upgrades[i].Level;

            return 0;
        }

        public void SetUpgradeLevel(string trackId, int level)
        {
            for (int i = 0; i < Upgrades.Count; i++)
            {
                if (Upgrades[i].TrackId != trackId)
                    continue;

                UpgradeLevelEntry entry = Upgrades[i];
                entry.Level = level;
                Upgrades[i] = entry;
                return;
            }

            Upgrades.Add(new UpgradeLevelEntry { TrackId = trackId, Level = level });
        }
    }
}
