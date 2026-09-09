using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Контактная шипованная мина, сбрасываемая Джаггернаутом-перехватчиком.
    /// Детонирует при наезде колесами игрока или при расстреле авто-турелью.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BossMine : MonoBehaviour, IDamageable
    {
        [Header("Mine Properties")]
        [SerializeField, Min(1f)] private float maxHealth = 15f;
        [SerializeField, Min(1f)] private float damage = 30f;
        [SerializeField, Min(1f)] private float explosionRadius = 5.5f;

        float currentHealth;
        bool hasExploded;
        GameObject beacon;
        float beepTimer;

        public bool IsDead => hasExploded;

        private void Awake()
        {
            currentHealth = maxHealth;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            // Корпус мины
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "MineBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            body.transform.localScale = new Vector3(1.4f, 0.25f, 1.4f);
            Destroy(body.GetComponent<Collider>());

            Renderer r = body.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = new Color(0.18f, 0.18f, 0.2f);
                r.sharedMaterial = m;
            }

            // Красный мигающий маячок в центре
            beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "MineBeacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            beacon.transform.localScale = Vector3.one * 0.4f;
            Destroy(beacon.GetComponent<Collider>());

            Renderer br = beacon.GetComponent<Renderer>();
            if (br != null)
            {
                Material bm = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                bm.color = Color.red;
                br.sharedMaterial = bm;
            }

            SphereCollider col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.0f;
            col.center = new Vector3(0f, 0.3f, 0f);
        }

        private void Update()
        {
            if (hasExploded) return;

            // Мигание
            if (beacon != null)
            {
                float p = Mathf.PingPong(Time.time * 8f, 1f);
                beacon.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.5f, p);
            }

            beepTimer -= Time.deltaTime;
            if (beepTimer <= 0f)
            {
                beepTimer = 0.8f;
                RogueDrive.Audio.AudioManager.Instance?.PlayMineBeep(1.4f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasExploded) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                Explode(true);
            }
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (hasExploded) return;

            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                Explode(false);
            }
        }

        void Explode(bool hitCarDirectly)
        {
            if (hasExploded) return;
            hasExploded = true;

            // Звук и сотрясение
            RogueDrive.Audio.AudioManager.Instance?.PlayExplosion(1.2f);
            ArcadeCameraFollow.Instance?.TriggerShake(0.85f, 0.35f);

            // Поражение объектов в радиусе
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col.gameObject == gameObject) continue;

                if (hitCarDirectly)
                {
                    ArcadeCarController car = col.GetComponentInParent<ArcadeCarController>();
                    if (car != null && car.Run != null)
                    {
                        car.Run.TakeDamage(damage);
                    }
                }

                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead && !(dmg is BossMine b && b.hasExploded))
                {
                    dmg.TakeDamage(damage * 1.5f);
                }
            }

            // Вспышка
            CreateFlash(transform.position);

            Destroy(gameObject);
        }

        void CreateFlash(Vector3 pos)
        {
            GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            f.name = "MineExplosionFlash";
            f.transform.position = pos + Vector3.up * 0.4f;
            f.transform.localScale = Vector3.one * (explosionRadius * 1.2f);
            Destroy(f.GetComponent<Collider>());

            Renderer r = f.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(1f, 0.4f, 0.1f, 0.85f);
                r.sharedMaterial = m;
            }

            Destroy(f, 0.18f);
        }
    }
}
