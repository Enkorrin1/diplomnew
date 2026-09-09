using UnityEngine;
using RogueDrive.Modifiers;
using RogueDrive.Audio;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Автоматическая турель на крыше машины.
    /// Вращается на 360°, непрерывно отслеживает ближайшего врага и ведёт огонь
    /// с учётом характеристик урона, скорострельности и модификаторов снарядов.
    /// </summary>
    public sealed class AutoTurret : MonoBehaviour
    {
        [Header("Targeting & Range")]
        [SerializeField, Min(1f)] private float range = 35f;
        [SerializeField, Min(10f)] private float rotationSpeed = 480f; // градусов в секунду
        [SerializeField] private Transform swivelTransform;         // вращающаяся часть турели
        [SerializeField] private Transform[] muzzleTransforms;      // точки вылета снарядов

        [Header("Base Stats")]
        [SerializeField, Min(1f)] private float baseDamage = 25f;
        [SerializeField, Min(0.1f)] private float baseFireRate = 3.5f; // выстрелов в секунду
        [SerializeField] private GameObject projectilePrefab;

        float fireCooldown;
        Transform currentTarget;

        StatBlock activeStats;
        ProjectilePipeline activeProjectiles;

        public float Range => range;
        public float Damage => activeStats != null && activeStats.Get(StatId.Damage) > 0f 
            ? Mathf.Max(baseDamage, activeStats.Get(StatId.Damage)) 
            : baseDamage;
        public float FireRate => activeStats != null && activeStats.Get(StatId.FireRate) > 0f ? activeStats.Get(StatId.FireRate) : baseFireRate;

        public void BindStats(StatBlock stats, ProjectilePipeline projectiles)
        {
            activeStats = stats;
            activeProjectiles = projectiles;
        }

        private void Awake()
        {
            UpgradeTurretVisualModel();
        }

        private void UpgradeTurretVisualModel()
        {
            if (swivelTransform == null)
            {
                swivelTransform = transform.Find("TurretSwivel");
            }

            if (swivelTransform != null)
            {
                // Всегда скрываем старые примитивные меши базы, ствола и куба поворотника
                Transform oldBarrel = swivelTransform.Find("Barrel");
                if (oldBarrel != null)
                {
                    Renderer ren = oldBarrel.GetComponent<Renderer>();
                    if (ren != null) ren.enabled = false;
                }
                Renderer swivelRen = swivelTransform.GetComponent<Renderer>();
                if (swivelRen != null) swivelRen.enabled = false;

                Transform baseT = transform.Find("TurretBase");
                if (baseT != null)
                {
                    baseT.localScale = new Vector3(0.35f, 0.05f, 0.35f);
                }

                if (swivelTransform.Find("AR_Turret_Gun") == null)
                {
                    GameObject weaponPrefab = null;
#if UNITY_EDITOR
                weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Low Poly AR Weapon Pack 1/Prefabs/Weapons/AR_A_1.prefab");
#endif
                if (weaponPrefab != null)
                {
                    GameObject gun = Instantiate(weaponPrefab, swivelTransform);
                    gun.name = "AR_Turret_Gun";
                    gun.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                    gun.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    gun.transform.localScale = Vector3.one * 1.6f;

                    // Удаляем лишние коллайдеры
                    Collider[] cols = gun.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < cols.Length; i++)
                    {
                        Destroy(cols[i]);
                    }

                    // Обновляем позицию дула на срез ствола винтовки
                    Transform muzzle = swivelTransform.Find("Muzzle");
                    if (muzzle != null)
                    {
                        muzzle.localPosition = new Vector3(0f, 0.12f, 1.4f);
                    }
                }
            }
        }
    }

        private void Update()
        {
            UpdateTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget(currentTarget.position, Time.deltaTime);

                fireCooldown -= Time.deltaTime;
                if (fireCooldown <= 0f)
                {
                    Fire();
                    fireCooldown = 1f / Mathf.Max(0.1f, FireRate);
                }
            }
            else
            {
                // Если врагов в радиусе нет — плавно возвращаем башню прямо по ходу движения
                if (swivelTransform != null)
                {
                    swivelTransform.localRotation = Quaternion.RotateTowards(
                        swivelTransform.localRotation,
                        Quaternion.identity,
                        rotationSpeed * 0.5f * Time.deltaTime);
                }
            }
        }

        void UpdateTarget()
        {
            if (currentTarget != null)
            {
                IDamageable d = currentTarget.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead || Vector3.Distance(transform.position, currentTarget.position) > range * 1.15f)
                {
                    currentTarget = null;
                }
            }

            if (currentTarget != null)
                return;

            // Поиск ближайшего врага
            Collider[] colliders = Physics.OverlapSphere(transform.position, range);
            float minDistance = float.MaxValue;
            Transform nearest = null;

            for (int i = 0; i < colliders.Length; i++)
            {
                IDamageable damageable = colliders[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead)
                    continue;

                float dist = Vector3.Distance(transform.position, colliders[i].transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = colliders[i].transform;
                }
            }

            currentTarget = nearest;
        }

        void RotateTowardsTarget(Vector3 targetPos, float dt)
        {
            if (swivelTransform == null)
                return;

            Vector3 direction = targetPos - swivelTransform.position;
            direction.y = 0f; // вращение строго в горизонтальной плоскости

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
                swivelTransform.rotation = Quaternion.RotateTowards(swivelTransform.rotation, targetRot, rotationSpeed * dt);
            }
        }

        void Fire()
        {
            if (projectilePrefab == null)
                return;

            int bounces = activeProjectiles != null ? activeProjectiles.Bounces : 0;
            float slow = activeProjectiles != null ? activeProjectiles.Slow : 0f;
            float burn = activeProjectiles != null ? activeProjectiles.Burn : 0f;
            float dmg = Damage;

            Transform[] muzzles = (muzzleTransforms != null && muzzleTransforms.Length > 0)
                ? muzzleTransforms
                : new[] { swivelTransform != null ? swivelTransform : transform };

            for (int i = 0; i < muzzles.Length; i++)
            {
                Transform muzzle = muzzles[i];
                Vector3 shootDir = muzzle.forward;
                if (currentTarget != null)
                {
                    // Целимся в центр массы врага (0.6м выше точки опоры)
                    Vector3 targetCenter = currentTarget.position + Vector3.up * 0.6f;
                    Vector3 toTarget = targetCenter - muzzle.position;
                    if (toTarget.sqrMagnitude > 0.001f)
                    {
                        shootDir = toTarget.normalized;
                    }
                }
                if (shootDir.sqrMagnitude < 0.001f)
                {
                    shootDir = transform.forward;
                }

                GameObject pObj = GameplayPool.Instance != null
                    ? GameplayPool.Instance.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(shootDir))
                    : Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(shootDir));

                Projectile proj = pObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.Launch(shootDir, dmg, bounces, slow, burn);
                }
            }

            AudioManager.Instance?.PlayShoot();
        }
    }
}
