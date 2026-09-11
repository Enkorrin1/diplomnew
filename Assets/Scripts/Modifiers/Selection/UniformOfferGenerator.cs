namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Равновероятная выборка среди допустимых модификаторов.
    /// Контрольная группа эксперимента: весов нет, правила пула выключены.
    /// Резолвер синергий нужен лишь для ставки «Синергия» в казино.
    /// </summary>
    public sealed class UniformOfferGenerator : OfferGeneratorBase
    {
        public UniformOfferGenerator(ModifierCatalog catalog,
                                     ISocketProvider sockets,
                                     IUnlockProvider unlocks,
                                     IRandomSource random,
                                     SynergyResolver synergies = null)
            : base(catalog, sockets, unlocks, random, synergies, PoolRules.Disabled)
        {
        }

        protected override float GetWeight(ModifierDefinition definition, BuildState build, RunContext context)
        {
            return 1f;
        }
    }
}
