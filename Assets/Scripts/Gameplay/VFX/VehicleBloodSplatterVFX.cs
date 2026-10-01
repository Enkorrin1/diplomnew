using System.Collections.Generic;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Динамическая система брызг и потеков крови зомби на кузове (Carmageddon / Earn to Die):
    /// При таране зомби на высокой скорости кровь разлетается брызгами и оседает
    /// на бампере, кенгурятнике, капоте и лобовом стекле.
    /// Кровь персистентно остаётся на машине на протяжении всего заезда!
    /// </summary>
    public sealed class VehicleBloodSplatterVFX : MonoBehaviour
    {
        public static VehicleBloodSplatterVFX Instance { get; private set; }

        [Header("Splatter Settings")]
        [SerializeField, Range(5, 50)] private int maxPersistentSplatters = 30;
        [SerializeField] private Color bloodColor = new Color(0.48f, 0.04f, 0.04f, 0.92f);
        [SerializeField] private Color driedBloodColor = new Color(0.28f, 0.02f, 0.02f, 0.85f);

        [Header("Bloodiness State")]
        [SerializeField, Range(0f, 1f)] private float bloodCoverage = 0f;

        private Transform splatterRoot;
        private List<GameObject> activeSplatters = new List<GameObject>();
        private ParticleSystem bloodBurstVFX;
        private Material bloodQuadMaterial;

        public float BloodCoverage => bloodCoverage;
        public int SplatterCount => activeSplatters.Count;

        private void Awake()
        {
            Instance = this;
            EnsureSplatterRoot();
            CreateBloodMaterial();
            CreateProceduralBloodEmitter();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Вызывается при скоростном таране зомби (из EnemyBase или при ударе бампером).
        /// </summary>
        public void RegisterZombieRam(Vector3 contactPoint, Vector3 normal, float carSpeedKmh)
        {
            float intensity = Mathf.Clamp01(carSpeedKmh / 60f);

            // 1. Частицы фонтана крови в точке удара
            if (bloodBurstVFX != null)
            {
                bloodBurstVFX.transform.position = contactPoint;
                if (normal != Vector3.zero) bloodBurstVFX.transform.rotation = Quaternion.LookRotation(normal);
                bloodBurstVFX.Emit(Mathf.RoundToInt(18 + intensity * 24));
            }

            // 2. Звук смачного мясного удара
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact();
            }

            // 3. Добавляем 2-4 персистентных пятна крови на бампер, решетку и капот
            int splatCount = Random.Range(2, 5);
            for (int i = 0; i < splatCount; i++)
            {
                SpawnPersistentBloodQuad(intensity);
            }

            // 4. Повышаем общий уровень загрязнения кровью
            bloodCoverage = Mathf.Clamp01(bloodCoverage + 0.06f);
        }

        private void SpawnPersistentBloodQuad(float intensity)
        {
            EnsureSplatterRoot();

            // Если превышен лимит — удаляем самое старое пятно
            if (activeSplatters.Count >= maxPersistentSplatters)
            {
                var oldest = activeSplatters[0];
                activeSplatters.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }

            // Создаем плоский квад с пятном крови
            GameObject splat = GameObject.CreatePrimitive(PrimitiveType.Quad);
            splat.name = "BloodSplatter_Stain";
            splat.transform.SetParent(splatterRoot, false);

            // Удаляем MeshCollider, чтобы пятно не блокировало лучи и физику
            Collider col = splat.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Случайное позиционирование в зоне передка: бампер, решетка, капот
            // Локальные координаты относительно машины:
            // X: -0.85 .. +0.85 (ширина капота)
            // Y: 0.35 .. 0.85 (высота бампера и наклона капота)
            // Z: 1.4 .. 2.25 (вылет вперед)
            float localX = Random.Range(-0.85f, 0.85f);
            float localZ = Random.Range(1.35f, 2.25f);

            // Высота плавно поднимается от бампера (Z=2.2, Y=0.35) к лобовому стеклу (Z=1.35, Y=0.88)
            float t = Mathf.InverseLerp(2.25f, 1.35f, localZ);
            float localY = Mathf.Lerp(0.38f, 0.88f, t) + Random.Range(-0.04f, 0.06f);

            splat.transform.localPosition = new Vector3(localX, localY, localZ);

            // Ориентация квада по наклону капота + случайный разворот
            float hoodAngle = Mathf.Lerp(5f, 28f, t);
            splat.transform.localRotation = Quaternion.Euler(hoodAngle, 0f, Random.Range(0f, 360f));

            // Случайный размер и форма потека
            float scale = Random.Range(0.25f, 0.65f) * Mathf.Lerp(0.8f, 1.3f, intensity);
            float stretch = Random.Range(0.85f, 1.45f);
            splat.transform.localScale = new Vector3(scale, scale * stretch, 1f);

            // Рендерер и цвет
            MeshRenderer mr = splat.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = bloodQuadMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // Назначаем материалу случайный оттенок свежей или загустевшей крови
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                Color c = Color.Lerp(bloodColor, driedBloodColor, Random.Range(0f, 0.7f));
                block.SetColor("_Color", c);
                mr.SetPropertyBlock(block);
            }

            activeSplatters.Add(splat);
        }

        private void EnsureSplatterRoot()
        {
            if (splatterRoot != null) return;

            Transform existing = transform.Find("PersistentBloodSplatters");
            if (existing != null)
            {
                splatterRoot = existing;
            }
            else
            {
                GameObject rootGo = new GameObject("PersistentBloodSplatters");
                rootGo.transform.SetParent(transform, false);
                splatterRoot = rootGo.transform;
            }
        }

        private void CreateBloodMaterial()
        {
            if (bloodQuadMaterial != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Unlit (Supports Lightmap)");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            bloodQuadMaterial = new Material(shader)
            {
                name = "Mat_ZombieBloodStain",
                color = bloodColor
            };
        }

        private void CreateProceduralBloodEmitter()
        {
            if (bloodBurstVFX != null) return;

            GameObject burstGo = new GameObject("BloodBurstParticleSystem");
            burstGo.transform.SetParent(transform, false);

            bloodBurstVFX = burstGo.AddComponent<ParticleSystem>();
            bloodBurstVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = bloodBurstVFX.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = new Color(0.6f, 0.03f, 0.03f, 0.95f);
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = bloodBurstVFX.emission;
            emission.enabled = false;

            var shape = bloodBurstVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.3f;

            var psr = burstGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = bloodQuadMaterial;
            }
        }

        /// <summary>
        /// Очистить кузов от крови (например, на мойке аванпоста или при поливе водой).
        /// </summary>
        public void CleanCar()
        {
            for (int i = 0; i < activeSplatters.Count; i++)
            {
                if (activeSplatters[i] != null)
                {
                    Destroy(activeSplatters[i]);
                }
            }
            activeSplatters.Clear();
            bloodCoverage = 0f;
        }
    }
}
