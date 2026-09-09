using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Разрушаемый деревянный ящик с припасами.
    /// При уничтожении выстрелами или таране сбрасывает золотые монеты и канистру топлива.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SupplyCrate : MonoBehaviour, IDamageable
    {
        [Header("Drop Settings")]
        [SerializeField, Min(1)] private int minCoins = 2;
        [SerializeField, Min(1)] private int maxCoins = 5;
        [SerializeField, Min(0f)] private float fuelBonus = 20f;
        [SerializeField] private GameObject coinPrefab;

        bool isDestroyed;

        public bool IsDead => isDestroyed;

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (isDestroyed) return;
            BreakCrate();
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckCollision(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckCollision(collision.gameObject);
        }

        void CheckCollision(GameObject target)
        {
            if (isDestroyed) return;

            ArcadeCarController car = target.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                BreakCrate();
            }
        }

        public void BreakCrate()
        {
            if (isDestroyed) return;
            isDestroyed = true;

            ArcadeCameraFollow.Instance?.TriggerShake(0.4f, 0.2f);
            RogueDrive.Audio.AudioManager.Instance?.PlayCrateBreak();

            // Начисление бонуса топлива
            GameRunController run = FindFirstObjectByType<GameRunController>();
            if (run != null && fuelBonus > 0f)
            {
                run.AddFuel(fuelBonus);
            }

            // Выпадение монет
            int count = Random.Range(minCoins, maxCoins + 1);
            if (coinPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 dropPos = transform.position + Random.insideUnitSphere * 0.5f;
                    dropPos.y = 0.5f;

                    if (GameplayPool.Instance != null)
                        GameplayPool.Instance.Spawn(coinPrefab, dropPos, Quaternion.identity);
                    else
                        Instantiate(coinPrefab, dropPos, Quaternion.identity);
                }
            }
            else if (run != null)
            {
                run.AddCoins(count);
            }

            // Визуальный эффект разлета обломков ящика
            SpawnDebris(transform.position);

            Destroy(gameObject);
        }

        void SpawnDebris(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "CrateDebris";
                plank.transform.position = pos + Random.insideUnitSphere * 0.3f;
                plank.transform.localScale = new Vector3(0.35f, 0.1f, 0.65f);
                plank.transform.rotation = Random.rotation;

                Collider c = plank.GetComponent<Collider>();
                if (c != null) Destroy(c);

                Renderer r = plank.GetComponent<Renderer>();
                if (r != null)
                {
                    Material m = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
                    m.color = new Color(0.6f, 0.42f, 0.25f);
                    r.sharedMaterial = m;
                }

                Destroy(plank, 0.6f);
            }
        }
    }
}
