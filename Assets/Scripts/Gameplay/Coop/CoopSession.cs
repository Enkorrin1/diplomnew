using System;
using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>
    /// LAN co-op session coordinator. Supports lobby connection in MainMenuScene,
    /// ready-state synchronization between players, and synchronized transition to Coop_Outskirts.
    /// </summary>
    public sealed class CoopSession : MonoBehaviour
    {
        public static CoopSession Instance { get; private set; }
        [SerializeField] private NetworkManager manager;
        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField] private Transform vehicleSpawn;
        [SerializeField] private Camera lobbyCamera;
        [SerializeField] private GameObject crewUiPrefab;

        private readonly HashSet<ulong> admitted = new HashSet<ulong>();
        private bool connecting;
        private float connectStarted;
        private bool loadingScene;
        private bool returnToMenu;
        private bool remoteConnected;
        private float loadStarted;
        private bool roadTransition;
        public bool LoadingScene => loadingScene;

        public string Status { get; private set; } = "Создайте экипаж или подключитесь по адресу хоста.";
        public NetworkManager Manager => manager;
        public bool Busy => connecting || (manager != null && manager.IsListening);
        public bool MenuOpen { get; private set; } = true;
        public const ushort Port = 7777;
        private ushort connectionPort = Port;
        public Vector3 SpawnPosition => vehicleSpawn != null 
            ? vehicleSpawn.position 
            : (SceneManager.GetActiveScene().name == "GarageScene" 
                ? new Vector3(0f, 0.55f, 0f) 
                : (SceneManager.GetActiveScene().name == "Stage1_Outskirts" 
                    ? new Vector3(0f, 0.45f, 16f) 
                    : new Vector3(0f, 0.45f, -22f)));

        // --- Lobby Ready & Sync State ---
        public bool HostReady { get; private set; }
        public bool ClientReady { get; private set; }
        public bool ClientConnected => manager != null && (manager.IsServer ? manager.ConnectedClientsIds.Count > 1 : remoteConnected);
        public float LaunchCountdown { get; private set; } = -1f;
        public Action OnLobbyStateChanged;

        private const string MsgLobbySetReady = "RD_Lobby_SetReady";
        private const string MsgLobbySync = "RD_Lobby_Sync";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Independent test runners must not connect to another task's host.
            var testArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < testArgs.Length; i++)
                if (testArgs[i] == "-coop-test-port" && ushort.TryParse(testArgs[i + 1], out var testPort) && testPort > 0)
                    connectionPort = testPort;
#endif
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<CoopScenePresentation>().Configure(crewUiPrefab);

            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            if (manager != null)
            {
                manager.NetworkConfig.EnableSceneManagement = true;
                if (vehiclePrefab != null) manager.AddNetworkPrefab(vehiclePrefab);
                manager.NetworkConfig.ConnectionApproval = true;
                manager.ConnectionApprovalCallback = Approve;
                manager.OnServerStarted += ServerStarted;
                manager.OnClientConnectedCallback += Connected;
                manager.OnClientDisconnectCallback += Disconnected;
                manager.OnTransportFailure += TransportFailed;
            }
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-coop-host") Host();
                if (args[i] == "-coop-connect" && i + 1 < args.Length) Join(args[++i]);
            }
        }

        public void Host()
        {
            if (manager == null || Busy || manager.ShutdownInProgress || returnToMenu) return;
            loadingScene = false;
            roadTransition = false;
            remoteConnected = false;
            admitted.Clear();
            HostReady = false;
            ClientReady = false;
            LaunchCountdown = -1f;

            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", connectionPort, "0.0.0.0");
            Status = "Создание экипажа…";
            if (!manager.StartHost())
            {
                Status = "Не удалось создать экипаж. Проверьте порт 7777.";
            }
            else
            {
                Status = "Экипаж создан · порт 7777";
                RegisterNamedMessageHandlers();
                BroadcastLobbySync();
            }
        }

        public void Join(string address)
        {
            if (manager == null || Busy || manager.ShutdownInProgress || returnToMenu) return;
            if (!IPAddress.TryParse((address ?? "").Trim(), out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                Status = "Введите IPv4-адрес хоста, например 192.168.1.10.";
                return;
            }

            HostReady = false;
            ClientReady = false;
            LaunchCountdown = -1f;

            manager.GetComponent<UnityTransport>().SetConnectionData(ip.ToString(), connectionPort);
            Status = "Подключение к " + ip + "…";
            connecting = manager.StartClient();
            connectStarted = Time.unscaledTime;
            if (!connecting) Status = "Не удалось начать подключение.";
        }

        public void Leave()
        {
            roadTransition = false;
            returnToMenu = SceneManager.GetActiveScene().name != "MainMenuScene";
            connecting = false;
            remoteConnected = false;
            loadingScene = false;
            HostReady = false;
            ClientReady = false;
            LaunchCountdown = -1f;

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
            }
            admitted.Clear();
            Status = "Вы вышли из экипажа.";
            SetMenu(true);
            OnLobbyStateChanged?.Invoke();
        }

        public void ToggleReady()
        {
            if (manager == null || !manager.IsConnectedClient || loadingScene || SceneManager.GetActiveScene().name != "MainMenuScene") return;

            if (manager.IsServer)
            {
                HostReady = !HostReady;
                CheckLaunchReadiness();
                BroadcastLobbySync();
                OnLobbyStateChanged?.Invoke();
            }
            else
            {
                bool requestedReady = !ClientReady;
                var writer = new FastBufferWriter(sizeof(bool), Unity.Collections.Allocator.Temp);
                writer.WriteValueSafe(requestedReady);
                manager.CustomMessagingManager.SendNamedMessage(MsgLobbySetReady, NetworkManager.ServerClientId, writer);
                writer.Dispose();
            }
        }

        private void RegisterNamedMessageHandlers()
        {
            if (manager == null || manager.CustomMessagingManager == null) return;

            // Server receives ready toggle from client
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MsgLobbySetReady, (senderClientId, reader) =>
            {
                if (!manager.IsServer || senderClientId == NetworkManager.ServerClientId ||
                    !manager.ConnectedClients.ContainsKey(senderClientId) || loadingScene ||
                    SceneManager.GetActiveScene().name != "MainMenuScene" || !reader.TryBeginRead(sizeof(bool))) return;
                reader.ReadValueSafe(out bool ready);
                ClientReady = ready;
                CheckLaunchReadiness();
                BroadcastLobbySync();
                OnLobbyStateChanged?.Invoke();
            });

            // Client receives sync state from server
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MsgLobbySync, (senderClientId, reader) =>
            {
                if (manager.IsServer || senderClientId != NetworkManager.ServerClientId ||
                    !reader.TryBeginRead(sizeof(bool) * 3 + sizeof(float))) return;
                reader.ReadValueSafe(out bool hReady);
                reader.ReadValueSafe(out bool cReady);
                reader.ReadValueSafe(out float countdown);
                reader.ReadValueSafe(out bool crewConnected);
                remoteConnected = crewConnected;
                HostReady = hReady;
                ClientReady = cReady;
                LaunchCountdown = countdown;
                OnLobbyStateChanged?.Invoke();
            });
        }

        private void BroadcastLobbySync()
        {
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return;

            var writer = new FastBufferWriter(sizeof(bool) * 3 + sizeof(float), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(HostReady);
            writer.WriteValueSafe(ClientReady);
            writer.WriteValueSafe(LaunchCountdown);
            writer.WriteValueSafe(ClientConnected);
            manager.CustomMessagingManager.SendNamedMessageToAll(MsgLobbySync, writer);
            writer.Dispose();
        }

        private void CheckLaunchReadiness()
        {
            if (!manager.IsServer) return;

            bool bothConnected = ClientConnected;
            // Co-op requires both players to confirm readiness before launching
            bool allReady = bothConnected && HostReady && ClientReady;

            if (allReady)
            {
                if (LaunchCountdown < 0f)
                {
                    LaunchCountdown = 2.5f; // 2.5 second launch sequence
                    RogueDrive.Audio.AudioManager.Instance?.PlayLevelUp();
                }
            }
            else
            {
                LaunchCountdown = -1f; // Cancel if someone toggles unready or disconnects
            }
        }

        public void StartSoloTestLaunch()
        {
            if (!Debug.isDebugBuild || manager == null || !manager.IsServer || loadingScene || SceneManager.GetActiveScene().name != "MainMenuScene") return;
            LaunchCountdown = 2.0f;
            RogueDrive.Audio.AudioManager.Instance?.PlayLevelUp();
            BroadcastLobbySync();
            OnLobbyStateChanged?.Invoke();
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool available = admitted.Count < 2;
            string scene = SceneManager.GetActiveScene().name;
            Vector3 recoveryPosition = default;
            var car = CoopVehicle.Instance;
            bool roadRecovery = scene == "Stage1_Outskirts" && car != null && !car.Transitioning.Value &&
                car.Stopped && car.TryRecoveryPosition(out recoveryPosition);
            bool safe = !loadingScene && (scene == "MainMenuScene" || scene == "GarageScene" ||
                roadRecovery || (scene == "Coop_Outskirts" && (CoopVehicle.Instance == null || CoopVehicle.Instance.AtStart)));
            response.Approved = available && safe;
            // In main menu lobby, do not instantiate 3D player objects yet.
            // When in GarageScene, Stage1_Outskirts or Coop_Outskirts, spawn player object directly.
            bool inGarage = SceneManager.GetActiveScene().name == "GarageScene";
            bool inGameScene = inGarage || SceneManager.GetActiveScene().name == "Stage1_Outskirts" || SceneManager.GetActiveScene().name == "Coop_Outskirts";
            response.CreatePlayerObject = inGameScene && response.Approved;
            response.Pending = false;
            response.Position = roadRecovery ? recoveryPosition : inGarage
                ? (admitted.Count == 0 ? new Vector3(6.67f, 1.05f, -7.70f) : new Vector3(5.50f, 1.05f, -7.70f))
                : SpawnPosition + new Vector3(admitted.Count == 0 ? -3f : 3f, 1f, 0f);
            response.Rotation = inGarage ? Quaternion.Euler(0f, -41f, 0f) : Quaternion.identity;
            response.Reason = response.Approved ? "" : !available ? "Экипаж уже заполнен (2/2)." : "Остановите машину на свободной площадке и дождитесь окончания загрузки.";
            if (response.Approved) admitted.Add(request.ClientNetworkId);
        }

        private void ServerStarted()
        {
            RegisterNamedMessageHandlers();
            manager.SceneManager.OnSceneEvent += OnSceneEvent;

            // If already in game scene (e.g. testing directly in GarageScene or Stage1_Outskirts)
            string curScene = SceneManager.GetActiveScene().name;
            if (curScene == "GarageScene" || curScene == "Stage1_Outskirts" || curScene == "Coop_Outskirts")
            {
                SpawnBunkerSession();
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.Load) loadingScene = true;
            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted) loadingScene = false;
            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted && 
               (sceneEvent.SceneName == "GarageScene" || sceneEvent.SceneName == "Stage1_Outskirts" || sceneEvent.SceneName == "Coop_Outskirts"))
            {
                if (manager != null && manager.IsServer)
                {
                    SpawnBunkerSession();
                    if (roadTransition)
                    {
                        var car = CoopVehicle.Instance;
                        car.ServerPlaceAt(new Vector3(0f, .65f, 16f), Quaternion.identity);
                        foreach (var client in manager.ConnectedClientsList)
                        {
                            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<CoopPlayer>() : null;
                            if (player != null) player.ServerSetSeat(player.Seat.Value,
                                player.Seat.Value >= 0 ? car.SeatPosition(player.Seat.Value) : car.transform.position + Vector3.right * 3f);
                        }
                        CoopQuestManager.Instance.QuestBunkerDeparted.Value = true;
                        roadTransition = false;
                        car.Transitioning.Value = false;
                        car.ServerFreeze(false);
                    }
                }
            }
        }

        private void SpawnBunkerSession()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool isGarage = sceneName == "GarageScene";
            bool isStage1 = sceneName == "Stage1_Outskirts";

            // If entering Stage1_Outskirts in Co-op, suppress offline single-player actors
            if (isStage1)
            {
                var standalonePlayer = GameObject.Find("Stage_Standalone_Player");
                if (standalonePlayer != null) standalonePlayer.SetActive(false);
                var standaloneCar = GameObject.Find("Classic Car_9");
                if (standaloneCar != null) standaloneCar.SetActive(false);
            }

            var spawnObj = GameObject.Find("Shared vehicle spawn");
            Vector3 basePos = spawnObj != null 
                ? spawnObj.transform.position 
                : (isGarage ? new Vector3(0f, 0.55f, 0f) : (isStage1 ? new Vector3(0f, 0.45f, 16f) : new Vector3(0f, 0.45f, -22f)));
            Quaternion baseRot = spawnObj != null 
                ? spawnObj.transform.rotation 
                : Quaternion.identity;

            // 1. Spawn vehicle
            if (vehiclePrefab != null && CoopVehicle.Instance == null)
            {
                var car = Instantiate(vehiclePrefab, basePos, baseRot);
                car.GetComponent<NetworkObject>().Spawn(false);
            }

            // 2. Spawn players for all connected clients
            int index = 0;
            foreach (var client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject == null)
                {
                    Vector3 playerPos;
                    Quaternion playerRot = Quaternion.identity;
                    if (isGarage)
                    {
                        // Spawn in bunker living area (beside sofa, console & arcade machine)
                        playerPos = index == 0 ? new Vector3(6.67f, 1.05f, -7.70f) : new Vector3(5.50f, 1.05f, -7.70f);
                        playerRot = Quaternion.Euler(0f, -41f, 0f);
                    }
                    else
                    {
                        playerPos = basePos + new Vector3(index == 0 ? -2.5f : 2.5f, 0.2f, 0f);
                    }

                    var playerObj = Instantiate(manager.NetworkConfig.PlayerPrefab, playerPos, playerRot);
                    playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId, false);
                }
                index++;
            }

            SetMenu(false);
        }

        private void Connected(ulong id)
        {
            connecting = false;
            RegisterNamedMessageHandlers();

            if (manager.IsServer)
            {
                CheckLaunchReadiness();
                BroadcastLobbySync();
            }

            if (id == manager.LocalClientId)
            {
                Status = manager.IsHost ? "Экипаж создан · порт 7777" : "Вы в экипаже";
                string active = SceneManager.GetActiveScene().name;
                if (active == "GarageScene" || active == "Stage1_Outskirts" || active == "Coop_Outskirts")
                {
                    SetMenu(false);
                }
            }

            OnLobbyStateChanged?.Invoke();
        }

        private void Disconnected(ulong id)
        {
            bool wasAdmitted = admitted.Remove(id);
            if (manager.IsServer && wasAdmitted)
            {
                CoopVehicle.Instance?.ReleaseDisconnected(id);
                ClientReady = false;
                CheckLaunchReadiness();
                BroadcastLobbySync();
            }

            if (id != manager.LocalClientId)
            {
                OnLobbyStateChanged?.Invoke();
                return;
            }

            connecting = false;
            remoteConnected = false;
            HostReady = ClientReady = false;
            LaunchCountdown = -1f;
            Status = string.IsNullOrEmpty(manager.DisconnectReason)
                ? "Соединение завершено. Можно создать экипаж или подключиться снова."
                : manager.DisconnectReason;
            SetMenu(true);
            OnLobbyStateChanged?.Invoke();

            string curScene = SceneManager.GetActiveScene().name;
            if (curScene == "GarageScene" || curScene == "Stage1_Outskirts" || curScene == "Coop_Outskirts")
            {
                returnToMenu = true;
            }
        }

        private void TransportFailed()
        {
            connecting = false;
            Status = "Ошибка соединения. Проверьте адрес и доступность порта 7777.";
            manager.Shutdown();
            SetMenu(true);
            OnLobbyStateChanged?.Invoke();
        }

        private void Update()
        {
            if (manager == null) return;
            if (loadingScene && manager.IsServer && Time.unscaledTime - loadStarted > 90f)
            {
                Leave();
                Status = "Загрузка не завершилась. Создайте экипаж повторно.";
            }
            if (returnToMenu && !manager.IsListening && !manager.ShutdownInProgress)
            {
                returnToMenu = false;
                SceneManager.LoadScene("MainMenuScene");
                return;
            }
            if (connecting && Time.unscaledTime - connectStarted > 12f)
            {
                Leave();
                Status = "Хост не ответил. Проверьте адрес, сеть/VPN и порт 7777.";
            }

            // Launch countdown handling on server
            if (manager != null && manager.IsServer && LaunchCountdown >= 0f)
            {
                LaunchCountdown -= Time.unscaledDeltaTime;
                if (LaunchCountdown <= 0f)
                {
                    LaunchCountdown = -1f;
                    LaunchGame();
                }
                else
                {
                    BroadcastLobbySync();
                }
                OnLobbyStateChanged?.Invoke();
            }

            string sName = SceneManager.GetActiveScene().name;
            if (Input.GetKeyDown(KeyCode.Escape) && manager.IsConnectedClient && 
               (sName == "GarageScene" || sName == "Stage1_Outskirts" || sName == "Coop_Outskirts"))
            {
                SetMenu(!MenuOpen);
            }

            if (lobbyCamera != null)
            {
                bool inLobby = CoopPlayer.Local == null;
                lobbyCamera.enabled = inLobby;
                var al = lobbyCamera.GetComponent<AudioListener>();
                if (al != null) al.enabled = inLobby;
            }
        }

        private void LaunchGame()
        {
            if (manager == null || !manager.IsServer || loadingScene) return;
            Status = "Запуск экспедиции... Загрузка Бункера 07.";
            loadingScene = true;
            loadStarted = Time.unscaledTime;
            var result = manager.SceneManager.LoadScene("GarageScene", LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                loadingScene = false;
                HostReady = ClientReady = false;
                Status = "Не удалось загрузить бункер: " + result;
                BroadcastLobbySync();
            }
        }

        public void SetMenu(bool open)
        {
            MenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        public bool TryDepartBunker()
        {
            var car = CoopVehicle.Instance;
            var quest = CoopQuestManager.Instance;
            if (manager == null || !manager.IsServer || loadingScene || car == null || quest == null ||
                !quest.CanDrive || !CrewReadyForDeparture() || SceneManager.GetActiveScene().name != "GarageScene") return false;
            foreach (var client in manager.ConnectedClientsList)
            {
                var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<CoopPlayer>() : null;
                if (player == null || player.Seat.Value < 0 || player.IsDowned.Value) return false;
            }
            loadingScene = true;
            loadStarted = Time.unscaledTime;
            roadTransition = true;
            car.Transitioning.Value = true;
            car.ServerFreeze(true);
            var result = manager.SceneManager.LoadScene("Stage1_Outskirts", LoadSceneMode.Single);
            if (result == SceneEventProgressStatus.Started) return true;
            loadingScene = roadTransition = false;
            car.Transitioning.Value = false;
            car.ServerFreeze(false);
            Status = "Не удалось загрузить трассу: " + result;
            return false;
        }

        public bool CrewReadyForDeparture()
        {
            if (manager == null || !manager.IsServer || manager.ConnectedClientsList.Count != 2) return false;
            foreach (var client in manager.ConnectedClientsList)
            {
                var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<CoopPlayer>() : null;
                if (player == null || player.Seat.Value < 0 || player.IsDowned.Value) return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (manager == null) return;
            manager.ConnectionApprovalCallback = null;
            manager.OnServerStarted -= ServerStarted;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
            manager.OnTransportFailure -= TransportFailed;
            if (manager.SceneManager != null)
            {
                manager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }
    }
}
