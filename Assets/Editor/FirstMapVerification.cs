#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueDrive.Gameplay;

namespace RogueDrive.EditorTools
{
    /// <summary>Run in an isolated batch project. Never opens or saves the user's gameplay scene.</summary>
    public static class FirstMapVerification
    {
        static int frames;
        static bool started;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            SessionState.SetBool("GarageScene3DDecorated", true);
            FirstMapSetup.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                // Static fields are reset by domain reload; use Play Mode entry hook below.
                if (!started)
                {
                    started = true;
                    var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    road.name = "Initial_Road";
                    road.transform.position = new Vector3(0f, -0.2f, 75f);
                    road.transform.localScale = new Vector3(24f, 0.4f, 150f);
                    var material = new Material(Shader.Find("Standard")) { color = new Color(0.14f, 0.15f, 0.18f) };
                    road.GetComponent<Renderer>().sharedMaterial = material;
                    new GameObject("Track").AddComponent<ProceduralTrackGenerator>();
                    var sun = new GameObject("Sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.shadows = LightShadows.Soft;
                    sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
                    sun.intensity = 1.2f;
                    RenderSettings.sun = sun;
                    QualitySettings.shadowDistance = 140f;
                }
                if (++frames < 20) return;
                var generator = UnityEngine.Object.FindFirstObjectByType<ProceduralTrackGenerator>();
                MethodInfo spawn = typeof(ProceduralTrackGenerator).GetMethod("SpawnNextChunk", Private);
                while (UnityEngine.Object.FindObjectsByType<TrackChunk>(FindObjectsSortMode.None).Length < 10)
                    spawn.Invoke(generator, new object[] { false });
                if (frames < 45) return;

                var chunks = UnityEngine.Object.FindObjectsByType<TrackChunk>(FindObjectsSortMode.None).OrderBy(c => c.transform.position.z).ToArray();
                var solids = UnityEngine.Object.FindObjectsByType<TrackObstacle>(FindObjectsSortMode.None).Where(o => o.name.EndsWith("_Solid")).ToArray();
                var debris = UnityEngine.Object.FindObjectsByType<TrackObstacle>(FindObjectsSortMode.None).Where(o => o.name.EndsWith("_Breakable")).ToArray();
                if (solids.Length < 5 || debris.Length < 10) throw new Exception("Obstacle diversity missing");
                if (!solids[0].TryConsume() || !solids[0].gameObject.activeSelf || solids[0].GetComponent<Collider>().isTrigger)
                    throw new Exception("Solid obstacle disappeared or has no collision");
                if (!debris[0].TryConsume() || debris[0].gameObject.activeSelf) throw new Exception("Debris cannot be cleared");
                if (solids[0].TryConsume()) throw new Exception("Repeated contact bypassed cooldown");
                var explosive = UnityEngine.Object.FindObjectsByType<ExplosiveBarrel>(FindObjectsSortMode.None).First();
                explosive.TakeDamage(1f);
                if (explosive.IsDead) throw new Exception("Barrel detonates on any small hit");
                explosive.TakeDamage(100f);
                if (!explosive.IsDead) throw new Exception("Barrel does not detonate when destroyed");
                var testCar = new GameObject("Contact test car");
                testCar.transform.position = new Vector3(10000f, 0f, 0f);
                testCar.AddComponent<ArcadeCarController>().enabled = false;
                var testBarrelObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                testBarrelObject.transform.position = new Vector3(10000f, 0f, 0f);
                var testBarrel = testBarrelObject.AddComponent<ExplosiveBarrel>();
                MethodInfo contact = typeof(ExplosiveBarrel).GetMethod("CheckCarRam", Private);
                contact.Invoke(testBarrel, new object[] { testCar, 1f });
                if (testBarrel.IsDead) throw new Exception("Gentle contact detonated barrel");
                contact.Invoke(testBarrel, new object[] { testCar, 8f });
                if (!testBarrel.IsDead) throw new Exception("Strong collision failed to detonate barrel");
                UnityEngine.Object.Destroy(testCar);
                for (int i = 0; i < chunks.Length - 1; i++)
                    if (Vector3.Distance(chunks[i].EndPosition, chunks[i + 1].transform.position) > 0.01f)
                        throw new Exception("Road connection gap");
                Directory.CreateDirectory("Logs/FirstMap");
                Capture(new Vector3(0f, 8f, 18f), new Vector3(0f, 3f, 85f), "01-entry");
                Capture(chunks[3].transform.TransformPoint(new Vector3(0f, 9f, -15f)), chunks[3].transform.TransformPoint(new Vector3(0f, 2f, 50f)), "02-service-station");
                Capture(chunks[7].transform.TransformPoint(new Vector3(0f, 10f, -12f)), chunks[7].transform.TransformPoint(new Vector3(0f, 2f, 65f)), "03-freight");
                Capture(chunks[9].transform.TransformPoint(new Vector3(0f, 8f, -20f)), chunks[9].transform.TransformPoint(new Vector3(0f, 3f, 40f)), "04-checkpoint");
                File.WriteAllText("Logs/FirstMap/verification.txt", $"PASS: 10 connected chunks; {solids.Length} solid obstacles, {debris.Length} breakables; solid survives contact and debounce; debris clears; barrel withstands small damage and gentle contact, detonates on lethal damage or strong contact; four Play Mode renders.\n");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(1);
            }
        }

        [InitializeOnLoadMethod]
        static void ResumeBatch()
        {
            if (!Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("RogueDrive.EditorTools.FirstMapVerification.RunBatch")) return;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    EditorApplication.update -= Tick;
                    EditorApplication.update += Tick;
                }
            };
        }

        static void Capture(Vector3 position, Vector3 lookAt, string name)
        {
            var go = new GameObject("Preview Camera");
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = 65f;
            camera.farClipPlane = 550f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.49f, 0.57f, 0.61f);
            var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/FirstMap/" + name + ".png", pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(pixels);
            UnityEngine.Object.Destroy(go);
        }
    }
}
#endif
