namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Равновероятная выборка среди допустимых модификаторов.
    /// Контрольная группа эксперимента.
    /// </summary>
    public sealed class UniformOfferGenerator : OfferGeneratorBase
    {
        public UniformOfferGenerator(ModifierCatalog catalog,
                                     ISocketProvider sockets,
                                     IUnlockProvider unlocks,
                                     IRandomSource random)
            : base(catalog, sockets, unlocks, random)
        {
        }

        protected override float GetWeight(ModifierDefinition definition, BuildState build, RunContext context)
        {
            return 1f;
        }
    }
}
