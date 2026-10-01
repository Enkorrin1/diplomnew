using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>World roots are serialized asleep, so additive loading cannot awaken duplicate gameplay.</summary>
    public sealed class JourneyStreamWorld : MonoBehaviour
    {
        [SerializeField] int segmentIndex;
        [SerializeField] GameObject world;
        public int SegmentIndex => segmentIndex;
        public GameObject World => world;
        public void Configure(int index, GameObject root) { segmentIndex = index; world = root; }
    }
}
