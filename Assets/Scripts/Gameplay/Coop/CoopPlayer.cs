using RogueDrive.Gameplay.Combat;
using RogueDrive.Gameplay.Hub;
using Unity.Netcode;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>
    /// Server-authoritative pedestrian with Navigator tactical roles, on-foot scavenging,
    /// and Downed State / Revive mechanics in Co-op.
    /// </summary>
    public sealed class CoopPlayer : NetworkBehaviour
    {
        public static CoopPlayer Local { get; private set; }
        public readonly NetworkVariable<int> Seat = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> IsDowned = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> BleedoutTimer = new NetworkVariable<float>(30f);
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(100f);
        public readonly NetworkVariable<int> CurrentAmmo = new NetworkVariable<int>(7, NetworkVariableReadPermission.Owner);
        public readonly NetworkVariable<int> ReserveAmmo = new NetworkVariable<int>(21, NetworkVariableReadPermission.Owner);
        public readonly NetworkVariable<bool> Reloading = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Owner);

        [SerializeField] private Transform avatar;
        private CharacterController controller;
        private Camera localCamera;
        private Vector2 movement;
        private bool brake, sprint;
        private float yaw, pitch, verticalVelocity, receivedAt, nextSend, nextSeatRequest;
        public string Feedback { get; private set; } = "";
        private float feedbackUntil;
        public bool TestControl { get; set; }

        private float reloadEndTime;
        private float nextFireTime = 0f;
        private bool isAiming = false;
        private float nextServerShot;
        private bool bleedoutReported;
        float nextCrewAction;

        public string FirearmInfo => Reloading.Value
            ? "<color=#FFCC00>ПЕРЕЗАРЯДКА...</color>"
            : $"ПИСТОЛЕТ 9ММ: <color=#FFFFFF><b>{CurrentAmmo.Value}</b></color>/7  (Личный запас: {ReserveAmmo.Value})  ·  [ЛКМ] Огонь  ·  [ПКМ] Прицел  ·  [R] Зарядить";
        bool HasReloadAmmo => ReserveAmmo.Value > 0 || (CoopSupplies.Instance?.Ammo.Value ?? 0) > 0;

        private void Awake() { controller = GetComponent<CharacterController>(); controller.enabled = false; }

        public override void OnNetworkSpawn()
        {
            controller.enabled = IsServer;
            NetworkObject.DestroyWithScene = false;
            if (!IsOwner) return;
            Local = this;
            var cameraObject = new GameObject("Coop local camera", typeof(Camera), typeof(AudioListener));
            localCamera = cameraObject.GetComponent<Camera>();
            localCamera.nearClipPlane = .08f;
            localCamera.farClipPlane = 1500;
            localCamera.fieldOfView = 68;
            cameraObject.tag = "MainCamera";
            DontDestroyOnLoad(cameraObject);
            yaw = transform.eulerAngles.y;
            CoopSession.Instance.SetMenu(false);
        }

        public override void OnNetworkDespawn()
        {
            controller.enabled = false;
            if (IsServer) CoopVehicle.Instance?.ReleaseDisconnected(OwnerClientId);
            if (Local == this) Local = null;
            if (localCamera != null) Destroy(localCamera.gameObject);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (Time.unscaledTime > feedbackUntil) Feedback = "";
            if (TestControl) return;

            bool inputAllowed = !CoopSession.Instance.MenuOpen && Application.isFocused;
            if (inputAllowed)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2f, -60f, 70f);

                // 1. Посадка в авто / смена мест (только если не ранен)
                if (!IsDowned.Value)
                {
                    if (Input.GetKeyDown(KeyCode.Q)) RequestCrewAction(0);
                    if (Input.GetKeyDown(KeyCode.R) && Seat.Value == 1) RequestCrewAction(1);
                    if (Input.GetKeyDown(KeyCode.H)) RequestCrewAction(2);
                    if (Input.GetKeyDown(KeyCode.E) && Seat.Value >= 0) RequestSeat(-1);
                    if (Input.GetKeyDown(KeyCode.F)) RequestSeat(Seat.Value == 1 ? 0 : 1);
                }


                // 3. Вылазка пешком: взаимодействие, поднятие напарника, стрельба
                if (Seat.Value < 0 && !IsDowned.Value && localCamera != null)
                {
                    HandleOnFootInteraction();
                    HandleOnFootFirearms();
                }
            }

            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 1f / 30f;
                SendInput(inputAllowed ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero,
                    !inputAllowed || Input.GetKey(KeyCode.Space), inputAllowed && Input.GetKey(KeyCode.LeftShift), yaw);
            }
        }

        private void HandleOnFootFirearms()
        {
            // Плавное прицеливание (ПКМ)
            isAiming = Input.GetMouseButton(1);
            float targetFov = isAiming ? 46f : 68f;
            localCamera.fieldOfView = Mathf.Lerp(localCamera.fieldOfView, targetFov, Time.deltaTime * 12f);

            if (Reloading.Value) return;

            // Перезарядка (R)
            if (Input.GetKeyDown(KeyCode.R) && CurrentAmmo.Value < 7 && HasReloadAmmo)
            {
                ReloadRpc();
                RogueDrive.Audio.AudioManager.Instance?.PlayKeysJingle();
                return;
            }

            // Выстрел (ЛКМ)
            if (Input.GetMouseButtonDown(0))
            {
                if (CurrentAmmo.Value <= 0)
                {
                    RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
                    Feedback = HasReloadAmmo ? "Магазин пуст! Нажмите [R] для перезарядки" : "Нет патронов 9мм! Обыщите тайники.";
                    feedbackUntil = Time.unscaledTime + 1.5f;
                    if (HasReloadAmmo)
                    {
                        ReloadRpc();
                        RogueDrive.Audio.AudioManager.Instance?.PlayKeysJingle();
                    }
                    return;
                }

                if (Time.unscaledTime >= nextFireTime)
                {
                    nextFireTime = Time.unscaledTime + 0.22f;
                    pitch = Mathf.Clamp(pitch - (isAiming ? 1.0f : 2.0f), -60f, 70f);
                    RogueDrive.Audio.AudioManager.Instance?.PlayShoot(0.12f);
                    FireShotRpc(localCamera.transform.position, localCamera.transform.forward);
                }
            }
        }

        private void HandleOnFootInteraction()
        {
            Ray ray = new Ray(localCamera.transform.position, localCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3.8f, ~0, QueryTriggerInteraction.Collide))
            {
                // Проверка на раненого напарника
                var targetPlayer = hit.collider.GetComponentInParent<CoopPlayer>();
                if (targetPlayer != null && targetPlayer != this && targetPlayer.IsDowned.Value)
                {
                    Feedback = "[E] Реанимировать напарника (Первая помощь)";
                    feedbackUntil = Time.unscaledTime + 0.1f;
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        ReviveTeammateRpc(targetPlayer.OwnerClientId);
                    }
                    return;
                }

                // Проверка на интерактивные точки бункера (генератор, колесо, аккумулятор, бак, ключи, ворота)
                var bunkerAction = hit.collider.GetComponentInParent<CoopBunkerInteractable>();
                if (bunkerAction != null && bunkerAction.CanInteract())
                {
                    Feedback = bunkerAction.PromptText;
                    feedbackUntil = Time.unscaledTime + 0.1f;
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        bunkerAction.Interact(this);
                    }
                    return;
                }

                // Проверка на автомобиль
                var car = hit.collider.GetComponentInParent<CoopVehicle>();
                if (car != null)
                {
                    Feedback = "[E] Занять место водителя  ·  [F] Место штурмана";
                    feedbackUntil = Time.unscaledTime + 0.1f;
                    if (Input.GetKeyDown(KeyCode.E)) RequestSeat(0);
                    return;
                }

                // Проверка на придорожный тайник
                var cache = hit.collider.GetComponentInParent<RoadsideSupplyCache>();
                if (cache != null && !cache.IsEmpty && CoopSupplies.Instance != null)
                {
                    Feedback = CoopSupplies.Instance.Prompt(cache, this);
                    feedbackUntil = Time.unscaledTime + 0.1f;
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        RequestCache(cache.Id);
                    }
                    return;
                }
            }
        }

        public void SendInput(Vector2 input, bool handbrake, bool running, float facing)
        {
            if (Seat.Value == 0 && input.y > 0.05f && CoopQuestManager.Instance != null && !CoopQuestManager.Instance.CanDrive)
            {
                Feedback = CoopQuestManager.Instance.GetDriveBlockReason();
                feedbackUntil = Time.unscaledTime + 2.0f;
                input.y = 0f;
            }

            if (IsSpawned && IsOwner) InputRpc(input, handbrake, running, facing);
        }

        [Rpc(SendTo.Server)]
        private void InputRpc(Vector2 input, bool handbrake, bool running, float facing, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || !CoopVehicle.Finite(input.x) || !CoopVehicle.Finite(input.y) || !CoopVehicle.Finite(facing)) return;
            movement = Vector2.ClampMagnitude(input, 1f);
            brake = handbrake;
            sprint = running && !IsDowned.Value;
            receivedAt = Time.unscaledTime;
            if (Seat.Value < 0) transform.rotation = Quaternion.Euler(0, facing % 360f, 0);
            CoopVehicle.Instance?.ApplyInput(OwnerClientId, IsDowned.Value ? Vector2.zero : movement, brake || IsDowned.Value);
        }

        [Rpc(SendTo.Server)]
        private void ReviveTeammateRpc(ulong targetClientId, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || IsDowned.Value || Seat.Value >= 0 || targetClientId == OwnerClientId) return;
            foreach (var player in FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None))
            {
                if (player.OwnerClientId == targetClientId && player.IsDowned.Value && player.BleedoutTimer.Value > 0f &&
                    Vector3.Distance(transform.position, player.transform.position) <= 3.8f &&
                    (!Physics.Linecast(transform.position + Vector3.up, player.transform.position + Vector3.up,
                        out var obstruction, ~0, QueryTriggerInteraction.Ignore) || obstruction.collider.GetComponentInParent<CoopPlayer>() == player))
                {
                    player.IsDowned.Value = false;
                    player.BleedoutTimer.Value = 30f;
                    player.Health.Value = 40f;
                    player.FeedbackRpc("✔ Вас поднял напарник!");
                    FeedbackRpc("✔ Напарник успешно реанимирован!");
                    break;
                }
            }
        }

        public void RequestCache(string id)
        {
            if (IsSpawned && IsOwner && !string.IsNullOrEmpty(id) && id.Length <= 120) CacheRpc(id);
        }
        [Rpc(SendTo.Server)]
        private void CacheRpc(string id, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == OwnerClientId) CoopSupplies.Instance?.ServerInteract(this, id);
        }
        public void RequestCrewAction(int action)
        { if (IsSpawned && IsOwner) CrewActionRpc(action); }
        [Rpc(SendTo.Server)]
        private void CrewActionRpc(int action, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || action < 0 || action > 2 || Time.unscaledTime < nextCrewAction) return;
            nextCrewAction = Time.unscaledTime + (action == 1 ? 3f : .5f);
            CoopSupplies.Instance?.ServerAction(this, action);
        }
        public void ServerFeedback(string message) { if (IsServer && IsSpawned) FeedbackRpc(message); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void TestFire()
        {
            if (IsSpawned && IsOwner)
                FireShotRpc(transform.position + Vector3.up * 1.65f, transform.forward);
        }

        public void TestReload()
        {
            if (IsSpawned && IsOwner) ReloadRpc();
        }
        public void TestAimFire(Vector3 point, bool invalidOrigin = false)
        {
            if (!IsSpawned || !IsOwner) return;
            Vector3 origin = transform.position + Vector3.up * 1.65f;
            FireShotRpc(invalidOrigin ? origin + Vector3.right * 10 : origin, (point - origin).normalized);
        }
#endif

        [Rpc(SendTo.Server)]
        private void ReloadRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Seat.Value >= 0 || IsDowned.Value ||
                Reloading.Value || CurrentAmmo.Value >= 7 || !HasReloadAmmo) return;
            Reloading.Value = true;
            reloadEndTime = Time.unscaledTime + 1.4f;
            FeedbackRpc("Перезарядка...");
        }

        [Rpc(SendTo.Server)]
        private void FireShotRpc(Vector3 origin, Vector3 direction, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Seat.Value >= 0 || IsDowned.Value ||
                Reloading.Value || CurrentAmmo.Value <= 0) return;
            if (!CoopVehicle.Finite(origin.x) || !CoopVehicle.Finite(origin.y) || !CoopVehicle.Finite(origin.z) ||
                !CoopVehicle.Finite(direction.x) || !CoopVehicle.Finite(direction.y) || !CoopVehicle.Finite(direction.z) ||
                direction.sqrMagnitude < .9f || direction.sqrMagnitude > 1.1f ||
                Vector3.Distance(origin, transform.position + Vector3.up * 1.65f) > 1f || Time.unscaledTime < nextServerShot) return;
            nextServerShot = Time.unscaledTime + .20f;
            CurrentAmmo.Value--;
            origin = transform.position + Vector3.up * 1.65f;
            direction.Normalize();

            Ray ray = new Ray(origin, direction);
            if (Physics.Raycast(ray, out RaycastHit hit, 55f, ~0, QueryTriggerInteraction.Ignore))
            {
                var zombie = hit.collider.GetComponentInParent<EncounterZombie>();
                if (zombie != null && !zombie.IsDead)
                {
                    bool headshot = hit.point.y > (zombie.transform.position.y + 1.35f);
                    float dmg = headshot ? 70f : 35f;
                    zombie.TakeDamage(dmg);
                    ShotImpactRpc(hit.point, hit.normal, true, headshot);
                    if (headshot) FeedbackRpc("💥 Точный выстрел в голову!");
                }
                else
                {
                    var obstacle = hit.collider.GetComponentInParent<DestructibleTrackObstacle>();
                    if (obstacle != null)
                    {
                        obstacle.TakeDamage(35f);
                        ShotImpactRpc(hit.point, hit.normal, false, false);
                    }
                    else
                    {
                        var dmg = hit.collider.GetComponentInParent<IDamageable>();
                        if (dmg != null && hit.collider.GetComponentInParent<CoopPlayer>() == null)
                        {
                            dmg.TakeDamage(35f);
                        }
                        ShotImpactRpc(hit.point, hit.normal, false, false);
                    }
                }
            }

            // Шум выстрела привлекает зомби
            Collider[] alertCols = Physics.OverlapSphere(origin, 35f);
            for (int i = 0; i < alertCols.Length; i++)
            {
                var z = alertCols[i].GetComponentInParent<EncounterZombie>();
                if (z != null && !z.IsDead)
                {
                    z.AlertToSound(origin);
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ShotImpactRpc(Vector3 point, Vector3 normal, bool isZombie, bool headshot)
        {
            if (isZombie)
            {
                RogueDrive.Audio.AudioManager.Instance?.PlayImpact(headshot ? 1.0f : 0.7f);
                GameObject blood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blood.name = "CoopBlood_FX";
                blood.transform.position = point;
                blood.transform.localScale = Vector3.one * (headshot ? 0.35f : 0.22f);
                Destroy(blood.GetComponent<Collider>());
                var r = blood.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                    m.color = new Color(0.65f, 0.05f, 0.05f, 0.95f);
                    r.sharedMaterial = m;
                }
                Destroy(blood, 0.22f);
            }
            else
            {
                RogueDrive.Audio.AudioManager.Instance?.PlayRicochet();
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "CoopSpark_FX";
                spark.transform.position = point + normal * 0.04f;
                spark.transform.localScale = Vector3.one * 0.12f;
                Destroy(spark.GetComponent<Collider>());
                var r = spark.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                    m.color = new Color(1f, 0.9f, 0.4f, 1f);
                    r.sharedMaterial = m;
                }
                Destroy(spark, 0.14f);
            }
        }

        public void ServerTakeDamage(float amount)
        {
            if (!IsServer || Health.Value <= 0f || !CoopVehicle.Finite(amount) || amount <= 0f) return;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            if (Health.Value <= 0f && !IsDowned.Value)
            {
                IsDowned.Value = true;
                bleedoutReported = false;
                CoopVehicle.Instance?.ApplyInput(OwnerClientId, Vector2.zero, true);
                BleedoutTimer.Value = 30f;
                FeedbackRpc("⚠ ВЫ ТЯЖЕЛО РАНЕНЫ! Ползите к напарнику за помощью!");
            }
        }

        public void RequestSeat(int seat)
        {
            if (IsSpawned && IsOwner) SeatRpc(seat);
        }

        [Rpc(SendTo.Server)]
        private void SeatRpc(int seat, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Time.unscaledTime < nextSeatRequest) return;
            nextSeatRequest = Time.unscaledTime + .15f;
            string result = CoopVehicle.Instance != null ? CoopVehicle.Instance.ChangeSeat(this, seat) : "Машина ещё загружается.";
            FeedbackRpc(result);
        }

        [Rpc(SendTo.Owner)]
        private void FeedbackRpc(string message) { Feedback = message; feedbackUntil = Time.unscaledTime + 3f; }

        public void ServerSetSeat(int seat, Vector3 position)
        {
            if (!IsServer) return;
            controller.enabled = false;
            Seat.Value = seat;
            transform.position = position;
            verticalVelocity = 0; movement = Vector2.zero;
            controller.enabled = seat < 0;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Reloading.Value && Time.unscaledTime >= reloadEndTime)
            {
                int loaded = Mathf.Min(7 - CurrentAmmo.Value, ReserveAmmo.Value);
                ReserveAmmo.Value -= loaded;
                loaded += CoopSupplies.Instance?.ServerTakeAmmo(7 - CurrentAmmo.Value - loaded) ?? 0;
                CurrentAmmo.Value += loaded;
                Reloading.Value = false;
                FeedbackRpc($"Пистолет заряжен: {CurrentAmmo.Value}/7");
            }
            if (CoopVehicle.Instance != null && CoopVehicle.Instance.Transitioning.Value) return;
            if (Seat.Value < 0 && transform.position.y < -20f &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GarageScene")
            {
                ServerSetSeat(-1, new Vector3(5.5f, 1.05f, -7.7f));
                FeedbackRpc("Персонаж возвращён на безопасную площадку бункера.");
            }

            // Обработка истекания кровью на сервере
            if (IsDowned.Value)
            {
                BleedoutTimer.Value = Mathf.Max(0f, BleedoutTimer.Value - Time.fixedDeltaTime);
                if (BleedoutTimer.Value <= 0f && !bleedoutReported)
                {
                    bleedoutReported = true;
                    FeedbackRpc("Погиб от потери крови...");
                }
            }

            if (Seat.Value >= 0)
            {
                if (CoopVehicle.Instance != null) transform.SetPositionAndRotation(CoopVehicle.Instance.SeatPosition(Seat.Value), CoopVehicle.Instance.transform.rotation);
                return;
            }

            if (Time.unscaledTime - receivedAt > .35f) movement = Vector2.zero;
            if (IsDowned.Value && BleedoutTimer.Value <= 0f) movement = Vector2.zero;
            if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2;
            verticalVelocity += Physics.gravity.y * Time.fixedDeltaTime;

            Vector3 direction = transform.right * movement.x + transform.forward * movement.y;
            float speed = IsDowned.Value ? 0.9f : (sprint ? 5.2f : 3.2f);
            controller.Move((direction * speed + Vector3.up * verticalVelocity) * Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;

            // Силуэт напарника: при ранении ложится на четвереньки
            if (avatar != null)
            {
                avatar.gameObject.SetActive(!IsOwner && Seat.Value < 0);
                if (IsDowned.Value)
                {
                    avatar.localScale = new Vector3(0.75f, 0.35f, 0.75f);
                    avatar.localPosition = Vector3.up * 0.25f;
                }
                else
                {
                    avatar.localScale = new Vector3(0.65f, 0.9f, 0.65f);
                    avatar.localPosition = Vector3.up * 0.9f;
                }
            }

            if (!IsOwner || localCamera == null) return;
            var car = CoopVehicle.Instance;
            Vector3 focus;
            Quaternion look;

            if (Seat.Value >= 0 && car != null)
            {
                focus = car.transform.position + Vector3.up * 1.5f;
                look = Seat.Value == 0 ? Quaternion.Euler(16, car.transform.eulerAngles.y, 0) : Quaternion.Euler(pitch + 12, yaw, 0);
                Vector3 offset = look * new Vector3(0, 1.5f, -7);
                Vector3 desired = focus + offset;
                  Vector3 cameraRay = desired - focus;
                  float cameraDistance = cameraRay.magnitude;
                  foreach (var hit in Physics.RaycastAll(focus, cameraRay.normalized, cameraDistance, ~0, QueryTriggerInteraction.Ignore))
                  {
                      if (hit.transform.IsChildOf(car.transform) || hit.collider.GetComponentInParent<CoopPlayer>() != null) continue;
                      cameraDistance = Mathf.Min(cameraDistance, Mathf.Max(.25f, hit.distance - .3f));
                  }
                  desired = focus + cameraRay.normalized * cameraDistance;
                localCamera.transform.position = desired;
                localCamera.transform.LookAt(focus + look * Vector3.forward * 3f);
            }
            else
            {
                // Раненая камера ползет у самой земли
                float eyeHeight = IsDowned.Value ? 0.45f : 1.65f;
                localCamera.transform.SetPositionAndRotation(transform.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0));
            }
        }
    }
}
