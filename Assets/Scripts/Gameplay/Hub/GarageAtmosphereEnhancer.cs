using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Создает атмосферные визуальные эффекты убежища:
    /// парящие пылинки в конусах света (Dust Particles), виньетку и контрастный дизельпанк-свет.
    /// </summary>
    public sealed class GarageAtmosphereEnhancer : MonoBehaviour
    {
        private ParticleSystem dustParticles;

        private void Start()
        {
            CreateDustParticles();
            SetupLightingAndFog();
        }

        private void SetupLightingAndFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.10f);
            RenderSettings.fogDensity = 0.015f;
        }

        private void CreateDustParticles()
        {
            if (dustParticles != null) return;

            GameObject dustObj = new GameObject("Garage_AtmosphericDust");
            dustObj.transform.SetParent(transform, false);
            dustObj.transform.position = new Vector3(0f, 2.5f, 2.0f);

            dustParticles = dustObj.AddComponent<ParticleSystem>();
            var main = dustParticles.main;
            main.loop = true;
            main.startLifetime = 10f;
            main.startSpeed = 0.15f;
            main.startSize = 0.04f;
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.35f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = dustParticles.emission;
            emission.rateOverTime = 12f;

            var shape = dustParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 6f, 16f);

            var velocityOverLifetime = dustParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var renderer = dustObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var dustMat = new Material(Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
            renderer.sharedMaterial = dustMat;
        }
    }
}
