using System;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Эффект модификатора. Не наследует ScriptableObject: хранится списком внутри
    /// ассета модификатора через SerializeReference, поэтому не требует отдельного
    /// файла ассета на каждый эффект.
    ///
    /// Метод вызывается при каждом пересчёте билда, а не единожды при применении.
    /// Реализация обязана быть чистой: только вклад в приёмники, без побочных
    /// изменений собственного состояния.
    /// </summary>
    [Serializable]
    public abstract class ModifierEffect
    {
        public abstract void Contribute(EffectContext context, int level);
    }
}
