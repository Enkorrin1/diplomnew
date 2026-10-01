using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public sealed class JourneyItemTemplate : MonoBehaviour
    {
        [SerializeField] string resourceKey;
        public string ResourceKey=>resourceKey;
        public void Configure(string key)=>resourceKey=key;
    }
}
