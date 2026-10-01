using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>Scene-authored Canvas controls; does not pause the shared simulation.</summary>
    public sealed class CoopSessionUI : MonoBehaviour
    {
        [SerializeField] private CoopSession session;
        [SerializeField] private GameObject menu;
        [SerializeField] private InputField address;
        [SerializeField] private Button host, join, leave, resume;
        [SerializeField] private Button mainMenu;
        [SerializeField] private Text status, hud, notice;
        [SerializeField] private Text networkHelp;
        [SerializeField] private Text questTracker;
        [SerializeField] private GameObject questBox;
        [SerializeField] private Text supplyTracker;

        private Canvas canvas;
        private static string cachedLocalIp;
        private GameObject hudSurface, noticeSurface, menuBackdrop;
        private GameObject crewSurface;
        private Text crewText, controlText;
        private float nextCrewRefresh;

        public static string GetLocalIPv4()
        {
            if (!string.IsNullOrEmpty(cachedLocalIp)) return cachedLocalIp;
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                string fallback = null;
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        string s = ip.ToString();
                        if (s.StartsWith("192.168.")) { cachedLocalIp = s; return cachedLocalIp; }
                        if (fallback == null) fallback = s;
                    }
                }
                if (fallback != null) { cachedLocalIp = fallback; return cachedLocalIp; }
            }
            catch { }
            cachedLocalIp = "127.0.0.1";
            return cachedLocalIp;
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            session = CoopSession.Instance;
        }

        private void Start()
        {
            PolishPresentation();
            if (session == null) session = CoopSession.Instance;
            if (session != null)
            {
                if (host != null) host.onClick.AddListener(session.Host);
                if (join != null) join.onClick.AddListener(() => session.Join(address.text));
                if (leave != null) leave.onClick.AddListener(session.Leave);
                if (resume != null) resume.onClick.AddListener(() => session.SetMenu(false));
            }
            if (mainMenu != null) mainMenu.onClick.AddListener(ReturnToMainMenu);

            if (networkHelp == null && menu != null)
            {
                var helpObj = menu.transform.Find("Network help");
                if (helpObj != null) networkHelp = helpObj.GetComponent<Text>();
            }

            if (networkHelp != null)
            {
                networkHelp.text = $"LAN / VPN  ·  ПОРТ 7777\nIP для напарника: <color=#E8B560><b>{GetLocalIPv4()}</b></color>\nПодключайтесь в бункере или на безопасной стоянке.";
            }
        }

        private void PolishPresentation()
        {
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; }
            if (menu != null)
            {
                var panel = menu.GetComponent<RectTransform>();
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
                panel.anchoredPosition = new Vector2(-340, 0); panel.sizeDelta = new Vector2(570, 690);
                RogueDrive.UI.ExpeditionPresentation.Surface(menu.GetComponent<Image>());
                Place(menu.transform.Find("Title"), 32, 26, 500, 58);
                var title = menu.transform.Find("Title")?.GetComponent<Text>(); if (title != null) { title.text = "ЭКИПАЖ"; title.alignment = TextAnchor.MiddleLeft; title.fontSize = 42; }
                Place(menu.transform.Find("Subtitle"), 32, 90, 500, 38);
                var subtitle = menu.transform.Find("Subtitle")?.GetComponent<Text>(); if (subtitle != null) { subtitle.fontSize = 19; subtitle.alignment = TextAnchor.MiddleLeft; }
                Place(address != null ? address.transform : null, 32, 162, 506, 50);
                RogueDrive.UI.LowPolyUi.Label(menu.transform, "ExpeditionAddressCaption", "АДРЕС ХОСТА  /  LAN ИЛИ VPN", new Vector2(32, -133), new Vector2(506, 24), 13, RogueDrive.UI.LowPolyUi.Muted);
                Place(host != null ? host.transform : null, 32, 234, 246, 54);
                Place(join != null ? join.transform : null, 292, 234, 246, 54);
                Place(resume != null ? resume.transform : null, 32, 306, 246, 54);
                Place(leave != null ? leave.transform : null, 292, 306, 246, 54);
                Place(mainMenu != null ? mainMenu.transform : null, 32, 578, 506, 50);
                Place(status != null ? status.transform : null, 32, 387, 506, 78);
                if (status != null) { status.fontSize = 18; status.alignment = TextAnchor.UpperLeft; }
                var help = menu.transform.Find("Network help"); Place(help, 32, 474, 506, 82);
                if (help != null) { networkHelp = help.GetComponent<Text>(); networkHelp.fontSize = 17; networkHelp.alignment = TextAnchor.UpperLeft; }
                var backdrop = RogueDrive.UI.LowPolyUi.Icon(transform, "coop_backdrop", Vector2.zero, Vector2.zero);
                menuBackdrop = backdrop.gameObject; backdrop.name = "ExpeditionCoopBackdrop"; backdrop.preserveAspect = false;
                backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one; backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
                backdrop.transform.SetAsFirstSibling();
                if (backdrop.sprite != null) { var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = backdrop.sprite.rect.width / backdrop.sprite.rect.height; }
            }
            if (hud != null)
            {
                hud.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); hud.fontSize = 17; hud.alignment = TextAnchor.UpperLeft; hud.color = RogueDrive.UI.LowPolyUi.Paper;
                hud.verticalOverflow = VerticalWrapMode.Overflow;
                Place(hud.transform, 46, 36, 1034, 100);
                foreach (var shadow in hud.GetComponents<Shadow>()) Destroy(shadow);
                var surface = RogueDrive.UI.LowPolyUi.Panel(transform, "ExpeditionCrewStatus", new Vector2(28, -24), new Vector2(1070, 100), RogueDrive.UI.LowPolyUi.Ink);
                hudSurface = surface.gameObject; surface.transform.SetSiblingIndex(hud.transform.GetSiblingIndex());
                controlText = RogueDrive.UI.LowPolyUi.Label(transform, "ExpeditionCrewControls", "", Vector2.zero, new Vector2(1450, 42), 16, RogueDrive.UI.LowPolyUi.Paper);
                var controlsRect = controlText.rectTransform; controlsRect.anchorMin = controlsRect.anchorMax = controlsRect.pivot = new Vector2(.5f, 0); controlsRect.anchoredPosition = new Vector2(0, 12);
                controlText.alignment = TextAnchor.MiddleCenter;
                var crewPanel = RogueDrive.UI.LowPolyUi.Panel(transform, "ExpeditionCrewMembers", Vector2.zero, new Vector2(450, 100), RogueDrive.UI.LowPolyUi.Ink);
                crewPanel.rectTransform.anchorMin = crewPanel.rectTransform.anchorMax = crewPanel.rectTransform.pivot = new Vector2(1, 1); crewPanel.rectTransform.anchoredPosition = new Vector2(-28, -24);
                crewSurface = crewPanel.gameObject;
                crewText = RogueDrive.UI.LowPolyUi.Label(crewPanel.transform, "CrewMembers", "", new Vector2(18, -10), new Vector2(414, 80), 17, RogueDrive.UI.LowPolyUi.Paper);
                crewText.verticalOverflow = VerticalWrapMode.Overflow;
            }
            // Keep the bound, live quest panel; disable obsolete duplicate authored trackers.
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text != questTracker && text.name == "Tracker Text") text.transform.parent.gameObject.SetActive(false);
            if (questBox != null)
            {
                var rect = questBox.GetComponent<RectTransform>(); rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-28, -160); rect.sizeDelta = new Vector2(410, 280);
                RogueDrive.UI.ExpeditionPresentation.Surface(questBox.GetComponent<Image>());
                foreach (var outline in questBox.GetComponents<Outline>()) Destroy(outline);
                if (questTracker != null) { questTracker.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); questTracker.fontSize = 18; questTracker.lineSpacing = 1.2f; questTracker.color = RogueDrive.UI.LowPolyUi.Paper; }
            }
            if (notice != null)
            {
                var rect = notice.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 45); rect.sizeDelta = new Vector2(1040, 62);
                notice.fontSize = 21; notice.color = RogueDrive.UI.LowPolyUi.Amber;
                var surface = RogueDrive.UI.LowPolyUi.Panel(transform, "ExpeditionNotice", Vector2.zero, new Vector2(1080, 74), RogueDrive.UI.LowPolyUi.Ink);
                surface.rectTransform.anchorMin = surface.rectTransform.anchorMax = surface.rectTransform.pivot = new Vector2(.5f, 0); surface.rectTransform.anchoredPosition = new Vector2(0, 39);
                surface.transform.SetSiblingIndex(notice.transform.GetSiblingIndex()); noticeSurface = surface.gameObject;
            }
            RogueDrive.UI.LowPolyUi.Apply(transform);
            if (host != null) RogueDrive.UI.LowPolyUi.StyleButton(host, true);
        }

        static void Place(Transform item, float x, float y, float width, float height) => RogueDrive.UI.ExpeditionPresentation.Place(item, x, y, width, height);

        public void ReturnToMainMenu()
        {
            if (mainMenu != null) mainMenu.interactable = false;
            if (session != null)
            {
                session.Leave();
                return;
            }
            enabled = false;
            RogueDrive.UI.SceneTransitionManager.SwitchScene("MainMenuScene");
        }

        private void Update()
        {
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (canvas.worldCamera == null || !canvas.worldCamera.enabled)
                {
                    canvas.worldCamera = Camera.main;
                }
            }

            if (session == null) session = CoopSession.Instance;
            if (session == null) return;

            if (menu != null) menu.SetActive(session.MenuOpen);
            if (menuBackdrop != null) menuBackdrop.SetActive(session.MenuOpen);
            if (hud != null) hud.gameObject.SetActive(!session.MenuOpen);
            if (hudSurface != null) hudSurface.SetActive(!session.MenuOpen);
            if (crewSurface != null) crewSurface.SetActive(!session.MenuOpen);
            if (controlText != null) controlText.gameObject.SetActive(!session.MenuOpen);
            if (questBox != null) questBox.SetActive(!session.MenuOpen && CoopQuestManager.Instance != null);
            if (notice != null) notice.gameObject.SetActive(!session.MenuOpen);
            if (noticeSurface != null) noticeSurface.SetActive(!session.MenuOpen && notice != null && !string.IsNullOrEmpty(notice.text));
            if (status != null) status.text = session.Status;
            if (host != null && join != null && address != null) host.interactable = join.interactable = address.interactable = !session.Busy;
            if (leave != null) leave.gameObject.SetActive(session.Busy);
            if (resume != null) resume.gameObject.SetActive(session.Manager != null && session.Manager.IsConnectedClient);
            if (mainMenu != null) mainMenu.gameObject.SetActive(true);

            var local = CoopPlayer.Local;
            var car = CoopVehicle.Instance;

            if (local != null && local.IsDowned.Value)
            {
                hud.text = $"<color=#FF4444>⚠ ТЯЖЕЛОЕ РАНЕНИЕ! ИСТЕКАНИЕ КРОВЬЮ: {Mathf.Max(0, local.BleedoutTimer.Value):0.0}с</color>\nПолзите к напарнику за помощью! [WASD — ползти]";
            }
            else if (local != null && local.Seat.Value == 1)
            {
                float stormDist = car != null ? car.StormDistance.Value : 900f;
                string stormStatus = stormDist > 80f ? "БЕЗОПАСНО" : stormDist > 40f ? "ПРИБЛИЖАЕТСЯ" : "ОПАСНОСТЬ!";
                hud.text = $"ЭКИПАЖ / ШТУРМАН     {(car != null ? Mathf.RoundToInt(car.Speed.Value * 3.6f) + " км/ч" : "")}  ·  HP {local.Health.Value:0}/100\nРАДАР БУРИ: {stormDist:0}м [{stormStatus}]\nQ — метка тайника  ·  R — ремонт на стоянке  ·  H — лечить экипаж  ·  E — выйти  ·  F — за руль";
            }
            else if (local != null && local.Seat.Value < 0)
            {
                hud.text = $"ЭКИПАЖ / ПЕШКОМ (ВЫЛАЗКА)     HP: {local.Health.Value:0}/100\n" +
                           $"{local.FirearmInfo}\n" +
                           $"WASD — идти  ·  Shift — бег  ·  E — тайник / помощь / сесть  ·  H — аптечка  ·  F — штурман  ·  Esc — меню";
            }
            else
            {
                string role = local == null ? "Ожидание подключения" : "ВОДИТЕЛЬ";
                hud.text = "ЭКИПАЖ / " + role + (car != null ? "     " + Mathf.RoundToInt(car.Speed.Value * 3.6f) + " км/ч" : "")
                    + (local != null ? $"  ·  HP {local.Health.Value:0}/100" : "")
                    + "\n" + (local == null ? "Кооператив на двоих · LAN / VPN"
                        : "WASD — вести  ·  Пробел — тормоз  ·  E — выйти  ·  F — сменить место  ·  Esc — меню");
            }

            var qm = CoopQuestManager.Instance;
            var supplies = CoopSupplies.Instance;
            if (supplyTracker != null)
            {
                supplyTracker.gameObject.SetActive(!session.MenuOpen && supplies != null && qm != null && !qm.IsBunkerPhase);
                if (supplies != null) supplyTracker.text = supplies.InventoryText + "\n" + supplies.MarkerText +
                    (car != null ? $"  ·  МАШИНА {car.Hull.Value:0}/100" : "");
            }
            if (questTracker != null && qm != null)
            {
                questTracker.text = qm.GetFormattedQuestLog().Replace("#00E5FF", "#E8B560").Replace("#FFAA33", "#A5B7BA").Replace("#FFCC00", "#E8B560").Replace("#55FF77", "#9DBC7D").Replace("📋 ", "");
            }

            if (local != null && !string.IsNullOrEmpty(local.Feedback))
            {
                notice.text = local.Feedback;
            }
            else if (qm != null && !string.IsNullOrEmpty(qm.LatestNotification))
            {
                notice.text = $"<color=#FFCC00><b>{qm.LatestNotification}</b></color>";
            }
            else
            {
                notice.text = car != null && car.Transitioning.Value ? "Экипаж выезжает. Загрузка трассы…"
                    : qm != null && qm.IsBunkerPhase && qm.CanDrive && car != null && car.CrewCount < 2
                    ? "Для выезда оба участника должны занять места в машине." : "";
            }
            if (hud != null && controlText != null && local != null)
            {
                string[] lines = hud.text.Split('\n');
                if (lines.Length > 1 && !local.IsDowned.Value)
                {
                    controlText.text = lines[lines.Length - 1];
                    hud.text = "<size=23><b>" + lines[0] + "</b></size>\n<color=#A5B7BA>" + (lines.Length > 2 ? lines[1] : "ОБЩАЯ ЭКСПЕДИЦИЯ  /  СЛЕДИТЕ ЗА МАШИНОЙ И НАПАРНИКОМ") + "</color>";
                }
                else controlText.text = "";
            }
            if (crewText != null && Time.unscaledTime >= nextCrewRefresh)
            {
                nextCrewRefresh = Time.unscaledTime + .2f;
                string crew = "<color=#E8B560><b>ЭКИПАЖ  /  02</b></color>\n";
                int count = 0;
                foreach (var member in Object.FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None))
                {
                    if (!member.IsSpawned) continue;
                    count++;
                    string role = member.IsDowned.Value ? "РАНЕН" : member.Seat.Value == 0 ? "ВОДИТЕЛЬ" : member.Seat.Value == 1 ? "ШТУРМАН" : "ВЫЛАЗКА";
                    string tint = member.IsDowned.Value ? "DF6C5A" : "9DBC7D";
                    crew += $"{(member.IsOwner ? "ВЫ" : "НАПАРНИК")}  ·  {role}  ·  <color=#{tint}>{member.Health.Value:0}%</color>\n";
                }
                if (count < 2) crew += "<color=#A5B7BA>ОЖИДАНИЕ НАПАРНИКА</color>";
                crewText.text = crew;
            }
        }
    }
}
