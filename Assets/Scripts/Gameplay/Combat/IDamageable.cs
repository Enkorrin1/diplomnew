namespace RogueDrive.Gameplay
{
    /// <summary>Интерфейс любого объекта, способного получать урон в игре (враги, разрушаемые объекты, босс).</summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f);
    }
}
