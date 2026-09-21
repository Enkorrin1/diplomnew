using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Modifiers;
using RogueDrive.Meta;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Главный координатор сессии заезда.
    /// Связывает данные модификаторов, физический автомобиль, авто-турель,
    /// систему опыта, генератор предложений и придорожное казино.
    ///
    /// Схема прогрессии: опыт с зомби → уровень → жетон казино. Жетоны копятся,
    /// а тратятся только в промежуточных пунктах на трассе (<see cref="BuffCasinoStop"/>),
    /// где слот-машина сама выдаёт случайный модификатор из взвешенного пула.
    /// </summary>
    public sealed class GameSessionCoordinator : MonoBehaviour
    {
        [Header("Data Configurations")]
        [SerializeField] private CarDefinition carDefinition;
        [SerializeField] private ModifierCatalog modifierCatalog;
        [SerializeField] private WeightingConfig weightingConfig;

        [Header("Scene References")]
        [SerializeField] private ArcadeCarController carController;
        [SerializeField] private AutoTurret turret;
        [SerializeField] private GameRunController runController;
        [SerializeField] private RunExperienceManager experienceManager;
        [SerializeField] private BuffCasinoView casinoView;

        [Header("Casino")]
        [Tooltip("Сколько кандидатов прокручивает барабан за одно вращение (первый — выпавший).")]
        [SerializeField, Range(3, 12)] private int reelSize = 8;
        [Tooltip("Задержка между въездом в пункт и открытием казино, сек.")]
        [SerializeField, Min(0f)] private float casinoOpenDelay = 0.8f;
        [Tooltip("Курс обмена непотраченных жетонов на монеты на финише этапа.")]
        [SerializeField, Min(0)] private int coinsPerUnspentToken = 10;

        ModifierSession session;
        ISocketProvider sockets;
        int killCounter;
        int casinoTokens;

        /// <summary>Жетоны, сознательно пронесённые мимо прошлого пункта: за них начислит банк.</summary>
        int bankedTokens;
        bool fullCarAnnounced;
        Coroutine casinoOpening;

        public ModifierSession Session => session;

        /// <summary>Накопленные, но ещё не потраченные жетоны казино (по одному за уровень).</summary>
        public int CasinoTokens => casinoTokens;

        public bool IsCasinoOpen => casinoView != null && casinoView.IsVisible;

        private void Awake()
        {
            RogueDrive.Gameplay.Narrative.CasinoCroupierVoice.ResetForRun();
            FindSceneReferences();
            InitializeModifierSession();
            BindCombatAndProgression();
        }

        private void OnEnable()
        {
            EnemyBase.AnyEnemyKilled += HandleEnemyKilled;
            BuffCasinoStop.StopReached += HandleCasinoStopReached;

            if (experienceManager != null)
                experienceManager.LevelUp += HandleLevelUp;
            if (runController != null)
                runController.StageFinishing += HandleStageFinishing;
        }

        private void OnDisable()
        {
            EnemyBase.AnyEnemyKilled -= HandleEnemyKilled;
            BuffCasinoStop.StopReached -= HandleCasinoStopReached;

            if (experienceManager != null)
                experienceManager.LevelUp -= HandleLevelUp;
            if (runController != null)
                runController.StageFinishing -= HandleStageFinishing;
        }

        void FindSceneReferences()
        {
            if (carController == null)
                carController = FindFirstObjectByType<ArcadeCarController>();
            if (turret == null)
                turret = FindFirstObjectByType<AutoTurret>();
            if (runController == null)
                runController = FindFirstObjectByType<GameRunController>();
            if (experienceManager == null)
                experienceManager = FindFirstObjectByType<RunExperienceManager>();
            if (casinoView == null)
                casinoView = FindFirstObjectByType<BuffCasinoView>();
            if (casinoView == null)
                Debug.LogWarning("[GameSessionCoordinator] В сцене нет BuffCasinoView — бафы в казино будут выдаваться без анимации.");
        }

        void InitializeModifierSession()
        {
            // Подключаем мета-прогресс и автопарк из сохранений
            GarageCatalog garageCatalog = Resources.Load<GarageCatalog>("GarageCatalog");
#if UNITY_EDITOR
            if (garageCatalog == null)
            {
                garageCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
            }
#endif
            IReadOnlyList<UpgradeTrack> tracks = garageCatalog != null ? garageCatalog.Upgrades : null;
            IReadOnlyList<CarDefinition> cars = garageCatalog != null ? garageCatalog.Cars : null;

            RogueDrive.Meta.MetaProgress meta = RogueDrive.Meta.SaveService.GetActiveProgress(tracks, cars);
            if (meta != null && meta.SelectedCar != null)
            {
                carDefinition = meta.SelectedCar;
                if (carController != null)
                {
                    carController.SetBodyColor(meta.SelectedCar.BodyColor);
                }
            }

            sockets = carController != null && carController.Sockets != null
                ? (ISocketProvider)carController.Sockets
                : new HeadlessSocketProvider(carDefinition);

            IBaseStatsProvider baseStats = (meta != null) ? (IBaseStatsProvider)meta : new CarBaseStats(carDefinition);
            IUnlockProvider unlocks = (meta != null && meta.Data.UnlockedModifierIds.Count > 0) ? (IUnlockProvider)meta : new AllUnlocked();
            int seed = Random.Range(1, 1000000);

            session = ModifierSession.Create(modifierCatalog, baseStats, sockets, unlocks, seed, weightingConfig);

            // Перенос билда с предыдущего этапа кампании
            string carId = carDefinition != null ? carDefinition.Id : null;
            restoredModifiers = CampaignCarryOver.Restore(carId, CurrentStageFromScene(), session);
        }

        int restoredModifiers;

        static int CurrentStageFromScene()
        {
            string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;
            for (int i = 1; i <= 4; i++)
                if (name.StartsWith("Stage" + i)) return i;
            return Mathf.Clamp(CampaignMapModal.SelectedStartSector, 1, 4);
        }

        private void Start()
        {
            if (restoredModifiers > 0)
            {
                PrototypeHud.Instance?.ShowBiomeNotification(
                    "БИЛД ПЕРЕНЕСЁН",
                    $"С ПРОШЛОГО ЭТАПА УСТАНОВЛЕНО МОДУЛЕЙ: {restoredModifiers}. СИНЕРГИЙ: {session.Build.ActiveSynergies.Count}",
                    new Color(0.4f, 0.9f, 1f));
            }
        }

        /// <summary>
        /// Финиш этапа: непотраченные жетоны обмениваются на монеты, билд запоминается
        /// для следующего этапа. Вызывается до закрытия заезда, пока монеты ещё начисляются.
        /// </summary>
        void HandleStageFinishing(int stageIndex)
        {
            if (session == null)
                return;

            if (casinoTokens > 0 && runController != null && coinsPerUnspentToken > 0)
            {
                int coins = casinoTokens * coinsPerUnspentToken;
                runController.AddCoins(coins);
                PrototypeHud.Instance?.ShowBiomeNotification(
                    "ЖЕТОНЫ ОБМЕНЯНЫ",
                    $"{casinoTokens} НЕПОТРАЧЕННЫХ ЖЕТОНОВ → +{coins} МОНЕТ",
                    new Color(1f, 0.85f, 0.2f));
                casinoTokens = 0;
            }

            if (stageIndex < 4)
                CampaignCarryOver.Store(carDefinition != null ? carDefinition.Id : null, session.Build, stageIndex);
            else
                CampaignCarryOver.Clear();
        }

        void BindCombatAndProgression()
        {
            if (session == null)
                return;

            if (turret != null)
            {
                turret.BindStats(session.Effects.Stats, session.Effects.Projectiles);
            }

            if (carController != null)
            {
                carController.BindStats(session.Effects.Stats);

                CarAuraController auras = carController.GetComponent<CarAuraController>();
                if (auras == null)
                {
                    auras = carController.gameObject.AddComponent<CarAuraController>();
                }
                auras.BindBehaviours(session.Effects.Behaviours);
            }

            if (runController != null)
            {
                runController.BindStats(session.Effects.Stats);
            }

            session.Service.Applied += (def, level) =>
            {
                Debug.Log($"[Modifier Applied] {def.DisplayName} (Уровень {level})");
                if (carController != null) carController.BindStats(session.Effects.Stats);
                if (runController != null) runController.BindStats(session.Effects.Stats);
                AnnounceFullCarOnce();
            };

            session.Service.SynergyActivated += (syn) =>
            {
                Debug.Log($"[Synergy Activated] {syn.DisplayName}!");
                ArcadeCameraFollow.Instance?.TriggerShake(0.8f, 0.4f);
            };
        }

        /// <summary>Новый уровень = один жетон казино. Выбор бафа откладывается до ближайшего пункта.</summary>
        void HandleLevelUp(int newLevel)
        {
            if (session == null)
                return;

            casinoTokens++;
            RogueDrive.Audio.AudioManager.Instance?.PlayCoin();
            PrototypeHud.Instance?.ShowBiomeNotification(
                $"УРОВЕНЬ {newLevel} — ЖЕТОН КАЗИНО +1",
                $"ЖЕТОНОВ: {casinoTokens}. ОБМЕНЯЙТЕ НА БАФ В БЛИЖАЙШЕМ ПУНКТЕ-КАЗИНО",
                new Color(1f, 0.85f, 0.2f));
        }

        void HandleCasinoStopReached(BuffCasinoStop stop, ArcadeCarController car)
        {
            if (session == null || stop == null)
                return;

            if (runController != null && runController.IsGameOver)
                return;

            RogueDrive.Gameplay.Narrative.CasinoCroupierVoice.OnArrival(stop, casinoTokens);

            if (casinoTokens <= 0)
            {
                PrototypeHud.Instance?.ShowBiomeNotification(
                    stop.StopName.ToUpperInvariant(),
                    "НЕТ ЖЕТОНОВ — НАБЕРИТЕ ОПЫТ С ЗОМБИ ДО СЛЕДУЮЩЕГО ПУНКТА",
                    new Color(1f, 0.3f, 0.85f));
                return;
            }

            if (casinoOpening != null) StopCoroutine(casinoOpening);
            casinoOpening = StartCoroutine(OpenCasinoRoutine(stop));
        }

        IEnumerator OpenCasinoRoutine(BuffCasinoStop stop)
        {
            // Даём машине проехать под аркой, затем пауза и слот-машина
            if (casinoOpenDelay > 0f)
                yield return new WaitForSeconds(casinoOpenDelay);

            casinoOpening = null;

            if (runController != null && runController.IsGameOver)
                yield break;

            CasinoBetPricing pricing = weightingConfig != null ? weightingConfig.BetPricing : CasinoBetPricing.Default;

            // Банк казино: за жетоны, пронесённые мимо прошлого пункта, начисляется надбавка
            int bonus = pricing.CarryOverBonus(bankedTokens);
            bankedTokens = 0;

            int spins = casinoTokens + bonus;
            casinoTokens = 0;

            if (bonus > 0)
            {
                PrototypeHud.Instance?.ShowBiomeNotification(
                    "БАНК КАЗИНО",
                    $"ЗА ПРОНЕСЁННЫЕ ЖЕТОНЫ НАЧИСЛЕНО +{bonus}",
                    new Color(0.3f, 1f, 0.7f));
            }

            var visit = new CasinoVisit
            {
                StopName = stop.StopName,
                Tokens = spins,
                CarryOverBonus = bonus,
                DrawReel = DrawCasinoReel,
                BetAvailable = IsBetAvailable,
                ApplyResult = ApplyCasinoResult,
                ReturnTokens = ReturnCasinoTokens,
                Build = session.Build,
                Synergies = session.Synergies,
                Generator = session.OfferGenerator,
                CarFull = () => sockets != null && sockets.AllOccupied,
                Pricing = pricing
            };

            if (casinoView != null)
            {
                casinoView.Show(visit);
            }
            else
            {
                // Нет экрана казино — выдаём бафы сразу обычными спинами
                var names = new List<string>();
                int left = spins;
                for (int i = 0; i < spins; i++)
                {
                    IReadOnlyList<ModifierDefinition> reel = DrawCasinoReel(CasinoBet.Standard);
                    if (reel.Count == 0) break;
                    ApplyCasinoResult(reel[0]);
                    names.Add(reel[0].DisplayName);
                    left--;
                }

                ReturnCasinoTokens(left);
                PrototypeHud.Instance?.ShowBiomeNotification(
                    stop.StopName.ToUpperInvariant(),
                    names.Count > 0 ? "ВЫПАЛО: " + string.Join(", ", names) : "ПУЛ МОДИФИКАТОРОВ ИСЧЕРПАН",
                    new Color(1f, 0.3f, 0.85f));
            }
        }

        /// <summary>
        /// Барабан одного вращения: взвешенная выборка без возвращения из пула,
        /// суженного ставкой. Первый элемент — выпавший модификатор, остальные
        /// лишь декорируют прокрутку.
        /// </summary>
        IReadOnlyList<ModifierDefinition> DrawCasinoReel(CasinoBet bet)
        {
            IReadOnlyList<ModifierDefinition> offers = session.OfferGenerator.Generate(
                session.Build, session.Context, reelSize, OfferConstraint.ForBet(bet));

            // Генератор переиспользует внутренний список — копируем
            var copy = new List<ModifierDefinition>(offers.Count);
            for (int i = 0; i < offers.Count; i++) copy.Add(offers[i]);
            return copy;
        }

        /// <summary>
        /// Игрок ушёл с пункта, не потратив всё: жетоны остаются при нём и попадают
        /// в банк. На следующем пункте за них начислится надбавка, а если пункта
        /// больше не будет — обменяются на монеты на финише этапа.
        /// </summary>
        void ReturnCasinoTokens(int tokens)
        {
            if (tokens <= 0)
                return;

            casinoTokens += tokens;
            bankedTokens = casinoTokens;

            PrototypeHud.Instance?.ShowBiomeNotification(
                "ЖЕТОНЫ СОХРАНЕНЫ",
                $"{tokens} ЖЕТОНОВ В БАНКЕ — НА СЛЕДУЮЩЕМ ПУНКТЕ КАЗИНО ДОПЛАТИТ",
                new Color(0.3f, 1f, 0.7f));
        }

        bool IsBetAvailable(CasinoBet bet)
        {
            return session != null
                && session.OfferGenerator.HasCandidates(session.Build, session.Context, OfferConstraint.ForBet(bet));
        }

        void ApplyCasinoResult(ModifierDefinition def)
        {
            if (session == null || def == null)
                return;

            session.Service.Apply(def);
        }

        /// <summary>
        /// Все сокеты корпуса заняты: с этого момента казино выдаёт только улучшения
        /// уже установленных модулей (правило пула LockNewModulesWhenSocketsFull).
        /// </summary>
        void AnnounceFullCarOnce()
        {
            if (fullCarAnnounced || sockets == null || !sockets.AllOccupied)
                return;

            fullCarAnnounced = true;
            PrototypeHud.Instance?.ShowBiomeNotification(
                "МАШИНА УКОМПЛЕКТОВАНА",
                "ВСЕ СОКЕТЫ ЗАНЯТЫ — КАЗИНО ТЕПЕРЬ ВЫДАЁТ ТОЛЬКО УЛУЧШЕНИЯ УСТАНОВЛЕННЫХ МОДУЛЕЙ",
                new Color(0.95f, 0.75f, 0.2f));
        }

        void HandleEnemyKilled(EnemyBase enemy)
        {
            killCounter++;

            // Бонус нитро за убийство
            if (runController != null)
            {
                runController.AddNitro(4f);
            }

            // Обработка триггеров ResourceRegistry (вампиризм и ремонт)
            if (session != null && session.Effects.Resources != null)
            {
                IReadOnlyList<ResourceTrigger> triggers = session.Effects.Resources.Triggers;
                for (int i = 0; i < triggers.Count; i++)
                {
                    ResourceTrigger t = triggers[i];
                    if (killCounter % t.KillsPerTrigger == 0)
                    {
                        if (t.Kind == ResourceKind.Fuel && runController != null)
                        {
                            runController.AddFuel(t.Amount);
                            Debug.Log($"[Resource Trigger] Восстановлено топливо: +{t.Amount}");
                        }
                        else if (t.Kind == ResourceKind.Health && runController != null)
                        {
                            runController.Heal(t.Amount);
                            Debug.Log($"[Resource Trigger] Ремонт кузова: +{t.Amount}");
                        }
                    }
                }
            }
        }
    }
}
