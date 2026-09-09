using UnityEngine;

namespace RogueDrive.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LaneCarController : MonoBehaviour
    {
        [Header("Lane movement")]
        [SerializeField, Min(0.1f)] private float laneWidth = 3f;
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 12f;
        [SerializeField, Min(0.1f)] private float forwardSpeed = 18f;
        [SerializeField, Min(0f)] private float fuelPerSecond = 1.5f;

        [Header("Collision")]
        [SerializeField, Min(0f)] private float impactDamage = 20f;

        Rigidbody body;
        GameRunController run;
        int targetLane;

        public void Configure(GameRunController controller)
        {
            run = controller;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void Update()
        {
            if (run == null || run.IsGameOver)
                return;

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                targetLane = Mathf.Max(-1, targetLane - 1);

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                targetLane = Mathf.Min(1, targetLane + 1);
        }

        void FixedUpdate()
        {
            if (run == null || run.IsGameOver)
                return;

            float deltaTime = Time.fixedDeltaTime;
            float targetX = targetLane * laneWidth;
            Vector3 position = body.position;
            position.x = Mathf.MoveTowards(position.x, targetX, laneChangeSpeed * deltaTime);
            position.z += forwardSpeed * deltaTime;

            body.MovePosition(position);
            run.ReportTravelled(forwardSpeed * deltaTime);
            run.ConsumeFuel(fuelPerSecond * deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (run == null || run.IsGameOver)
                return;

            TrackObstacle obstacle = other.GetComponent<TrackObstacle>();

            if (obstacle == null || !obstacle.TryConsume())
                return;

            run.TakeDamage(impactDamage);
        }
    }
}
