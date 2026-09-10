using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Marks a road segment that is intentionally authored and saved in the scene.</summary>
    public sealed class BakedMapChunk : MonoBehaviour
    {
        [SerializeField, Min(0)] private int order;

        public int Order => order;

        public void Configure(int sequenceOrder)
        {
            order = sequenceOrder;
        }
    }
}
