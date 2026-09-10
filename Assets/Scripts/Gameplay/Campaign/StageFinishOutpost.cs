using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Финишная укрепленная локация (Форпост / База эвакуации) в конце каждого этапа (3000 м).
    /// При въезде машины через защитный периметр фиксирует победное прохождение этапа,
    /// включает сигнальные прожекторы, активирует фанфар и открывает экран победы.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StageFinishOutpost : MonoBehaviour
    {
        [Header("Stage Configuration")]
        [SerializeField, Range(1, 4)] private int stageIndex = 1;
        [SerializeField] private string outpostName = "Форпост эвакуации";

        [Header("Rewards")]
        [SerializeField] private int bonusCoins = 50;

        bool isPassed;
        GameObject outpostVisualRoot;
        Light[] spotlights;

        public int StageIndex => stageIndex;
        public string OutpostName => outpostName;

        public void Configure(int stage, string name)
        {
            stageIndex = stage;
            outpostName = name;
        }

        private void Awake()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(28f, 10f, 6f);
            col.center = new Vector3(0f, 5f, 0f);

            Transform existing = transform.Find("OutpostVisualRoot");
            if (existing != null)
            {
                outpostVisualRoot = existing.gameObject;
                spotlights = outpostVisualRoot.GetComponentsInChildren<Light>(true);
            }
            else
            {
                BuildOutpostStructure();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPassed) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car == null) return;

            PassOutpost(car);
        }

        void PassOutpost(ArcadeCarController car)
        {
            isPassed = true;

            // Звук триумфального финала этапа
            RogueDrive.Audio.AudioManager.Instance?.PlayFanfare();

            // Встряска камеры
            ArcadeCameraFollow.Instance?.TriggerShake(0.8f, 0.4f);

            // Включение всех прожекторов базы в ярко-зеленый победный цвет
            if (spotlights != null)
            {
                foreach (Light light in spotlights)
                {
                    if (light != null)
                    {
                        light.color = new Color(0.2f, 1f, 0.3f);
                        light.intensity *= 1.5f;
                    }
                }
            }

            // Уведомление в HUD
            PrototypeHud.Instance?.ShowBiomeNotification(
                $"ЭТАП {stageIndex} ПРОЙДЕН! ★",
                $"{outpostName.ToUpper()} — БЕЗОПАСНАЯ ЗОНА ДОСТИГНУТА! (+{bonusCoins} МОНЕТ)",
                new Color(0.2f, 1f, 0.4f));

            // Фиксация победы в контроллере заезда
            GameRunController run = car.Run != null ? car.Run : FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                run.ReportStageCompleted(stageIndex);
            }
        }

        public void BuildOutpostStructure()
        {
            outpostVisualRoot = new GameObject("OutpostVisualRoot");
            outpostVisualRoot.transform.SetParent(transform, false);

            Material steelMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            steelMat.color = new Color(0.25f, 0.28f, 0.32f);

            Material concreteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            concreteMat.color = new Color(0.45f, 0.45f, 0.43f);

            Material neonGreenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            neonGreenMat.color = new Color(0.2f, 1f, 0.4f);

            Material warningYellowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            warningYellowMat.color = new Color(0.95f, 0.75f, 0.1f);

            // 1. Массивные ворота базы (Шлюз периметра)
            CreateBox("Bunker_Pillar_Left", new Vector3(-13f, 5f, 0f), new Vector3(3.5f, 10f, 4f), concreteMat);
            CreateBox("Bunker_Pillar_Right", new Vector3(13f, 5f, 0f), new Vector3(3.5f, 10f, 4f), concreteMat);
            CreateBox("Gate_Crossbeam", new Vector3(0f, 9.5f, 0f), new Vector3(29f, 2f, 3.5f), steelMat);
            CreateBox("Gate_NeonBanner", new Vector3(0f, 9.5f, -1.85f), new Vector3(20f, 1.2f, 0.2f), neonGreenMat);

            // 2. Наблюдательные вышки
            CreateWatchtower(new Vector3(-16f, 0f, -5f), steelMat, warningYellowMat);
            CreateWatchtower(new Vector3(16f, 0f, -5f), steelMat, warningYellowMat);

            // 3. Бетонные блоки ограждения перед базой
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) <= 1) continue;
                CreateBox($"Concrete_Barrier_L_{i}", new Vector3(-11f, 0.75f, i * 6f), new Vector3(1.2f, 1.5f, 4.5f), concreteMat);
                CreateBox($"Concrete_Barrier_R_{i}", new Vector3(11f, 0.75f, i * 6f), new Vector3(1.2f, 1.5f, 4.5f), concreteMat);
            }

            // 4. Прожекторы безопасности базы
            var l1 = CreateSpotlight("Spotlight_Left", new Vector3(-13f, 10f, -2f), Quaternion.Euler(45f, 30f, 0f));
            var l2 = CreateSpotlight("Spotlight_Right", new Vector3(13f, 10f, -2f), Quaternion.Euler(45f, -30f, 0f));
            spotlights = new[] { l1, l2 };
        }

        GameObject CreateBox(string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(outpostVisualRoot.transform, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            var col = obj.GetComponent<Collider>();
            RemoveColliderForAuthoredVisual(col);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        void CreateWatchtower(Vector3 pos, Material steel, Material warning)
        {
            GameObject tower = new GameObject("Watchtower");
            tower.transform.SetParent(outpostVisualRoot.transform, false);
            tower.transform.localPosition = pos;

            CreateBoxInParent(tower, "Leg_FL", new Vector3(-1.5f, 6f, -1.5f), new Vector3(0.5f, 12f, 0.5f), steel);
            CreateBoxInParent(tower, "Leg_FR", new Vector3(1.5f, 6f, -1.5f), new Vector3(0.5f, 12f, 0.5f), steel);
            CreateBoxInParent(tower, "Leg_BL", new Vector3(-1.5f, 6f, 1.5f), new Vector3(0.5f, 12f, 0.5f), steel);
            CreateBoxInParent(tower, "Leg_BR", new Vector3(1.5f, 6f, 1.5f), new Vector3(0.5f, 12f, 0.5f), steel);
            CreateBoxInParent(tower, "Cabin_Floor", new Vector3(0f, 12f, 0f), new Vector3(4.5f, 0.6f, 4.5f), steel);
            CreateBoxInParent(tower, "Cabin_Roof", new Vector3(0f, 15f, 0f), new Vector3(4.8f, 0.4f, 4.8f), warning);
        }

        void CreateBoxInParent(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            var col = obj.GetComponent<Collider>();
            RemoveColliderForAuthoredVisual(col);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
        }

        Light CreateSpotlight(string name, Vector3 pos, Quaternion rot)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(outpostVisualRoot.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;

            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.9f, 0.7f);
            l.intensity = 3.5f;
            l.range = 35f;
            l.spotAngle = 60f;
            return l;
        }

        static void RemoveColliderForAuthoredVisual(Collider collider)
        {
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }
}
