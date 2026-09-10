using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Привязывает одноразовые Cartoon FX к боевым событиям. Префабы назначаются
    /// генератором прототипа, поэтому runtime-код не зависит от UnityEditor.
    /// </summary>
    public sealed class CombatVfxCatalog : MonoBehaviour
    {
        public static CombatVfxCatalog Instance { get; private set; }

        [Header("Cartoon FX prefabs")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject bulletHitPrefab;
        [SerializeField] private GameObject enemyDeathPrefab;
        [SerializeField] private GameObject ramImpactPrefab;

        [Header("Scale Modifiers")]
        [SerializeField, Range(0.05f, 2f)] private float muzzleFlashScale = 0.25f;
        [SerializeField, Range(0.05f, 2f)] private float bulletHitScale = 0.65f;
        [SerializeField, Range(0.05f, 2f)] private float enemyDeathScale = 1.0f;
        [SerializeField, Range(0.05f, 2f)] private float ramImpactScale = 1.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation) => Spawn(muzzleFlashPrefab, position, rotation, 0.6f, muzzleFlashScale);
        public void SpawnBulletHit(Vector3 position, Quaternion rotation) => Spawn(bulletHitPrefab, position, rotation, 1.2f, bulletHitScale);
        public void SpawnEnemyDeath(Vector3 position) => Spawn(enemyDeathPrefab, position + Vector3.up * 0.6f, Quaternion.identity, 3f, enemyDeathScale);
        public void SpawnRamImpact(Vector3 position, Vector3 direction) => Spawn(ramImpactPrefab, position + Vector3.up * 0.45f, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward), 2.5f, ramImpactScale);

        static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float maxLifetime, float scale = 1f)
        {
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, position, rotation);
            if (Mathf.Abs(scale - 1f) > 0.001f)
            {
                instance.transform.localScale = Vector3.one * scale;

                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    var main = particles[i].main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                var lights = instance.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    lights[i].range *= scale;
                }
            }
            Destroy(instance, maxLifetime);
        }
    }
}
