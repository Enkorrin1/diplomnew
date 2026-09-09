using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Элитный бронированный громила-минион.
    /// Обладает повышенным запасом здоровья, ковшом-отвалом спереди (снижающим урон спереди на 50%),
    /// останавливает машину при лобовом таране и гарантированно сбрасывает 5 монет и топливо.
    /// </summary>
    public sealed class EliteJuggernautMinion : EnemyBase
    {
        [Header("Armor Shield")]
        [SerializeField, Range(0.1f, 1f)] private float frontalDamageReduction = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = 220f;
            currentHealth = maxHealth;
            baseSpeed = 3.5f;
            contactDamage = 30f;
            xpReward = 8;
            coinReward = 5;
            resistsRamming = true;
            ramVulnerability = 0.35f;

            BuildVisuals();
        }

        void BuildVisuals()
        {
            transform.localScale = Vector3.one * 1.35f;

            // Лобовой ковш-отбойник
            GameObject plow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plow.name = "FrontPlow";
            plow.transform.SetParent(transform, false);
            plow.transform.localPosition = new Vector3(0f, 0.4f, 0.85f);
            plow.transform.localScale = new Vector3(1.8f, 0.9f, 0.4f);
            Destroy(plow.GetComponent<Collider>());

            Renderer r = plow.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = new Color(0.15f, 0.15f, 0.18f);
                r.sharedMaterial = m;
            }
        }

        public override void TakeDamage(float amount, float slowAmount = 0f, float burnDmg = 0f)
        {
            // Проверка попадания спереди относительно взгляда громилы
            float actualAmount = amount;
            if (playerTarget != null)
            {
                Vector3 toPlayer = (playerTarget.position - transform.position).normalized;
                float frontDot = Vector3.Dot(transform.forward, toPlayer);
                if (frontDot > 0.2f)
                {
                    actualAmount *= frontalDamageReduction; // Лобовой щит поглощает половину урона
                }
            }

            base.TakeDamage(actualAmount, slowAmount * 0.5f, burnDmg);
        }

        protected override void Die()
        {
            // Гарантированное выпадение топлива в дополнение к монетам
            GameRunController run = playerCar != null && playerCar.Run != null ? playerCar.Run : FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                run.AddFuel(15f);
            }

            base.Die();
        }
    }
}
