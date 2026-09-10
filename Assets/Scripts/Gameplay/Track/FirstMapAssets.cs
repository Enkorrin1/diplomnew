using UnityEngine;

namespace RogueDrive.Gameplay
{
    [CreateAssetMenu(menuName = "RogueDrive/First Map Assets")]
    public sealed class FirstMapAssets : ScriptableObject
    {
        public GameObject garage, gasStation, station, container, lamp, sign, cone, barrier;
        public GameObject sedan, van, police, military, pallet, barrel, tent;
        public GameObject[] rocks;
    }
}
