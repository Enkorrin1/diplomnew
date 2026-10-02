#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Track;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Diagnostics
{
    /// <summary>
    /// Opt-in замер кадров в development-сборке: «-routeBenchmark папка». Гараж и точки маршрута,
    /// в каждой — средний FPS, худший кадр и снимок экрана. Буря на время замера остановлена.
    /// </summary>
    public sealed class RouteBenchmark : MonoBehaviour
    {
        static readonly (string name, float distance, float stormGap)[] Points =
        {
            ("01-city", 1500, 900), ("02-fields", 9000, 900), ("03-sto1", 15620, 900),
            ("04-canyon", 19300, 900), ("05-sto2", 25260, 900), ("06-tunnel", 28500, 900),
            ("07-pass", 31200, 900), ("08-sto3", 35420, 900), ("09-citadel", 40500, 900),
            ("10-storm-close", 12000, 120),
        };
        const float SampleSeconds = 6f;

        string folder;
        readonly StringBuilder report = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-routeBenchmark")
                {
                    var runner = new GameObject("RouteBenchmark").AddComponent<RouteBenchmark>();
                    runner.folder = Path.GetFullPath(args[i + 1]);
                    Directory.CreateDirectory(runner.folder);
                    DontDestroyOnLoad(runner.gameObject);
                    Application.runInBackground = true;
                    return;
                }
        }

        IEnumerator Start()
        {
            report.AppendLine($"{SystemInfo.graphicsDeviceName} | {SystemInfo.processorType} | {Screen.width}x{Screen.height}");
            report.AppendLine($"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pixelLights={QualitySettings.pixelLightCount} shadows={QualitySettings.shadows} vSync={QualitySettings.vSyncCount}");
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;

            SceneManager.LoadScene("GarageScene");
            yield return new WaitForSecondsRealtime(12);
            yield return Measure("00-garage");

            SceneManager.LoadScene("Stage1_Outskirts");
            yield return Until(() => SeamlessJourneyStream.Instance != null && FindFirstObjectByType<ArcadeCarController>() != null, 60);
            yield return new WaitForSecondsRealtime(3);
            var stream = SeamlessJourneyStream.Instance;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            stream.Route.GetType().GetField("stormSpeed", flags)?.SetValue(stream.Route, 0f);

            foreach (var point in Points)
            {
                var car = FindFirstObjectByType<ArcadeCarController>();
                var body = car.GetComponent<Rigidbody>();
                body.isKinematic = true;
                stream.Route.Evaluate(point.distance, out var p, out var q);
                car.transform.SetPositionAndRotation(p + Vector3.up * .3f, q);
                body.position = car.transform.position; body.rotation = q;
                var storm = CreepingStormBarrier.Instance;
                if (storm != null) typeof(CreepingStormBarrier).GetField("routeDistance", flags)?.SetValue(storm, point.distance - point.stormGap);
                yield return new WaitForSecondsRealtime(25);   // подгрузка и постепенное включение региона
                yield return Measure(point.name);
            }
            // Вклад отдельных систем: та же точка у бури, системы выключаются по одной.
            yield return Toggle("storm-no-mirror", () => Part<StormRearMirror>("Rear_Mirror_UI", false), () => Part<StormRearMirror>("Rear_Mirror_UI", true));
            yield return Toggle("storm-no-wall", () => Part<StormFrontView>("Wall", false), () => Part<StormFrontView>("Wall", true));
            yield return Toggle("storm-no-postfx", () => SetLayers(false), () => SetLayers(true));
            yield return Toggle("storm-no-shadows", () => QualitySettings.shadows = ShadowQuality.Disable, () => QualitySettings.shadows = ShadowQuality.All);
            yield return Toggle("storm-1-pixel-light", () => QualitySettings.pixelLightCount = 1, () => QualitySettings.pixelLightCount = 8);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Application.Quit(0);
        }

        IEnumerator Measure(string name)
        {
            Time.timeScale = 1;
            var frames = new System.Collections.Generic.List<float>();
            float end = Time.realtimeSinceStartup + SampleSeconds;
            while (Time.realtimeSinceStartup < end) { yield return null; frames.Add(Time.unscaledDeltaTime); }
            frames.Sort();
            float avg = frames.Count / frames.Sum();
            float worst = frames[frames.Count - 1] * 1000f;
            float low1 = 1f / frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count * .99f) - 1, 0, frames.Count - 1)];
            report.AppendLine($"{name,-16} avg={avg,6:F1} fps  1%low={low1,6:F1} fps  worst={worst,6:F1} ms  frames={frames.Count}");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.5f);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        }

        IEnumerator Toggle(string name, Action off, Action on)
        {
            off(); yield return new WaitForSecondsRealtime(1.5f);
            yield return Measure(name);
            on(); yield return new WaitForSecondsRealtime(1f);
        }

        // Выключает компонент и его дочерний объект (панель зеркала или саму стену).
        static void Part<T>(string child, bool on) where T : MonoBehaviour
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.enabled = on;
                var t = c.transform.Find(child);
                if (t != null) t.gameObject.SetActive(on);
            }
        }

        static void SetLayers(bool on)
        {
            foreach (var layer in FindObjectsByType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(FindObjectsSortMode.None)) layer.enabled = on;
        }

        IEnumerator Until(Func<bool> condition, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
#endif
