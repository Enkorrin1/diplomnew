using System.Globalization;
using System.Text;

namespace RogueDrive.Simulation
{
    /// <summary>Результат одного прогона. Строка выгрузки CSV.</summary>
    public struct RunResult
    {
        public int Seed;
        public string Generator;
        public string Agent;
        public string Configuration;

        public bool Completed;
        public float Distance;
        public float TimeSeconds;
        public int Kills;
        public int Levels;

        public float HealthLeft;
        public float FuelLeft;

        public int SynergyCount;
        public int ModifierCount;
        public string BuildSignature;

        /// <summary>Номер выбора, на котором замкнулась первая синергия; −1, если синергий не было.</summary>
        public int PicksToFirstSynergy;

        /// <summary>Дистанция, на которой замкнулась первая синергия; −1, если синергий не было.</summary>
        public float DistanceAtFirstSynergy;

        /// <summary>Жетоны казино: потрачено и пропало непотраченными (только агенты казино).</summary>
        public int TokensSpent;
        public int TokensWasted;
        public int RareBets;
        public int SynergyBets;

        /// <summary>Сколько раз гарантия от невезения вмешалась в выборку.</summary>
        public int PityTriggers;

        public static string CsvHeader =>
            "seed;generator;agent;configuration;completed;distance;time;kills;levels;" +
            "health_left;fuel_left;synergies;modifiers;build;" +
            "picks_to_first_synergy;distance_at_first_synergy;tokens_spent;tokens_wasted;" +
            "rare_bets;synergy_bets;pity_triggers";

        public string ToCsvRow()
        {
            var culture = CultureInfo.InvariantCulture;
            var builder = new StringBuilder(200);

            builder.Append(Seed).Append(';');
            builder.Append(Generator).Append(';');
            builder.Append(Agent).Append(';');
            builder.Append(Configuration).Append(';');
            builder.Append(Completed ? 1 : 0).Append(';');
            builder.Append(Distance.ToString("F1", culture)).Append(';');
            builder.Append(TimeSeconds.ToString("F1", culture)).Append(';');
            builder.Append(Kills).Append(';');
            builder.Append(Levels).Append(';');
            builder.Append(HealthLeft.ToString("F1", culture)).Append(';');
            builder.Append(FuelLeft.ToString("F1", culture)).Append(';');
            builder.Append(SynergyCount).Append(';');
            builder.Append(ModifierCount).Append(';');
            builder.Append(BuildSignature).Append(';');
            builder.Append(PicksToFirstSynergy).Append(';');
            builder.Append(DistanceAtFirstSynergy.ToString("F1", culture)).Append(';');
            builder.Append(TokensSpent).Append(';');
            builder.Append(TokensWasted).Append(';');
            builder.Append(RareBets).Append(';');
            builder.Append(SynergyBets).Append(';');
            builder.Append(PityTriggers);

            return builder.ToString();
        }
    }
}
