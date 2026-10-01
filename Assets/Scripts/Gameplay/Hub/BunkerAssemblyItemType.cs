namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальные типы переносимых автодеталей и полевых предметов (для бункера, заправок, блокпостов и открытого мира).
    /// </summary>
    public enum CarPartItemType
    {
        None = 0,
        Wheel = 1,          // Запасное колесо на ступицу
        Battery = 2,        // Силовой аккумулятор под капот
        FuelCanister = 3,   // Канистра бензина (15 л)
        WaterCanister = 4,  // Канистра воды для радиатора
        Crowbar = 5,        // Монтировка для самообороны и взлома ящиков
        OilCanister = 7, Engine = 8, Radiator = 9,
        Axe = 6             // Пожарный топор для самообороны и расчистки завалов
    }

    /// <summary>
    /// Псевдоним типа для обратной совместимости со старыми вызовами.
    /// </summary>
    public enum BunkerAssemblyItemType
    {
        None = 0,
        Wheel = 1,
        Battery = 2,
        FuelCanister = 3,
        WaterCanister = 4,
        Crowbar = 5,
        Axe = 6, OilCanister = 7, Engine = 8, Radiator = 9
    }
}
