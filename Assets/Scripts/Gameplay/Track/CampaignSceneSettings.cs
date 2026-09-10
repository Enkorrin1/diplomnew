using UnityEngine;
namespace RogueDrive.Gameplay
{
    [CreateAssetMenu(menuName="RogueDrive/Campaign Scene Settings")]
    public sealed class CampaignSceneSettings : ScriptableObject
    {
        [SerializeField] private BiomeConfig[] biomes = BiomeConfig.GetDefaultBiomes();
        [SerializeField, Min(50)] private float enemyActivationDistance = 240f;
        public BiomeConfig[] Biomes => biomes;
        public float EnemyActivationDistance => enemyActivationDistance;
    }
}
