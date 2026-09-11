using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Gameplay.Narrative;
using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Campaign
{
    /// <summary>
    /// Финальная кинематографичная катсцена «Спасение в Цитадели»:
    /// 1. После победы над Джаггернаутом гигантские бронированные ворота Цитадели распахиваются.
    /// 2. Сирены стихают, аварийный красный свет сменяется зелеными посадочными огнями.
    /// 3. Автомобиль игрока въезжает внутрь крепости.
    /// 4. За спиной машины с оглушительным грохотом захлопывается 50-тонный бронешлюз.
    /// 5. Диспетчер «Маяк» объявляет о завершении протокола «Закат» и спасении.
    /// 6. Фиксация победы во всей кампании и переход к финальным титрам / результатам.
    /// </summary>
    public sealed class CitadelEndingSequence : MonoBehaviour
    {
        [Header("Citadel Gate Hierarchy")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private Light[] runwayLights;
        [SerializeField] private ParticleSystem steamPuff;

        [Header("Timing")]
        [SerializeField] private float doorOpenDuration = 3.5f;
        [SerializeField] private float carDriveInDuration = 3.0f;
        [SerializeField] private float doorCloseDuration = 1.2f;

        private bool isEndingTriggered = false;
        private ArcadeCarController playerCar;

        private void Start()
        {
            // Подписываемся на поражение босса Джаггернаута
            BossJuggernaut.BossDefeated += OnBossDefeated;

            if (leftDoor == null || rightDoor == null)
            {
                BuildCitadelGatePlaceholders();
            }
        }

        private void OnDestroy()
        {
            BossJuggernaut.BossDefeated -= OnBossDefeated;
        }

        private void OnBossDefeated(BossJuggernaut boss)
        {
            if (isEndingTriggered) return;
            isEndingTriggered = true;

            StartCoroutine(PlayEndingRoutine());
        }

        /// <summary>
        /// Позволяет запустить финальную катсцену триггером или кодом.
        /// </summary>
        public void TriggerCitadelEnding()
        {
            if (isEndingTriggered) return;
            isEndingTriggered = true;
            StartCoroutine(PlayEndingRoutine());
        }

        private IEnumerator PlayEndingRoutine()
        {
            playerCar = FindFirstObjectByType<ArcadeCarController>();

            // ── Шаг 1: Радиопереговоры о победе и открытии ворот ─────────────────────────
            RadioTransmissionSystem.Instance.PlayCitadelVictory();

            yield return new WaitForSeconds(1.5f);

            // ── Шаг 2: Распахивание ворот Цитадели ───────────────────────────────────────
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGateOpen();
            }
            if (ArcadeCameraFollow.Instance != null)
            {
                ArcadeCameraFollow.Instance.TriggerShake(0.8f, 1.5f);
            }

            // Переключаем огни в зеленый
            SetLightsColor(new Color(0.2f, 1f, 0.4f), 3.0f);

            float openElapsed = 0f;
            Vector3 leftClosed = leftDoor != null ? leftDoor.localPosition : Vector3.zero;
            Vector3 rightClosed = rightDoor != null ? rightDoor.localPosition : Vector3.zero;
            Vector3 leftOpen = leftClosed + Vector3.left * 12f;
            Vector3 rightOpen = rightClosed + Vector3.right * 12f;

            while (openElapsed < doorOpenDuration)
            {
                openElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, openElapsed / doorOpenDuration);

                if (leftDoor != null) leftDoor.localPosition = Vector3.Lerp(leftClosed, leftOpen, t);
                if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(rightClosed, rightOpen, t);

                yield return null;
            }

            // ── Шаг 3: Въезд машины внутрь Цитадели ──────────────────────────────────────
            if (playerCar != null)
            {
                var rb = playerCar.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                Vector3 carStartPos = playerCar.transform.position;
                Vector3 carTargetPos = transform.position + transform.forward * 25f;
                Quaternion startRot = playerCar.transform.rotation;
                Quaternion targetRot = Quaternion.LookRotation(transform.forward);

                float driveElapsed = 0f;
                while (driveElapsed < carDriveInDuration)
                {
                    driveElapsed += Time.deltaTime;
                    float t = driveElapsed / carDriveInDuration;

                    playerCar.transform.position = Vector3.Lerp(carStartPos, carTargetPos, t);
                    playerCar.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }

            // ── Шаг 4: Захлопывание бронешлюза за спиной ─────────────────────────────────
            float closeElapsed = 0f;
            while (closeElapsed < doorCloseDuration)
            {
                closeElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, closeElapsed / doorCloseDuration);

                if (leftDoor != null) leftDoor.localPosition = Vector3.Lerp(leftOpen, leftClosed, t);
                if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(rightOpen, rightClosed, t);

                yield return null;
            }

            // Оглушительный удар смыкания створок
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact(1.0f);
                AudioManager.Instance.PlayFanfare();
            }
            if (ArcadeCameraFollow.Instance != null)
            {
                ArcadeCameraFollow.Instance.TriggerShake(1.2f, 0.8f);
            }

            yield return new WaitForSeconds(1.0f);

            // ── Шаг 5: Переход к экрану полной победы ────────────────────────────────────
            var run = FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                run.ReportBossDefeated();
            }
        }

        private void SetLightsColor(Color col, float intensity)
        {
            if (runwayLights == null) return;
            for (int i = 0; i < runwayLights.Length; i++)
            {
                if (runwayLights[i] != null)
                {
                    runwayLights[i].color = col;
                    runwayLights[i].intensity = intensity;
                }
            }
        }

        public void BuildCitadelGatePlaceholders()
        {
            Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.18f, 0.20f, 0.22f) // Монолитный армированный бетон
            };

            Material gateMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.28f, 0.18f, 0.12f) // Тяжелая титановая бронесталь
            };

            Material hazardMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.95f, 0.75f, 0.1f)
            };

            // Левый монолит стены
            GameObject wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallL.name = "CitadelWall_Left";
            wallL.transform.SetParent(transform, false);
            wallL.transform.localPosition = new Vector3(-24f, 12f, 0f);
            wallL.transform.localScale = new Vector3(20f, 24f, 6f);
            wallL.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Правый монолит стены
            GameObject wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallR.name = "CitadelWall_Right";
            wallR.transform.SetParent(transform, false);
            wallR.transform.localPosition = new Vector3(24f, 12f, 0f);
            wallR.transform.localScale = new Vector3(20f, 24f, 6f);
            wallR.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Верхняя перемычка
            GameObject topLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topLintel.name = "CitadelWall_TopLintel";
            topLintel.transform.SetParent(transform, false);
            topLintel.transform.localPosition = new Vector3(0f, 20f, 0f);
            topLintel.transform.localScale = new Vector3(32f, 8f, 7f);
            topLintel.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Левая створка ворот
            GameObject gateL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateL.name = "Gate_Door_Left";
            gateL.transform.SetParent(transform, false);
            gateL.transform.localPosition = new Vector3(-6.2f, 7.5f, 0.5f);
            gateL.transform.localScale = new Vector3(12.5f, 15f, 1.8f);
            gateL.GetComponent<Renderer>().sharedMaterial = gateMat;
            leftDoor = gateL.transform;

            // Правая створка ворот
            GameObject gateR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateR.name = "Gate_Door_Right";
            gateR.transform.SetParent(transform, false);
            gateR.transform.localPosition = new Vector3(6.2f, 7.5f, 0.5f);
            gateR.transform.localScale = new Vector3(12.5f, 15f, 1.8f);
            gateR.GetComponent<Renderer>().sharedMaterial = gateMat;
            rightDoor = gateR.transform;

            // Предупреждающая желтая полоса над шлюзом
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Hazard_Banner";
            stripe.transform.SetParent(transform, false);
            stripe.transform.localPosition = new Vector3(0f, 15.5f, -3.1f);
            stripe.transform.localScale = new Vector3(24f, 1.2f, 0.2f);
            stripe.GetComponent<Renderer>().sharedMaterial = hazardMat;

            // Посадочные огни шлюза
            Light l1 = CreateLight("RunwayLight_L", new Vector3(-12f, 15f, -3.5f), Color.red);
            Light l2 = CreateLight("RunwayLight_R", new Vector3(12f, 15f, -3.5f), Color.red);
            runwayLights = new[] { l1, l2 };
        }

        private Light CreateLight(string name, Vector3 pos, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = pos;

            Light l = obj.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = 4.0f;
            l.range = 30f;
            l.spotAngle = 70f;
            return l;
        }
    }
}
