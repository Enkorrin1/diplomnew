using UnityEngine;
using UnityEditor;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.Editor
{
    public static class CrowbarAuthoring
    {
        public static void Build(bool force = false)
        {
            const string folder = "Assets/Resources/Combat";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Crowbar.prefab");
            if (!force && existing != null && existing.GetComponent<GarageCheckpointItem>() != null) return;
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var steel = new Material(Shader.Find("Standard")) { color = new Color(.23f,.27f,.29f) };
            steel.SetFloat("_Metallic", .8f); steel.SetFloat("_Glossiness", .45f);
            AssetDatabase.CreateAsset(steel, folder + "/CrowbarSteel.mat");
            var grip = new Material(Shader.Find("Standard")) { color = new Color(.45f,.055f,.025f) };
            AssetDatabase.CreateAsset(grip, folder + "/CrowbarGrip.mat");
            var root = new GameObject("Crowbar");
            var points = new[] { new Vector3(0,-.28f,0), new Vector3(0,.25f,0), new Vector3(0,.30f,.025f),
                new Vector3(0,.32f,.07f), new Vector3(0,.30f,.12f), new Vector3(0,.27f,.14f) };
            for (int i=0; i<points.Length-1; i++)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                part.name = "Forged steel"; part.transform.SetParent(root.transform);
                var delta = points[i+1]-points[i];
                part.transform.localPosition = (points[i]+points[i+1])*.5f;
                part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
                part.transform.localScale = new Vector3(.028f,delta.magnitude*.5f,.028f);
                part.GetComponent<Renderer>().sharedMaterial = steel;
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Red grip"; handle.transform.SetParent(root.transform);
            handle.transform.localPosition = new Vector3(0,-.15f,0);
            handle.transform.localScale = new Vector3(.036f,.085f,.036f);
            handle.GetComponent<Renderer>().sharedMaterial = grip;
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0,.02f,.035f); box.size = new Vector3(.04f,.63f,.15f);
            root.AddComponent<Rigidbody>().mass = 1.4f;
            var prop = root.AddComponent<BunkerPhysicsProp>();
            prop.Configure("Монтировка"); prop.SetPocketSized(true);
            root.AddComponent<MeleeWeapon>();
            var checkpoint = root.AddComponent<GarageCheckpointItem>();
            checkpoint.id = "combat_crowbar";
            PrefabUtility.SaveAsPrefabAsset(root, folder + "/Crowbar.prefab");
            Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        }
    }
}
