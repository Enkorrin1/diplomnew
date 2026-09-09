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

        public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation) => Spawn(muzzleFlashPrefab, position, rotation, 1.2f);
        public void SpawnBulletHit(Vector3 position, Quaternion rotation) => Spawn(bulletHitPrefab, position, rotation, 1.5f);
        public void SpawnEnemyDeath(Vector3 position) => Spawn(enemyDeathPrefab, position + Vector3.up * 0.6f, Quaternion.identity, 3f);
        public void SpawnRamImpact(Vector3 position, Vector3 direction) => Spawn(ramImpactPrefab, position + Vector3.up * 0.45f, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward), 2.5f);

        static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float maxLifetime)
        {
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, position, rotation);
            Destroy(instance, maxLifetime);
        }
    }
}
