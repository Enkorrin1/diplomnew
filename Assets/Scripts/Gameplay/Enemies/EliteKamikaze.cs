using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Элитный враг-камикадзе.
    /// Быстрый мутант с установленной на крыше тикающей бомбой.
    /// При сближении с машиной игрока начинает учащенно пищать и взрывается,
    /// нанося огромный урон по площади и сотрясая экран.
    /// </summary>
    public sealed class EliteKamikaze : EnemyBase
    {
        [Header("Kamikaze Explosion")]
        [SerializeField, Min(1f)] private float detonationRadius = 3.5f;
        [SerializeField, Min(1f)] private float explosionRadius = 6.0f;
        [SerializeField, Min(10f)] private float explosionDamage = 40f;

        float beepTimer;
        bool hasDetonated;
        GameObject beaconLight;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = 40f;
            baseSpeed = 8.5f;
            contactDamage = 35f;
            xpReward = 3;
            coinReward = 3;

            BuildVisuals();
        }

        void BuildVisuals()
        {
            // Красный мигающий маячок на крыше
            beaconLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beaconLight.name = "BombBeacon";
            beaconLight.transform.SetParent(transform, false);
            beaconLight.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            beaconLight.transform.localScale = Vector3.one * 0.45f;
            Destroy(beaconLight.GetComponent<Collider>());

            Renderer r = beaconLight.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = Color.red;
                r.sharedMaterial = m;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (IsDead || hasDetonated) return;

            // Мигание маячка
            if (beaconLight != null)
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                beaconLight.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.55f, pulse);
            }

            if (playerTarget == null) return;

            float dist = Vector3.Distance(transform.position, playerTarget.position);

            // Учащающийся писк бомбы
            beepTimer -= Time.deltaTime;
            float beepInterval = Mathf.Clamp(dist / 20f, 0.12f, 0.6f);
            if (beepTimer <= 0f && dist < 18f)
            {
                beepTimer = beepInterval;
                float pitch = Mathf.Lerp(1.8f, 1.1f, dist / 18f);
                RogueDrive.Audio.AudioManager.Instance?.PlayMineBeep(pitch);
            }

            // Автоподрыв при достижении критической дистанции
            if (dist <= detonationRadius)
            {
                Detonate();
            }
        }

        public override void TakeDamage(float amount, float slowAmount = 0f, float burnDmg = 0f)
        {
            if (hasDetonated) return;

            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                Detonate();
            }
        }

        void Detonate()
        {
            if (hasDetonated) return;
            hasDetonated = true;

            // Звук и тряска камеры
            RogueDrive.Audio.AudioManager.Instance?.PlayExplosion(1.1f);
            ArcadeCameraFollow.Instance?.TriggerShake(0.85f, 0.4f);

            // Урон по площади
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col.gameObject == gameObject) continue;

                // Урон машине игрока
                ArcadeCarController car = col.GetComponentInParent<ArcadeCarController>();
                if (car != null && car.Run != null)
                {
                    car.Run.TakeDamage(explosionDamage);
                }

                // Урон другим врагам и бочкам в радиусе взрыва
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead && !(dmg is EliteKamikaze k && k.hasDetonated))
                {
                    dmg.TakeDamage(explosionDamage * 2.5f);
                }
            }

            // Вспышка взрыва
            CreateExplosionFlash(transform.position);

            Die();
        }

        void CreateExplosionFlash(Vector3 pos)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "KamikazeFlash";
            flash.transform.position = pos + Vector3.up * 0.5f;
            flash.transform.localScale = Vector3.one * (explosionRadius * 1.1f);
            Destroy(flash.GetComponent<Collider>());

            Renderer r = flash.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(1f, 0.35f, 0.05f, 0.85f);
                r.sharedMaterial = m;
            }

            Destroy(flash, 0.2f);
        }
    }
}
