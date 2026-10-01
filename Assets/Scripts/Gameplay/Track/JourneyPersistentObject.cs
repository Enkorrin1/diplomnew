using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Stable authored identity. The original instance survives scenery unload/reload.</summary>
    public sealed class JourneyPersistentObject : MonoBehaviour
    {
        [SerializeField] string persistentId;
        public string Id => persistentId;
        public void Configure(string id) => persistentId = id;
    }
}
