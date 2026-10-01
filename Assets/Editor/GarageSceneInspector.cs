#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.EditorTools
{
    public static class GarageSceneInspector
    {
        [MenuItem("Tools/Inspect Garage")]
        public static void Inspect()
        {
            Debug.Log("=== GARAGE SCENE AUDIT ===");
            var player = Object.FindFirstObjectByType<GaragePlayerController>();
            Debug.Log($"Player: {(player != null ? player.name + " at " + player.transform.position : "MISSING")}");

            var gen = Object.FindFirstObjectByType<GarageGeneratorSwitch>();
            Debug.Log($"Generator: {(gen != null ? gen.name + " at " + gen.transform.position : "MISSING")}");

            var keys = Object.FindFirstObjectByType<GarageCarKeys>();
            Debug.Log($"Keys: {(keys != null ? keys.name + " at " + keys.transform.position : "MISSING")}");

            var gate = Object.FindFirstObjectByType<GarageSwingGateController>();
            Debug.Log($"Gate: {(gate != null ? gate.name + " at " + gate.transform.position : "MISSING")}");

            var gateSwitch = Object.FindFirstObjectByType<GarageGateSwitchInteractable>();
            Debug.Log($"GateSwitch: {(gateSwitch != null ? gateSwitch.name + " at " + gateSwitch.transform.position : "MISSING")}");

            var boarding = Object.FindFirstObjectByType<GarageVehicleBoarding>();
            Debug.Log($"Boarding: {(boarding != null ? boarding.name + " at " + boarding.transform.position : "MISSING")}");

            var assembly = Object.FindFirstObjectByType<BunkerStarterCarAssembly>();
            Debug.Log($"Assembly: {(assembly != null ? assembly.name + " at " + assembly.transform.position : "MISSING")}");

            var hotspots = Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None);
            Debug.Log($"Hotspots count: {hotspots.Length}");
            foreach (var h in hotspots)
            {
                Debug.Log($"  Hotspot: {h.name}, Required: {h.RequiredItem}, Pos: {h.transform.position}, Active: {h.gameObject.activeInHierarchy}");
            }

            var items = Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None);
            Debug.Log($"CarPartItems count: {items.Length}");
            foreach (var it in items)
            {
                Debug.Log($"  Item: {it.name}, Type: {it.ItemType}, Pos: {it.transform.position}, Active: {it.gameObject.activeInHierarchy}");
            }

            var exit = Object.FindFirstObjectByType<GarageExitTrigger>();
            Debug.Log($"ExitTrigger: {(exit != null ? exit.name + " at " + exit.transform.position : "MISSING")}");
            Debug.Log("=== END AUDIT ===");
        }
    }
}
#endif
