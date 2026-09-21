using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>
    /// Интерактивная дорожная угроза: токсичная кислотная лужа на дорожном полотне.
    /// При наезде на высокой скорости вызывает занос автомобиля (снижение сцепления шин),
    /// наносит урон прочности кузова и активирует брызги кислоты.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AcidPuddleHazard : MonoBehaviour
    {
        [Header("Hazard Parameters")]
        [SerializeField, Min(1f)] private float damageAmount = 14f;
        [SerializeField, Range(0.1f, 1f)] private float speedPenaltyFactor = 0.82f;
        [SerializeField, Min(0.1f)] private float retriggerDelay = 1.0f;

        private float lastTriggerTime = -10f;
        private MeshRenderer puddleRenderer;

        private void Awake()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            if (col.size == Vector3.one)
            {
                col.size = new Vector3(5.5f, 0.8f, 7.0f);
                col.center = new Vector3(0f, 0.4f, 0f);
            }

            EnsureVisualPuddleMesh();
        }

        private void EnsureVisualPuddleMesh()
        {
            Transform meshChild = transform.Find("PuddleMesh");
            if (meshChild == null)
            {
                GameObject puddleObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puddleObj.name = "PuddleMesh";
                puddleObj.transform.SetParent(transform, false);
                puddleObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                puddleObj.transform.localScale = new Vector3(5f, 6.5f, 1f);
                puddleObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

                // Удаляем встроенный MeshCollider от примитива
                Collider c = puddleObj.GetComponent<Collider>();
                if (c != null) Destroy(c);

                puddleRenderer = puddleObj.GetComponent<MeshRenderer>();
                Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse"));
                mat.color = new Color(0.25f, 0.95f, 0.35f, 0.85f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.65f, 0.20f) * 1.5f);
                puddleRenderer.sharedMaterial = mat;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHazardTrigger(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            HandleHazardTrigger(other.gameObject);
        }

        private void HandleHazardTrigger(GameObject target)
        {
            if (Time.time - lastTriggerTime < retriggerDelay) return;

            ArcadeCarController car = target.GetComponentInParent<ArcadeCarController>();
            if (car == null) return;

            lastTriggerTime = Time.time;

            // Нанесение урона
            GameRunController run = car.Run != null ? car.Run : FindFirstObjectByType<GameRunController>();
            if (run != null && !run.IsGameOver)
            {
                run.TakeDamage(damageAmount);
            }

            // Штраф к скорости
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity *= speedPenaltyFactor;
            }

            // Тактильный сок и звук шипения кислоты
            ArcadeCameraFollow.Instance?.TriggerShake(0.35f, 0.25f);
            AudioManager.Instance?.PlayHit(0.65f); // звуковой отклик
        }
    }
}
