using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Элитный лидер стаи.
    /// Окружен пульсирующей золотой командной аурой, которая ускоряет всех
    /// соседних зомби и мутантов на +40%, организуя стремительный набег.
    /// </summary>
    public sealed class ElitePackLeader : EnemyBase
    {
        [Header("Leader Aura")]
        [SerializeField, Min(5f)] private float auraRadius = 14f;
        [SerializeField, Range(0.1f, 1f)] private float speedBuffPercent = 0.40f;

        GameObject auraVisual;
        float auraScanTimer;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = 130f;
            currentHealth = maxHealth;
            baseSpeed = 4.5f;
            contactDamage = 22f;
            xpReward = 6;
            coinReward = 4;

            BuildVisuals();
        }

        void BuildVisuals()
        {
            // Золотой диск ауры вокруг лидера
            auraVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            auraVisual.name = "LeaderAuraDisk";
            auraVisual.transform.SetParent(transform, false);
            auraVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            auraVisual.transform.localScale = new Vector3(auraRadius * 2f, 0.02f, auraRadius * 2f);
            Destroy(auraVisual.GetComponent<Collider>());

            Renderer r = auraVisual.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(1f, 0.8f, 0.1f, 0.25f);
                r.sharedMaterial = m;
            }

            // Золотая корона/шипы на лидере
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "LeaderCrown";
            crown.transform.SetParent(transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            crown.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Destroy(crown.GetComponent<Collider>());

            Renderer cr = crown.GetComponent<Renderer>();
            if (cr != null)
            {
                Material cm = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                cm.color = new Color(1f, 0.75f, 0.1f);
                cr.sharedMaterial = cm;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (IsDead) return;

            // Пульсация диска ауры
            if (auraVisual != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.06f;
                auraVisual.transform.localScale = new Vector3(auraRadius * 2f * pulse, 0.02f, auraRadius * 2f * pulse);
            }

            // Периодическое наложение баффа на соседних врагов
            auraScanTimer -= Time.deltaTime;
            if (auraScanTimer <= 0f)
            {
                auraScanTimer = 0.5f;
                BuffNearbyEnemies();
            }
        }

        void BuffNearbyEnemies()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, auraRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                EnemyBase otherEnemy = colliders[i].GetComponentInParent<EnemyBase>();
                if (otherEnemy != null && otherEnemy != this && !otherEnemy.IsDead)
                {
                    // Снимаем замедление с союзников и даем ускорение
                    otherEnemy.TakeDamage(0f, -speedBuffPercent, 0f);
                }
            }
        }
    }
}
