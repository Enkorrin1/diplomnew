using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>Панель предупреждения о близкой буре — объект сцены; показывает и пишет текст CreepingStormBarrier.</summary>
    public sealed class StormRadarPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text distanceText;

        public GameObject Panel => panel;
        public Text DistanceText => distanceText;
    }
}
