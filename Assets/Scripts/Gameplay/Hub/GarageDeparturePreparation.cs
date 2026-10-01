using System.Collections;
using System.Collections.Generic;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    public sealed class GarageDeparturePreparation : MonoBehaviour, IGarageInteractable
    {
        public static GarageDeparturePreparation Instance { get; private set; }
        public Transform[] briefingShots;
        public Canvas briefingCanvas;
        public Text subtitle;
        public bool BriefingSeen { get; private set; }
        public bool IsRunning { get; private set; }
        public bool FuelPacked { get; private set; }
        public bool RepairPacked { get; private set; }
        public bool Ready => BriefingSeen && FuelPacked && RepairPacked;
        public string Objective => !BriefingSeen
            ? "<b>ПЕРЕД ВЫЕЗДОМ</b>\n• Подойдите к карте маршрута у стеллажей [E]"
            : "<b>ПРИПАСЫ В ДОРОГУ</b>\n" + (FuelPacked ? "✓" : "•") + " Запас бензина — в багажник\n"
                + (RepairPacked ? "✓" : "•") + " Ремкомплект — в багажник\n"
                + "Аптечка и вода — на ваш выбор\n[E] у багажника · Shift + ЛКМ — переложить";

        GaragePlayerController player;
        Camera cameraUsed;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        float cameraFov;
        bool wasLocked, rayWasEnabled;
        GarageInteractionRaycaster ray;
        readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
        float nextRefresh;

        void Awake() { Instance = this; if (briefingCanvas != null) briefingCanvas.enabled = false; }
        void OnDestroy() { RestoreView(); if (Instance == this) Instance = null; }
        void OnDisable() { if (IsRunning) { StopAllCoroutines(); RestoreView(); } }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh || IsRunning) return;
            nextRefresh = Time.unscaledTime + .2f;
            RefreshCargo();
        }
        public void RefreshCargo()
        {
            bool fuel = false, repair = false;
            var trunk = VehicleCargoTrunk.Instance;
            if (trunk != null) for (int i = 0; i < trunk.MaxSlots; i++)
            {
                var item = trunk.GetSlot(i).WorldObject;
                if (item == null) continue;
                var fluid = item.GetComponent<FluidContainer>();
                fuel |= fluid != null && fluid.FluidType == BunkerFluidType.Gasoline && fluid.CurrentLiters >= 1f;
                repair |= item.GetComponent<GarageItemFunction>()?.Kind == GarageItemFunction.ItemKind.RepairKit;
            }
            bool changed = fuel != FuelPacked || repair != RepairPacked;
            FuelPacked = fuel; RepairPacked = repair;
            if (changed) GaragePrologueManager.Instance?.RefreshObjective();
        }
        public string GetPromptText() => !CanInteract() ? "Карта маршрута: сначала подготовьте автомобиль" : "[E] Изучить маршрут до безопасной СТО";
        public bool CanInteract() => !IsRunning && GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPowerOn
            && BunkerStarterCarAssembly.Instance != null && BunkerStarterCarAssembly.Instance.IsAssemblyComplete;
        public void Interact(GaragePlayerController actor)
        {
            if (!CanInteract() || actor == null || actor.IsMovementLocked) return;
            player = actor;
            StartCoroutine(Briefing());
        }
        public void RestoreProgress(bool seen) { BriefingSeen = seen; RefreshCargo(); }
        public void SkipBriefing() { if (IsRunning) { StopAllCoroutines(); CompleteBriefing(); } }

        IEnumerator Briefing()
        {
            if (briefingShots == null || briefingShots.Length < 3 || player.PlayerCamera == null) yield break;
            // Arrival checkpoint precedes the supply choice; replaying the briefing never overwrites it.
            if (!BriefingSeen) GarageDepartureCheckpoint.CaptureArrival();
            IsRunning = true;
            cameraUsed = player.PlayerCamera;
            cameraPosition = cameraUsed.transform.localPosition;
            cameraRotation = cameraUsed.transform.localRotation;
            cameraFov = cameraUsed.fieldOfView;
            wasLocked = player.IsMovementLocked;
            player.SetMovementLocked(true);
            ray = cameraUsed.GetComponent<GarageInteractionRaycaster>();
            if (ray != null) { rayWasEnabled = ray.enabled; ray.enabled = false; }
            InventoryWindowUI.Instance?.Close();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c != briefingCanvas && c.gameObject.scene == gameObject.scene) { canvases[c] = c.enabled; c.enabled = false; }
            briefingCanvas.enabled = true;
            string[] lines = {
                "БЛИЖАЙШАЯ ЦЕЛЬ — БЕЗОПАСНАЯ СТО\nВыезжайте из Бункера 07 и следуйте по дороге через окраину.",
                "ФРОНТ БУРИ ПРИБЛИЖАЕТСЯ СЗАДИ\nОстановки дают добычу, но отнимают время. Не задерживайтесь на дороге.",
                "ПОДГОТОВЬТЕ ЗАПАС\nЗагрузите бензин и ремкомплект. Выберите дополнительные припасы перед выездом." };
            for (int i = 0; i < 3; i++)
            {
                subtitle.text = lines[i];
                Transform shot = briefingShots[i];
                for (float t = 0; t < 5f; t += Time.unscaledDeltaTime)
                {
                    cameraUsed.transform.SetPositionAndRotation(shot.position + shot.forward * (.12f * t), shot.rotation);
                    cameraUsed.fieldOfView = 48;
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) { CompleteBriefing(); yield break; }
                    yield return null;
                }
            }
            CompleteBriefing();
        }
        void CompleteBriefing()
        {
            BriefingSeen = true;
            RestoreView();
            GaragePrologueManager.Instance?.RefreshObjective();
            GarageInteractionUI.Instance?.ShowNotification("Запас бензина и ремкомплект ждут на столе припасов. Перенесите их в багажник.", 6f);
        }
        void RestoreView()
        {
            if (!IsRunning) return;
            if (cameraUsed != null) { cameraUsed.transform.localPosition = cameraPosition; cameraUsed.transform.localRotation = cameraRotation; cameraUsed.fieldOfView = cameraFov; }
            if (player != null) player.SetMovementLocked(wasLocked);
            if (ray != null) ray.enabled = rayWasEnabled;
            foreach (var c in canvases) if (c.Key != null) c.Key.enabled = c.Value;
            canvases.Clear();
            if (briefingCanvas != null) briefingCanvas.enabled = false;
            IsRunning = false;
        }
    }
}
