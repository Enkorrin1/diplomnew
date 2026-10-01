using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Small, always-resident route data; geometry lives in independent world scenes.</summary>
    public sealed class JourneyStreamCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Segment
        {
            public string scenePath;
            public string entryScene;
            public string title;
            public float startDistance;
            public float roadEndDistance;
            public float endDistance;
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
        }

        public Segment[] segments = Array.Empty<Segment>();
        public Vector3[] points = Array.Empty<Vector3>();
        public Material sky;
        public AudioClip forest, field, rain;
        public Material dust;
        public int SegmentAt(float distance)
        {
            for (int i = segments.Length - 1; i > 0; i--)
                if (distance >= segments[i].startDistance) return i;
            return 0;
        }
    }
}
