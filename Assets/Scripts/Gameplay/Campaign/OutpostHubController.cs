using System;
using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Narrative;
using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay.Campaign
{
    /// <summary>
    /// Контроллер промежуточных баз (Аванпост 1: СТО, Аванпост 2: Лагерь рейдеров, Аванпост 3: Ангар химзавода).
    /// Отвечает за:
    /// 1. Генерацию модульных 3D-заглушек базы (навесы, верстаки, канистры, терминалы).
    /// 2. Размещение брошенной машины с поэтапным интерактивным ремонтом.
    /// 3. Переход в режим осмотра от первого лица (или управление машиной в безопасной зоне).
    /// 4. Радиограмму от Цитадели при въезде.
    /// 5. Терминал выездных ворот в следующий сектор или эвакуацию в Бункер 07.
    /// </summary>
    public sealed class OutpostHubController : MonoBehaviour
    {
        [Header("Outpost Type")]
        [SerializeField, Range(1, 3)] private int stageIndex = 1;
        [SerializeField] private string outpostName = "Заброшенная СТО";

        [Header("References")]
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private AbandonedVehicleRepairable repairableVehicle;
        [SerializeField] private Transform playerSpawnPoint;

        private bool hasPlayerArrived;
        private GaragePlayerController fpPlayer;

        public int StageIndex => stageIndex;
        public string OutpostName => outpostName;

        public void Configure(int stage, string name)
        {
            stageIndex = stage;
            outpostName = name;
        }

        private void Start()
        {
            if (environmentRoot == null)
            {
                BuildOutpostPlaceholders();
            }
        }

        /// <summary>
        /// Вызывается при въезде автомобиля игрока в безопасный периметр базы.
        /// </summary>
        public void OnCarEnteredSafeZone(ArcadeCarController car)
        {
            if (hasPlayerArrived) return;
            hasPlayerArrived = true;

            StartCoroutine(ArrivalSequenceRoutine(car));
        }

        private IEnumerator ArrivalSequenceRoutine(ArcadeCarController car)
        {
            // Плавное торможение машины игрока
            if (car != null)
            {
                var rb = car.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float timer = 0f;
                    Vector3 initVel = rb.linearVelocity;
                    while (timer < 1.2f)
                    {
                        timer += Time.deltaTime;
                        rb.linearVelocity = Vector3.Lerp(initVel, Vector3.zero, timer / 1.2f);
                        yield return null;
                    }
                    rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
            }

            // Радиосообщение от «Маяка»
            RadioTransmissionSystem.Instance.PlayOutpostArrival(stageIndex);

            yield return new WaitForSeconds(1.0f);

            // Активируем режим первого лица на базе
            SpawnOrEnableFPPlayer(car);
        }

        private void SpawnOrEnableFPPlayer(ArcadeCarController car)
        {
            Vector3 spawnPos = car != null ? car.transform.position + car.transform.right * -2.4f + Vector3.up * 0.2f : transform.position;
            if (playerSpawnPoint != null) spawnPos = playerSpawnPoint.position;

            GameObject playerObj = new GameObject("Outpost_FP_Player");
            playerObj.transform.position = spawnPos;

            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            fpPlayer = playerObj.AddComponent<GaragePlayerController>();
            playerObj.AddComponent<GarageInteractionRaycaster>();

            // Камера первого лица
            GameObject camObj = new GameObject("FP_Camera");
            camObj.transform.SetParent(playerObj.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;

            // Переключаем главную камеру машины на камеру первого лица
            if (ArcadeCameraFollow.Instance != null)
            {
                ArcadeCameraFollow.Instance.gameObject.SetActive(false);
            }
        }

        #region Procedural Outpost Builders (Blender-Ready Placeholders)

        public void BuildOutpostPlaceholders()
        {
            GameObject root = new GameObject($"Outpost_{stageIndex}_Placeholders");
            root.transform.SetParent(transform, false);
            environmentRoot = root.transform;

            Material concreteMat = CreateMaterial(new Color(0.38f, 0.40f, 0.42f));
            Material rustSteelMat = CreateMaterial(new Color(0.32f, 0.22f, 0.16f));
            Material hazardMat = CreateMaterial(new Color(0.95f, 0.75f, 0.1f));
            Material whitePaintMat = CreateMaterial(new Color(0.85f, 0.85f, 0.85f));

            // 1. Бетонный фундамент площадки
            CreateBox(environmentRoot, "Outpost_ConcretePad", new Vector3(0f, -0.1f, 12f), new Vector3(32f, 0.2f, 30f), concreteMat);

            // В зависимости от сектора строим уникальный тип базы
            switch (stageIndex)
            {
                case 1:
                    BuildServiceStationBase(environmentRoot, rustSteelMat, hazardMat);
                    break;
                case 2:
                    BuildRaiderCampBase(environmentRoot, rustSteelMat, hazardMat);
                    break;
                case 3:
                    BuildChemPlantHangarBase(environmentRoot, whitePaintMat, hazardMat);
                    break;
            }

            // 3. Выездной шлюз и терминал отправки
            BuildDepartureGate(environmentRoot, rustSteelMat, hazardMat);
        }

        void BuildServiceStationBase(Transform root, Material steelMat, Material hazardMat)
        {
            // Навес СТО (Canopy)
            CreateBox(root, "Canopy_Pillar_L", new Vector3(-8f, 3.5f, 5f), new Vector3(0.6f, 7f, 0.6f), steelMat);
            CreateBox(root, "Canopy_Pillar_R", new Vector3(8f, 3.5f, 5f), new Vector3(0.6f, 7f, 0.6f), steelMat);
            CreateBox(root, "Canopy_Roof", new Vector3(0f, 7.2f, 5f), new Vector3(18f, 0.4f, 14f), steelMat);

            // Двухстоечный подъемник
            GameObject liftObj = new GameObject("CarLift_Station");
            liftObj.transform.SetParent(root, false);
            liftObj.transform.localPosition = new Vector3(5.5f, 0f, 6f);
            var lift = liftObj.AddComponent<CarLiftStation>();

            // Автомобиль на подъемнике (Пикап «Следопыт»)
            GameObject suvCar = new GameObject("Abandoned_Pickup_Pathfinder");
            suvCar.transform.SetParent(lift.GetCarAnchor(), false);
            suvCar.transform.localPosition = Vector3.zero;

            repairableVehicle = suvCar.AddComponent<AbandonedVehicleRepairable>();
            repairableVehicle.Configure("suv", "Пикап «Следопыт»");

            // Верстак с колесом
            GameObject bench = CreateBox(root, "Workbench_TireStation", new Vector3(-6.5f, 0.5f, 3f), new Vector3(1.2f, 1.0f, 2.4f), steelMat);
            var repairHotspot1 = bench.AddComponent<OutpostRepairHotspot>();
            repairHotspot1.Configure(repairableVehicle, 0, "[E] Взять колесо и смонтировать на ступицу пикапа");

            // Бочки с канистрой масла
            GameObject oilBarrel = CreateCylinder(root, "Oil_Barrel_Station", new Vector3(5.5f, 0.6f, 1.5f), new Vector3(0.8f, 1.2f, 0.8f), hazardMat);
            var repairHotspot2 = oilBarrel.AddComponent<OutpostRepairHotspot>();
            repairHotspot2.Configure(repairableVehicle, 1, "[E] Залить моторное масло и топливо в пикап");
        }

        void BuildRaiderCampBase(Transform root, Material rustMat, Material hazardMat)
        {
            // Огороженный капонир из мешков с песком и стальных щитов
            CreateBox(root, "BunkerWall_Left", new Vector3(-8f, 1.8f, 6f), new Vector3(1.2f, 3.6f, 12f), rustMat);
            CreateBox(root, "BunkerWall_Right", new Vector3(8f, 1.8f, 6f), new Vector3(1.2f, 3.6f, 12f), rustMat);
            CreateBox(root, "BunkerWall_Back", new Vector3(0f, 1.8f, 12f), new Vector3(16f, 3.6f, 1.2f), rustMat);

            // Тяжелый броневик «Бастион»
            GameObject armCar = new GameObject("Abandoned_Armored_Bastion");
            armCar.transform.SetParent(root, false);
            armCar.transform.localPosition = new Vector3(0f, 0.5f, 6f);

            repairableVehicle = armCar.AddComponent<AbandonedVehicleRepairable>();
            repairableVehicle.Configure("armored", "Броневик «Бастион»");

            // Генератор с аккумулятором
            GameObject gen = CreateBox(root, "Generator_Battery_Station", new Vector3(-5.5f, 0.7f, 4f), new Vector3(1.4f, 1.4f, 1.2f), hazardMat);
            var hotspot1 = gen.AddComponent<OutpostRepairHotspot>();
            hotspot1.Configure(repairableVehicle, 0, "[E] Снять аккумулятор с генератора и подключить к броневику");

            // Бронеплита на подставках
            GameObject plate = CreateBox(root, "Armor_Welding_Station", new Vector3(5.5f, 0.6f, 5f), new Vector3(0.3f, 1.2f, 2.0f), rustMat);
            var hotspot2 = plate.AddComponent<OutpostRepairHotspot>();
            hotspot2.Configure(repairableVehicle, 1, "[E] Приварить лобовой бронелист к раме броневика");
        }

        void BuildChemPlantHangarBase(Transform root, Material whiteMat, Material hazardMat)
        {
            // Чистый лабораторный ангар
            CreateBox(root, "Lab_Wall_L", new Vector3(-9f, 3f, 6f), new Vector3(0.5f, 6f, 14f), whiteMat);
            CreateBox(root, "Lab_Wall_R", new Vector3(9f, 3f, 6f), new Vector3(0.5f, 6f, 14f), whiteMat);
            CreateBox(root, "Lab_Ceiling", new Vector3(0f, 6.2f, 6f), new Vector3(18.5f, 0.4f, 14f), whiteMat);

            // Экспериментальный спорткар «Фантом»
            GameObject sportCar = new GameObject("Abandoned_Sport_Phantom");
            sportCar.transform.SetParent(root, false);
            sportCar.transform.localPosition = new Vector3(0f, 0.4f, 6f);

            repairableVehicle = sportCar.AddComponent<AbandonedVehicleRepairable>();
            repairableVehicle.Configure("sport", "Спорткар «Фантом»");

            // Компрессор высокого давления
            GameObject comp = CreateBox(root, "Air_Compressor_Station", new Vector3(-6.5f, 0.7f, 4f), new Vector3(1.2f, 1.4f, 1.2f), hazardMat);
            var hotspot1 = comp.AddComponent<OutpostRepairHotspot>();
            hotspot1.Configure(repairableVehicle, 0, "[E] Продуть инжекторы компрессором");

            // Стенд нитро-топлива
            GameObject nitro = CreateCylinder(root, "Nitro_Fuel_Station", new Vector3(6.5f, 0.8f, 5f), new Vector3(0.7f, 1.6f, 0.7f), CreateMaterial(new Color(0.2f, 0.8f, 1f)));
            var hotspot2 = nitro.AddComponent<OutpostRepairHotspot>();
            hotspot2.Configure(repairableVehicle, 1, "[E] Заправить турбины высокооктановой нитро-смесью");
        }

        void BuildDepartureGate(Transform root, Material steelMat, Material hazardMat)
        {
            Transform gateGroup = new GameObject("Outpost_Departure_Gate").transform;
            gateGroup.SetParent(root, false);
            gateGroup.localPosition = new Vector3(0f, 0f, 22f);

            // Колонны и перекладина
            CreateBox(gateGroup, "Pillar_Left", new Vector3(-10f, 4f, 0f), new Vector3(2f, 8f, 2f), steelMat);
            CreateBox(gateGroup, "Pillar_Right", new Vector3(10f, 4f, 0f), new Vector3(2f, 8f, 2f), steelMat);
            CreateBox(gateGroup, "Crossbeam", new Vector3(0f, 8.5f, 0f), new Vector3(22f, 1.2f, 2f), hazardMat);

            // Терминал управления выездом
            GameObject terminal = CreateBox(gateGroup, "Departure_Terminal", new Vector3(6.5f, 1.4f, -1.2f), new Vector3(0.6f, 1.2f, 0.6f), hazardMat);
            var departureInteractable = terminal.AddComponent<OutpostDepartureTerminal>();
            departureInteractable.Configure(this);
        }

        #endregion

        #region Helper Geometry Builders

        private GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        private GameObject CreateCylinder(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        private Material CreateMaterial(Color color)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = color
            };
            return m;
        }

        #endregion
    }

    /// <summary>
    /// Интерактивная точка починки конкретного узла на базе (колесо, масло, батарея и т.д.).
    /// </summary>
    public sealed class OutpostRepairHotspot : MonoBehaviour, IGarageInteractable
    {
        private AbandonedVehicleRepairable targetVehicle;
        private int requiredStep;
        private string promptText;
        private bool isCompleted;

        public void Configure(AbandonedVehicleRepairable vehicle, int step, string prompt)
        {
            targetVehicle = vehicle;
            requiredStep = step;
            promptText = prompt;
        }

        public string GetPromptText()
        {
            if (isCompleted) return "✓ Деталь установлена";
            return promptText;
        }

        public bool CanInteract()
        {
            return !isCompleted && targetVehicle != null && !targetVehicle.IsRepaired;
        }

        public void Interact(GaragePlayerController player)
        {
            if (!CanInteract()) return;
            isCompleted = true;

            // Выполняем шаг починки машины
            if (targetVehicle != null)
            {
                targetVehicle.AdvanceRepairStep();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }
        }
    }

    /// <summary>
    /// Пульт открытия ворот базы для выезда в следующий сектор.
    /// </summary>
    public sealed class OutpostDepartureTerminal : MonoBehaviour, IGarageInteractable
    {
        private OutpostHubController outpost;

        public void Configure(OutpostHubController hub)
        {
            outpost = hub;
        }

        public string GetPromptText()
        {
            int nextStage = outpost != null ? outpost.StageIndex + 1 : 2;
            return $"[E] Открыть шлюз и продолжить путь: Сектор {nextStage}";
        }

        public bool CanInteract() => true;

        public void Interact(GaragePlayerController player)
        {
            int nextStage = outpost != null ? outpost.StageIndex + 1 : 2;
            string targetScene = nextStage switch
            {
                2 => "Stage2_Wasteland",
                3 => "Stage3_Industrial",
                4 => "Stage4_Citadel",
                _ => "GarageScene"
            };

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGateOpen();
            }

            SceneTransitionManager.SwitchScene(targetScene);
        }
    }
}
