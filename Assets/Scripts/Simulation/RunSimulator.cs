using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Simulation
{
    /// <summary>Параметры одной серии прогонов.</summary>
    public sealed class SimulationSetup
    {
        public ModifierCatalog Catalog;
        public CarDefinition Car;
        public DifficultyProfile Difficulty;

        /// <summary>Ноль означает равновероятную выборку (контрольная группа).</summary>
        public WeightingConfig Weighting;

        /// <summary>
        /// База характеристик. Ноль означает голую машину без учёта покупок в гараже —
        /// именно так ставится чистый эксперимент по системе модификаторов.
        /// </summary>
        public IBaseStatsProvider BaseStats;

        /// <summary>Ноль означает полностью открытый пул.</summary>
        public IUnlockProvider Unlocks;

        /// <summary>Метка конфигурации для выгрузки: имя варианта настроек.</summary>
        public string ConfigurationName = "default";

        public AgentKind Agent = AgentKind.Priority;

        public float StepSeconds = 0.25f;
        public float MaxRunSeconds = 600f;
        public int OffersPerLevel = 3;
    }

    public enum AgentKind
    {
        Random,
        Priority,
        SynergySeeking,

        /// <summary>
        /// Игровая схема «казино»: игрок не выбирает, применяется первый элемент
        /// взвешенной выборки. Жетоны копятся за уровни и тратятся в пунктах на
        /// трассе обычной ставкой. Поведение игрока исключено, измеряется только генератор.
        /// </summary>
        Casino,

        /// <summary>Та же схема казино, но жетоны ставятся по жадной политике ставок.</summary>
        CasinoBettor
    }

    /// <summary>
    /// Прогон одного заезда без отрисовки. Отбор и применение модификаторов
    /// выполняются штатными классами системы, поэтому измеряется та же логика,
    /// которая работает в игре.
    ///
    /// Для агентов казино воспроизводится игровая схема прогрессии: уровень даёт
    /// жетон, жетоны тратятся при пересечении отметок пунктов-казино, непотраченные
    /// к концу заезда жетоны пропадают.
    /// </summary>
    public sealed class RunSimulator
    {
        public RunResult Run(SimulationSetup setup, int seed)
        {
            var sockets = new HeadlessSocketProvider(setup.Car);
            IBaseStatsProvider baseStats = setup.BaseStats ?? new CarBaseStats(setup.Car);
            IUnlockProvider unlocks = setup.Unlocks ?? new AllUnlocked();

            ModifierSession session = ModifierSession.Create(
                setup.Catalog, baseStats, sockets, unlocks, seed, setup.Weighting);

            var modelRandom = new SeededRandom(seed ^ 0x5f3759df);
            var agentRandom = new SeededRandom(seed ^ 0x27d4eb2f);

            ISimulationAgent agent = CreateAgent(setup.Agent, session, agentRandom);
            ICasinoPolicy casino = agent as ICasinoPolicy;
            IRunModel model = new AnalyticRunModel(setup.Difficulty, modelRandom);

            RunContext context = session.Context;
            context.CampaignLevel = setup.Difficulty.LevelLength > 0f ? 1 : 0;

            float[] stops = casino != null && setup.Difficulty.CasinoStopDistances != null
                ? setup.Difficulty.CasinoStopDistances
                : new float[0];
            int nextStop = 0;

            float experience = 0f;
            int levels = 0;
            int killsSinceStart = 0;
            float time = 0f;

            var tally = new CasinoTally();
            int picksToFirstSynergy = -1;
            float distanceAtFirstSynergy = -1f;

            var triggerCounters = new Dictionary<int, int>();

            while (time < setup.MaxRunSeconds && !model.IsFinished(context))
            {
                StepOutcome outcome = model.Step(context, session.Effects, setup.StepSeconds);

                context.Distance += outcome.DistanceGained;
                context.Health -= outcome.DamageTaken;
                context.Fuel -= outcome.FuelSpent;
                context.Kills += outcome.Kills;
                killsSinceStart += outcome.Kills;
                time += setup.StepSeconds;

                ApplyResourceTriggers(context, session.Effects, killsSinceStart, triggerCounters);

                experience += outcome.Kills * setup.Difficulty.ExperiencePerKill;

                while (experience >= ExperienceThreshold(setup.Difficulty, levels))
                {
                    experience -= ExperienceThreshold(setup.Difficulty, levels);
                    levels++;

                    if (casino != null)
                    {
                        tally.Tokens++;
                        continue;
                    }

                    // Пул исчерпан — заезд продолжается без новых модификаторов
                    if (!OfferAndApply(session, agent, setup.OffersPerLevel))
                        break;
                }

                // Пункты казино: жетоны тратятся при пересечении отметки
                while (casino != null && nextStop < stops.Length && context.Distance >= stops[nextStop])
                {
                    nextStop++;
                    SpendTokens(session, casino, tally);
                }

                if (picksToFirstSynergy < 0 && session.Build.ActiveSynergies.Count > 0)
                {
                    picksToFirstSynergy = session.Build.TotalPicks;
                    distanceAtFirstSynergy = context.Distance;
                }
            }

            return new RunResult
            {
                Seed = seed,
                Generator = setup.Weighting != null ? "weighted" : "uniform",
                Agent = agent.Name,
                Configuration = setup.ConfigurationName,
                Completed = setup.Difficulty.LevelLength > 0f
                            && context.Distance >= setup.Difficulty.LevelLength
                            && context.Health > 0f && context.Fuel > 0f,
                Distance = context.Distance,
                TimeSeconds = time,
                Kills = context.Kills,
                Levels = levels,
                HealthLeft = Mathf.Max(0f, context.Health),
                FuelLeft = Mathf.Max(0f, context.Fuel),
                SynergyCount = session.Build.ActiveSynergies.Count,
                ModifierCount = session.Build.Levels.Count,
                BuildSignature = session.Build.Signature(),
                PicksToFirstSynergy = picksToFirstSynergy,
                DistanceAtFirstSynergy = distanceAtFirstSynergy,
                TokensSpent = tally.Spent,
                TokensWasted = tally.Tokens,
                RareBets = tally.RareBets,
                SynergyBets = tally.SynergyBets,
                PityTriggers = session.OfferGenerator.PityTriggers
            };
        }

        sealed class CasinoTally
        {
            public int Tokens;
            public int Spent;
            public int RareBets;
            public int SynergyBets;
        }

        /// <summary>Визит в казино: все накопленные жетоны тратятся по политике ставок.</summary>
        static void SpendTokens(ModifierSession session, ICasinoPolicy policy, CasinoTally tally)
        {
            while (tally.Tokens > 0)
            {
                CasinoBet bet = policy.ChooseBet(tally.Tokens, session);
                int cost = CasinoBetRules.Cost(bet);

                if (cost > tally.Tokens)
                {
                    bet = CasinoBet.Standard;
                    cost = 1;
                }

                IReadOnlyList<ModifierDefinition> offers = session.OfferGenerator.Generate(
                    session.Build, session.Context, 1, OfferConstraint.ForBet(bet));

                if (offers.Count == 0)
                {
                    if (bet == CasinoBet.Standard)
                        return; // пул исчерпан — остаток жетонов пропадает

                    // Под ставку кандидатов не нашлось — обычный спин
                    offers = session.OfferGenerator.Generate(session.Build, session.Context, 1);
                    if (offers.Count == 0)
                        return;

                    bet = CasinoBet.Standard;
                    cost = 1;
                }

                session.Service.Apply(offers[0]);
                tally.Tokens -= cost;
                tally.Spent += cost;

                if (bet == CasinoBet.RareGuaranteed) tally.RareBets++;
                else if (bet == CasinoBet.SynergyHunt) tally.SynergyBets++;
            }
        }

        static bool OfferAndApply(ModifierSession session, ISimulationAgent agent, int offersPerLevel)
        {
            IReadOnlyList<ModifierDefinition> offers =
                session.OfferGenerator.Generate(session.Build, session.Context, offersPerLevel);

            if (offers.Count == 0)
                return false;

            int choice = agent.Choose(offers, session.Context);

            if (choice < 0 || choice >= offers.Count)
                return false;

            session.Service.Apply(offers[choice]);
            return true;
        }

        static float ExperienceThreshold(DifficultyProfile profile, int currentLevel)
        {
            return Mathf.Max(1f, profile.ExperienceToNextLevel.Evaluate(currentLevel));
        }

        static void ApplyResourceTriggers(RunContext context,
                                          EffectContext effects,
                                          int totalKills,
                                          Dictionary<int, int> counters)
        {
            IReadOnlyList<ResourceTrigger> triggers = effects.Resources.Triggers;

            for (int i = 0; i < triggers.Count; i++)
            {
                ResourceTrigger trigger = triggers[i];
                int expected = totalKills / trigger.KillsPerTrigger;

                counters.TryGetValue(i, out int fired);

                if (expected <= fired)
                    continue;

                counters[i] = expected;
                int times = expected - fired;

                if (trigger.Kind == ResourceKind.Fuel)
                {
                    float max = context.Stats.Get(StatId.FuelCapacity);
                    context.Fuel = Mathf.Min(max, context.Fuel + max * trigger.Amount * times);
                }
                else
                {
                    float max = context.Stats.Get(StatId.MaxHealth);
                    context.Health = Mathf.Min(max, context.Health + max * trigger.Amount * times);
                }
            }
        }

        static ISimulationAgent CreateAgent(AgentKind kind, ModifierSession session, IRandomSource random)
        {
            switch (kind)
            {
                case AgentKind.Random:
                    return new RandomAgent(random);

                case AgentKind.SynergySeeking:
                    return new SynergySeekingAgent(session.Synergies, new PriorityAgent());

                case AgentKind.Casino:
                    return new CasinoAgent();

                case AgentKind.CasinoBettor:
                    return new CasinoBettorAgent();

                default:
                    return new PriorityAgent();
            }
        }
    }
}
