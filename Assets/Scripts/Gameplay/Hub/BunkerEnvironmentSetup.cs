using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Автоматически выстраивает структуру подземного бункера с временными заглушками:
    /// 1. Жилой угол: кровать со спальником, тумбочка с шипящей рацией, катсцена пробуждения.
    /// 2. Силовой угол: дизель-генератор с рубильником.
    /// 3. Зона выезда: наклонный пандус вверх и двустворчатые распашные гермоворота.
    /// 4. Машина: зоны 3D-инспекции и прокачки по взгляду (CarInspectionRig).
    /// Пользователь в любой момент сможет заменить заглушки на свои модели из Blender!
    /// </summary>
    public sealed class BunkerEnvironmentSetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [SerializeField] private bool buildPlaceholdersOnAwake = false; // Отключено: объекты должны быть статично расставлены в сцене

        private void Awake()
        {
            if (buildPlaceholdersOnAwake)
            {
                SetupBunker();
            }
        }

        [ContextMenu("Создать заглушки в сцене (для ручной настройки)")]
        public void SetupBunker()
        {
            Transform bunkerRoot = transform;

            // 1. ЖИЛОЙ УГОЛ (КРОВАТЬ И РАЦИЯ)
            Transform livingCorner = bunkerRoot.Find("Living_Corner");
            if (livingCorner == null)
            {
                livingCorner = new GameObject("Living_Corner").transform;
                livingCorner.SetParent(bunkerRoot, false);
                livingCorner.localPosition = new Vector3(-8.5f, 0f, -8.5f);

                // Кровать / раскладушка
                GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bed.name = "Placeholder_Bed";
                bed.transform.SetParent(livingCorner, false);
                bed.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                bed.transform.localScale = new Vector3(1.2f, 0.4f, 2.2f);
                SetColor(bed, new Color(0.20f, 0.32f, 0.28f)); // защитный армейский брезент

                // Подушка
                GameObject pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillow.name = "Placeholder_Pillow";
                pillow.transform.SetParent(bed.transform, false);
                pillow.transform.localPosition = new Vector3(0f, 0.65f, 0.35f);
                pillow.transform.localScale = new Vector3(0.85f, 0.35f, 0.25f);
                SetColor(pillow, new Color(0.45f, 0.42f, 0.38f));

                // Тумбочка из ящика
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Placeholder_Nightstand";
                table.transform.SetParent(livingCorner, false);
                table.transform.localPosition = new Vector3(1.1f, 0.35f, 0.7f);
                table.transform.localScale = new Vector3(0.65f, 0.7f, 0.65f);
                SetColor(table, new Color(0.35f, 0.28f, 0.20f)); // дерево

                // Рация с антенной
                GameObject radio = GameObject.CreatePrimitive(PrimitiveType.Cube);
                radio.name = "Placeholder_Radio";
                radio.transform.SetParent(table.transform, false);
                radio.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                radio.transform.localScale = new Vector3(0.45f, 0.35f, 0.30f);
                SetColor(radio, new Color(0.15f, 0.16f, 0.18f)); // черный военный пластик

                // Антенна
                GameObject ant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ant.name = "Antenna";
                ant.transform.SetParent(radio.transform, false);
                ant.transform.localPosition = new Vector3(0.35f, 0.8f, 0f);
                ant.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
                SetColor(ant, Color.gray);
            }

            // Навешиваем катсцену на жилой угол
            var cutscene = GetComponent<BunkerPrologueCutscene>();
            if (cutscene == null) cutscene = gameObject.AddComponent<BunkerPrologueCutscene>();

            // 2. ВЫЕЗДНОЙ ПАНДУС И РАСПАШНЫЕ ВОРОТА
            Transform exitRamp = bunkerRoot.Find("Exit_Ramp_And_Gate");
            if (exitRamp == null)
            {
                exitRamp = new GameObject("Exit_Ramp_And_Gate").transform;
                exitRamp.SetParent(bunkerRoot, false);
                exitRamp.localPosition = new Vector3(0f, 0f, 10.5f);

                // Наклонная рампа пола (пандус выезда вверх на поверхность)
                GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = "Placeholder_InclineRamp";
                ramp.transform.SetParent(exitRamp, false);
                ramp.transform.localPosition = new Vector3(0f, 1.25f, -2.5f);
                ramp.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f); // подъем вверх
                ramp.transform.localScale = new Vector3(8.5f, 0.4f, 12f);
                SetColor(ramp, new Color(0.18f, 0.19f, 0.22f)); // рифленый бетон

                // Распашные гермоворота
                GameObject gateRoot = new GameObject("Bunker_SwingBlastGate");
                gateRoot.transform.SetParent(exitRamp, false);
                gateRoot.transform.localPosition = new Vector3(0f, 2.5f, 3.5f);
                gateRoot.AddComponent<GarageSwingGateController>();

                // Пульт управления воротами на стене
                GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = "Gate_Control_Terminal";
                console.transform.SetParent(exitRamp, false);
                console.transform.localPosition = new Vector3(4.8f, 3.2f, 2.8f);
                console.transform.localScale = new Vector3(0.4f, 0.7f, 0.5f);
                SetColor(console, new Color(0.25f, 0.28f, 0.30f));
            }

            // 3. ЗОНЫ 3D-ИНСПЕКЦИИ И ПРОКАЧКИ НА МАШИНЕ
            var carPodium = GameObject.Find("PodiumAnchor") ?? GameObject.Find("Car_Podium") ?? GameObject.Find("PlayerCar_Podium");
            if (carPodium != null)
            {
                var rig = carPodium.GetComponent<CarInspectionRig>();
                if (rig == null) rig = carPodium.AddComponent<CarInspectionRig>();
                rig.SetupHotspots();

                var boarding = carPodium.GetComponent<GarageVehicleBoarding>();
                if (boarding == null) carPodium.AddComponent<GarageVehicleBoarding>();
            }

            // 4. КОНТРОЛЛЕР ИГРОКА ОТ 1-ГО ЛИЦА
            EnsurePlayerController();
        }

        private void EnsurePlayerController()
        {
            var existingPlayer = FindFirstObjectByType<GaragePlayerController>();
            if (existingPlayer != null) return;

            GameObject playerObj = new GameObject("Bunker_FP_Player");
            playerObj.transform.position = new Vector3(-8.5f, 0.1f, -7.5f);
            playerObj.transform.rotation = Quaternion.Euler(0f, 45f, 0f); // смотрит в сторону машины

            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var pc = playerObj.AddComponent<GaragePlayerController>();
            playerObj.AddComponent<GarageInteractionRaycaster>();

            GameObject camObj = new GameObject("PlayerCamera");
            camObj.transform.SetParent(playerObj.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;
            camObj.tag = "MainCamera";

            // Отключаем старые статичные камеры
            Camera[] allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in allCams)
            {
                if (c != cam) c.gameObject.SetActive(false);
            }
        }

        private static void SetColor(GameObject obj, Color color)
        {
            var r = obj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            }
        }
    }
}
