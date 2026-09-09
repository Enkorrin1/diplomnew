using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Процедурная анимация бега/ковыляния для зомби и мутантов (Procedural Run Bobbing).
    /// Добавляет реалистичное покачивание корпуса, наклон при поворотах и шаговый ритм,
    /// устраняя эффект неестественного скольжения по асфальту.
    /// </summary>
    public sealed class EnemyVisualBobbing : MonoBehaviour
    {
        [Header("Bobbing Settings")]
        [SerializeField] private float stepFrequency = 8.5f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float tiltAngle = 5.5f;

        private Transform visualTransform;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private EnemyBase enemyBase;
        private float randomSeed;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            ResolveVisual();
            randomSeed = Random.Range(0f, 100f);
        }

        private void Start()
        {
            if (visualTransform == null || visualTransform == transform)
            {
                ResolveVisual();
            }
        }

        private void ResolveVisual()
        {
            Transform bodyChild = transform.Find("VisualModel") ?? transform.Find("Body") ?? transform.Find("Visual") ?? transform.Find("VisualBody");
            visualTransform = bodyChild != null ? bodyChild : transform;

            initialLocalPos = visualTransform.localPosition;
            initialLocalRot = visualTransform.localRotation;
        }

        private void Update()
        {
            if (visualTransform == null) return;
            if (enemyBase != null && enemyBase.IsDead) return;

            float t = (Time.time + randomSeed) * stepFrequency;

            // Вертикальное покачивание (шаг)
            float verticalOffset = Mathf.Abs(Mathf.Sin(t)) * bobHeight;

            // Боковое покачивание (переваливание с ноги на ногу)
            float sideTilt = Mathf.Sin(t * 0.5f) * tiltAngle;

            visualTransform.localPosition = initialLocalPos + new Vector3(0f, verticalOffset, 0f);
            visualTransform.localRotation = initialLocalRot * Quaternion.Euler(0f, 0f, sideTilt);
        }
    }
}
