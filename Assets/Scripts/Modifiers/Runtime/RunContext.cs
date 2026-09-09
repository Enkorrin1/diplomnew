namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Состояние текущего заезда, доступное генератору предложений.
    /// Не зависит от сцены, что позволяет использовать тот же объект в симуляции.
    /// </summary>
    public sealed class RunContext
    {
        public BuildState Build { get; }
        public StatBlock Stats { get; }

        public float Health;
        public float Fuel;
        public float Distance;
        public int Kills;

        /// <summary>Номер уровня кампании; ноль означает режим непрерывного заезда.</summary>
        public int CampaignLevel;

        public RunContext(BuildState build, StatBlock stats)
        {
            Build = build;
            Stats = stats;
        }

        public bool IsEndless => CampaignLevel == 0;

        public float HealthFraction
        {
            get
            {
                float max = Stats.Get(StatId.MaxHealth);
                return max > 0f ? Health / max : 0f;
            }
        }

        public float FuelFraction
        {
            get
            {
                float max = Stats.Get(StatId.FuelCapacity);
                return max > 0f ? Fuel / max : 0f;
            }
        }

        public void RefillToMaximum()
        {
            Health = Stats.Get(StatId.MaxHealth);
            Fuel = Stats.Get(StatId.FuelCapacity);
        }
    }
}
