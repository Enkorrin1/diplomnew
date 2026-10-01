using RogueDrive.Gameplay.Hub;
using Unity.Netcode;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    [DefaultExecutionOrder(-50)]
    public sealed class CoopVehicle : NetworkBehaviour
    {
        public const ulong Empty = ulong.MaxValue;
        public static CoopVehicle Instance { get; private set; }
        public readonly NetworkVariable<ulong> Driver = new NetworkVariable<ulong>(Empty);
        public readonly NetworkVariable<ulong> Navigator = new NetworkVariable<ulong>(Empty);
        public readonly NetworkVariable<float> Speed = new NetworkVariable<float>();
        public readonly NetworkVariable<bool> Transitioning = new NetworkVariable<bool>();
        public readonly NetworkVariable<float> Hull = new NetworkVariable<float>(100);
        public readonly NetworkVariable<float> StormFront = new NetworkVariable<float>();
        public readonly NetworkVariable<float> StormDistance = new NetworkVariable<float>(900);
        float nextImpact;
        [SerializeField] private float stopSpeed = .5f;
        [SerializeField] private float boardingDistance = 4.5f;
        private Rigidbody body;
        private GarageDriveOutVehicle driving;
        private float lastInput;
        private Vector3 startPosition;
        public bool AtStart => Vector3.Distance(transform.position, startPosition) < 18f && body.linearVelocity.magnitude < stopSpeed;
        public bool Stopped => body.linearVelocity.magnitude <= stopSpeed;
        public int CrewCount => (Driver.Value == Empty ? 0 : 1) + (Navigator.Value == Empty ? 0 : 1);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            driving = GetComponent<GarageDriveOutVehicle>();
            driving.ExternalInput = true;
            driving.enabled = false;
            var offlineDriving = GetComponent<ArcadeCarController>();
            if (offlineDriving != null) offlineDriving.enabled = false;
            var offlineEntry = GetComponent<VehiclePassengerEntry>();
            if (offlineEntry != null) offlineEntry.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            startPosition = transform.position;
            body.isKinematic = !IsServer;
            driving.enabled = IsServer;
            if (IsServer) { driving.EnableDriving(); driving.SetExternalInput(0, 0, true); }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            driving.enabled = false;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            Speed.Value = body.linearVelocity.magnitude;
            if (Hull.Value <= 0 || Driver.Value == Empty || Time.unscaledTime - lastInput > .35f)
                driving.SetExternalInput(0, 0, true);
        }

        public void ApplyInput(ulong sender, Vector2 input, bool brake)
        {
            if (Transitioning.Value) return;
            if (!IsServer || sender != Driver.Value || !Finite(input.x) || !Finite(input.y)) return;
            if (Hull.Value <= 0) { driving.SetExternalInput(0, 0, true); return; }
            if (CoopQuestManager.Instance != null && !CoopQuestManager.Instance.CanDrive)
            {
                input.y = 0f;
            }
            lastInput = Time.unscaledTime;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GarageScene" &&
                !CoopSession.Instance.CrewReadyForDeparture())
            {
                input = Vector2.zero;
                brake = true;
            }
            driving.SetExternalInput(input.y, input.x, brake);
        }

        public string ChangeSeat(CoopPlayer player, int requested)
        {
            if (Transitioning.Value) return "Дождитесь загрузки трассы.";
            if (!IsServer || player == null || requested < -1 || requested > 1) return "Недопустимое действие.";
            if (player.IsDowned.Value) return "Раненый не может занимать или менять место.";
            int current = player.Seat.Value;
            if (!Stopped) return "Сначала полностью остановите машину.";
            if (requested == current) return "Вы уже на этом месте.";
            if (current < 0 && Vector3.Distance(player.transform.position, transform.position) > boardingDistance)
                return "Подойдите ближе к машине.";
            if (requested == 0 && Driver.Value != Empty) return "Место водителя занято.";
            if (requested == 1 && Navigator.Value != Empty) return "Место штурмана занято.";
            Vector3 exit = default;
            if (requested == -1 && !TryExitPosition(current, out exit)) return "Выход заблокирован. Переставьте машину.";

            ReleaseDisconnected(player.OwnerClientId);
            if (requested == 0) Driver.Value = player.OwnerClientId;
            if (requested == 1) Navigator.Value = player.OwnerClientId;
            player.ServerSetSeat(requested, requested < 0 ? exit : SeatPosition(requested));
            return "";
        }

        public void ReleaseDisconnected(ulong id)
        {
            if (!IsServer) return;
            if (Driver.Value == id) { Driver.Value = Empty; driving.SetExternalInput(0, 0, true); }
            if (Navigator.Value == id) Navigator.Value = Empty;
        }

        public Vector3 SeatPosition(int seat) => transform.TransformPoint(new Vector3(seat == 0 ? -.48f : .48f, .6f, 0));

        public void ServerFreeze(bool frozen)
        {
            if (!IsServer) return;
            driving.SetExternalInput(0, 0, true);
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = frozen;
            driving.enabled = !frozen;
        }

        public void ServerPlaceAt(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            transform.SetPositionAndRotation(position, rotation);
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, rotation, transform.localScale);
            startPosition = position;
        }

        private bool TryExitPosition(int seat, out Vector3 position)
        {
            foreach (float side in new[] { seat == 0 ? -1f : 1f, seat == 0 ? 1f : -1f })
            {
                Vector3 above = transform.TransformPoint(new Vector3(side * 2.4f, 2f, 0));
                if (!Physics.Raycast(above, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                Vector3 feet = hit.point + Vector3.up * .08f;
                if (Physics.CheckCapsule(feet + Vector3.up * .4f, feet + Vector3.up * 1.4f, .32f, ~0, QueryTriggerInteraction.Ignore)) continue;
                position = feet;
                return true;
            }
            position = default;
            return false;
        }
        public bool TryRecoveryPosition(out Vector3 position) => TryExitPosition(1, out position);

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || Transitioning.Value || Time.unscaledTime < nextImpact ||
                !(CoopQuestManager.Instance?.QuestBunkerDeparted.Value ?? false) || collision.relativeVelocity.magnitude < 6) return;
            // Landing on the road is suspension work; a frontal/side impact damages the hull.
            bool obstacle = false;
            foreach (var contact in collision.contacts) if (Mathf.Abs(contact.normal.y) < .6f) { obstacle = true; break; }
            if (!obstacle) return;
            nextImpact = Time.unscaledTime + .75f;
            Hull.Value = Mathf.Max(0, Hull.Value - Mathf.Min(30, (collision.relativeVelocity.magnitude - 6) * 2));
        }
    }
}
