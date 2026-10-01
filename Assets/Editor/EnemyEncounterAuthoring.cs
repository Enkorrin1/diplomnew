using System;
using System.IO;
using RogueDrive.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RogueDrive.Editor
{
    public static class EnemyEncounterAuthoring
    {
        const string Folder = "Assets/Content/Encounters";
        const string PrefabPath = Folder + "/RoadsideWalker.prefab";
        const string RootName = "Encounter_LastGasStation";

        [MenuItem("RogueDrive/Enemies/Build First Roadside Encounter")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage1_Outskirts" && scene.name != "Coop_Outskirts") throw new InvalidOperationException("Open Stage1_Outskirts or Coop_Outskirts first.");
            if (GameObject.Find(RootName) != null) throw new InvalidOperationException("Encounter exists; preserve authored changes. Use Rebuild Navigation for geometry changes.");
            var site = GameObject.Find("Detour_1_Last_Gas_Station");
            if (site == null) throw new InvalidOperationException("First roadside site is missing.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreateWalker();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create roadside encounter");
            root.transform.SetParent(site.transform, false);
            root.transform.position = new Vector3(-346f, 8f, 1330f);
            root.transform.rotation = Quaternion.identity;
            root.AddComponent<RoadsideEncounter>();
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.center = new Vector3(0f, 2f, 0f);
            surface.size = new Vector3(150f, 32f, 150f);
            surface.overrideVoxelSize = true;
            surface.voxelSize = .13f;
            Bake(surface);
            Vector3[] offsets = { new Vector3(-8f, 0f, 4f), new Vector3(8f, 0f, 12f), new Vector3(-4f, 0f, -11f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 desired = root.transform.position + offsets[i];
                if (!NavMesh.SamplePosition(desired, out var hit, 5f, NavMesh.AllAreas))
                    throw new InvalidOperationException("No walkable spawn at " + desired);
                var walker = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                walker.name = "RoadsideWalker_" + (i + 1);
                walker.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0f, 190f + i * 95f, 0f));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Enemies] First encounter saved: three persistent walkers and local navigation.");
        }

        static GameObject CreateWalker()
        {
            var root = new GameObject("RoadsideWalker");
            try
            {
                root.AddComponent<CapsuleCollider>();
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true; body.useGravity = false;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                agent.radius = .4f; agent.height = 1.85f;
                var zombie = root.AddComponent<EncounterZombie>();
                var so = new SerializedObject(zombie);
                so.FindProperty("maxHealth").floatValue = 45f;
                so.FindProperty("baseSpeed").floatValue = 2.1f;
                so.FindProperty("contactDamage").floatValue = 12f;
                so.FindProperty("xpReward").intValue = 0;
                so.FindProperty("coinReward").intValue = 0;
                so.FindProperty("visualModelOffset").vector3Value = new Vector3(0f, .12f, 0f);
                so.FindProperty("visualModelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Downloads/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Zombie.prefab");
                if (so.FindProperty("visualModelPrefab").objectReferenceValue == null)
                    throw new InvalidOperationException("Zombie model is missing.");
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("RogueDrive/Enemies/Rebuild First Encounter Navigation")]
        public static void RebuildNavigation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before baking.");
            var root = GameObject.Find(RootName);
            if (root == null) throw new InvalidOperationException("Build the encounter first.");
            Bake(root.GetComponent<NavMeshSurface>());
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        static void Bake(NavMeshSurface surface)
        {
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed.");
            string path = Folder + "/LastGasStationNavigation.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
            else
            {
                var generated = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(generated, existing);
                surface.navMeshData = existing;
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                surface.AddData();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
