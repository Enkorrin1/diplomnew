using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Физическая контрольная точка (чекпоинт) на границе секторов кампании.
    /// Пересечение арки восстанавливает топливо (+30%), чинит корпус (+25%),
    /// дает бонусные монеты, фиксирует прогресс кампании и воспроизводит победный фанфар.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SectorCheckpoint : MonoBehaviour
    {
        [Header("Sector Info")]
        [SerializeField, Range(1, 4)] private int sectorIndex = 1;
        [SerializeField] private string sectorName = "Сектор 1: Пригородная автострада";

        [Header("Bonuses")]
        [SerializeField, Range(0.1f, 0.5f)] private float fuelBonusRatio = 0.30f;
        [SerializeField, Range(0.1f, 0.5f)] private float healthRepairRatio = 0.25f;
        [SerializeField, Min(0)] private int coinReward = 15;

        bool isPassed;
        GameObject archRoot;

        public int SectorIndex => sectorIndex;

        public void Configure(int sector, string name)
        {
            sectorIndex = sector;
            sectorName = name;
        }

        private void Awake()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(24f, 8f, 4f);
            col.center = new Vector3(0f, 4f, 0f);

            Transform existing = transform.Find("CheckpointArchVisual");
            if (existing != null) archRoot = existing.gameObject;
            else BuildCheckpointArch();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPassed) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car == null) return;

            PassCheckpoint(car);
        }

        void PassCheckpoint(ArcadeCarController car)
        {
            isPassed = true;

            GameRunController run = car.Run != null ? car.Run : FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                run.AddFuel(run.MaxFuel * fuelBonusRatio);
                run.Heal(run.MaxHealth * healthRepairRatio);
                run.AddCoins(coinReward);

                // Сохранение открытого сектора в постоянный мета-прогресс
                try
                {
                    var meta = RogueDrive.Meta.SaveService.GetActiveProgress();
                    if (meta != null)
                    {
                        meta.RegisterRunResult(sectorIndex + 1, run.Distance);
                        RogueDrive.Meta.SaveService.SaveActive();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SectorCheckpoint] Ошибка сохранения прогресса: {ex.Message}");
                }
            }

            // Звук торжественного фанфара
            RogueDrive.Audio.AudioManager.Instance?.PlayFanfare();

            // Встряска камеры
            ArcadeCameraFollow.Instance?.TriggerShake(0.5f, 0.3f);

            // Уведомление в HUD
            PrototypeHud.Instance?.ShowBiomeNotification(
                $"СЕКТОР {sectorIndex} ПРОЙДЕН! ★",
                $"ТОПЛИВО +{fuelBonusRatio * 100:0}% | КОРПУС +{healthRepairRatio * 100:0}% | +{coinReward} МОНЕТ",
                new Color(0.2f, 1f, 0.4f));

            // Активация неонового зеленого свечения на арке
            SetArchColor(new Color(0.1f, 1f, 0.3f, 1f));
        }

        void BuildCheckpointArch()
        {
            archRoot = new GameObject("CheckpointArchVisual");
            archRoot.transform.SetParent(transform, false);

            Material pillarMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pillarMat.color = new Color(0.2f, 0.22f, 0.25f);

            Material neonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            neonMat.color = new Color(1f, 0.85f, 0.1f);

            // Левая опора
            CreatePillar(new Vector3(-10.5f, 4f, 0f), pillarMat);
            // Правая опора
            CreatePillar(new Vector3(10.5f, 4f, 0f), pillarMat);

            // Верхняя горизонтальная балка
            GameObject crossbar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossbar.name = "ArchCrossbar";
            crossbar.transform.SetParent(archRoot.transform, false);
            crossbar.transform.localPosition = new Vector3(0f, 7.5f, 0f);
            crossbar.transform.localScale = new Vector3(23f, 1.2f, 1.6f);
            Destroy(crossbar.GetComponent<Collider>());
            crossbar.GetComponent<Renderer>().sharedMaterial = pillarMat;

            // Неоновый знак "CHECKPOINT"
            GameObject neonSign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neonSign.name = "ArchNeonBanner";
            neonSign.transform.SetParent(archRoot.transform, false);
            neonSign.transform.localPosition = new Vector3(0f, 7.5f, -0.85f);
            neonSign.transform.localScale = new Vector3(16f, 0.8f, 0.15f);
            Destroy(neonSign.GetComponent<Collider>());
            neonSign.GetComponent<Renderer>().sharedMaterial = neonMat;
        }

        void CreatePillar(Vector3 pos, Material mat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "ArchPillar";
            pillar.transform.SetParent(archRoot.transform, false);
            pillar.transform.localPosition = pos;
            pillar.transform.localScale = new Vector3(1.4f, 8f, 1.4f);
            Destroy(pillar.GetComponent<Collider>());
            pillar.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void SetArchColor(Color c)
        {
            if (archRoot == null) return;
            Transform neon = archRoot.transform.Find("ArchNeonBanner");
            if (neon != null)
            {
                Renderer r = neon.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = c;
                }
            }
        }
    }
}
