using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Saved route geometry and pacing for the bunker-to-workshop prototype.</summary>
    public sealed class StageRoute : MonoBehaviour
    {
        public static StageRoute Instance { get; private set; }
        [SerializeField] Vector3[] points = new Vector3[0];
        [SerializeField] float stormSpeed = 11f;
        [SerializeField] float stormLead = 900f;
        [SerializeField] float fuelMultiplier = .08f;
        [SerializeField] float coolantMultiplier = .35f;
        [SerializeField] float expectedDrivingSpeed = 18f;
        [SerializeField] float plannedStopSeconds = 300f;
        [SerializeField] bool preserveAuthoredEnvironment;
        float[] cumulative;
        ArcadeCarController car;
        GameRunController run;
        public float Length { get { EnsureLengths(); return cumulative.Length == 0 ? 0 : cumulative[cumulative.Length - 1]; } }
        public float StormSpeed => stormSpeed;
        public float StormLead => stormLead;
        public float FuelMultiplier => fuelMultiplier;
        public float CoolantMultiplier => coolantMultiplier;
        public float ExpectedMinutes => (Length / expectedDrivingSpeed + plannedStopSeconds) / 60f;
        public float Progress { get; private set; }
        public int PointCount => points.Length;
        public bool PreserveAuthoredEnvironment => preserveAuthoredEnvironment;
        public void SetPreserveAuthoredEnvironment(bool value) => preserveAuthoredEnvironment = value;
        public Vector3[] CopyPoints() => (Vector3[])points.Clone();
        public void CopyPacingFrom(StageRoute source)
        {stormSpeed=source.stormSpeed;stormLead=source.stormLead;fuelMultiplier=source.fuelMultiplier;coolantMultiplier=source.coolantMultiplier;}

        void OnEnable() { Instance = this; EnsureLengths(); }
        void OnDisable() { if (Instance == this) Instance = null; }
        void OnValidate() { cumulative = null; }
        public void Configure(Vector3[] worldPoints) { points = worldPoints; cumulative = null; EnsureLengths(); }

        void EnsureLengths()
        {
            if (cumulative != null && cumulative.Length == points.Length) return;
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        public float ProjectDistance(Vector3 worldPosition)
        {
            EnsureLengths();
            float best = float.PositiveInfinity, distance = 0;
            worldPosition.y = 0;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i]; a.y = b.y = 0;
                Vector3 edge = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(worldPosition - a, edge) / Mathf.Max(.0001f, edge.sqrMagnitude));
                float error = (worldPosition - (a + edge * t)).sqrMagnitude;
                if (error >= best) continue;
                best = error; distance = Mathf.Lerp(cumulative[i - 1], cumulative[i], t);
            }
            return distance;
        }

        public void Evaluate(float distance, out Vector3 position, out Quaternion rotation)
        {
            EnsureLengths();
            position = points.Length > 0 ? points[0] : transform.position;
            rotation = Quaternion.identity;
            if (points.Length < 2) return;
            int i = 1;
            while (i < points.Length - 1 && cumulative[i] < distance) i++;
            Vector3 edge = points[i] - points[i - 1];
            float length = Mathf.Max(.001f, cumulative[i] - cumulative[i - 1]);
            position = points[i - 1] + edge * ((distance - cumulative[i - 1]) / length);
            if (edge.sqrMagnitude > .0001f) rotation = Quaternion.LookRotation(edge, Vector3.up);
        }

        void LateUpdate()
        {
            if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null) run = FindFirstObjectByType<GameRunController>();
            if (car == null || run == null || run.IsGameOver) return;
            Progress = ProjectDistance(car.transform.position);
            run.ReportRoutePosition(Progress);
            var fuel = car.GetComponent<VehicleModularState>();
            if (fuel != null) run.SyncPhysicalFuel(fuel.FuelLiters / fuel.MaxFuelLiters);
        }
    }
}
