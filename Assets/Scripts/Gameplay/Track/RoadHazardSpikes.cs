using RogueDrive.Audio;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Combat;
using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>
    /// Придорожная полоса с шипами рейдеров (Raider Spike Strip Hazard):
    /// Опасное дорожное препятствие, перекрывающее часть полосы движения.
    /// При наезде на высокой скорости мгновенно прокалывает шины автомобиля,
    /// снижая сцепление с дорогой и вызывая занос.
    /// Может быть уничтожена выстрелами турели или объехана по обочине.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoadHazardSpikes : MonoBehaviour, IDamageable
    {
        [Header("Settings")]
        [SerializeField] private float spikeDamage = 25f;
        [SerializeField] private int tiresToPuncture = 1;
        [SerializeField] private bool isTriggered = false;

        public bool IsDead => isTriggered;
        public float SpikeDamage => spikeDamage;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            if (col.size == Vector3.one)
            {
                col.size = new Vector3(6f, 0.4f, 1.2f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isTriggered) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                PunctureVehicle(car);
            }
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            // Уничтожение шипов выстрелами турели
            if (isTriggered) return;
            isTriggered = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact();
            }

            Destroy(gameObject, 0.1f);
        }

        private void PunctureVehicle(ArcadeCarController car)
        {
            isTriggered = true;

            var modular = car.GetComponent<VehicleModularState>() ?? VehicleModularState.Instance;
            if (modular != null)
            {
                // Прокалываем случайное переднее или заднее колесо
                int randomTire = Random.Range(0, 4);
                modular.DamageTire(randomTire, 0.9f);

                if (tiresToPuncture > 1)
                {
                    int secondTire = (randomTire + 1) % 4;
                    modular.DamageTire(secondTire, 0.8f);
                }
            }

            if (spikeDamage > 0f)
            {
                var run = Object.FindFirstObjectByType<GameRunController>();
                if (run != null)
                {
                    run.TakeDamage(spikeDamage);
                }
            }

            // Звук хлопка лопнувшей покрышки и скрежета диска по асфальту
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCrash(1.1f);
            }

            ArcadeCameraFollow.Instance?.TriggerShake(0.7f, 0.35f);

            // Сообщение на экран
            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification("⚠️ ПРОКОЛ ШИНЫ! Шипы рейдеров! Сцепление с дорогой нарушено!", 3.5f);
            }

            Destroy(gameObject, 0.2f);
        }
    }
}
