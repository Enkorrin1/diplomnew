namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Набор приёмников, в которые эффекты вносят вклад при пересчёте билда.
    /// Пересобирается целиком, чтобы результат зависел только от текущего состава
    /// билда, а не от истории применений.
    /// </summary>
    public sealed class EffectContext
    {
        public StatBlock Stats { get; }
        public ProjectilePipeline Projectiles { get; }
        public BehaviourRegistry Behaviours { get; }
        public ResourceRegistry Resources { get; }

        public EffectContext(StatBlock stats,
                             ProjectilePipeline projectiles,
                             BehaviourRegistry behaviours,
                             ResourceRegistry resources)
        {
            Stats = stats;
            Projectiles = projectiles;
            Behaviours = behaviours;
            Resources = resources;
        }

        public static EffectContext CreateDefault()
        {
            return new EffectContext(new StatBlock(),
                                     new ProjectilePipeline(),
                                     new BehaviourRegistry(),
                                     new ResourceRegistry());
        }

        /// <summary>Сброс накопленного вклада перед пересчётом.</summary>
        public void Clear()
        {
            Projectiles.Clear();
            Behaviours.Clear();
            Resources.Clear();
        }
    }
}
