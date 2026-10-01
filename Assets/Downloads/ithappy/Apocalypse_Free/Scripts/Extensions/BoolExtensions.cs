using System;

namespace ithappy.City_Characters_Mini.CharacterCustomizationTool.Editor.Extensions
{
    public static class BoolExtensions
    {
        public static bool Do(this bool b, Action action)
        {
            if (b)
            {
                action();
            }

            return b;
        }
    }
}