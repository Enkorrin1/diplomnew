using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>
    /// Контроллер модального окна лобби совместной игры в главном меню.
    /// Позволяет создавать экипаж (Host), подключаться по IP (Client),
    /// синхронизировать статус готовности [ГОТОВ] и запускать совместную игру.
    /// </summary>
    public sealed class CoopLobbyModal : MonoBehaviour
    {
        [Header("Root Panel")]
        [SerializeField] private GameObject modalRoot;

        [Header("Connection Controls")]
        [SerializeField] private InputField addressInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button closeButton;

        [Header("Status & Information")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text myIpText;
        [SerializeField] private Text launchStatusText;

        [Header("Player Slots")]
        [SerializeField] private Text hostSlotText;
        [SerializeField] private Text clientSlotText;

        [Header("Ready Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Text readyButtonText;
        [SerializeField] private Image readyButtonBg;
        [SerializeField] private Button soloLaunchButton;

        [Header("Colors")]
        [SerializeField] private Color readyColor = new Color(0.15f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color unreadyColor = new Color(0.18f, 0.25f, 0.35f, 1f);

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            if (soloLaunchButton != null) soloLaunchButton.onClick.AddListener(OnSoloLaunchClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void Start()
        {
            RogueDrive.UI.ExpeditionPresentation.Lobby(modalRoot != null ? modalRoot.transform : null);
            if (addressInput != null && string.IsNullOrEmpty(addressInput.text))
            {
                addressInput.text = "127.0.0.1";
            }

            if (myIpText != null)
            {
                myIpText.text = $"Ваш локальный IP: {GetLocalIPv4()}  ·  Порт: 7777";
            }
        }

        public void Open()
        {
            RogueDrive.UI.ExpeditionPresentation.Lobby(modalRoot != null ? modalRoot.transform : null);
            EnsureCoopSessionPrefab();

            if (modalRoot != null) modalRoot.SetActive(true);

            if (myIpText != null)
            {
                myIpText.text = $"Ваш локальный IP: {GetLocalIPv4()}  ·  Порт: 7777";
            }

            UpdateUI();
        }

        public void Close()
        {
            var session = CoopSession.Instance;
            if (session != null && session.Busy)
            {
                session.Leave();
            }

            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private void EnsureCoopSessionPrefab()
        {
            if (CoopSession.Instance == null)
            {
                var prefab = Resources.Load<GameObject>("CrewSession");
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Coop/CrewSession.prefab");
                }
#endif
                if (prefab != null)
                {
                    var instance = Instantiate(prefab);
                    instance.name = "Crew session";
                }
            }
        }

        private void OnHostClicked()
        {
            EnsureCoopSessionPrefab();
            CoopSession.Instance?.Host();
            RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
        }

        private void OnJoinClicked()
        {
            EnsureCoopSessionPrefab();
            string ip = addressInput != null ? addressInput.text : "127.0.0.1";
            CoopSession.Instance?.Join(ip);
            RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
        }

        private void OnLeaveClicked()
        {
            CoopSession.Instance?.Leave();
            RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
        }

        private void OnReadyClicked()
        {
            CoopSession.Instance?.ToggleReady();
            RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
        }

        private void OnSoloLaunchClicked()
        {
            CoopSession.Instance?.StartSoloTestLaunch();
            RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            var session = CoopSession.Instance;
            bool exists = session != null;
            bool busy = exists && session.Busy;
            bool isConnected = exists && session.Manager != null && session.Manager.IsConnectedClient;
            bool isHost = exists && session.Manager != null && session.Manager.IsHost;

            if (statusText != null)
            {
                statusText.text = exists ? session.Status : "Инициализация сетевого модуля...";
            }

            if (hostButton != null) hostButton.interactable = !busy;
            if (joinButton != null) joinButton.interactable = !busy;
            if (addressInput != null) addressInput.interactable = !busy;
            if (leaveButton != null) leaveButton.gameObject.SetActive(busy);

            // Ready Button
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(isConnected);
                if (isConnected)
                {
                    bool isLocalReady = isHost ? session.HostReady : session.ClientReady;
                    if (readyButtonText != null)
                    {
                        readyButtonText.text = isLocalReady ? "✔ ГОТОВ К ВЫЕЗДУ" : "ГОТОВИТЬСЯ К ВЫЕЗДУ";
                    }
                    if (readyButtonBg != null)
                    {
                        readyButtonBg.color = Color.white;
                        var colors = readyButton.colors;
                        colors.normalColor = isLocalReady ? RogueDrive.UI.LowPolyUi.Healthy : RogueDrive.UI.LowPolyUi.Amber;
                        colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, .18f);
                        colors.selectedColor = colors.highlightedColor;
                        colors.pressedColor = Color.Lerp(colors.normalColor, RogueDrive.UI.LowPolyUi.Ink, .25f);
                        readyButton.colors = colors;
                    }
                }
            }

            // Solo Launch Button (available for Host to test scene alone without waiting for 2nd peer)
            if (soloLaunchButton != null)
            {
                bool canSolo = exists && isHost && !session.ClientConnected && session.HostReady && session.LaunchCountdown < 0f;
                soloLaunchButton.gameObject.SetActive(canSolo);
            }

            // Slots display
            if (hostSlotText != null)
            {
                if (!exists || session.Manager == null || !session.Manager.IsListening)
                {
                    hostSlotText.text = "<b>ИГРОК 1: КОМАНДИР (ХОСТ)</b>\n<color=#888888>[ НЕ СОЗДАН ]</color>";
                }
                else
                {
                    string readyStr = session.HostReady 
                        ? "<color=#55FF77>✔ ГОТОВ</color>" 
                        : "<color=#FFAA33>[ ] НЕ ГОТОВ</color>";
                    hostSlotText.text = $"<b>ИГРОК 1: КОМАНДИР (ХОСТ)</b>\n{readyStr}";
                }
            }

            if (clientSlotText != null)
            {
                if (!exists || !session.ClientConnected)
                {
                    string placeholder = busy && isHost 
                        ? "<color=#FFCC00>Ожидание подключения напарника...</color>" 
                        : "<color=#888888>[ СВОБОДНО ]</color>";
                    clientSlotText.text = $"<b>ИГРОК 2: НАПАРНИК</b>\n{placeholder}";
                }
                else
                {
                    string readyStr = session.ClientReady 
                        ? "<color=#55FF77>✔ ГОТОВ</color>" 
                        : "<color=#FFAA33>[ ] НЕ ГОТОВ</color>";
                    clientSlotText.text = $"<b>ИГРОК 2: НАПАРНИК</b>\n{readyStr}";
                }
            }

            // Launch Status
            if (launchStatusText != null)
            {
                if (!isConnected)
                {
                    launchStatusText.text = "Создайте экипаж или введите IP-адрес хоста для подключения.";
                }
                else if (session.LaunchCountdown >= 0f)
                {
                    int sec = Mathf.Max(1, Mathf.CeilToInt(session.LaunchCountdown));
                    launchStatusText.text = $"<color=#55FF77><b>🚀 ВСЕ БОЙЦЫ ГОТОВЫ! ЗАПУСК ЭКСПЕДИЦИИ ЧЕРЕЗ {sec}...</b></color>";
                }
                else if (session.ClientConnected)
                {
                    int readyCount = (session.HostReady ? 1 : 0) + (session.ClientReady ? 1 : 0);
                    launchStatusText.text = $"Ожидание подтверждения готовности экипажа: <b>{readyCount}/2</b>";
                }
                else
                {
                    launchStatusText.text = session.HostReady
                        ? "<color=#FFCC00>Вы готовы! Ожидание подключения напарника...</color>"
                        : "Напарник еще не подключился. Нажмите «ГОТОВ» для подтверждения боевой готовности.";
                }
            }
        }

        private static string GetLocalIPv4()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint != null ? endPoint.Address.ToString() : "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
