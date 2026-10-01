using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>One-time, editor-only extension. Everything is saved and editable in Stage1.</summary>
    public static class StageRouteAuthoring
    {
        const string Folder = "Assets/Content/RoutePrototype";
        static Material ground, asphalt;

        [MenuItem("RogueDrive/Route/Build First Journey Prototype")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage1_Outskirts") throw new InvalidOperationException("Open Stage1_Outskirts first.");
            var roots = scene.GetRootGameObjects();
            var world = roots.Single(g => g.name == "Stage_World");
            var authored = world.transform.Find("AuthoredStageWorld");
            if (authored.GetComponent<StageRoute>() != null)
                throw new InvalidOperationException("Route already exists. Edit its authored objects instead of overwriting them.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "RoutePrototype");
            ground = Material("Verge", new Color(.29f, .30f, .23f));
            asphalt = Material("Layby", new Color(.26f, .27f, .26f));
            var art = Resources.Load<FirstMapAssets>("FirstMapAssets");
            if (art == null) throw new InvalidOperationException("FirstMapAssets is required.");
            bool wasActive = world.activeSelf;
            world.SetActive(true);
            try
            {
                var chunks = authored.GetComponentsInChildren<TrackChunk>(true)
                    .OrderBy(c => c.GetComponent<BakedMapChunk>()?.Order ?? 0).ToList();
                var last = chunks.Last();
                float length = 150f + chunks.Sum(c => c.Length);
                int order = chunks.Max(c => c.GetComponent<BakedMapChunk>()?.Order ?? 0) + 1;
                int addition = 0;
                while (length < 13200f)
                {
                    // Broad bends, always returning to the northbound bearing; no loops or self-crossings.
                    int beat = addition % 16;
                    string module = beat == 2 || beat == 10 ? "CurveRight" : beat == 5 || beat == 13 ? "CurveLeft" : "Straight";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrackModules/" + module + ".prefab");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, authored);
                    go.name = $"Journey_{order:D3}_{module}";
                    go.transform.SetPositionAndRotation(last.EndPosition, last.EndRotation);
                    var marker = go.GetComponent<BakedMapChunk>() ?? go.AddComponent<BakedMapChunk>();
                    marker.Configure(order++);
                    last = go.GetComponent<TrackChunk>();
                    chunks.Add(last);
                    DressVerge(last, art, addition++);
                    length += last.Length;
                }

                var route = authored.gameObject.AddComponent<StageRoute>();
                var points = new List<Vector3> { Vector3.zero };
                foreach (var chunk in chunks)
                {
                    AddPoint(points, chunk.transform.position);
                    foreach (var road in chunk.RoadRenderers)
                        if (road != null) AddPoint(points, new Vector3(road.bounds.center.x, 0f, road.bounds.center.z));
                    AddPoint(points, chunk.EndPosition);
                }
                route.Configure(points.ToArray());
                var stops = new GameObject("Journey_RoadsideStops").transform;
                stops.SetParent(authored, false);
                float[] distances = { 1500, 3400, 5600, 7900, 10100, 12100 };
                string[] titles = { "01_Last_Fuel_Station", "02_Evacuation_Camp", "03_Freight_Yard", "04_Road_Maintenance", "05_Abandoned_Convoy", "06_Service_Approach" };
                for (int i = 0; i < distances.Length; i++)
                    BuildStop(route, chunks, stops, art, distances[i], i, titles[i]);

                var finish = authored.GetComponentInChildren<StageFinishOutpost>(true);
                finish.transform.SetPositionAndRotation(last.EndPosition, last.EndRotation);
                finish.Configure(1, "Безопасная СТО — Северная");
                var service = finish.GetComponent<WorkshopServiceZone>() ?? finish.gameObject.AddComponent<WorkshopServiceZone>();
                service.Configure("СТО Северная", true, WorkshopEquipment.All);
                // Leave a drivable stopping apron beyond the finish trigger.
                Box(finish.transform, "Arrival_Apron", new Vector3(0, -.22f, 18), new Vector3(60, .4f, 50), asphalt);
                Place(art.garage, finish.transform, new Vector3(22, 0, 16), 90, 15);
                foreach (var generator in roots.SelectMany(g => g.GetComponentsInChildren<ProceduralTrackGenerator>(true)))
                {
                    generator.AuthoredCampaign = authored;
                    generator.StageTargetDistance = route.Length;
                    generator.DynamicBiomesByDistance = false;
                    generator.SceneAuthoredMode = true;
                }
                // This old kilometer is a second, overlapping road, not part of the journey.
                var baked = world.transform.Find("BakedFirstMap");
                if (baked != null) baked.gameObject.SetActive(false);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Route] {chunks.Count} chunks; {route.Length:F0} m; {route.ExpectedMinutes:F1} min including 5 min stops; 6 optional laybys.");
            }
            finally { world.SetActive(wasActive); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AddPoint(List<Vector3> points, Vector3 point)
        {
            point.y = 0;
            if (Vector3.Distance(points[points.Count - 1], point) > .05f) points.Add(point);
        }

        static void DressVerge(TrackChunk chunk, FirstMapAssets art, int index)
        {
            foreach (var r in chunk.RoadRenderers)
            {
                if (r == null) continue;
                var groundObject = Box(chunk.transform, "Landscape", Vector3.zero, Vector3.one, ground);
                groundObject.transform.SetPositionAndRotation(new Vector3(r.bounds.center.x, -.35f, r.bounds.center.z), r.transform.rotation);
                groundObject.transform.localScale = new Vector3(260, .4f, r.transform.lossyScale.z + 6);
            }
            if (index % 3 == 0)
            {
                Place(art.rocks[index % art.rocks.Length], chunk.transform, new Vector3(-38, -.05f, 35), index * 17, 7 + index % 6);
                Place(art.rocks[(index + 2) % art.rocks.Length], chunk.transform, new Vector3(64, -.05f, 76), index * 31, 14);
            }
            if (index % 9 == 0)
                Place(index % 2 == 0 ? art.van : art.sedan, chunk.transform, new Vector3(17, 0, 52), 16, 4.8f);
            if (index % 13 == 0)
                Place(art.garage, chunk.transform, new Vector3(-52, 0, 70), 90, 14);
        }

        static void BuildStop(StageRoute route, List<TrackChunk> chunks, Transform parent, FirstMapAssets art, float distance, int index, string title)
        {
            // Laybys connect on a straight module, so no invisible guardrail blocks the access.
            var chunk = chunks.Where(c => c.Type == ChunkType.Straight)
                .OrderBy(c => Mathf.Abs(route.ProjectDistance(c.transform.position) + 50 - distance)).First();
            int side = index % 2 == 0 ? 1 : -1;
            var stop = new GameObject(title).transform;
            stop.SetParent(parent, false);
            stop.SetPositionAndRotation(chunk.transform.position + chunk.transform.forward * 50, chunk.transform.rotation);
            foreach (var t in chunk.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf("Guardrail", StringComparison.OrdinalIgnoreCase) >= 0
                    && Mathf.Sign(chunk.transform.InverseTransformPoint(t.position).x) == side)
                    t.gameObject.SetActive(false);
            Box(stop, "Parking_Access", new Vector3(side * 22, -.21f, 0), new Vector3(36, .4f, 76), asphalt);
            GameObject landmark = index == 0 ? art.gasStation : index == 1 ? art.tent : index == 2 ? art.container : index == 3 ? art.garage : index == 4 ? art.military : art.station;
            Place(landmark, stop, new Vector3(side * 34, 0, 8), side * -90, index == 1 ? 9 : index == 4 ? 6 : 18);
            Place(art.van, stop, new Vector3(side * 20, 0, -23), side * 12, 5.5f);
            Place(art.lamp, stop, new Vector3(side * 13, 0, -33), 0, 6);
            Place(art.pallet, stop, new Vector3(side * 27, 0, -7), 0, 1.6f);
            if (index == 2)
                Place(art.container, stop, new Vector3(side * 34, 0, -19), side * -90, 14);
            if (index == 4)
            {
                Place(art.military, stop, new Vector3(side * 25, 0, 22), -8, 6);
                Place(art.barrier, stop, new Vector3(side * 15, 0, 32), 0, 4);
            }
            // Physical, portable containers use the existing inventory and pouring system.
            string kind = index % 2 == 0 ? "FuelCanister" : "WaterCanister";
            for (int n = 0; n < 2; n++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VehicleService/" + kind + ".prefab");
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stop);
                item.name = kind + "_Loot_" + n;
                item.transform.localRotation = Quaternion.Euler(0, 12 * n, 0);
                var renderers = item.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                item.transform.position += stop.TransformPoint(new Vector3(side * 25 + n * .6f, .04f + bounds.extents.y, -7)) - bounds.center;
            }
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return go;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.localScale *= size / Mathf.Max(b.size.x, b.size.y, b.size.z);
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.position += parent.TransformPoint(position) - new Vector3(b.center.x, b.min.y, b.center.z);
            // Decorative art never brings vehicle controllers or dynamic bodies into the route.
            foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
