using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>
    /// Synchronizes shared two-player crew questline:
    /// Phase 0: Bunker 07 vehicle preparation & blast gate departure.
    /// Phase 1: Road journey stops and checkpoints along the highway.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class CoopQuestManager : NetworkBehaviour
    {
        public static CoopQuestManager Instance { get; private set; }

        [Header("Synchronized Objectives")]
        public readonly NetworkVariable<bool> QuestGeneratorRunning = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestWheelMounted = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestBatteryMounted = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> QuestFuelLiters = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<bool> QuestKeyCollected = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestGatesOpened = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestBunkerDeparted = new NetworkVariable<bool>(false);

        [Header("Road Journey Objectives")]
        public readonly NetworkVariable<bool> QuestStop1FuelScavenged = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestStop2CampExplored = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestStop3ServiceVisited = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> QuestDestinationReached = new NetworkVariable<bool>(false);

        [Header("Bunker Environment References")]
        [SerializeField] private Light[] workshopMainLights;
        [SerializeField] private Light[] emergencyAmberLights;
        [SerializeField] private Transform leftGateDoor;
        [SerializeField] private Transform rightGateDoor;
        [SerializeField] private AudioSource sirenAudio;

        public const float RequiredFuel = 15f;

        public bool IsBunkerPhase => !QuestBunkerDeparted.Value;
        public bool CanDrive => QuestGeneratorRunning.Value && QuestWheelMounted.Value && 
                                QuestBatteryMounted.Value && QuestFuelLiters.Value >= RequiredFuel && 
                                QuestKeyCollected.Value && QuestGatesOpened.Value;

        public string LatestNotification { get; private set; } = "";
        private float notificationExpiry = 0f;

        private void Awake()
        {
            // Scene copies are legacy authoring data. Only the spawned shared car
            // owns the expedition; Awake must not replace its singleton.
            if (GetComponent<CoopVehicle>() == null) enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            if (GetComponent<CoopVehicle>() == null) return;
            Instance = this;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
            FindSceneReferences();
            QuestGeneratorRunning.OnValueChanged += OnGeneratorStateChanged;
            QuestWheelMounted.OnValueChanged += OnWheelMountedChanged;
            QuestBatteryMounted.OnValueChanged += OnBatteryMountedChanged;
            QuestFuelLiters.OnValueChanged += OnFuelChanged;
            QuestKeyCollected.OnValueChanged += OnKeyCollectedChanged;
            QuestGatesOpened.OnValueChanged += OnGatesStateChanged;
            QuestBunkerDeparted.OnValueChanged += OnBunkerDepartedChanged;

            // Initial visual sync
            UpdateBunkerLighting(QuestGeneratorRunning.Value);
            if (QuestGatesOpened.Value)
            {
                OpenGatesVisualImmediate();
            }
            else
            {
                CloseGatesVisualImmediate();
            }
            StartCoroutine(ApplyInitialPresentation());
        }

        public override void OnNetworkDespawn()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) Instance = null;
            QuestGeneratorRunning.OnValueChanged -= OnGeneratorStateChanged;
            QuestWheelMounted.OnValueChanged -= OnWheelMountedChanged;
            QuestBatteryMounted.OnValueChanged -= OnBatteryMountedChanged;
            QuestFuelLiters.OnValueChanged -= OnFuelChanged;
            QuestKeyCollected.OnValueChanged -= OnKeyCollectedChanged;
            QuestGatesOpened.OnValueChanged -= OnGatesStateChanged;
            QuestBunkerDeparted.OnValueChanged -= OnBunkerDepartedChanged;
        }

        private void Update()
        {
            if (Time.unscaledTime > notificationExpiry)
            {
                LatestNotification = "";
            }

            if (!IsSpawned || !IsServer || Instance != this || QuestBunkerDeparted.Value) return;

            // Check if vehicle has crossed the blast door threshold into the road
            var car = CoopVehicle.Instance;
            bool isGarage = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GarageScene";
            float thresholdZ = isGarage ? 13.0f : 2.0f;
            if (isGarage && car != null && car.transform.position.z > thresholdZ && CanDrive)
            {
                CoopSession.Instance?.TryDepartBunker();
            }
        }

        private void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (IsSpawned) StartCoroutine(ApplyInitialPresentation());
        }

        private IEnumerator ApplyInitialPresentation()
        {
            yield return null;
            if (!IsSpawned) yield break;
            FindSceneReferences();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GarageScene") yield break;
            RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance?.RestorePreparation(QuestGeneratorRunning.Value, QuestKeyCollected.Value);
            RogueDrive.Gameplay.Hub.GarageSwingGateController.Instance?.ApplyNetworkState(QuestGatesOpened.Value);
        }

        // --- Server RPCs for shared interactions ---

        private bool CanUseStation(ulong sender, CoopBunkerActionType action)
        {
            if (!IsServer || !IsSpawned || !IsBunkerPhase || !NetworkManager.ConnectedClients.TryGetValue(sender, out var client) || client.PlayerObject == null) return false;
            var player = client.PlayerObject.GetComponent<CoopPlayer>();
            if (player == null || player.IsDowned.Value || player.Seat.Value >= 0) return false;
            foreach (var station in FindObjectsByType<CoopBunkerInteractable>(FindObjectsSortMode.None))
            {
                if (station.ActionType != action || !station.CanInteract()) continue;
                var collider = station.GetComponentInChildren<Collider>();
                Vector3 eye = player.transform.position + Vector3.up * 1.65f;
                Vector3 target = collider != null ? CoopBunkerInteractable.InteractionPoint(collider, eye) : station.transform.position;
                if (Vector3.Distance(eye, target) > 4.2f) continue;
                Vector3 delta = target - eye;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<CoopPlayer>() == player ||
                        hit.collider.GetComponentInParent<CoopBunkerInteractable>() == station) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) return true;
            }
            return false;
        }

        [Rpc(SendTo.Server)]
        public void StartGeneratorServerRpc(RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.Generator)) return;
            if (QuestGeneratorRunning.Value) return;
            QuestGeneratorRunning.Value = true;
            NotifyQuestProgressClientRpc("⚡ Аварийный генератор запущен! Электросеть восстановлена.", true);
        }

        [Rpc(SendTo.Server)]
        public void MountWheelServerRpc(RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.WheelMount)) return;
            if (QuestWheelMounted.Value) return;
            QuestWheelMounted.Value = true;
            NotifyQuestProgressClientRpc("🔧 Переднее колесо смонтировано на автомобиль!", true);
        }

        [Rpc(SendTo.Server)]
        public void MountBatteryServerRpc(RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.BatteryMount)) return;
            if (QuestBatteryMounted.Value) return;
            QuestBatteryMounted.Value = true;
            NotifyQuestProgressClientRpc("🔋 Аккумулятор установлен в моторный отсек!", true);
        }

        [Rpc(SendTo.Server)]
        public void AddFuelServerRpc(float amount, RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.FuelRefill) || !CoopVehicle.Finite(amount) || amount <= 0f) return;
            amount = 10f;
            if (QuestFuelLiters.Value >= RequiredFuel) return;
            QuestFuelLiters.Value = Mathf.Min(RequiredFuel, QuestFuelLiters.Value + amount);
            bool isComplete = QuestFuelLiters.Value >= RequiredFuel;
            NotifyQuestProgressClientRpc(
                isComplete 
                    ? "⛽ Бак полностью заправлен (15 / 15 л)!" 
                    : $"⛽ Залито топливо: {QuestFuelLiters.Value:0} / {RequiredFuel:0} л", 
                isComplete);
        }

        [Rpc(SendTo.Server)]
        public void CollectKeyServerRpc(RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.CollectKeys)) return;
            if (QuestKeyCollected.Value) return;
            QuestKeyCollected.Value = true;
            NotifyQuestProgressClientRpc("🔑 Ключи зажигания и стартовое снаряжение экипажа получены!", true);
        }

        [Rpc(SendTo.Server)]
        public void OpenGatesServerRpc(RpcParams rpc = default)
        {
            if (!CanUseStation(rpc.Receive.SenderClientId, CoopBunkerActionType.OpenGates)) return;
            if (QuestGatesOpened.Value) return;
            if (!QuestGeneratorRunning.Value)
            {
                NotifyWarningClientRpc("⚠️ Сначала запустите генератор для питания привода ворот!");
                return;
            }
            QuestGatesOpened.Value = true;
            NotifyQuestProgressClientRpc("🚪 Защитные гермоворота открыты! Путь на трассу свободен.", true);
        }

        public void ServerCompleteRoadStop(int stopIndex)
        {
            if (!IsServer || !IsSpawned || IsBunkerPhase) return;
            switch (stopIndex)
            {
                case 1:
                    if (!QuestStop1FuelScavenged.Value)
                    {
                        QuestStop1FuelScavenged.Value = true;
                        NotifyQuestProgressClientRpc("[Остановка 1: АЗС] Тайник патруля добавлен в общий запас!", true);
                    }
                    break;
                case 2:
                    if (!QuestStop2CampExplored.Value)
                    {
                        QuestStop2CampExplored.Value = true;
                        NotifyQuestProgressClientRpc("🌲 [Остановка 2: Лесной лагерь] Провизия и медикаменты пополнены!", true);
                    }
                    break;
                case 3:
                    if (!QuestStop3ServiceVisited.Value)
                    {
                        QuestStop3ServiceVisited.Value = true;
                        NotifyQuestProgressClientRpc("[Остановка 3: Грузовой двор] Ремонтные материалы собраны!", true);
                    }
                    break;
                case 4:
                    if (!QuestDestinationReached.Value)
                    {
                        QuestDestinationReached.Value = true;
                        NotifyQuestProgressClientRpc("🏁 [ФИНАЛ: СТО «Северная»] Экипаж успешно добрался до безопасного комплекса!", true);
                    }
                    break;
            }
        }

        // --- Client RPC Notifications ---

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyQuestProgressClientRpc(string message, bool isComplete)
        {
            LatestNotification = message;
            notificationExpiry = Time.unscaledTime + 4.5f;

            if (isComplete)
            {
                RogueDrive.Audio.AudioManager.Instance?.PlayLevelUp();
            }
            else
            {
                RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyWarningClientRpc(string message)
        {
            LatestNotification = message;
            notificationExpiry = Time.unscaledTime + 3.0f;
            RogueDrive.Audio.AudioManager.Instance?.PlayRicochet();
        }

        // --- Visual Callbacks ---

        private void OnGeneratorStateChanged(bool previous, bool current)
        {
            UpdateBunkerLighting(current);
            if (current)
            {
                RogueDrive.Audio.AudioManager.Instance?.PlayImpact(0.5f);
                RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance?.SetPower(true);
            }
        }

        private void OnWheelMountedChanged(bool previous, bool current)
        {
            // Station visuals follow the network variable; never mutate solo inventory.
        }

        private void OnBatteryMountedChanged(bool previous, bool current)
        {
        }

        private void OnFuelChanged(float previous, float current)
        {
        }

        private void OnKeyCollectedChanged(bool previous, bool current)
        {
            if (current)
            {
                RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance?.PickUpKeys();
            }
        }

        private void OnGatesStateChanged(bool previous, bool current)
        {
            if (current)
            {
                RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance?.OpenGate();
                var gate = RogueDrive.Gameplay.Hub.GarageSwingGateController.Instance;
                if (gate != null) gate.ApplyNetworkState(true);
                else StartCoroutine(OpenGatesRoutine());
            }
            else
            {
                CloseGatesVisualImmediate();
            }
        }

        private void OnBunkerDepartedChanged(bool previous, bool current)
        {
            if (current && IsServer)
            {
                NotifyQuestProgressClientRpc("🚀 Экипаж покинул Бункер 07! Начало экспедиции по шоссе.", true);
            }
        }

        public void FindSceneReferences()
        {
            if (leftGateDoor == null)
            {
                var dl = GameObject.Find("Stage_World/Bunker_Exterior/Bunker_SwingBlastGate/Door_Left_Hinge")
                      ?? GameObject.Find("Bunker_SwingBlastGate/Door_Left_Hinge")
                      ?? GameObject.Find("GarageHubRoot/Exit_Ramp_And_Gate/Bunker_SwingBlastGate/Door_Left_Hinge");
                if (dl != null) leftGateDoor = dl.transform;
            }
            if (rightGateDoor == null)
            {
                var dr = GameObject.Find("Stage_World/Bunker_Exterior/Bunker_SwingBlastGate/Door_Right_Hinge")
                      ?? GameObject.Find("Bunker_SwingBlastGate/Door_Right_Hinge")
                      ?? GameObject.Find("GarageHubRoot/Exit_Ramp_And_Gate/Bunker_SwingBlastGate/Door_Right_Hinge");
                if (dr != null) rightGateDoor = dr.transform;
            }
            if (workshopMainLights == null || workshopMainLights.Length == 0)
            {
                var mlRoot = GameObject.Find("Stage_World/Coop_Bunker_Workshop/Lighting");
                if (mlRoot != null)
                {
                    var allLights = mlRoot.GetComponentsInChildren<Light>(true);
                    var mains = new System.Collections.Generic.List<Light>();
                    var ambers = new System.Collections.Generic.List<Light>();
                    foreach (var l in allLights)
                    {
                        if (l.name.Contains("Main") || l.name.Contains("Spot")) mains.Add(l);
                        else if (l.name.Contains("Amber")) ambers.Add(l);
                    }
                    workshopMainLights = mains.ToArray();
                    emergencyAmberLights = ambers.ToArray();
                }
            }
            UpdateBunkerLighting(QuestGeneratorRunning.Value);
        }

        public void BindGateDoors(Transform left, Transform right)
        {
            leftGateDoor = left;
            rightGateDoor = right;
        }

        public void BindLights(Light[] mainLights, Light[] amberLights)
        {
            workshopMainLights = mainLights;
            emergencyAmberLights = amberLights;
            UpdateBunkerLighting(QuestGeneratorRunning.Value);
        }

        private void UpdateBunkerLighting(bool generatorOn)
        {
            if (workshopMainLights != null)
            {
                foreach (var l in workshopMainLights)
                {
                    if (l != null) l.enabled = generatorOn;
                }
            }

            if (emergencyAmberLights != null)
            {
                foreach (var l in emergencyAmberLights)
                {
                    if (l != null) l.enabled = !generatorOn;
                }
            }
        }

        private void OpenGatesVisualImmediate()
        {
            if (leftGateDoor != null) leftGateDoor.localRotation = Quaternion.Euler(0f, 265f, 0f);
            if (rightGateDoor != null) rightGateDoor.localRotation = Quaternion.Euler(0f, 95f, 0f);
        }

        private void CloseGatesVisualImmediate()
        {
            if (leftGateDoor != null) leftGateDoor.localRotation = Quaternion.identity;
            if (rightGateDoor != null) rightGateDoor.localRotation = Quaternion.identity;
        }

        private IEnumerator OpenGatesRoutine()
        {
            float elapsed = 0f;
            float duration = 3.2f;

            Quaternion leftStart = leftGateDoor != null ? leftGateDoor.localRotation : Quaternion.identity;
            Quaternion leftTarget = Quaternion.Euler(0f, 265f, 0f);

            Quaternion rightStart = rightGateDoor != null ? rightGateDoor.localRotation : Quaternion.identity;
            Quaternion rightTarget = Quaternion.Euler(0f, 95f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (leftGateDoor != null) leftGateDoor.localRotation = Quaternion.Slerp(leftStart, leftTarget, t);
                if (rightGateDoor != null) rightGateDoor.localRotation = Quaternion.Slerp(rightStart, rightTarget, t);
                yield return null;
            }

            OpenGatesVisualImmediate();
        }

        public string GetDriveBlockReason()
        {
            if (!QuestGeneratorRunning.Value) return "Сначала запустите резервный генератор бункера!";
            if (!QuestWheelMounted.Value) return "На автомобиле не установлено переднее колесо!";
            if (!QuestBatteryMounted.Value) return "В моторном отсеке отсутствует аккумулятор!";
            if (QuestFuelLiters.Value < RequiredFuel) return $"В бензобаке мало топлива ({QuestFuelLiters.Value:0}/15 л)!";
            if (!QuestKeyCollected.Value) return "Заберите ключи зажигания со стола снаряжения!";
            if (!QuestGatesOpened.Value) return "Откройте защитные гермоворота на пульте!";
            return "";
        }

        public string GetFormattedQuestLog()
        {
            var sb = new System.Text.StringBuilder();

            if (IsBunkerPhase)
            {
                sb.AppendLine("<color=#00E5FF><b>📋 ПОДГОТОВКА В БУНКЕРЕ 07:</b></color>");
                sb.AppendLine(FormatLine(QuestGeneratorRunning.Value, "1. Запустить генератор бункера"));
                sb.AppendLine(FormatLine(QuestWheelMounted.Value, "2. Смонтировать колесо на машину"));
                sb.AppendLine(FormatLine(QuestBatteryMounted.Value, "3. Установить аккумулятор в отсек"));
                sb.AppendLine(FormatLine(QuestFuelLiters.Value >= 15f, $"4. Залить топливо ({QuestFuelLiters.Value:0}/15 л)"));
                sb.AppendLine(FormatLine(QuestKeyCollected.Value, "5. Забрать ключи и снаряжение"));
                sb.AppendLine(FormatLine(QuestGatesOpened.Value, "6. Открыть защитные гермоворота"));

                var car = CoopVehicle.Instance;
                int crew = car != null ? car.CrewCount : 0;
                sb.AppendLine(FormatLine(crew >= 2, $"7. Занять места в машине ({crew}/2)"));
                sb.AppendLine(FormatLine(false, "8. Выехать на трассу «Северная»"));
            }
            else
            {
                sb.AppendLine("<color=#FFCC00><b>🛣️ МАРШРУТ: БУНКЕР 07 ➔ СТО «СЕВЕРНАЯ»:</b></color>");
                sb.AppendLine("<color=#55FF77>[✔] Старт: Прорыв из бункера</color>");
                sb.AppendLine(FormatLine(QuestStop1FuelScavenged.Value, "АЗС (1.4 км) — тайник патруля"));
                sb.AppendLine(FormatLine(QuestStop2CampExplored.Value, "Лесной лагерь (3.2 км) — аптечный резерв"));
                sb.AppendLine(FormatLine(QuestStop3ServiceVisited.Value, "Грузовой двор (9.6 км) — ремонтный резерв"));
                sb.AppendLine(FormatLine(QuestDestinationReached.Value, "Финал: СТО «Северная» — безопасная база"));
            }

            return sb.ToString();
        }

        private static string FormatLine(bool done, string label)
        {
            return done 
                ? $"<color=#55FF77>[✔] {label}</color>" 
                : $"<color=#FFAA33>[ ] {label}</color>";
        }
    }
}
