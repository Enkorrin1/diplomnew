using System;

namespace ithappy.City_Characters_Mini.CharacterCustomizationTool.Editor.Extensions
{
    public static class GenericExtensions
    {
        public static void Then<T>(this T value, Action<T> action) => action(value);
    }
}