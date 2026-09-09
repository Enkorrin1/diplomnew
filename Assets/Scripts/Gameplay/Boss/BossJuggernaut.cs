using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    public enum BossPhase
    {
        Phase1_Minefield = 1,
        Phase2_ArtilleryEscort = 2,
        Phase3_BerserkRam = 3
    }

    /// <summary>
    /// Финальный босс 4-го сектора: Бронированный Джаггернаут-перехватчик.
    /// Трехфазный бой: сброс мин, залповый огонь с эскортом камикадзе,
    /// яростный лобовой/бортовой таран и грандиозный финальный взрыв с эвакуацией.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BossJuggernaut : MonoBehaviour, IDamageable
    {
        public static event Action<BossJuggernaut> BossSpawned;
        public static event Action<BossJuggernaut> BossDefeated;
        public static event Action<BossJuggernaut> BossDamaged;

        [Header("Boss Stats")]
        [SerializeField, Min(100f)] private float maxHealth = 1500f;
        [SerializeField] private string bossTitle = "ДЖАГГЕРНАУТ-ПЕРЕХВАТЧИК // MK-IV";

        [Header("Combat Prefabs")]
        [SerializeField] private GameObject minePrefab;
        [SerializeField] private GameObject kamikazePrefab;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject coinPrefab;

        float currentHealth;
        BossPhase currentPhase = BossPhase.Phase1_Minefield;
        bool isDead;

        ArcadeCarController playerCar;
        Rigidbody rb;

        float attackTimer;
        float escortTimer;
        float turretShotTimer;
        float sirenTimer;

        public bool IsDead => isDead;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public BossPhase Phase => currentPhase;
        public string BossTitle => bossTitle;

        private void Awake()
        {
            currentHealth = maxHealth;
            rb = GetComponent<Rigidbody>();
            rb.mass = 12000f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider col = GetComponent<BoxCollider>();
            col.size = new Vector3(3.8f, 3.2f, 9.5f);
            col.center = new Vector3(0f, 1.6f, 0f);

            BuildBossModel();
        }

        private void Start()
        {
            playerCar = FindFirstObjectByType<ArcadeCarController>();
            BossSpawned?.Invoke(this);

            // Сирена тревоги при появлении босса
            RogueDrive.Audio.AudioManager.Instance?.PlaySiren(1.0f);
            ArcadeCameraFollow.Instance?.TriggerShake(1.0f, 0.6f);

            PrototypeHud.Instance?.ShowBiomeNotification(
                "ВНИМАНИЕ: ОСОБО ОПАСНАЯ ЦЕЛЬ!",
                bossTitle,
                new Color(1f, 0.15f, 0.15f));
        }

        private void Update()
        {
            if (isDead) return;

            if (playerCar == null)
            {
                playerCar = FindFirstObjectByType<ArcadeCarController>();
                if (playerCar == null) return;
            }

            float dt = Time.deltaTime;
            UpdatePhase();
            ExecuteBossBehavior(dt);
        }

        void UpdatePhase()
        {
            float hpPercent = currentHealth / maxHealth;

            if (hpPercent > 0.66f && currentPhase != BossPhase.Phase1_Minefield)
            {
                currentPhase = BossPhase.Phase1_Minefield;
            }
            else if (hpPercent <= 0.66f && hpPercent > 0.33f && currentPhase != BossPhase.Phase2_ArtilleryEscort)
            {
                currentPhase = BossPhase.Phase2_ArtilleryEscort;
                OnPhase2Entered();
            }
            else if (hpPercent <= 0.33f && currentPhase != BossPhase.Phase3_BerserkRam)
            {
                currentPhase = BossPhase.Phase3_BerserkRam;
                OnPhase3Entered();
            }
        }

        void OnPhase2Entered()
        {
            RogueDrive.Audio.AudioManager.Instance?.PlaySiren(1.0f);
            ArcadeCameraFollow.Instance?.TriggerShake(0.8f, 0.5f);
            PrototypeHud.Instance?.ShowBiomeNotification(
                "ФАЗА 2: ЗАЛПОВЫЙ ОГОНЬ И ЭСКОРТ!",
                "Босс выпускает дронов-камикадзе и открывает огонь из турелей!",
                Color.yellow);
        }

        void OnPhase3Entered()
        {
            RogueDrive.Audio.AudioManager.Instance?.PlaySiren(1.0f);
            RogueDrive.Audio.AudioManager.Instance?.PlayExplosion(1.0f);
            ArcadeCameraFollow.Instance?.TriggerShake(1.2f, 0.8f);
            PrototypeHud.Instance?.ShowBiomeNotification(
                "ФАЗА 3: ЯРОСТНЫЙ ТАРАН (BERSERK)!",
                "Джаггернаут включает форсаж и идет на смертельный таран!",
                new Color(1f, 0.2f, 0.2f));
        }

        void ExecuteBossBehavior(float dt)
        {
            Vector3 targetPos = playerCar.transform.position;
            Vector3 myPos = transform.position;

            float playerSpeed = Mathf.Max(12f, playerCar.SpeedMps);

            switch (currentPhase)
            {
                case BossPhase.Phase1_Minefield:
                    // Держится на 28м впереди игрока
                    Vector3 desiredFront = targetPos + playerCar.transform.forward * 28f;
                    desiredFront.y = 0.5f;
                    transform.position = Vector3.MoveTowards(transform.position, desiredFront, (playerSpeed + 1.5f) * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, playerCar.transform.rotation, dt * 3f);

                    // Сброс мин сзади
                    attackTimer -= dt;
                    if (attackTimer <= 0f)
                    {
                        attackTimer = 3.2f;
                        DropMine();
                    }
                    break;

                case BossPhase.Phase2_ArtilleryEscort:
                    // Держится на 24м впереди игрока, смещаясь зигзагом
                    float weaveX = Mathf.Sin(Time.time * 1.5f) * 6.5f;
                    Vector3 desiredP2 = targetPos + playerCar.transform.forward * 24f + playerCar.transform.right * weaveX;
                    desiredP2.y = 0.5f;
                    transform.position = Vector3.MoveTowards(transform.position, desiredP2, (playerSpeed + 2.0f) * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, playerCar.transform.rotation, dt * 3f);

                    // Сброс мин
                    attackTimer -= dt;
                    if (attackTimer <= 0f)
                    {
                        attackTimer = 2.4f;
                        DropMine();
                    }

                    // Призыв эскорта камикадзе
                    escortTimer -= dt;
                    if (escortTimer <= 0f)
                    {
                        escortTimer = 6.0f;
                        SpawnEscort();
                    }

                    // Стрельба турелями назад по игроку
                    turretShotTimer -= dt;
                    if (turretShotTimer <= 0f)
                    {
                        turretShotTimer = 1.4f;
                        FireTurretBurst();
                    }
                    break;

                case BossPhase.Phase3_BerserkRam:
                    // Босс стремительно сближается для тарана!
                    Vector3 ramDirection = (targetPos - myPos);
                    ramDirection.y = 0f;
                    if (ramDirection.sqrMagnitude > 1f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ramDirection), dt * 4f);
                        transform.position += transform.forward * (playerSpeed * 1.4f * dt);
                    }

                    // Регулярные сирены
                    sirenTimer -= dt;
                    if (sirenTimer <= 0f)
                    {
                        sirenTimer = 2.0f;
                        RogueDrive.Audio.AudioManager.Instance?.PlaySiren(0.8f);
                    }
                    break;
            }
        }

        void DropMine()
        {
            Vector3 dropPos = transform.position - transform.forward * 5.2f + transform.right * UnityEngine.Random.Range(-2.5f, 2.5f);
            dropPos.y = 0.3f;

            if (minePrefab != null)
            {
                Instantiate(minePrefab, dropPos, Quaternion.identity);
            }
            else
            {
                // Процедурный спавн если префаб не привязан в инспекторе
                GameObject m = new GameObject("Proc_BossMine");
                m.transform.position = dropPos;
                m.AddComponent<BossMine>();
            }

            RogueDrive.Audio.AudioManager.Instance?.PlayMineBeep(1.2f);
        }

        void SpawnEscort()
        {
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 spawnPos = transform.position + transform.right * (i * 8.5f) + transform.forward * 2f;
                spawnPos.y = 0.5f;

                if (kamikazePrefab != null)
                {
                    Instantiate(kamikazePrefab, spawnPos, Quaternion.identity);
                }
                else
                {
                    GameObject k = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    k.name = "Proc_Kamikaze";
                    k.transform.position = spawnPos;
                    k.AddComponent<EliteKamikaze>();
                }
            }
        }

        void FireTurretBurst()
        {
            RogueDrive.Audio.AudioManager.Instance?.PlayShoot(0.2f);

            Vector3 fireOrigin = transform.position + Vector3.up * 2.5f - transform.forward * 3f;
            Vector3 toCar = (playerCar.transform.position - fireOrigin).normalized;

            if (projectilePrefab != null)
            {
                GameObject proj = Instantiate(projectilePrefab, fireOrigin, Quaternion.LookRotation(toCar));
                Projectile p = proj.GetComponent<Projectile>();
                if (p != null) p.Launch(toCar, 12f);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isDead) return;

            ArcadeCarController car = collision.gameObject.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                float ramDmg = currentPhase == BossPhase.Phase3_BerserkRam ? 45f : 20f;
                car.Run?.TakeDamage(ramDmg);
                TakeDamage(60f); // Машина игрока тоже наносит урон боссу при таране

                ArcadeCameraFollow.Instance?.TriggerShake(1.0f, 0.5f);
                RogueDrive.Audio.AudioManager.Instance?.PlayCrash(1.2f);
            }
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (isDead) return;

            currentHealth -= amount;
            BossDamaged?.Invoke(this);

            if (currentHealth <= 0f)
            {
                DefeatBoss();
            }
        }

        void DefeatBoss()
        {
            if (isDead) return;
            isDead = true;
            currentHealth = 0f;

            // Замедление времени для триумфального финала
            Time.timeScale = 0.22f;

            // Взрывы и тряска
            RogueDrive.Audio.AudioManager.Instance?.PlayExplosion(1.5f);
            RogueDrive.Audio.AudioManager.Instance?.PlayFanfare();
            ArcadeCameraFollow.Instance?.TriggerShake(1.5f, 1.2f);

            // Создание серии детонаций по корпусу
            CreateMultipleExplosions();

            // Спавн мега-дропа золота и полного бака топлива
            SpawnVictoryLoot();

            // Фиксация победы в кампании
            try
            {
                var meta = RogueDrive.Meta.SaveService.GetActiveProgress();
                if (meta != null)
                {
                    meta.RegisterRunResult(5, playerCar != null && playerCar.Run != null ? playerCar.Run.Distance : 4000f);
                    meta.AddCoins(50);
                    RogueDrive.Meta.SaveService.SaveActive();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BossJuggernaut] Ошибка сохранения победы: {ex.Message}");
            }

            BossDefeated?.Invoke(this);

            // Восстановление нормального масштаба времени через 2.5 секунды
            Invoke(nameof(RestoreTimeScaleAndNotifyVictory), 0.6f);
        }

        void RestoreTimeScaleAndNotifyVictory()
        {
            Time.timeScale = 1.0f;
            PrototypeHud.Instance?.ShowCampaignVictoryScreen();
        }

        void CreateMultipleExplosions()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-1.5f, 1.5f),
                    UnityEngine.Random.Range(0.5f, 2.5f),
                    UnityEngine.Random.Range(-3.5f, 3.5f));

                GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "BossExplosionFlash";
                flash.transform.position = transform.position + offset;
                flash.transform.localScale = Vector3.one * UnityEngine.Random.Range(4f, 7f);
                Destroy(flash.GetComponent<Collider>());

                Renderer r = flash.GetComponent<Renderer>();
                if (r != null)
                {
                    Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                    m.color = new Color(1f, 0.45f, 0.1f, 0.9f);
                    r.sharedMaterial = m;
                }

                Destroy(flash, 0.4f + i * 0.1f);
            }
        }

        void SpawnVictoryLoot()
        {
            GameRunController run = playerCar != null ? playerCar.Run : FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                run.AddCoins(50);
                run.AddFuel(run.MaxFuel);
                run.Heal(run.MaxHealth);
            }

            // Золотой фонтан монет
            for (int i = 0; i < 15; i++)
            {
                Vector3 pos = transform.position + UnityEngine.Random.insideUnitSphere * 2.5f;
                pos.y = 0.5f;

                if (coinPrefab != null)
                {
                    Instantiate(coinPrefab, pos, Quaternion.identity);
                }
                else
                {
                    GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coin.name = "VictoryCoin";
                    coin.transform.position = pos;
                    coin.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
                    coin.AddComponent<CoinPickup>();
                }
            }
        }

        void BuildBossModel()
        {
            GameObject model = new GameObject("BossVisualModel");
            model.transform.SetParent(transform, false);

            Material armorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            armorMat.color = new Color(0.12f, 0.13f, 0.15f);

            Material platingMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            platingMat.color = new Color(0.28f, 0.1f, 0.1f); // Темно-бордовые бронеплиты

            Material redLightMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            redLightMat.color = new Color(1f, 0.1f, 0.1f);

            // Основной кузов грузовика (Main Frame)
            CreateCube(model.transform, new Vector3(0f, 1.4f, 0f), new Vector3(3.6f, 2.2f, 9.0f), armorMat);

            // Кабина (Cabin)
            CreateCube(model.transform, new Vector3(0f, 2.6f, 2.2f), new Vector3(3.4f, 1.4f, 3.2f), platingMat);

            // Лобовая бронерешетка / ковш (Front Ram)
            CreateCube(model.transform, new Vector3(0f, 0.8f, 4.6f), new Vector3(3.8f, 1.4f, 0.6f), armorMat);

            // Красные фары (Red Headlights)
            CreateCube(model.transform, new Vector3(-1.3f, 1.2f, 4.85f), new Vector3(0.5f, 0.3f, 0.1f), redLightMat);
            CreateCube(model.transform, new Vector3(1.3f, 1.2f, 4.85f), new Vector3(0.5f, 0.3f, 0.1f), redLightMat);

            // Сдвоенные турели на крыше (Dual Roof Turrets)
            CreateCube(model.transform, new Vector3(-1.0f, 3.5f, 0.5f), new Vector3(0.6f, 0.6f, 1.8f), armorMat);
            CreateCube(model.transform, new Vector3(1.0f, 3.5f, 0.5f), new Vector3(0.6f, 0.6f, 1.8f), armorMat);

            // 8 тяжелых колес (8 Heavy Wheels)
            float[] zWheelPos = { -3.2f, -1.2f, 1.2f, 3.2f };
            Material wheelMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wheelMat.color = new Color(0.05f, 0.05f, 0.05f);

            foreach (float z in zWheelPos)
            {
                CreateWheel(model.transform, new Vector3(-1.9f, 0.6f, z), wheelMat);
                CreateWheel(model.transform, new Vector3(1.9f, 0.6f, z), wheelMat);
            }
        }

        void CreateCube(Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            Destroy(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void CreateWheel(Transform parent, Vector3 localPos, Material mat)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPos;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(1.2f, 0.35f, 1.2f);
            Destroy(wheel.GetComponent<Collider>());
            wheel.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
