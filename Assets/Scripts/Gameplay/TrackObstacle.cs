using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Single-use obstacle used by the first playable track.</summary>
    public sealed class TrackObstacle : MonoBehaviour
    {
        bool consumed;

        public bool TryConsume()
        {
            if (consumed)
                return false;

            consumed = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
