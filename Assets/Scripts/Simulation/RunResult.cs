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

        public static string CsvHeader =>
            "seed;generator;agent;configuration;completed;distance;time;kills;levels;" +
            "health_left;fuel_left;synergies;modifiers;build";

        public string ToCsvRow()
        {
            var culture = CultureInfo.InvariantCulture;
            var builder = new StringBuilder(160);

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
            builder.Append(BuildSignature);

            return builder.ToString();
        }
    }
}
