using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Authored roadside beats for the first kilometre. Geometry follows each road segment.</summary>
    public sealed class FirstMapDressing : MonoBehaviour
    {
        FirstMapAssets assets;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        static readonly Color Soil = new Color(0.24f, 0.255f, 0.21f);
        static readonly Color Concrete = new Color(0.43f, 0.45f, 0.42f);
        static readonly Color Paint = new Color(0.83f, 0.68f, 0.36f);
        static readonly Color Steel = new Color(0.16f, 0.23f, 0.24f);

        void Awake() => EnsureAssets();

        void EnsureAssets()
        {
            if (assets == null)
                assets = Resources.Load<FirstMapAssets>("FirstMapAssets");
        }

        public void DressStart(Transform parentOverride = null)
        {
            EnsureAssets();
            if (assets == null) return;
            GameObject road = GameObject.Find("Initial_Road");
            if (road != null && road.TryGetComponent(out Renderer renderer))
                DressSection(renderer, -1, true, true, parentOverride);
        }

        public void Dress(TrackChunk chunk, float distance, bool safe, Transform parentOverride = null)
        {
            EnsureAssets();
            if (assets == null) return;
            int beat = Mathf.FloorToInt(distance / 100f);
            foreach (Transform child in chunk.transform)
                if (child.name.StartsWith("Dash_")) child.gameObject.SetActive(false);
            Renderer[] roads = chunk.RoadRenderers;
            if (roads == null) return;
            for (int i = 0; i < roads.Length; i++)
                if (roads[i] != null) DressSection(roads[i], beat, safe, i == roads.Length / 2, parentOverride);
        }

        void DressSection(Renderer road, int beat, bool safe, bool landmark = true, Transform parentOverride = null)
        {
            // A unit-scale frame avoids inheriting the road cube's non-uniform scale.
            var root = new GameObject("Outskirts_Dressing");
            root.transform.SetParent(parentOverride != null ? parentOverride : road.transform.parent, true);
            root.transform.SetPositionAndRotation(new Vector3(road.transform.position.x, 0f, road.transform.position.z), road.transform.rotation);
            Transform frame = root.transform;
            float length = Mathf.Abs(road.transform.lossyScale.z);
            float halfWidth = Mathf.Abs(road.transform.lossyScale.x) * 0.5f;
            Box(frame, "Scrubland", new Vector3(0f, -0.6f, 0f), new Vector3(220f, 0.5f, length + 2f), Soil);
            for (float z = -length * 0.5f + 6f; z < length * 0.5f; z += 18f)
            {
                Box(frame, "Worn centre dash", new Vector3(0f, 0.065f, z), new Vector3(0.2f, 0.012f, 5f), Paint * 0.8f);
                Box(frame, "Asphalt repair", new Vector3(beat % 2 == 0 ? 3.5f : -3.5f, 0.008f, z + 4f), new Vector3(2.4f, 0.01f, 3.8f), new Color(0.125f, 0.135f, 0.14f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box(frame, "Gravel shoulder", new Vector3(side * (halfWidth + 3f), -0.12f, 0f), new Vector3(5.5f, 0.18f, length), Concrete * 0.72f);
                Box(frame, "Faded edge paint", new Vector3(side * (halfWidth - 0.6f), 0.018f, 0f), new Vector3(0.14f, 0.015f, length), Paint);
                for (float z = -length * 0.5f + 10f; z < length * 0.5f; z += 32f)
                {
                    Place(frame, assets.lamp, new Vector3(side * (halfWidth + 2f), 0f, z), side > 0 ? 180f : 0f, new Vector3(4f, 7f, 4f));
                    if (assets.rocks != null && assets.rocks.Length > 0)
                        Place(frame, assets.rocks[(Mathf.Abs(beat) + (side > 0 ? 1 : 0)) % assets.rocks.Length],
                            new Vector3(side * (halfWidth + 11f), 0f, z + 7f), beat * 27f, new Vector3(3f, 2f, 3f));
                    Box(frame, "Collapsed masonry", new Vector3(side * (halfWidth + 7f), 0.35f, z + 11f), new Vector3(2.8f, 0.7f, 1.5f), Concrete * 0.8f);
                    Place(frame, assets.pallet, new Vector3(side * (halfWidth + 7f), 0f, z + 17f), 32f, new Vector3(2f, 1f, 2f));
                }
                // Low-cost skyline silhouettes frame the road, with gaps and broken rooflines.
                if (landmark)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        float h = 9f + ((Mathf.Abs(beat) * 7 + b * 3) % 17);
                        Vector3 p = new Vector3(side * (53f + b * 15f), h * 0.5f - 0.3f, (b - 1) * 17f);
                        Box(frame, "Distant abandoned block", p, new Vector3(10f, h, 14f), new Color(0.27f + b * 0.025f, 0.31f, 0.30f));
                        for (int floor = 0; floor < (int)h / 4; floor++)
                            Box(frame, "Dark window strip", p + new Vector3(-side * 5.02f, -h * 0.5f + floor * 4 + 2f, 0f),
                                new Vector3(0.035f, 1.4f, 10f), Steel);
                    }
                }
            }

            if (!landmark) return;
            int area = Mathf.Clamp(beat / 2, 0, 4);
            string[] titles = { "01 / OUTSKIRTS", "02 / LAST FUEL", "03 / EVACUATION QUEUE", "04 / FREIGHT YARD", "05 / QUARANTINE" };
            if (beat < 0 || beat % 2 == 0) Gantry(frame, titles[area], -Mathf.Min(32f, length * 0.3f), halfWidth);

            if (area == 0)
            {
                Place(frame, assets.garage, new Vector3(-halfWidth - 15f, 0f, 5f), 90f, new Vector3(23f, 9f, 23f));
                Place(frame, assets.station, new Vector3(halfWidth + 19f, 0f, 15f), -90f, new Vector3(25f, 13f, 25f));
                Place(frame, assets.sedan, new Vector3(-halfWidth - 6f, 0f, -15f), 12f, new Vector3(2.2f, 2f, 4.8f));
            }
            else if (area == 1)
            {
                Box(frame, "Service apron", new Vector3(-halfWidth - 17f, -0.06f, 0f), new Vector3(30f, 0.12f, 65f), Concrete);
                Place(frame, assets.gasStation, new Vector3(-halfWidth - 19f, 0f, 0f), 90f, new Vector3(30f, 12f, 30f));
                Place(frame, assets.van, new Vector3(-halfWidth - 7f, 0f, -15f), 165f, new Vector3(2.4f, 2.8f, 5.5f));
                Place(frame, assets.sign, new Vector3(halfWidth + 2f, 0f, -6f), 0f, new Vector3(3f, 5f, 3f));
            }
            else if (area == 2)
            {
                for (int c = 0; c < 4; c++)
                    Place(frame, c % 2 == 0 ? assets.sedan : assets.van, new Vector3(halfWidth + 5f, 0f, -20f + c * 12f),
                        175f + c * 5f, new Vector3(2.6f, 2.8f, 5f));
                Place(frame, assets.police, new Vector3(-halfWidth - 5f, 0f, 3f), 25f, new Vector3(2.5f, 2.5f, 5f));
                Place(frame, assets.tent, new Vector3(-halfWidth - 15f, 0f, 15f), 90f, new Vector3(7f, 5f, 9f));
            }
            else if (area == 3)
            {
                for (int c = 0; c < 3; c++)
                {
                    Place(frame, assets.container, new Vector3(-halfWidth - 12f, 0f, -16f + c * 14f), 90f, new Vector3(12f, 3f, 12f));
                    Place(frame, assets.container, new Vector3(halfWidth + 12f, c == 1 ? 2.7f : 0f, -16f + c * 14f), 90f, new Vector3(12f, 3f, 12f));
                }
            }
            else
            {
                Place(frame, assets.military, new Vector3(-halfWidth - 6f, 0f, 0f), 20f, new Vector3(3.2f, 4f, 6f));
                Place(frame, assets.tent, new Vector3(halfWidth + 12f, 0f, 0f), -90f, new Vector3(8f, 5f, 10f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(frame, "Checkpoint tower", new Vector3(side * (halfWidth + 6), 3.5f, 20f), new Vector3(4f, 7f, 4f), Concrete);
                    Box(frame, "Observation slit", new Vector3(side * (halfWidth + 6), 5.4f, 17.98f), new Vector3(3.4f, 1f, 0.05f), Steel);
                }
            }
            if (!safe) Hazards(frame, beat);
        }

        void Hazards(Transform frame, int beat)
        {
            float side = beat % 2 == 0 ? -1f : 1f;
            GameObject solid = beat < 4 ? assets.barrier : beat < 6 ? assets.sedan : beat < 8 ? assets.container : assets.barrier;
            // Obstacles stay on the shoulders of the driving corridor; x=-3..3 remains passable.
            Place(frame, solid, new Vector3(side * 7.5f, 0f, -3f), beat < 6 ? 12f : 90f,
                new Vector3(4f, 3f, 6f), 1);
            Place(frame, assets.pallet, new Vector3(-side * 6f, 0f, 7f), 20f, new Vector3(2.5f, 1.2f, 2.5f), 2);
            for (int i = 0; i < 3; i++)
                Place(frame, assets.cone, new Vector3(side * (5.5f + i * 0.6f), 0f, -12f + i * 2f), 0f, new Vector3(0.7f, 1f, 0.7f), 2);
            if (beat % 3 == 0)
            {
                GameObject barrel = Place(frame, assets.barrel, new Vector3(-side * 8f, 0f, -6f), 0f, new Vector3(1.2f, 1.5f, 1.2f), 3);
                if (barrel != null)
                {
                    foreach (Renderer r in barrel.GetComponentsInChildren<Renderer>())
                    {
                        r.SetPropertyBlock(null);
                        r.sharedMaterial = MaterialFor(new Color(0.68f, 0.12f, 0.075f));
                    }
                    Box(barrel.transform, "Warning band", new Vector3(0f, 0.8f, 0f), new Vector3(1.05f, 0.16f, 1.05f), Paint);
                }
            }
        }

        void Gantry(Transform frame, string title, float z, float halfWidth)
        {
            for (int s = -1; s <= 1; s += 2)
                Box(frame, "Gantry pillar", new Vector3(s * (halfWidth + 1f), 4.5f, z), new Vector3(0.45f, 9f, 0.5f), Steel);
            Box(frame, "Gantry crossbeam", new Vector3(0f, 8.6f, z), new Vector3(halfWidth * 2f + 3f, 0.45f, 0.5f), Steel);
            Box(frame, "Route sign", new Vector3(0f, 7.5f, z - 0.15f), new Vector3(14f, 2f, 0.18f), Steel);
            var label = new GameObject("Route lettering");
            label.transform.SetParent(frame, false);
            label.transform.localPosition = new Vector3(0f, 7.5f, z - 0.26f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = title + "  >>>";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.16f;
            text.color = new Color(0.94f, 0.90f, 0.72f);
        }

        GameObject Place(Transform parent, GameObject prefab, Vector3 position, float yaw, Vector3 maxSize, int hazard = 0)
        {
            if (prefab == null) return null;
            var holder = new GameObject(prefab.name + (hazard == 1 ? "_Solid" : hazard == 2 ? "_Breakable" : hazard == 3 ? "_Explosive" : "_Scenery"));
            holder.transform.SetParent(parent, false);
            var visual = Instantiate(prefab, holder.transform);
            visual.SetActive(true);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(holder);
                else Destroy(holder);
#else
                Destroy(holder);
#endif
                return null;
            }
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", new Color(0.73f, 0.70f, 0.59f));
            foreach (Renderer r in renderers) r.SetPropertyBlock(tint);
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer r in renderers)
            {
                Bounds local = r.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = holder.transform.InverseTransformPoint(r.transform.TransformPoint(p));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            float scale = Mathf.Min(maxSize.x / Mathf.Max(bounds.size.x, 0.01f), maxSize.y / Mathf.Max(bounds.size.y, 0.01f), maxSize.z / Mathf.Max(bounds.size.z, 0.01f));
            visual.transform.localScale *= scale;
            Vector3 center = bounds.center;
            visual.transform.localPosition = -center * scale + Vector3.up * bounds.size.y * scale * 0.5f;
            if (hazard != 0)
            {
                var box = holder.AddComponent<BoxCollider>();
                box.size = bounds.size * scale;
                box.center = Vector3.up * box.size.y * 0.5f;
                if (hazard == 3) holder.AddComponent<ExplosiveBarrel>();
                else
                {
                    holder.AddComponent<TrackObstacle>().Configure(hazard == 2);
                    box.isTrigger = hazard == 2;
                }
            }
            holder.transform.localPosition = position;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return holder;
        }

        void Box(Transform parent, string label, Vector3 pos, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Collider>().enabled = false;
            Collider collider = go.GetComponent<Collider>();
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(collider);
            else Destroy(collider);
#else
            Destroy(collider);
#endif
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
        }

        Material MaterialFor(Color color)
        {
            if (materials.TryGetValue(color, out Material material)) return material;
            material = new Material(Shader.Find("Standard")) { color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", 0.12f);
            materials.Add(color, material);
            return material;
        }

        void OnDestroy()
        {
            if (!Application.isPlaying) return;
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
        }
    }
}
