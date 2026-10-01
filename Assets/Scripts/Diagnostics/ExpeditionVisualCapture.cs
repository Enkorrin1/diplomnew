#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using RogueDrive.Gameplay.Coop;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Diagnostics
{
    /// <summary>Opt-in development-player visual checks. No automation is installed in release players.</summary>
    public sealed class ExpeditionVisualCapture : MonoBehaviour
    {
        string folder, role;
        int width = 1280, height = 720;
        int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 2 < args.Length; i++)
                if (args[i] == "-visual-polish-capture")
                {
                    var runner = new GameObject("ExpeditionVisualCapture").AddComponent<ExpeditionVisualCapture>();
                    runner.folder = Path.GetFullPath(args[i + 1]); runner.role = args[i + 2];
                    for (int j = 0; j + 1 < args.Length; j++)
                    {
                        if (args[j] == "-screen-width") int.TryParse(args[j + 1], out runner.width);
                        if (args[j] == "-screen-height") int.TryParse(args[j + 1], out runner.height);
                    }
                    Directory.CreateDirectory(runner.folder); DontDestroyOnLoad(runner.gameObject);
                    Application.runInBackground = true; return;
                }
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Path.Combine(folder ?? Application.temporaryCachePath, "errors.txt"), message + "\n" + stack + "\n"); }
        }
        IEnumerator Capture(string name)
        {
            // The game's saved graphics settings intentionally override Unity launch flags.
            // Override only the test window after each scene has applied those settings.
            Screen.SetResolution(Mathf.Max(800, width), Mathf.Max(600, height), FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.5f);
        }
        IEnumerator Until(Func<bool> condition, float timeout = 30)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!condition()) { errors++; File.AppendAllText(Path.Combine(folder, "errors.txt"), "Timeout in " + SceneManager.GetActiveScene().name + "\n"); }
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3);
            if (role == "screens")
            {
                yield return Capture("main-menu");
                var view = FindFirstObjectByType<SceneUIView>();
                view.Navigate(2); yield return Capture("settings-screen");
                var graphics = FindFirstObjectByType<GraphicsSettingsPanel>();
                foreach (var button in graphics.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (button.name == "SettingsTab1") button.onClick.Invoke();
                yield return Capture("settings-graphics");
                view.Navigate(7); view.Navigate(3); yield return Capture("about"); view.Navigate(7);
                var lobby = FindFirstObjectByType<CoopLobbyModal>(FindObjectsInactive.Include);
                lobby.Open(); yield return Capture("coop-lobby"); lobby.Close();
                SceneManager.LoadScene("GarageScene"); yield return new WaitForSecondsRealtime(5);
                var inventory = FindFirstObjectByType<InventoryWindowUI>(FindObjectsInactive.Include);
                if (inventory != null)
                {
                    inventory.GetComponent<RogueDrive.Gameplay.Hub.GaragePlayerController>().SetMovementLocked(false);
                    inventory.OpenForTrunk(null); yield return Capture("inventory"); inventory.Close();
                }
                else { errors++; Debug.LogError("Visual capture: inventory missing"); }
                var service = FindFirstObjectByType<VehicleDashboardPanelsUI>();
                service.ShowEnginePanel(); yield return Capture("vehicle-service"); service.ClosePanel();
                service.ShowFuelInletPanel(); yield return Capture("fuel-service"); service.ClosePanel();
                SceneManager.LoadScene("Stage1_Outskirts"); yield return new WaitForSecondsRealtime(4);
                yield return Capture("driving-hud");
                var pause = FindFirstObjectByType<PauseMenuUI>();
                if (pause != null) { pause.PauseGame(); yield return Capture("pause"); pause.ResumeGame(); }
            }
            else
            {
                var lobby = FindFirstObjectByType<CoopLobbyModal>(FindObjectsInactive.Include);
                lobby.Open(); yield return null;
                var session = CoopSession.Instance;
                if (role == "host") session.Host(); else session.Join("127.0.0.1");
                yield return Until(() => session.Manager != null && session.Manager.IsConnectedClient && session.ClientConnected, 45);
                yield return Capture("lobby-connected-" + role);
                session.ToggleReady();
                yield return Until(() => SceneManager.GetActiveScene().name == "GarageScene" && CoopPlayer.Local != null, 45);
                yield return new WaitForSecondsRealtime(2);
                yield return Capture("coop-hud-" + role);
                session.SetMenu(true); yield return Capture("coop-menu-" + role); session.SetMenu(false);
                File.WriteAllText(Path.Combine(folder, "network-" + role + ".txt"), "connected=" + session.Manager.IsConnectedClient + "\nclients=" + session.ClientConnected + "\nscene=" + SceneManager.GetActiveScene().name + "\n");
                // Keep both peers alive until both have finished their screenshots.
                yield return Until(() => File.Exists(Path.Combine(folder, "network-host.txt")) && File.Exists(Path.Combine(folder, "network-client.txt")), 20);
            }
            File.WriteAllText(Path.Combine(folder, "complete-" + role + ".txt"), "errors=" + errors + "\n");
            Application.Quit(errors == 0 ? 0 : 1);
        }
    }
}
#endif
