using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    // Stable identity of an authored item; never use runtime instance IDs in saves.
    public sealed class GarageCheckpointItem : MonoBehaviour
    {
        public string id;
    }
}
