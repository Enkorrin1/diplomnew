using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Опускает на грунт камни, которые висят над рельефом. У мешей ландшафта часто нет коллайдеров,
// поэтому на время замера им вешаются временные MeshCollider, а коллайдеры самих камней выключаются.
public static class GroundFloatingRocks
{
    const float Tolerance = .3f, Sink = .15f;

    [MenuItem("RogueDrive/Journey/Ground Floating Rocks")]
    public static void Run()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var report = new System.Text.StringBuilder();
        foreach(var entry in EditorBuildSettings.scenes)
        {
            if(!entry.enabled || !(entry.path.Contains("Route0") || entry.path.Contains("Stage"))) continue;
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            int moved = GroundScene(scene);
            report.AppendLine($"{scene.name}: {moved}");
            if(moved > 0){ EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
        Debug.Log("[Rocks] Grounded per scene:\n" + report);
    }

    static bool IsRock(Mesh mesh)
    {
        var name = mesh.name.ToLowerInvariant();
        return name.StartsWith("rock") || name.Contains("boulder") || name.Contains("stone");
    }

    static int GroundScene(UnityEngine.SceneManagement.Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var inactive = new List<GameObject>();
        foreach(var root in roots) if(!root.activeSelf){ root.SetActive(true); inactive.Add(root); }

        var temps = new List<MeshCollider>();
        var rocks = new List<MeshRenderer>();
        var rockColliders = new List<Collider>();
        foreach(var root in roots)
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if(filter == null || filter.sharedMesh == null) continue;
            if(IsRock(filter.sharedMesh))
            {
                rocks.Add(renderer);
                foreach(var c in renderer.GetComponentsInChildren<Collider>()) if(c.enabled){ c.enabled = false; rockColliders.Add(c); }
                continue;
            }
            if(renderer.GetComponent<Collider>() != null) continue;
            var temp = renderer.gameObject.AddComponent<MeshCollider>();
            temp.sharedMesh = filter.sharedMesh;
            temps.Add(temp);
        }
        Physics.SyncTransforms();

        int moved = 0;
        foreach(var rock in rocks)
        {
            float gap = Gap(rock.bounds);
            if(gap <= Tolerance || float.IsInfinity(gap)) continue;
            Undo.RecordObject(rock.transform, "Ground rock");
            rock.transform.position += Vector3.down * (gap + Sink);
            moved++;
        }

        foreach(var c in rockColliders) c.enabled = true;
        foreach(var t in temps) Object.DestroyImmediate(t);
        foreach(var root in inactive) root.SetActive(false);
        return moved;
    }

    // Наименьший зазор между низом камня и землёй в центре и четырёх точках на 30% габарита.
    static float Gap(Bounds b)
    {
        float gap = float.PositiveInfinity;
        var points = new[]{ b.center,
            b.center + new Vector3(b.extents.x * .3f, 0, 0), b.center - new Vector3(b.extents.x * .3f, 0, 0),
            b.center + new Vector3(0, 0, b.extents.z * .3f), b.center - new Vector3(0, 0, b.extents.z * .3f) };
        foreach(var p in points)
            if(Physics.Raycast(new Vector3(p.x, b.max.y + 5f, p.z), Vector3.down, out var hit, 600f))
                gap = Mathf.Min(gap, b.min.y - hit.point.y);
        return gap;
    }
}
