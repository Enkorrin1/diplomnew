namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Сборка графа объектов системы модификаторов на один заезд.
    /// Единственная точка, где решается, какой генератор предложений используется,
    /// поэтому переключение между контрольной и экспериментальной группами
    /// не затрагивает остальной код.
    /// </summary>
    public sealed class ModifierSession
    {
        public BuildState Build { get; }
        public EffectContext Effects { get; }
        public RunContext Context { get; }
        public ModifierService Service { get; }
        public IOfferGenerator OfferGenerator { get; }
        public SynergyResolver Synergies { get; }

        ModifierSession(BuildState build,
                        EffectContext effects,
                        RunContext context,
                        ModifierService service,
                        IOfferGenerator offerGenerator,
                        SynergyResolver synergies)
        {
            Build = build;
            Effects = effects;
            Context = context;
            Service = service;
            OfferGenerator = offerGenerator;
            Synergies = synergies;
        }

        /// <param name="baseStats">
        /// База заезда: характеристики машины вместе с купленными улучшениями.
        /// </param>
        /// <param name="weighting">Ноль означает равновероятную выборку (контрольная группа).</param>
        public static ModifierSession Create(ModifierCatalog catalog,
                                             IBaseStatsProvider baseStats,
                                             ISocketProvider sockets,
                                             IUnlockProvider unlocks,
                                             int seed,
                                             WeightingConfig weighting)
        {
            var build = new BuildState();
            var effects = EffectContext.CreateDefault();
            var context = new RunContext(build, effects.Stats);
            var synergies = new SynergyResolver(catalog);
            var random = new SeededRandom(seed);

            var service = new ModifierService(catalog, build, effects, synergies, sockets, baseStats);
            service.Recalculate();
            context.RefillToMaximum();

            IOfferGenerator generator = weighting != null
                ? new WeightedOfferGenerator(catalog, sockets, unlocks, random, weighting, synergies)
                : (IOfferGenerator)new UniformOfferGenerator(catalog, sockets, unlocks, random);

            return new ModifierSession(build, effects, context, service, generator, synergies);
        }
    }
}
