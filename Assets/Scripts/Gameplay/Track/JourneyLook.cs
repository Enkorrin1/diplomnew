using UnityEngine;
using PostProcessVolume = UnityEngine.Rendering.PostProcessing.PostProcessVolume;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Тон маршрута: цветокоррекция плавно сменяется по регионам, штормовой слой
    /// проявляется вместе с атмосферой JourneyPresentation. Объёмы лежат в сцене.
    /// </summary>
    public sealed class JourneyLook : MonoBehaviour
    {
        [Tooltip("Объёмы по индексу региона маршрута (город, каньон, перевал, цитадель)")]
        [SerializeField] PostProcessVolume[] regionVolumes;
        [SerializeField] PostProcessVolume stormVolume;
        [Tooltip("Регион, если сцена запущена без непрерывного маршрута")]
        [SerializeField] int fallbackRegion;
        [SerializeField, Min(.01f)] float regionBlendPerSecond = .15f;
        [SerializeField, Min(.01f)] float stormBlendPerSecond = .5f;

        void Start() => Blend(true);
        void Update() => Blend(false);

        void Blend(bool immediate)
        {
            var journey = SeamlessJourneyStream.Instance;
            int region = journey != null ? journey.CurrentSegmentIndex : fallbackRegion;
            float step = immediate ? 1 : Time.deltaTime * regionBlendPerSecond;
            for (int i = 0; i < regionVolumes.Length; i++)
                if (regionVolumes[i] != null)
                    regionVolumes[i].weight = Mathf.MoveTowards(regionVolumes[i].weight, i == region ? 1 : 0, step);
            if (stormVolume == null) return;
            float storm = JourneyPresentation.Instance != null ? JourneyPresentation.Instance.Atmosphere : 0;
            stormVolume.weight = Mathf.MoveTowards(stormVolume.weight, storm, immediate ? 1 : Time.deltaTime * stormBlendPerSecond);
        }
    }
}
