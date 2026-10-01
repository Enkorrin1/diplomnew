using UnityEngine;

namespace RogueDrive.UI
{
    /// <summary>Non-destructive slicing of the generated low-poly illustration atlases.</summary>
    internal static class WorkshopArt
    {
        static readonly Sprite[] sections = new Sprite[3];
        static readonly Sprite[] parts = new Sprite[16];
        public static Sprite Section(int index) => Tile("sections", sections, index, 3, 1);
        public static Sprite Part(int index) => Tile("parts", parts, index, 4, 4);
        static Sprite Tile(string name, Sprite[] cache, int index, int columns, int rows)
        {
            if (index < 0 || index >= cache.Length) return null;
            if (cache[index] != null) return cache[index];
            var texture = Resources.Load<Texture2D>("UI/Workshop/" + name);
            if (texture == null) return null;
            float width = texture.width / (float)columns, height = texture.height / (float)rows;
            var rect = new Rect(index % columns * width, (rows - 1 - index / columns) * height, width, height);
            // The middle illustration has a small neighbour fragment at its edge.
            if(name == "sections" && index == 1) rect.width *= .94f;
            return cache[index] = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }
    }
}
