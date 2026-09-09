using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Главный координатор сессии заезда.
    /// Связывает данные модификаторов, физический автомобиль, авто-турель,
    /// систему опыта, генератор предложений и интерфейс повышения уровня.
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
        [SerializeField] private LevelUpView levelUpView;

        ModifierSession session;
        int killCounter;

        public ModifierSession Session => session;

        private void Awake()
        {
            FindSceneReferences();
            InitializeModifierSession();
            BindCombatAndProgression();
        }

        private void OnEnable()
        {
            EnemyBase.AnyEnemyKilled += HandleEnemyKilled;

            if (experienceManager != null)
                experienceManager.LevelUp += HandleLevelUp;

            if (levelUpView != null)
            {
                levelUpView.OfferSelected += HandleOfferSelected;
                levelUpView.RerollRequested += HandleRerollRequested;
            }
        }

        private void OnDisable()
        {
            EnemyBase.AnyEnemyKilled -= HandleEnemyKilled;

            if (experienceManager != null)
                experienceManager.LevelUp -= HandleLevelUp;

            if (levelUpView != null)
            {
                levelUpView.OfferSelected -= HandleOfferSelected;
                levelUpView.RerollRequested -= HandleRerollRequested;
            }
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
            if (levelUpView == null)
                levelUpView = FindFirstObjectByType<LevelUpView>();
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

            ISocketProvider sockets = carController != null && carController.Sockets != null
                ? (ISocketProvider)carController.Sockets
                : new HeadlessSocketProvider(carDefinition);

            IBaseStatsProvider baseStats = (meta != null) ? (IBaseStatsProvider)meta : new CarBaseStats(carDefinition);
            IUnlockProvider unlocks = (meta != null && meta.Data.UnlockedModifierIds.Count > 0) ? (IUnlockProvider)meta : new AllUnlocked();
            int seed = Random.Range(1, 1000000);

            session = ModifierSession.Create(modifierCatalog, baseStats, sockets, unlocks, seed, weightingConfig);
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
            };

            session.Service.SynergyActivated += (syn) =>
            {
                Debug.Log($"[Synergy Activated] {syn.DisplayName}!");
                ArcadeCameraFollow.Instance?.TriggerShake(0.8f, 0.4f);
            };
        }

        int pendingLevelUps;

        void HandleLevelUp(int newLevel)
        {
            if (session == null || levelUpView == null)
                return;

            if (levelUpView.IsVisible)
            {
                pendingLevelUps++;
                return;
            }

            IReadOnlyList<ModifierDefinition> offers = session.OfferGenerator.Generate(
                session.Build, session.Context, 3);

            levelUpView.Show(offers, session.Build, session.Synergies);
        }

        void HandleOfferSelected(ModifierDefinition def)
        {
            if (session == null || def == null)
                return;

            session.Service.Apply(def);

            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                IReadOnlyList<ModifierDefinition> offers = session.OfferGenerator.Generate(
                    session.Build, session.Context, 3);

                levelUpView.Show(offers, session.Build, session.Synergies);
            }
        }

        void HandleRerollRequested()
        {
            if (session == null || levelUpView == null)
                return;

            IReadOnlyList<ModifierDefinition> offers = session.OfferGenerator.Generate(
                session.Build, session.Context, 3);

            levelUpView.Show(offers, session.Build, session.Synergies);
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
