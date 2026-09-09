using System.Collections.Generic;
using System.IO;
using RogueDrive.Meta;
using RogueDrive.Modifiers;
using RogueDrive.Simulation;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Генератор базового набора ассетов: двенадцать модификаторов, пять синергий,
    /// три архетипа машин, направления прокачки, профили сложности и конфигурации
    /// взвешивания вместе с готовой серией прогонов.
    ///
    /// Значения здесь — стартовые, а не окончательные: баланс выводится симуляцией.
    /// Смысл генератора в том, чтобы получить воспроизводимую отправную точку,
    /// а не набивать три десятка ассетов вручную.
    /// </summary>
    public static class ContentBootstrap
    {
        const string Root = "Assets/Content";

        [MenuItem("RogueDrive/Создать базовый контент")]
        public static void CreateContent()
        {
            EnsureFolders();

            List<ModifierDefinition> modifiers = CreateModifiers();
            List<SynergyDefinition> synergies = CreateSynergies();
            List<CarDefinition> cars = CreateCars();
            List<UpgradeTrack> tracks = CreateUpgradeTracks();

            ModifierCatalog catalog = CreateAsset<ModifierCatalog>($"{Root}/ModifierCatalog.asset");
            catalog.EditorPopulate(modifiers, synergies);
            EditorUtility.SetDirty(catalog);

            CreateAsset<RewardConfig>($"{Root}/RewardConfig.asset");

            DifficultyProfile campaign = CreateCampaignProfile();
            DifficultyProfile endless = CreateEndlessProfile();

            WeightingConfig neutral = CreateWeighting("Neutral", 1f, 1f, 1f, 1f, 1f, 0f);
            WeightingConfig moderate = CreateWeighting("Moderate", 1f, 0.45f, 0.15f, 1.8f, 1.4f, 0.35f);
            WeightingConfig strong = CreateWeighting("Strong", 1f, 0.45f, 0.15f, 3.5f, 2.2f, 0.45f);

            // cars[1] — бронированная: единственный архетип с полным набором типов сокетов.
            CreateBatch(catalog, cars[1], campaign, neutral, moderate, strong);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Создано: {modifiers.Count} модификаторов, {synergies.Count} синергий, " +
                      $"{cars.Count} машин, {tracks.Count} направлений прокачки. " +
                      $"Профили: {campaign.name}, {endless.name}.");
        }

        // --- Модификаторы -----------------------------------------------------------

        static List<ModifierDefinition> CreateModifiers()
        {
            var list = new List<ModifierDefinition>
            {
                // Атакующие
                Modifier("multishot", "Мульти-выстрел", ModifierCategory.Offensive, Rarity.Common,
                    SocketType.Hood, new[] { SynergyTag.Projectile },
                    Stat(StatId.Damage, 4f, 12f), Stat(StatId.FireRate, 0.4f, 1.2f)),

                Modifier("ricochet", "Рикошет", ModifierCategory.Offensive, Rarity.Rare,
                    SocketType.None, new[] { SynergyTag.Projectile },
                    Projectile(bounces: 1, slow: 0f, burn: 0f)),

                Modifier("side_saws", "Орбитальные пилы", ModifierCategory.Offensive, Rarity.Rare,
                    SocketType.Side, new[] { SynergyTag.Melee },
                    Aura("side_saws", 3f, 6f, 14f, 34f, 0f)),

                Modifier("ram_plow", "Таранный отвал", ModifierCategory.Offensive, Rarity.Common,
                    SocketType.Bumper, new[] { SynergyTag.Ram },
                    Stat(StatId.Mass, 120f, 360f), Aura("ram_plow", 2f, 3.5f, 8f, 22f, 0f)),

                // Стихийные
                Modifier("fire_trail", "Огненный след", ModifierCategory.Elemental, Rarity.Common,
                    SocketType.Exhaust, new[] { SynergyTag.Fire },
                    Aura("fire_trail", 2.5f, 5f, 10f, 26f, 0f)),

                Modifier("chain_lightning", "Цепная молния", ModifierCategory.Elemental, Rarity.Rare,
                    SocketType.None, new[] { SynergyTag.Lightning },
                    Aura("chain_lightning", 3f, 5.5f, 12f, 30f, 1.5f)),

                Modifier("frost_ammo", "Замораживающие снаряды", ModifierCategory.Elemental, Rarity.Common,
                    SocketType.None, new[] { SynergyTag.Frost, SynergyTag.Projectile },
                    Projectile(bounces: 0, slow: 0.12f, burn: 0f)),

                Modifier("shockwave", "Ударная волна", ModifierCategory.Elemental, Rarity.Epic,
                    SocketType.Roof, new[] { SynergyTag.Melee },
                    Aura("shockwave", 4f, 8f, 20f, 55f, 3f)),

                // Защитные и ресурсные
                Modifier("fuel_vampire", "Топливный вампиризм", ModifierCategory.Defensive, Rarity.Rare,
                    SocketType.None, new[] { SynergyTag.Resource },
                    Resource(ResourceKind.Fuel, 50, 0.03f, 0.08f)),

                Modifier("magnet_bumper", "Магнитный бампер", ModifierCategory.Defensive, Rarity.Common,
                    SocketType.Bumper, new[] { SynergyTag.Magnet },
                    Stat(StatId.PickupRadius, 3f, 9f)),

                Modifier("energy_shield", "Силовой щит", ModifierCategory.Defensive, Rarity.Epic,
                    SocketType.None, new[] { SynergyTag.Defense },
                    Aura("energy_shield", 0f, 0f, 0f, 0f, 18f)),

                Modifier("repair_kit", "Ремонтный комплект", ModifierCategory.Defensive, Rarity.Common,
                    SocketType.None, new[] { SynergyTag.Resource, SynergyTag.Defense },
                    Resource(ResourceKind.Health, 60, 0.04f, 0.10f))
            };

            return list;
        }

        static ModifierDefinition Modifier(string id, string title,
                                           ModifierCategory category, Rarity rarity,
                                           SocketType socket, SynergyTag[] tags,
                                           params ModifierEffect[] effects)
        {
            var definition = CreateAsset<ModifierDefinition>($"{Root}/Modifiers/{id}.asset");

            definition.Id = id;
            definition.DisplayName = title;
            definition.Category = category;
            definition.Rarity = rarity;
            definition.MaxLevel = 3;
            definition.RequiredSocket = socket;
            definition.Tags = tags;
            definition.Effects = new List<ModifierEffect>(effects);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        static StatEffect Stat(StatId target, float atLevel1, float atLevel3)
        {
            return new StatEffect
            {
                Target = target,
                AdditivePerLevel = AnimationCurve.Linear(1f, atLevel1, 3f, atLevel3),
                MultiplierPerLevel = AnimationCurve.Constant(1f, 3f, 1f)
            };
        }

        static ProjectileEffect Projectile(int bounces, float slow, float burn)
        {
            return new ProjectileEffect
            {
                BouncesPerLevel = bounces,
                SlowPerLevel = slow,
                BurnPerLevel = burn
            };
        }

        static AuraEffect Aura(string behaviourId,
                               float radius1, float radius3,
                               float damage1, float damage3,
                               float interval)
        {
            return new AuraEffect
            {
                BehaviourId = behaviourId,
                RadiusPerLevel = AnimationCurve.Linear(1f, radius1, 3f, radius3),
                TickDamagePerLevel = AnimationCurve.Linear(1f, damage1, 3f, damage3),
                IntervalPerLevel = AnimationCurve.Constant(1f, 3f, interval)
            };
        }

        static ResourceEffect Resource(ResourceKind kind, int killsPerTrigger,
                                       float amount1, float amount3)
        {
            return new ResourceEffect
            {
                Kind = kind,
                KillsPerTrigger = killsPerTrigger,
                AmountPerLevel = AnimationCurve.Linear(1f, amount1, 3f, amount3)
            };
        }

        // --- Синергии ---------------------------------------------------------------

        static List<SynergyDefinition> CreateSynergies()
        {
            return new List<SynergyDefinition>
            {
                Synergy("thermal_shock", "Термошок", new[] { "frost_ammo", "fire_trail" },
                    Aura("thermal_shock", 3f, 3f, 18f, 18f, 0f)),

                Synergy("charged_rounds", "Заряженные снаряды", new[] { "chain_lightning", "multishot" },
                    Projectile(bounces: 1, slow: 0f, burn: 6f)),

                Synergy("meat_grinder", "Мясорубка", new[] { "side_saws", "magnet_bumper" },
                    Aura("meat_grinder", 4f, 4f, 16f, 16f, 0f)),

                Synergy("impact_burst", "Ударный разряд", new[] { "ram_plow", "energy_shield" },
                    Aura("impact_burst", 5f, 5f, 30f, 30f, 6f)),

                Synergy("siphon_wave", "Волна-сифон", new[] { "fuel_vampire", "shockwave" },
                    Resource(ResourceKind.Fuel, 25, 0.04f, 0.04f))
            };
        }

        static SynergyDefinition Synergy(string id, string title, string[] required,
                                         params ModifierEffect[] effects)
        {
            var definition = CreateAsset<SynergyDefinition>($"{Root}/Synergies/{id}.asset");

            definition.Id = id;
            definition.DisplayName = title;
            definition.RequiredModifierIds = required;
            definition.Effects = new List<ModifierEffect>(effects);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        // --- Машины -----------------------------------------------------------------

        static List<CarDefinition> CreateCars()
        {
            return new List<CarDefinition>
            {
                Car("light", "Лёгкая", 4,
                    health: 80f, fuel: 90f, drain: 0.9f, speed: 26f, mass: 900f,
                    sockets: new[]
                    {
                        Socket(SocketType.Roof, 1), Socket(SocketType.Hood, 1),
                        Socket(SocketType.Side, 1), Socket(SocketType.Exhaust, 1)
                    }),

                // Бронированная покрывает все пять типов сокетов: только на ней
                // доступен весь пул, поэтому она используется в базовой серии прогонов.
                Car("armored", "Бронированная", 6,
                    health: 140f, fuel: 100f, drain: 1.1f, speed: 20f, mass: 1400f,
                    sockets: new[]
                    {
                        Socket(SocketType.Roof, 1), Socket(SocketType.Hood, 2),
                        Socket(SocketType.Side, 1), Socket(SocketType.Exhaust, 1),
                        Socket(SocketType.Bumper, 1)
                    }),

                Car("truck", "Грузовик", 7,
                    health: 190f, fuel: 130f, drain: 1.5f, speed: 17f, mass: 2200f,
                    sockets: new[]
                    {
                        Socket(SocketType.Roof, 1), Socket(SocketType.Hood, 2),
                        Socket(SocketType.Side, 2), Socket(SocketType.Exhaust, 1),
                        Socket(SocketType.Bumper, 1)
                    })
            };
        }

        static SocketCapacity Socket(SocketType type, int count) =>
            new SocketCapacity { Type = type, Count = count };

        static CarDefinition Car(string id, string title, int expectedSockets,
                                 float health, float fuel, float drain,
                                 float speed, float mass, SocketCapacity[] sockets)
        {
            var definition = CreateAsset<CarDefinition>($"{Root}/Cars/{id}.asset");

            definition.Id = id;
            definition.DisplayName = title;
            definition.Sockets = sockets;
            definition.BaseStats = new[]
            {
                new StatValue(StatId.Damage, 10f),
                new StatValue(StatId.FireRate, 2f),
                new StatValue(StatId.MaxHealth, health),
                new StatValue(StatId.FuelCapacity, fuel),
                new StatValue(StatId.FuelDrain, drain),
                new StatValue(StatId.Speed, speed),
                new StatValue(StatId.Mass, mass),
                new StatValue(StatId.PickupRadius, 4f)
            };

            int total = 0;

            for (int i = 0; i < sockets.Length; i++)
                total += sockets[i].Count;

            if (total != expectedSockets)
                Debug.LogWarning($"{id}: сокетов {total}, ожидалось {expectedSockets}.");

            EditorUtility.SetDirty(definition);
            return definition;
        }

        // --- Прокачка ---------------------------------------------------------------

        static List<UpgradeTrack> CreateUpgradeTracks()
        {
            return new List<UpgradeTrack>
            {
                Track("hull", "Прочность кузова", StatId.MaxHealth, 25f, 5, 300, 1.45f),
                Track("tank", "Объём бака", StatId.FuelCapacity, 20f, 5, 280, 1.45f),
                Track("engine", "Двигатель", StatId.Speed, 2f, 5, 320, 1.5f),
                Track("armament", "Вооружение", StatId.Damage, 3f, 5, 350, 1.5f)
            };
        }

        static UpgradeTrack Track(string id, string title, StatId target,
                                  float valuePerLevel, int maxLevel, int baseCost, float growth)
        {
            var track = CreateAsset<UpgradeTrack>($"{Root}/Upgrades/{id}.asset");

            track.Id = id;
            track.DisplayName = title;
            track.Target = target;
            track.ValuePerLevel = valuePerLevel;
            track.MaxLevel = maxLevel;
            track.BaseCost = baseCost;
            track.CostGrowth = growth;

            EditorUtility.SetDirty(track);
            return track;
        }

        // --- Сложность и взвешивание ------------------------------------------------

        static DifficultyProfile CreateCampaignProfile()
        {
            var profile = CreateAsset<DifficultyProfile>($"{Root}/Simulation/Difficulty_Campaign_01.asset");

            // Подобрано первой калибровочной итерацией: голая машина гибнет
            // примерно на трети дистанции, доехать до финиша можно только собрав билд.
            profile.LevelLength = 4000f;
            profile.EnemiesPerSecond = AnimationCurve.Linear(0f, 2f, 4000f, 9f);
            profile.EnemyHealth = AnimationCurve.Linear(0f, 8f, 4000f, 34f);
            profile.BlockedLaneFraction = AnimationCurve.Linear(0f, 0.05f, 4000f, 0.45f);
            profile.ContactDamage = 3.5f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static DifficultyProfile CreateEndlessProfile()
        {
            var profile = CreateAsset<DifficultyProfile>($"{Root}/Simulation/Difficulty_Endless.asset");

            profile.LevelLength = 0f;
            profile.EnemiesPerSecond = AnimationCurve.Linear(0f, 2f, 12000f, 22f);
            profile.EnemyHealth = AnimationCurve.Linear(0f, 8f, 12000f, 105f);
            profile.BlockedLaneFraction = AnimationCurve.Linear(0f, 0.05f, 12000f, 0.6f);
            profile.ContactDamage = 3.5f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static WeightingConfig CreateWeighting(string name,
                                               float common, float rare, float epic,
                                               float synergyBonus, float roleCompensation,
                                               float lowResourceThreshold)
        {
            var config = CreateAsset<WeightingConfig>($"{Root}/Simulation/Weighting_{name}.asset");

            config.RarityWeights = new[] { common, rare, epic };
            config.SynergyBonus = synergyBonus;
            config.RoleCompensation = roleCompensation;
            config.LowResourceThreshold = lowResourceThreshold;

            config.StackFalloff = name == "Neutral"
                ? AnimationCurve.Constant(0f, 16f, 1f)
                : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.7f), new Keyframe(2f, 0.4f));

            EditorUtility.SetDirty(config);
            return config;
        }

        static void CreateBatch(ModifierCatalog catalog, CarDefinition car,
                                DifficultyProfile difficulty, params WeightingConfig[] variants)
        {
            var batch = CreateAsset<SimulationBatch>($"{Root}/Simulation/Batch_Baseline.asset");

            batch.Catalog = catalog;
            batch.Car = car;
            batch.Difficulty = difficulty;
            batch.RunsPerVariant = 1000;
            batch.Agents = new[] { AgentKind.Random, AgentKind.Priority, AgentKind.SynergySeeking };

            var list = new List<WeightingVariant>
            {
                // Контрольная группа: генератор равновероятной выборки.
                new WeightingVariant { Name = "uniform", Config = null }
            };

            for (int i = 0; i < variants.Length; i++)
                list.Add(new WeightingVariant
                {
                    Name = variants[i].name.Replace("Weighting_", string.Empty),
                    Config = variants[i]
                });

            batch.Variants = list.ToArray();

            EditorUtility.SetDirty(batch);
        }

        // --- Вспомогательное --------------------------------------------------------

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolders()
        {
            string[] folders =
            {
                Root,
                $"{Root}/Modifiers",
                $"{Root}/Synergies",
                $"{Root}/Cars",
                $"{Root}/Upgrades",
                $"{Root}/Simulation"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i]))
                    continue;

                string parent = Path.GetDirectoryName(folders[i]).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folders[i]));
            }
        }
    }
}
