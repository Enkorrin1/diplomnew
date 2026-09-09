using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    public enum BiomeType
    {
        HighwayOutskirts = 0,
        DustyWasteland = 1,
        ToxicIndustrial = 2,
        CitadelApproach = 3
    }

    /// <summary>
    /// Конфигурация визуального биома трассы.
    /// Определяет цвета асфальта, отбойников, разметки, атмосферного тумана
    /// и частоту появления взрывных бочек и ящиков с припасами.
    /// </summary>
    [Serializable]
    public sealed class BiomeConfig
    {
        public BiomeType type;
        public string title;
        public string subtitle;
        public float startDistance;
        public float endDistance;

        public Color roadColor;
        public Color guardrailColor;
        public Color markingColor;
        public Color fogColor;
        public float fogDensity;

        public float enemyDensityMultiplier = 1.0f;
        public float barrelSpawnChance = 0.5f;
        public float crateSpawnChance = 0.4f;

        public BiomeConfig(BiomeType type, string title, string subtitle, float start, float end,
            Color road, Color guardrail, Color marking, Color fog, float density,
            float enemyMult, float barrelChance, float crateChance)
        {
            this.type = type;
            this.title = title;
            this.subtitle = subtitle;
            this.startDistance = start;
            this.endDistance = end;
            this.roadColor = road;
            this.guardrailColor = guardrail;
            this.markingColor = marking;
            this.fogColor = fog;
            this.fogDensity = density;
            this.enemyDensityMultiplier = enemyMult;
            this.barrelSpawnChance = barrelChance;
            this.crateSpawnChance = crateChance;
        }

        public static BiomeConfig[] GetDefaultBiomes()
        {
            return new BiomeConfig[]
            {
                // Биом 1: Шоссе (0 - 1000м) - Чистый темный асфальт, белая разметка, серебристые отбойники
                new BiomeConfig(
                    BiomeType.HighwayOutskirts,
                    "ШОССЕ: ПРИГОРОД",
                    "СЕКТОР 01 — АСФАЛЬТ И СТАЛЬ",
                    0f, 1000f,
                    new Color(0.14f, 0.15f, 0.18f), // Road
                    new Color(0.65f, 0.68f, 0.72f), // Guardrail
                    new Color(0.95f, 0.95f, 0.95f), // Marking
                    new Color(0.45f, 0.55f, 0.65f), // Fog
                    0.003f, 1.0f, 0.35f, 0.45f
                ),

                // Биом 2: Пылевая Пустошь (1000 - 2500м) - Песчано-рыжий асфальт, ржавые барьеры, теплый песочный туман
                new BiomeConfig(
                    BiomeType.DustyWasteland,
                    "ПЫЛЕВАЯ ПУСТОШЬ",
                    "СЕКТОР 02 — ПЕСЧАНЫЕ БУРИ И РЖАВЧИНА",
                    1000f, 2500f,
                    new Color(0.28f, 0.22f, 0.16f), // Sand-worn road
                    new Color(0.72f, 0.38f, 0.18f), // Rusted guardrails
                    new Color(0.85f, 0.72f, 0.45f), // Faded yellow marking
                    new Color(0.75f, 0.55f, 0.32f), // Amber sand fog
                    0.006f, 1.25f, 0.50f, 0.40f
                ),

                // Биом 3: Затопленная Промзона (2500 - 4500м) - Мокрый темный битум, неоново-кислотные отбойники, токсичный туман
                new BiomeConfig(
                    BiomeType.ToxicIndustrial,
                    "ЗАТОПЛЕННАЯ ПРОМЗОНА",
                    "СЕКТОР 03 — КИСЛОТНЫЕ ИСПАРЕНИЯ И РУИНЫ",
                    2500f, 4500f,
                    new Color(0.08f, 0.12f, 0.10f), // Wet toxic asphalt
                    new Color(0.25f, 0.65f, 0.35f), // Acid-green oxidized guardrails
                    new Color(0.30f, 0.85f, 0.45f), // Neon warning marking
                    new Color(0.18f, 0.40f, 0.28f), // Toxic green fog
                    0.008f, 1.50f, 0.65f, 0.35f
                ),

                // Биом 4: Подступы к Цитадели (4500м+) - Тяжелый бронебетон, красные барьеры тревоги, багровый туман
                new BiomeConfig(
                    BiomeType.CitadelApproach,
                    "ПОДСТУПЫ К ЦИТАДЕЛИ",
                    "СЕКТОР 04 — ФОРТИФИКАЦИИ И ТРЕВОГА",
                    4500f, 99999f,
                    new Color(0.12f, 0.10f, 0.12f), // Reinforced blast road
                    new Color(0.82f, 0.18f, 0.18f), // Red alert barriers
                    new Color(1.00f, 0.25f, 0.20f), // Crimson neon marking
                    new Color(0.55f, 0.15f, 0.15f), // Ominous red sky fog
                    0.010f, 1.85f, 0.75f, 0.30f
                )
            };
        }
    }
}
