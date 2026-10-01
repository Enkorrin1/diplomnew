using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Two shots, one continuous vehicle. Stage1 is loaded asleep before the exit.</summary>
    [DefaultExecutionOrder(-11000)]
    public sealed class GarageSceneExitCinematic : MonoBehaviour
    {
        [SerializeField] private string targetSceneName = "Stage1_Outskirts";
        [SerializeField] private Transform gatePlane;
        [SerializeField] private Transform interiorShot;
        [SerializeField, Min(1f)] private float minimumSpeed = 3.5f;
        [SerializeField, Min(1f)] private float maximumSpeed = 6f;
        [SerializeField, Min(0.5f)] private float exteriorDuration = 2.4f;
        [SerializeField, Min(0.1f)] private float cameraReturnDuration = 1f;
        [Header("Bunker launch")]
        [SerializeField, Min(1f)] private float launchSpeed = 13f;
        [SerializeField, Min(1f)] private float rampLength = 18f;
        [SerializeField, Min(0.1f)] private float rampHeight = 1.6f;
        [SerializeField, Min(0.1f)] private float jumpDuration = 1.1f;
        [SerializeField, Min(0.1f)] private float revDuration = 2.8f;
        [SerializeField] private ParticleSystem[] tireSmoke;
        [SerializeField] private AudioClip landingImpact;

        public static GarageSceneExitCinematic Pending { get; private set; }
        public string Phase { get; private set; } = "Loading";
        public bool IsReady => arrival != null && arrival.IsConfigured;
        public bool IsRunning { get; private set; }
        public bool Completed { get; private set; }
        private StageGarageArrival arrival;
        private GarageDriveOutController controller;
        private GarageDriveOutVehicle vehicle;
        private Rigidbody body;
        private Camera shotCamera;
        private Scene garageScene;
        private bool transferred;
        private float originalFov;
        private float rideHeight;
        private RigidbodyInterpolation originalInterpolation;
        private readonly Dictionary<Canvas, bool> hiddenCanvases = new Dictionary<Canvas, bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pending = null;

        private void Awake()
        {
            garageScene = gameObject.scene;
            Pending = this;
        }

        private IEnumerator Start()
        {
            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Phase = "LoadFailed";
                Debug.LogError("[GarageExit] Stage1 is missing from Build Settings.", this);
                yield break;
            }
            var scene = SceneManager.GetSceneByName(targetSceneName);
            if (!scene.isLoaded)
            {
                AsyncOperation loading = null;
                try { loading = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive); }
                catch (System.Exception error) { Debug.LogException(error, this); }
                if (loading == null) { Phase = "LoadFailed"; yield break; }
                loading.completed += _ =>
                {
                    if (this != null) return;
                    var abandoned = SceneManager.GetSceneByName(targetSceneName);
                    if (abandoned.isLoaded && SceneManager.GetActiveScene() != abandoned)
                        SceneManager.UnloadSceneAsync(abandoned);
                };
                yield return loading;
                scene = SceneManager.GetSceneByName(targetSceneName);
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                arrival = root.GetComponentInChildren<StageGarageArrival>(true);
                if (arrival != null) break;
            }
            // Also handles a target scene that was already open additively in the Editor.
            if (arrival != null) arrival.SuspendForGarage();
            Phase = IsReady ? "Ready" : "LoadFailed";
            if (!IsReady) Debug.LogError("[GarageExit] Stage1 arrival markers are not configured.", this);
        }

        public bool TryBegin(GarageDriveOutVehicle candidate)
        {
            if (IsRunning || Completed) return true;
            controller = GarageDriveOutController.Instance;
            if (!IsReady || candidate == null || !candidate.IsDrivingEnabled || !candidate.enabled
                || controller == null || !controller.IsDriving || controller.DrivingCamera == null
                || gatePlane == null || interiorShot == null || candidate.GetComponent<ArcadeCarController>() == null) return false;
            if (Vector3.Dot(candidate.transform.forward, gatePlane.forward) < 0.5f) return false;
            vehicle = candidate;
            body = vehicle.GetComponent<Rigidbody>();
            shotCamera = controller.DrivingCamera;
            IsRunning = true;
            StartCoroutine(Play());
            return true;
        }

        private IEnumerator Play()
        {
            try
            {
            float speed = Mathf.Clamp(Mathf.Abs(vehicle.SpeedMps), minimumSpeed, maximumSpeed);
            originalFov = shotCamera.fieldOfView;
            originalInterpolation = body.interpolation;
            rideHeight = Mathf.Clamp(body.position.y - GroundHeight(body.position), 0.03f, 0.12f);
            controller.CinematicControl = true;
            vehicle.CinematicControl = true;
            vehicle.SimulatedThrottle = vehicle.SimulatedSteer = 0f;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            var roadCar = vehicle.GetComponent<ArcadeCarController>();
            roadCar.enabled = false;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.gameObject.scene == garageScene)
                {
                    hiddenCanvases[canvas] = canvas.enabled;
                    canvas.enabled = false;
                }
            GarageInteractionUI.Instance?.HidePrompt();
            GarageInteractionUI.Instance?.HideBanner();
            Phase = "Interior";
            Vector3 start = body.position;
            Quaternion startRotation = body.rotation;
            Move(start, startRotation, 0f);
            start = body.position;
            Phase = "Ignition";
            var presentation = GaragePresentationDirector.Instance;
            if (presentation != null && presentation.workshopEffects != null && presentation.latch != null)
                presentation.workshopEffects.PlayOneShot(presentation.latch, .65f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                Frame(start + startRotation * new Vector3(-2.7f, 1.8f, -2.6f));
                yield return null;
            }
            var openingGate = gatePlane.GetComponent<GarageSwingGateController>();
            openingGate?.OpenGate();
            Phase = "GateOpening";
            float gateDeadline = Time.realtimeSinceStartup + 8f;
            while (openingGate != null && !openingGate.IsOpen && Time.realtimeSinceStartup < gateDeadline)
            {
                Frame(start + startRotation * new Vector3(-2.8f, 1.7f, -3f), gatePlane.position + Vector3.up);
                yield return null;
            }
            if (openingGate != null && !openingGate.IsOpen) yield break;
            Phase = "Revving";
            for (float t = 0f; t < revDuration; t += Time.deltaTime)
            {
                vehicle.CinematicRevs = 0.25f + 0.7f * Mathf.Pow(Mathf.Sin(t * 3.5f), 2f);
                Frame(start + startRotation * new Vector3(-2.7f, 0.85f, -2.5f));
                shotCamera.transform.position += Vector3.up * (Mathf.Sin(t * 45f) * 0.008f * vehicle.CinematicRevs);
                yield return null;
            }
            Phase = "Interior";
            vehicle.CinematicRevs = 1f;
            vehicle.CinematicWheelSlip = 16f;
            foreach (var smoke in tireSmoke) if (smoke != null) smoke.Play();
            // Cut as the nose reaches the door plane, not after the entire car has passed.
            Vector3 cut = gatePlane.position - gatePlane.forward * 2.15f + Vector3.up * 0.4f;
            float duration = Mathf.Max(1.2f, Vector3.Distance(start, cut) / 5f);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);
                Move(Vector3.Lerp(start, cut, u * u), Quaternion.Slerp(startRotation, gatePlane.rotation, Mathf.SmoothStep(0f, 1f, u)), Mathf.Lerp(0f, launchSpeed, u));
                Frame(interiorShot.position, Vector3.Lerp(vehicle.transform.position + Vector3.up * 0.9f, gatePlane.position + Vector3.up, 0.25f));
                yield return null;
            }
            Move(cut, gatePlane.rotation, speed);

            var destination = arrival.gameObject.scene;
            arrival.Reveal(gatePlane);
            SceneManager.SetActiveScene(destination);
            GaragePrologueManager.Instance?.MarkPrologueCompleted();
            GarageInteractionUI.Instance?.SetObjective(string.Empty);
            TransferActors(destination);
            transferred = true;
            var outsideStart = arrival.GatePlane.position - arrival.GatePlane.forward * 2.15f + Vector3.up * 0.4f;
            Move(outsideStart, arrival.GatePlane.rotation, speed);
            Frame(arrival.ExteriorShot.position);
            // Disable old geometry before rendering the exterior, then release its memory asynchronously.
            foreach (var root in garageScene.GetRootGameObjects()) root.SetActive(false);
            arrival.PrepareEnvironment();
            shotCamera.clearFlags = CameraClearFlags.Skybox;
            shotCamera.backgroundColor = RenderSettings.fogColor;
            SceneManager.UnloadSceneAsync(garageScene);
            // Let the sky/environment update before rendering the first exterior frame.
            yield return null;
            Phase = "Exterior";
            speed = launchSpeed;
            Vector3 rampStart = arrival.GatePlane.position;
            float rampDuration = (rampLength + 2.15f) / speed;
            for (float t = 0f; t < rampDuration; t += Time.deltaTime)
            {
                float z = -2.15f + speed * t;
                Vector3 p = rampStart + arrival.GatePlane.forward * z;
                p.y = rampStart.y + rideHeight + rampHeight * Mathf.Clamp01(z / rampLength);
                Place(p, arrival.GatePlane.rotation * Quaternion.Euler(-Mathf.Atan2(rampHeight, rampLength) * Mathf.Rad2Deg, 0f, 0f), speed);
                vehicle.CinematicWheelSlip = Mathf.Lerp(12f, 0f, t / rampDuration);
                Frame(arrival.ExteriorShot.position);
                yield return null;
            }

            foreach (var smoke in tireSmoke) if (smoke != null) smoke.Stop();
            vehicle.CinematicWheelSlip = 0f;
            Phase = "Airborne";
            for (float t = 0f; t < jumpDuration; t += Time.deltaTime)
            {
                float u = t / jumpDuration;
                Vector3 p = rampStart + arrival.GatePlane.forward * (rampLength + speed * t);
                p.y = rampStart.y + rideHeight + Mathf.Lerp(rampHeight, 0f, u) + Mathf.Sin(u * Mathf.PI) * 1.1f;
                Place(p, arrival.GatePlane.rotation * Quaternion.Euler(Mathf.Lerp(-8f, 9f, u), 0f, 0f), speed);
                // Track ahead of the car so it never crosses through the exterior camera.
                Frame(arrival.ExteriorShot.position + arrival.GatePlane.forward * (speed * t));
                yield return null;
            }
            Phase = "Landing";
            foreach (var smoke in tireSmoke) if (smoke != null) smoke.Emit(18);
            if (landingImpact != null) vehicle.GetComponent<AudioSource>()?.PlayOneShot(landingImpact, .45f);
            for (float t = 0f; t < 0.65f; t += Time.deltaTime)
            {
                Move(body.position + arrival.GatePlane.forward * (speed * Time.deltaTime), arrival.GatePlane.rotation, speed);
                Frame(arrival.ExteriorShot.position + arrival.GatePlane.forward * (speed * (jumpDuration + t)));
                shotCamera.transform.position += Vector3.up * (Mathf.Sin(t * 30f) * 0.12f * Mathf.Exp(-t * 7f));
                yield return null;
            }

            Phase = "CameraReturn";
            Vector3 from = shotCamera.transform.position;
            Quaternion fromRotation = shotCamera.transform.rotation;
            for (float t = 0f; t < cameraReturnDuration; t += Time.deltaTime)
            {
                Move(body.position + arrival.GatePlane.forward * (speed * Time.deltaTime), arrival.GatePlane.rotation, speed);
                float u = Mathf.SmoothStep(0f, 1f, t / cameraReturnDuration);
                controller.GetDefaultFollowPose(out Vector3 follow, out Quaternion rotation);
                shotCamera.transform.SetPositionAndRotation(Vector3.Lerp(from, follow, u), Quaternion.Slerp(fromRotation, rotation, u));
                shotCamera.fieldOfView = Mathf.Lerp(55f, originalFov, u);
                yield return null;
            }

            RestoreControl(speed);
            arrival.BeginGameplay(roadCar);
            foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenCanvases.Clear();
            Completed = true;
            IsRunning = false;
            Phase = "Complete";
            Pending = null;
            Debug.Log("[GarageExit] GarageScene -> Stage1_Outskirts complete; original car and inventory preserved.");
            }
            finally
            {
                if (!Completed)
                {
                    RestoreControl(0f);
                    foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
                    hiddenCanvases.Clear();
                    IsRunning = false;
                    Phase = "Interrupted";
                }
            }
        }

        private void Move(Vector3 position, Quaternion rotation, float speed)
        {
            Vector3 forward = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) * Vector3.forward;
            float front = GroundHeight(position + forward * 1.38f);
            float rear = GroundHeight(position - forward * 1.35f);
            position.y = (front + rear) * .5f + rideHeight;
            rotation = Quaternion.LookRotation(forward * 2.73f + Vector3.up * (front - rear), Vector3.up);
            Place(position, rotation, speed);
        }

        private void Place(Vector3 position, Quaternion rotation, float speed)
        {
            body.position = position;
            body.rotation = rotation;
            vehicle.transform.SetPositionAndRotation(position, rotation);
            vehicle.SetCinematicSpeed(speed);
        }

        private float GroundHeight(Vector3 position)
        {
            float nearest = float.MaxValue;
            float ground = position.y - rideHeight;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * 1.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != vehicle.gameObject.scene || hit.transform.IsChildOf(vehicle.transform)
                    || hit.normal.y < 0.5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                ground = hit.point.y;
            }
            return ground;
        }

        private void Frame(Vector3 position, Vector3? lookAt = null)
        {
            Vector3 focus = lookAt ?? vehicle.transform.position + Vector3.up * 0.9f;
            if (!lookAt.HasValue)
            {
                // Bound the complete car in the narrower axis, including narrow windows.
                float halfVertical = 55f * .5f * Mathf.Deg2Rad;
                float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * shotCamera.aspect);
                float minimumDistance = 3.1f / Mathf.Sin(Mathf.Min(halfVertical, halfHorizontal));
                Vector3 offset = position - focus;
                if (offset.magnitude < minimumDistance)
                    position = focus + offset.normalized * minimumDistance;
            }
            shotCamera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(focus - position));
            shotCamera.fieldOfView = 55f;
        }

        private static void MoveRoot(Transform actor, Scene destination)
        {
            if (actor == null || actor.gameObject.scene == destination) return;
            actor.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(actor.gameObject, destination);
        }

        private void TransferActors(Scene destination)
        {
            MoveRoot(transform, destination);
            MoveRoot(vehicle.transform, destination);
            MoveRoot(shotCamera.transform, destination);
            MoveRoot(controller.transform, destination);
            MoveRoot(controller.SeatedPlayer != null ? controller.SeatedPlayer.transform : null, destination);
            var garageMenu = controller.GetComponent<RogueDrive.Meta.GarageUIController>();
            if (garageMenu != null) garageMenu.enabled = false;
            // These UI controllers own runtime-created canvases. Move both owners and canvases.
            MoveRoot(GarageInteractionUI.Instance != null ? GarageInteractionUI.Instance.transform : null, destination);
            MoveRoot(PocketInventoryUI.Instance != null ? PocketInventoryUI.Instance.transform : null, destination);
            foreach (var hud in FindObjectsByType<VehicleModularTacticalHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                MoveRoot(hud.transform, destination);
            foreach (var pair in hiddenCanvases)
                if (pair.Key != null && (pair.Key.name.Contains("Inventory") || pair.Key.name.Contains("Interaction")
                    || pair.Key.name.Contains("Tactical") || pair.Key.name.Contains("Dashboard")))
                    MoveRoot(pair.Key.transform.root, destination);
        }

        private void RestoreControl(float speed)
        {
            if (body != null)
            {
                body.isKinematic = false;
                body.interpolation = originalInterpolation;
                body.linearVelocity = vehicle.transform.forward * speed;
                body.angularVelocity = Vector3.zero;
            }
            if (vehicle != null)
            {
                vehicle.CinematicControl = false;
                vehicle.CinematicRevs = vehicle.CinematicWheelSlip = 0f;
            }
            foreach (var smoke in tireSmoke) if (smoke != null) smoke.Stop();
            if (controller != null) controller.FinishCinematic();
            if (shotCamera != null) shotCamera.fieldOfView = originalFov;
        }

        private void OnDestroy()
        {
            if (Pending == this) Pending = null;
            if (IsRunning) RestoreControl(0f);
            foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            if (!transferred && arrival != null && arrival.gameObject.scene.isLoaded)
                SceneManager.UnloadSceneAsync(arrival.gameObject.scene);
        }
    }
}
