namespace RogueDrive.Modifiers
{
    public enum ModifierCategory
    {
        Offensive,
        Elemental,
        Defensive
    }

    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2
    }

    public enum SocketType
    {
        None,
        Roof,
        Hood,
        Side,
        Exhaust,
        Bumper
    }

    public enum StatId
    {
        Damage,
        FireRate,
        MaxHealth,
        FuelCapacity,
        FuelDrain,
        Speed,
        Mass,
        PickupRadius,
        Grip,
        Suspension
    }

    public enum ResourceKind
    {
        Fuel,
        Health
    }

    public enum SynergyTag
    {
        None,
        Fire,
        Frost,
        Lightning,
        Melee,
        Projectile,
        Defense,
        Resource,
        Magnet,
        Ram
    }
}
