using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object = UnityEngine.Object;

// Общие помощники редакторских команд оформления: расстановка префабов с посадкой на поверхность,
// тонировка копиями материалов, коллайдеры по габаритам, надписи, блоки и эффекты.
public static class SceneDressingKit
{
    public const string TintFolder = "Assets/Art/Tinted";
    static readonly Dictionary<string, Material> tintCache = new Dictionary<string, Material>();

    public static GameObject Prop(Transform parent, string path, Vector3 local, float yaw, float scale, Color tint, string tintKey, bool collider, bool lay = false)
    {
        var go = Instantiate(parent, path);
        if(go == null) return null;
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        if(lay) LayFlat(go);
        SitOn(go, parent.TransformPoint(local).y);
        if(tintKey != null) Tint(go, tint, tintKey);
        if(collider && go.GetComponentInChildren<Collider>() == null) BoundsCollider(go);
        return go;
    }

    public static GameObject Instantiate(Transform parent, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab == null) { Debug.LogWarning("[Dressing] Missing prefab " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static GameObject Fx(Transform parent, string path, Vector3 local, float scale)
    {
        var go = Instantiate(parent, path);
        if(go == null) return null;
        go.transform.localPosition = local; go.transform.localScale = Vector3.one * scale;
        foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main; main.loop = true; main.playOnAwake = true;
        }
        return go;
    }

    // Колесо и подобные предметы кладутся плашмя: самая тонкая ось становится вертикальной.
    public static void LayFlat(GameObject go)
    {
        var size = Bounds(go).size;
        if(size.x < size.y && size.x < size.z) go.transform.Rotate(0, 0, 90, Space.Self);
        else if(size.z < size.y && size.z < size.x) go.transform.Rotate(90, 0, 0, Space.Self);
    }

    public static void SitOn(GameObject go, float worldY)
    {
        var b = Bounds(go);
        go.transform.position += Vector3.up * (worldY - b.min.y);
    }

    public static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if(renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        foreach(var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // Коллайдер по габаритам модели в её собственных осях (без вращения, чтобы ящик не раздувался).
    public static void BoundsCollider(GameObject go)
    {
        var rotation = go.transform.rotation;
        go.transform.rotation = Quaternion.identity;
        var b = Bounds(go);
        go.transform.rotation = rotation;
        var box = go.AddComponent<BoxCollider>();
        var s = go.transform.lossyScale;
        var offset = b.center - go.transform.position;
        box.center = new Vector3(offset.x / s.x, offset.y / s.y, offset.z / s.z);
        box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
    }

    public static void Tint(GameObject go, Color tint, string key)
    {
        if(!AssetDatabase.IsValidFolder(TintFolder)) AssetDatabase.CreateFolder("Assets/Art", "Tinted");
        foreach(var r in go.GetComponentsInChildren<Renderer>())
        {
            if(r is ParticleSystemRenderer) continue;
            var mats = r.sharedMaterials;
            for(int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if(src == null || !src.HasProperty("_Color")) continue;
                string id = src.name + "_" + key;
                if(!tintCache.TryGetValue(id, out var m) || m == null)
                {
                    string path = TintFolder + "/" + string.Concat(id.Split(System.IO.Path.GetInvalidFileNameChars())) + ".mat";
                    m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
                    var c = src.color * tint;
                    m.color = new Color(c.r, c.g, c.b, src.color.a);
                    EditorUtility.SetDirty(m);
                    tintCache[id] = m;
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    // Надпись TextMesh с проверкой глубины; читается, когда камера смотрит вдоль forward.
    public static TextMesh Label(Transform parent, string name, Vector3 local, string text, float size, Color color, Vector3 forward)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        var tm = go.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tm.text = text; tm.fontSize = 64; tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/WorldText/WorldText_LegacyRuntime.mat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return tm;
    }

    public static GameObject Block(Transform parent, string name, Vector3 local, Vector3 size, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    public static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    public static Light AddLight(Transform parent, string name, LightType type, Vector3 local, Color color, float intensity, float range)
    {
        var light = Child(parent, name).gameObject.AddComponent<Light>();
        light.transform.localPosition = local;
        light.type = type; light.color = color; light.intensity = intensity; light.range = range;
        return light;
    }

    // ---------- Материалы ----------

    public static Material MaterialAsset(string folder, string name, string shader)
    {
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        else if(m.shader.name != shader) m.shader = Shader.Find(shader);
        return m;
    }

    public static Material Std(string folder, string name, Color color, float gloss, float metallic, Texture2D tex = null, Vector2 tiling = default)
    {
        var m = MaterialAsset(folder, name, "Standard");
        m.color = color; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metallic);
        m.mainTexture = tex; if(tex != null) m.mainTextureScale = tiling;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material Emissive(string folder, string name, Color hdr)
    {
        var m = MaterialAsset(folder, name, "Standard");
        m.color = Color.black; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", hdr);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material Tri(string folder, string name, Texture2D tex, Color color, float scale, float gloss, Texture2D dirt, float dirtStrength)
    {
        var m = MaterialAsset(folder, name, "RogueDrive/Triplanar");
        m.mainTexture = tex; m.color = color; m.SetFloat("_Scale", scale); m.SetFloat("_Glossiness", gloss);
        m.SetTexture("_Dirt", dirt); m.SetFloat("_DirtScale", .035f); m.SetFloat("_DirtStrength", dirtStrength);
        EditorUtility.SetDirty(m);
        return m;
    }
}
