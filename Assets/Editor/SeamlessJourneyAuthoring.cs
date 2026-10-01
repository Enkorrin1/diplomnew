using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class SeamlessJourneyAuthoring
    {
        const string Folder = "Assets/Content/SeamlessJourney";
        const string WorldFolder = "Assets/Scenes/Journey";
        const float LinkLength = 650;
        // Opening scenes can unload assets referenced only by the native call stack.
        static JourneyStreamCatalog bakingCatalog;
        static readonly string[] EntryNames = { "Stage1_Outskirts", "Stage2_Wasteland", "Stage3_Industrial", "Stage4_Citadel" };
        static readonly string[] Titles = { "ГОРОД И ЛЕСА", "ОХРИСТЫЙ КАНЬОН", "ГОРНЫЙ ПЕРЕВАЛ", "ПОДСТУПЫ К ЦИТАДЕЛИ" };

        [MenuItem("RogueDrive/Route/Bake Seamless Campaign Streaming")]
        public static void Build()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save current scene edits first.");
            Directory.CreateDirectory("Temp/SeamlessJourney");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "SeamlessJourney");
            if (!AssetDatabase.IsValidFolder(WorldFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "Journey");
            string catalogPath = Folder + "/Campaign.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<JourneyStreamCatalog>(catalogPath);
            if (catalog == null)
            { catalog = ScriptableObject.CreateInstance<JourneyStreamCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            bakingCatalog = catalog;
            catalog.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            catalog.segments = new JourneyStreamCatalog.Segment[4];
            var combined = new List<Vector3>();
            float accumulated = 0;
            Vector3 nextStart = Vector3.zero;
            Quaternion nextRotation = Quaternion.identity;

            // Read all existing geometry before adding connections. No original road meshes are deformed.
            for (int i = 0; i < 4; i++)
            {
                string path = "Assets/Scenes/" + EntryNames[i] + ".unity";
                string backup = "Temp/SeamlessJourney/" + EntryNames[i] + ".unity";
                if (!File.Exists(backup)) File.Copy(path, backup);
                var scene = EditorSceneManager.OpenScene(path);
                var route = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageRoute>(true)).Single();
                var points = route.CopyPoints();
                route.Evaluate(0, out var start, out var heading);
                Quaternion rotation = nextRotation * Quaternion.Inverse(heading);
                Vector3 translation = nextStart - rotation * start;
                if (i == 0) { rotation = Quaternion.identity; translation = Vector3.zero; }
                var segment = new JourneyStreamCatalog.Segment
                {
                    entryScene = EntryNames[i], scenePath = WorldFolder + "/Route0" + (i + 1) + "_World.unity",
                    title = Titles[i], startDistance = accumulated, position = translation, rotation = rotation,
                    roadEndDistance = accumulated + route.Length
                };
                catalog.segments[i] = segment;
                foreach (var point in points)
                {
                    Vector3 p = translation + rotation * point;
                    if (combined.Count == 0 || Vector3.Distance(combined[combined.Count - 1], p) > .001f) combined.Add(p);
                }
                accumulated += route.Length;
                route.Evaluate(route.Length, out var end, out var endHeading);
                Vector3 globalEnd = translation + rotation * end;
                nextRotation = rotation * endHeading;
                if (i < 3)
                {
                    for (int n = 1; n <= 130; n++) combined.Add(globalEnd + nextRotation * Vector3.forward * (n * 5));
                    accumulated += LinkLength;
                }
                nextStart = globalEnd + nextRotation * Vector3.forward * LinkLength;
                segment.endDistance = accumulated;
                if (i == 0)
                {
                    var presentation = route.GetComponentInChildren<JourneyPresentation>(true);
                    if (presentation != null) catalog.sky = new SerializedObject(presentation).FindProperty("journeySky").objectReferenceValue as Material;
                    var sound = route.GetComponentInChildren<JourneySoundscape>(true);
                    if (sound != null)
                    {
                        var so = new SerializedObject(sound);
                        catalog.forest = so.FindProperty("forestClip").objectReferenceValue as AudioClip;
                        catalog.field = so.FindProperty("fieldClip").objectReferenceValue as AudioClip;
                        catalog.rain = so.FindProperty("rainClip").objectReferenceValue as AudioClip;
                        catalog.dust = so.FindProperty("dustMaterial").objectReferenceValue as Material;
                    }
                }
            }
            catalog.points = combined.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();

            for (int i = 0; i < 4; i++)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + EntryNames[i] + ".unity");
                var world = scene.GetRootGameObjects().Single(g => g.name == (i == 0 ? "Stage_World" : "Authored_Journey"));
                var route = world.GetComponentInChildren<StageRoute>(true);
                if (i == 0)
                {
                    var preview = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Road_Asset_Preview");
                    if (preview != null) preview.transform.SetParent(world.transform, true);
                }
                foreach (var finish in world.GetComponentsInChildren<StageFinishOutpost>(true))
                    if (i < 3) finish.enabled = false;
                if (i < 3) AddLink(world.transform, route, i);
                MarkState(world, i);
                var entry = scene.GetRootGameObjects().Select(g => g.GetComponent<JourneyStreamEntry>()).FirstOrDefault(x => x != null);
                if (entry == null) entry = new GameObject("Seamless_Journey_Entry").AddComponent<JourneyStreamEntry>();
                entry.Configure(catalog, i, world, route);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

                // Work on a scene copy. Entry actors and inactive legacy backups stay untouched in the original.
                EditorSceneManager.SaveScene(scene, catalog.segments[i].scenePath, true);
                scene = EditorSceneManager.OpenScene(catalog.segments[i].scenePath);
                world = scene.GetRootGameObjects().Single(g => g.name == (i == 0 ? "Stage_World" : "Authored_Journey"));
                foreach (var root in scene.GetRootGameObjects()) if (root != world) Object.DestroyImmediate(root);
                foreach (var local in world.GetComponentsInChildren<StageRoute>(true)) Object.DestroyImmediate(local);
                foreach (var presentation in world.GetComponentsInChildren<JourneyPresentation>(true)) Object.DestroyImmediate(presentation);
                foreach (var sound in world.GetComponentsInChildren<JourneySoundscape>(true)) Object.DestroyImmediate(sound);
                foreach (var light in world.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional) Object.DestroyImmediate(light.gameObject);
                foreach (var finish in world.GetComponentsInChildren<StageFinishOutpost>(true))
                    if (i < 3) Object.DestroyImmediate(finish);
                // The padded start/finish aprons would overlap the same-height connector surface.
                foreach (var t in world.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "Start_Apron" || i < 3 && t.name == "Finish_Apron")) t.gameObject.SetActive(false);
                var placement = catalog.segments[i];
                world.transform.SetPositionAndRotation(placement.position, placement.rotation);
                world.SetActive(false);
                new GameObject("Journey_World_Descriptor").AddComponent<JourneyStreamWorld>().Configure(i, world);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            var buildScenes = EditorBuildSettings.scenes.ToList();
            foreach (var segment in catalog.segments)
            {
                int existing = buildScenes.FindIndex(s => s.path == segment.scenePath);
                if (existing >= 0) buildScenes[existing] = new EditorBuildSettingsScene(segment.scenePath, true);
                else buildScenes.Add(new EditorBuildSettingsScene(segment.scenePath, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray(); AssetDatabase.SaveAssets();
            catalog.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            bakingCatalog = null;
            Debug.Log("[JourneyStream] Baked four worlds; shared route " + accumulated.ToString("F1") + " m.");
        }

        static bool ActiveUnder(Transform t, Transform world)
        {
            for (; t != null && t != world; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }

        static void MarkState(GameObject world, int index)
        {
            var candidates = new HashSet<Transform>();
            foreach (var script in world.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null || !ActiveUnder(script.transform, world.transform)) continue;
                if (!(script.GetType().Namespace ?? "").StartsWith("RogueDrive")) continue;
                if (script is StageRoute || script is TrackChunk || script is BakedMapChunk || script is JourneyPresentation ||
                    script is JourneySoundscape || script is StageFinishOutpost || script is JourneyPersistentObject) continue;
                candidates.Add(script.transform);
            }
            foreach (var body in world.GetComponentsInChildren<Rigidbody>(true))
                if (ActiveUnder(body.transform, world.transform)) candidates.Add(body.transform);
            foreach (var t in candidates)
            {
                bool nested = false;
                for (var p = t.parent; p != null && p != world.transform; p = p.parent)
                    if (candidates.Contains(p)) { nested = true; break; }
                if (nested || t == world.transform) continue;
                string id = "route" + index;
                for (var p = t; p != null && p != world.transform; p = p.parent) id += "/" + p.name + "#" + p.GetSiblingIndex();
                var marker = t.GetComponent<JourneyPersistentObject>() ?? t.gameObject.AddComponent<JourneyPersistentObject>();
                marker.Configure(id);
            }
        }

        static void AddLink(Transform world, StageRoute route, int index)
        {
            if (world.Find("Seamless_Exit_Link") != null) return;
            var root = new GameObject("Seamless_Exit_Link").transform; root.SetParent(world, false);
            route.Evaluate(route.Length, out var end, out var heading);
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/RemainingRoutes/Asphalt.mat");
            var paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/RemainingRoutes/Road_Paint.mat");
            var soil = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/RemainingRoutes/Soil_" + Mathf.Max(2,index+1) + ".mat");
            LinkStrip(root, index, "Seamless_Connector_Road", end, heading, 0, LinkLength, -7, 7, .04f, asphalt, true);
            LinkStrip(root, index, "Connector_Terrain", end, heading, 0, LinkLength, -300, 300, -.25f, soil, true);
            LinkStrip(root, index, "Left_Edge", end, heading, 0, LinkLength, -6.65f, -6.48f, .058f, paint, false);
            LinkStrip(root, index, "Right_Edge", end, heading, 0, LinkLength, 6.48f, 6.65f, .058f, paint, false);
            for (float d=0;d<LinkLength;d+=18) LinkStrip(root,index,"Dash_"+d,end,heading,d,Mathf.Min(d+7,LinkLength),-.09f,.09f,.06f,paint,false);
        }

        static void LinkStrip(Transform parent, int index, string name, Vector3 p, Quaternion q, float start, float end,
            float left, float right, float y, Material material, bool collision)
        {
            var mesh = new Mesh { name = name };
            FillStrip(mesh, p+q*new Vector3(left,y,start), p+q*new Vector3(left,y,end),
                p+q*new Vector3(right,y,end), p+q*new Vector3(right,y,start));
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Link"+index+"_"+name+".asset"));
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        public static void FillStrip(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a,b)/100f));
            int columns = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a,d)/100f));
            var vertices = new Vector3[(rows+1)*(columns+1)];
            var triangles = new int[rows*columns*6];
            int stride = columns+1, t = 0;
            for (int z=0;z<=rows;z++)
                for (int x=0;x<=columns;x++)
                    vertices[z*stride+x] = Vector3.Lerp(Vector3.Lerp(a,d,(float)x/columns),
                        Vector3.Lerp(b,c,(float)x/columns),(float)z/rows);
            for (int z=0;z<rows;z++)
                for (int x=0;x<columns;x++)
                {
                    int v=z*stride+x;
                    triangles[t++]=v; triangles[t++]=v+stride; triangles[t++]=v+stride+1;
                    triangles[t++]=v; triangles[t++]=v+stride+1; triangles[t++]=v+1;
                }
            mesh.Clear(); mesh.vertices=vertices; mesh.triangles=triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
    }
}
