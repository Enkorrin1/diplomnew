using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Сигнальный огонь мачты: вспыхивает светом и свечением материала с заданным периодом.</summary>
    public sealed class BlinkingBeacon : MonoBehaviour
    {
        [SerializeField] Renderer lamp;
        [SerializeField] Light glow;
        [SerializeField, ColorUsage(false, true)] Color emission = new Color(4f, .3f, .2f);
        [SerializeField, Min(.1f)] float period = 1.6f;
        [SerializeField, Range(.05f, .9f)] float onFraction = .25f;
        [SerializeField] float phase;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        float glowIntensity;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (glow != null) glowIntensity = glow.intensity;
        }

        void Update()
        {
            bool on = Mathf.Repeat(Time.time / period + phase, 1f) < onFraction;
            if (lamp != null) { block.SetColor(EmissionId, on ? emission : Color.black); lamp.SetPropertyBlock(block); }
            if (glow != null) glow.intensity = on ? glowIntensity : 0;
        }
    }
}
