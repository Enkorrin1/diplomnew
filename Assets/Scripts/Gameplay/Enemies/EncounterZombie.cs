using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.AI;

namespace RogueDrive.Gameplay
{
    /// <summary>A persistent roadside walker. Does not use the legacy road chase/contact attack.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class EncounterZombie : EnemyBase
    {
        public enum BehaviourState { Idle, Alert, Chase, Windup, Recovery, Search, Return, Dead, Stagger }

        [Header("Encounter perception")]
        [SerializeField, Min(1f)] float sightDistance = 18f;
        [SerializeField, Range(30f, 360f)] float fieldOfView = 120f;
        [SerializeField, Min(1f)] float leashDistance = 55f;
        [SerializeField, Min(.1f)] float searchDuration = 5f;
        [Header("Readable melee")]
        [SerializeField, Min(.2f)] float attackReach = 1.35f;
        [SerializeField, Min(.2f)] float windupDuration = .8f;
        [SerializeField, Min(.2f)] float recoveryDuration = 1.1f;

        public BehaviourState State { get; private set; }
        public Transform CurrentTarget => target;
        public Vector3 HomePosition => home;
        public int SuccessfulHits { get; private set; }

        NavMeshAgent agent;
        CapsuleCollider hitCollider;
        Transform target;
        GaragePlayerController pedestrian;
        GameRunController run;
        Vector3 home, lastKnown;
        float stateTime, perceptionTime, pathTime, resolveTime, lastSeen = -100f, ramTime = -100f;
        bool initialized, visible;
        Vector3 carVelocityBeforePhysics;
        string crewId;
        public string CrewId => crewId ?? (crewId = Coop.CoopEnemies.Identity(this));
        public bool CrewInitialized => initialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development fixture pauses decisions while keeping death presentation running.
        public bool TestHold { get; set; }
#endif
        bool CrewActive => Coop.CoopSession.Instance != null && Coop.CoopSession.Instance.Busy;
        bool CrewReplica => CrewActive &&
            (Coop.CoopSession.Instance == null || !Coop.CoopSession.Instance.Manager.IsServer);

        public Coop.CoopEnemies.Snapshot CaptureCrewState(string id) => new Coop.CoopEnemies.Snapshot
        {
            id = id, position = transform.position, rotation = transform.rotation, health = currentHealth,
            age = stateTime, state = (int)State, hits = SuccessfulHits, active = gameObject.activeInHierarchy,
            speed = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0
        };
        public void RestoreCrewHistory(Coop.CoopEnemies.Snapshot s)
        {
            currentHealth = s.health; State = (BehaviourState)s.state; stateTime = s.age; SuccessfulHits = s.hits;
            transform.SetPositionAndRotation(s.position, s.rotation);
            if (currentHealth <= 0) { if(hitCollider != null) hitCollider.enabled = false; if(agent != null) agent.enabled = false; gameObject.SetActive(false); }
        }
        void UpdateReplica()
        {
            if (agent != null) agent.enabled = false;
            if (Coop.CoopEnemies.Instance == null || !Coop.CoopEnemies.Instance.TryState(CrewId, out var s)) return;
            bool firstDeath = !IsDead && s.health <= 0;
            bool attack = State != BehaviourState.Windup && s.state == (int)BehaviourState.Windup;
            if (s.health < currentHealth && !firstDeath) mecanimController?.TriggerHit();
            currentHealth = s.health; State = (BehaviourState)s.state; stateTime = s.age;
            SuccessfulHits = s.hits;
            float blend = 1 - Mathf.Exp(-25 * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Distance(transform.position, s.position) > 3 ? s.position :
                Vector3.Lerp(transform.position, s.position, blend), Quaternion.Slerp(transform.rotation, s.rotation, blend));
            hitCollider.enabled = !IsDead && s.active;
            if (spawnedVisualInstance != null) spawnedVisualInstance.SetActive(s.active);
            mecanimController?.SetSpeed(s.speed);
            if (attack) mecanimController?.TriggerAttack(windupDuration + .25f);
            if (firstDeath) mecanimController?.TriggerDeath();
            if (IsDead && stateTime >= 2.5f) gameObject.SetActive(false);
        }
        public override void TakeDamage(float amount, float slowAmount = 0, float burnDmg = 0)
        {
            if (CrewReplica || !Coop.CoopVehicle.Finite(amount) || amount <= 0) return;
            base.TakeDamage(amount, slowAmount, burnDmg);
        }

        void FixedUpdate()
        {
            if (CrewReplica) return;
            // Collision callbacks run after the solver, which may already have stopped the car.
            var body = playerCar != null ? playerCar.GetComponent<Rigidbody>() : null;
            carVelocityBeforePhysics = body != null ? body.GetPointVelocity(transform.position) : Vector3.zero;
        }

        protected override void Awake()
        {
            // Prefab values remain editable; do not replace them on every spawn.
            base.Awake();
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false; // Wait until the scene's NavMeshSurface has registered its data.
            hitCollider = GetComponent<CapsuleCollider>();
            home = transform.position;
            _ = CrewId;
            agent.speed = baseSpeed;
            agent.angularSpeed = 240f;
            agent.acceleration = 9f;
            agent.stoppingDistance = .25f;
            agent.radius = .4f;
            agent.height = 1.85f;
            agent.baseOffset = 0f;
            hitCollider.radius = .4f;
            hitCollider.height = 1.85f;
            hitCollider.center = Vector3.up * .925f;
            hitCollider.isTrigger = false;
        }

        protected override void OnEnable()
        {
            if (!initialized)
            {
                currentHealth = maxHealth;
                initialized = true;
                State = BehaviourState.Idle;
                if (CrewActive && Coop.CoopEnemies.Instance != null && Coop.CoopEnemies.Instance.TryState(CrewId, out var prior))
                    RestoreCrewHistory(prior);
            }
            // Re-enabling a site preserves health/death; it never creates a fresh encounter.
            if (IsDead) { gameObject.SetActive(false); return; }
            perceptionTime = resolveTime = pathTime = 0f;
        }

        void OnDisable()
        {
            if (agent != null) agent.enabled = false;
        }

        protected override void Update()
        {
            if (CrewReplica) { UpdateReplica(); return; }
            if (CrewActive && (Coop.CoopVehicle.Instance == null || Coop.CoopVehicle.Instance.Transitioning.Value)) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stateTime += dt;
            if (IsDead)
            {
                if (stateTime >= 2.5f) gameObject.SetActive(false);
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestHold) { StopMoving(); return; }
#endif
            if (Time.time >= resolveTime)
            {
                resolveTime = Time.time + .5f;
                if (run == null) run = FindFirstObjectByType<GameRunController>();
                if (playerCar == null) playerCar = FindFirstObjectByType<ArcadeCarController>();
                if (pedestrian == null || !pedestrian.isActiveAndEnabled)
                    pedestrian = FindFirstObjectByType<GaragePlayerController>();
                if (CrewActive) { playerCar = Coop.CoopVehicle.Instance?.GetComponent<ArcadeCarController>(); pedestrian = null; run = null; }
            }
            if (run != null && run.IsGameOver) { StopMoving(); return; }
            if (!agent.enabled)
            {
                // Scene activation order differs between Editor and player builds. Register only
                // after nearby navigation exists, including when returning from deactivation.
                if (!NavMesh.SamplePosition(transform.position, out var start, .75f, agent.areaMask)) return;
                transform.position = start.position;
                agent.enabled = true;
            }
            if (!agent.isOnNavMesh) return; // Never teleport to an unrelated navigation island.
            if (burnTimer > 0f)
            {
                float burning = Mathf.Min(dt, burnTimer);
                burnTimer -= dt;
                TakeDamage(burnDamagePerSec * burning);
                if (IsDead) return;
            }
            slowTimer = Mathf.Max(0f, slowTimer - dt);
            if (slowTimer <= 0f) slowFactor = 0f;
            agent.speed = baseSpeed * (1f - slowFactor);

            if (State != BehaviourState.Windup && State != BehaviourState.Recovery && State != BehaviourState.Stagger && Time.time >= perceptionTime)
            {
                perceptionTime = Time.time + .2f;
                Perceive();
            }

            switch (State)
            {
                case BehaviourState.Stagger:
                    StopMoving();
                    if (stateTime >= .9f) ChangeState(BehaviourState.Search);
                    break;
                case BehaviourState.Idle:
                    StopMoving();
                    break;
                case BehaviourState.Alert:
                    StopMoving(); Face(lastKnown, dt);
                    if (stateTime >= .45f) ChangeState(BehaviourState.Chase);
                    break;
                case BehaviourState.Chase:
                    if (Vector3.Distance(home, transform.position) > leashDistance || IsProtected(lastKnown))
                    { Forget(); ChangeState(BehaviourState.Return); break; }
                    if (!visible || !ValidTarget(target))
                    { ChangeState(BehaviourState.Search); break; }
                    lastKnown = TargetPoint(target);
                    Face(lastKnown, dt);
                    if (InReach(target) && HasLineOfSight(target))
                    {
                        StopMoving();
                        ChangeState(BehaviourState.Windup);
                        mecanimController?.TriggerAttack(windupDuration + .25f);
                    }
                    else MoveTo(lastKnown);
                    break;
                case BehaviourState.Windup:
                    StopMoving();
                    if (stateTime >= windupDuration)
                    {
                        // Revalidate the original target at impact. Boarding/dodging cancels the hit.
                        if (ValidTarget(target) && InReach(target) && HasLineOfSight(target)) ApplyMelee();
                        ChangeState(BehaviourState.Recovery);
                    }
                    break;
                case BehaviourState.Recovery:
                    StopMoving();
                    if (stateTime >= recoveryDuration) ChangeState(BehaviourState.Chase);
                    break;
                case BehaviourState.Search:
                    MoveTo(lastKnown);
                    if (stateTime >= searchDuration)
                    { Forget(); ChangeState(BehaviourState.Return); }
                    break;
                case BehaviourState.Return:
                    MoveTo(home);
                    if (Vector3.Distance(transform.position, home) < .65f)
                        ChangeState(BehaviourState.Idle);
                    break;
            }
            mecanimController?.SetSpeed(agent.velocity.magnitude);
        }

        public bool TryShove(Vector3 source)
        {
            if (CrewReplica) return false;
            if (IsDead || !agent.enabled || !agent.isOnNavMesh || IsProtected(transform.position)) return false;
            StopMoving();
            Vector3 direction = transform.position - source; direction.y = 0f;
            direction.Normalize();
            float distance = 1.2f;
            Vector3 origin = transform.position;
            foreach (var hit in Physics.CapsuleCastAll(origin + Vector3.up * .45f, origin + Vector3.up * 1.4f,
                .38f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .05f));
            Vector3 destination = origin + direction * distance;
            if (NavMesh.Raycast(origin, destination, out var edge, agent.areaMask)) destination = edge.position;
            agent.Move(destination - origin);
            ChangeState(BehaviourState.Stagger);
            return true;
        }

        void Perceive()
        {
            Transform seen = null;
            if (pedestrian != null && CanSee(pedestrian.transform)) seen = pedestrian.transform;
            if (seen == null)
            {
                foreach (var coop in FindObjectsByType<RogueDrive.Gameplay.Coop.CoopPlayer>(FindObjectsSortMode.None))
                {
                    if (coop.Seat.Value < 0 && !coop.IsDowned.Value && CanSee(coop.transform))
                    {
                        seen = coop.transform;
                        break;
                    }
                }
            }
            if (seen == null && playerCar != null && CanSee(playerCar.transform)) seen = playerCar.transform;
            if (seen == null && RogueDrive.Gameplay.Coop.CoopVehicle.Instance != null && CanSee(RogueDrive.Gameplay.Coop.CoopVehicle.Instance.transform))
                seen = RogueDrive.Gameplay.Coop.CoopVehicle.Instance.transform;
            visible = seen != null;
            if (visible)
            {
                target = seen;
                lastKnown = TargetPoint(seen);
                lastSeen = Time.time;
                if (State == BehaviourState.Idle || State == BehaviourState.Return)
                    ChangeState(BehaviourState.Alert);
                else if (State == BehaviourState.Search) ChangeState(BehaviourState.Chase);
                return;
            }
            // Seeing boarding connects the remembered pedestrian to the nearby vehicle only.
            if (target != null && !target.gameObject.activeInHierarchy && playerCar != null
                && Time.time - lastSeen < .4f && Vector3.Distance(target.position, playerCar.transform.position) < 5f
                && CanSee(playerCar.transform))
            {
                target = playerCar.transform; lastKnown = TargetPoint(target); visible = true; return;
            }
            if (State == BehaviourState.Return) return;
            if (TryHear(out Vector3 sound))
            {
                lastKnown = sound;
                target = null;
                if (State == BehaviourState.Idle || State == BehaviourState.Chase)
                    ChangeState(BehaviourState.Search);
            }
        }

        public void AlertToSound(Vector3 soundPos)
        {
            if (CrewReplica) return;
            if (IsDead) return;
            lastKnown = soundPos;
            target = null;
            if (State == BehaviourState.Idle || State == BehaviourState.Return)
                ChangeState(BehaviourState.Alert);
            else if (State == BehaviourState.Search)
                ChangeState(BehaviourState.Chase);
        }

        bool CanSee(Transform candidate)
        {
            if (!ValidTarget(candidate) || Vector3.Distance(candidate.position, home) > leashDistance) return false;

            // Если проверяем автомобиль: работающий двигатель слышен дальше и во все стороны
            var car = candidate.GetComponent<ArcadeCarController>();
            if (car != null)
            {
                var workshop = car.GetComponent<VehicleWorkshop>();
                bool engineRunning = workshop != null && !workshop.EngineStopped;
                float carDetectDist = engineRunning ? 32f : sightDistance;
                Vector3 carDelta = TargetPoint(candidate) - (transform.position + Vector3.up);
                if (carDelta.sqrMagnitude > carDetectDist * carDetectDist) return false;
                if (engineRunning) return HasLineOfSight(candidate);
            }

            Vector3 delta = TargetPoint(candidate) - (transform.position + Vector3.up);
            if (delta.sqrMagnitude > sightDistance * sightDistance) return false;
            delta.y = 0f;
            // Within arm's reach the walker can notice something behind it as well.
            if (delta.sqrMagnitude > 4f && Vector3.Angle(transform.forward, delta) > fieldOfView * .5f) return false;
            return HasLineOfSight(candidate);
        }

        bool TryHear(out Vector3 sound)
        {
            sound = default;
            if (pedestrian != null && ValidTarget(pedestrian.transform))
            {
                var cc = pedestrian.GetComponent<CharacterController>();
                float speed = cc != null ? cc.velocity.magnitude : 0f;
                float radius = pedestrian.IsCrouching ? 1.5f : speed > 4f ? 10f : 4f;
                if (speed > .5f && Vector3.Distance(transform.position, pedestrian.transform.position) < radius)
                { sound = pedestrian.transform.position; return true; }
            }
            foreach (var coop in FindObjectsByType<RogueDrive.Gameplay.Coop.CoopPlayer>(FindObjectsSortMode.None))
            {
                if (coop.Seat.Value < 0 && !coop.IsDowned.Value && Vector3.Distance(transform.position, coop.transform.position) < 8f)
                { sound = coop.transform.position; return true; }
            }
            if (playerCar != null && ValidTarget(playerCar.transform))
            {
                var workshop = playerCar.GetComponent<VehicleWorkshop>();
                if (workshop != null && !workshop.EngineStopped
                    && Vector3.Distance(transform.position, playerCar.transform.position) < 22f)
                { sound = playerCar.transform.position; return true; }
            }
            return false;
        }

        bool ValidTarget(Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || IsProtected(candidate.position)) return false;
            var coopPerson = candidate.GetComponent<RogueDrive.Gameplay.Coop.CoopPlayer>();
            if (coopPerson != null) return coopPerson.IsSpawned && coopPerson.Seat.Value < 0 && !coopPerson.IsDowned.Value && coopPerson.Health.Value > 0;
            if (CrewActive) return candidate.GetComponent<Coop.CoopVehicle>() != null;
            var person = candidate.GetComponent<GaragePlayerController>();
            return person == null || (person.isActiveAndEnabled && person.GetComponent<PlayerFieldNeeds>()?.IsDead != true);
        }

        static bool IsProtected(Vector3 point) => WorkshopServiceZone.Find(point)?.Safe == true;

        Vector3 TargetPoint(Transform candidate)
        {
            var coopPerson = candidate.GetComponent<RogueDrive.Gameplay.Coop.CoopPlayer>();
            if (coopPerson != null) return candidate.position + Vector3.up * (coopPerson.IsDowned.Value ? .4f : 1f);
            var person = candidate.GetComponent<GaragePlayerController>();
            if (person != null) return candidate.position + Vector3.up * (person.IsCrouching ? .65f : 1f);
            var hull = candidate.GetComponent<BoxCollider>();
            return hull != null ? hull.ClosestPoint(transform.position + Vector3.up) : candidate.position + Vector3.up;
        }

        bool InReach(Transform candidate)
        {
            Vector3 delta = TargetPoint(candidate) - (transform.position + Vector3.up);
            return delta.sqrMagnitude <= attackReach * attackReach;
        }

        bool HasLineOfSight(Transform candidate)
        {
            Vector3 origin = transform.position + Vector3.up * 1.45f;
            Vector3 delta = TargetPoint(candidate) - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(candidate)) continue;
                return false;
            }
            return true;
        }

        void MoveTo(Vector3 point)
        {
            if (Time.time < pathTime) return;
            pathTime = Time.time + .3f;
            point.y = transform.position.y;
            if (IsProtected(point) || !NavMesh.SamplePosition(point, out var hit, 2.5f, agent.areaMask))
            { StopMoving(); return; }
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            mecanimController?.SetSpeed(0f);
        }

        void Face(Vector3 point, float dt)
        {
            Vector3 delta = point - transform.position; delta.y = 0f;
            if (delta.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 240f * dt);
        }

        void ApplyMelee()
        {
            var person = target.GetComponent<GaragePlayerController>();
            if (person != null) PlayerFieldNeeds.For(person).TakeDamage(contactDamage);
            else if (target.GetComponent<RogueDrive.Gameplay.Coop.CoopPlayer>() is RogueDrive.Gameplay.Coop.CoopPlayer coopPerson)
                coopPerson.ServerTakeDamage(contactDamage);
            else if (target.GetComponent<ArcadeCarController>() is ArcadeCarController car) DamageCar(car, contactDamage);
            SuccessfulHits++;
            RogueDrive.Audio.AudioManager.Instance?.PlayImpact();
        }

        private static float lastCarAlertBannerTime = -10f;

        void DamageCar(ArcadeCarController car, float amount)
        {
            if (CrewActive)
            {
                var crew = car.GetComponent<Coop.CoopVehicle>();
                if (crew != null && crew.IsServer && !crew.Transitioning.Value)
                    crew.Hull.Value = Mathf.Max(0, crew.Hull.Value - amount);
                return;
            }
            var modular = car.GetComponent<VehicleModularState>();
            if(modular?.Workshop?.Sheltered??false)return;
            if (modular != null)
            {
                Vector3 localPos = car.transform.InverseTransformDirection(transform.position - car.transform.position);
                modular.ApplyComponentDamage(amount, localPos);

                // Если зомби бьёт сбоку у колеса — дополнительно повреждаем шину
                if (Mathf.Abs(localPos.x) > 0.55f)
                {
                    int tireIdx = (localPos.z >= 0 ? 0 : 2) + (localPos.x > 0 ? 1 : 0);
                    modular.DamageTire(tireIdx, 0.08f);
                }
            }

            (car.Run != null ? car.Run : run)?.TakeChassisDamage(amount);

            if (Time.time - lastCarAlertBannerTime > 4.5f)
            {
                lastCarAlertBannerTime = Time.time;
                GarageInteractionUI.Instance?.ShowBanner("⚠ Зомби атакуют припаркованный автомобиль!", 3.5f);
            }
        }

        void Forget() { target = null; visible = false; }
        void ChangeState(BehaviourState next) { State = next; stateTime = 0f; pathTime = 0f; }

        protected override void OnTriggerEnter(Collider other) { }
        protected override void OnCollisionEnter(Collision collision)
        {
            if (CrewReplica) return;
            var car = collision.gameObject.GetComponentInParent<ArcadeCarController>();
            if (IsDead || car == null || Time.time - ramTime < .6f || (run != null && run.IsGameOver)) return;
            var body = car.GetComponent<Rigidbody>();
            if (body == null || IsProtected(car.transform.position)) return;
            Vector3 direction = transform.position - car.transform.position; direction.y = 0f;
            float closingSpeed = Vector3.Dot(body.GetPointVelocity(transform.position), direction.normalized);
            if (car == playerCar)
                closingSpeed = Mathf.Max(closingSpeed, Vector3.Dot(carVelocityBeforePhysics, direction.normalized));
            if (closingSpeed < 2f) return; // A parked car is a melee target, never an instant kill.
            ramTime = Time.time;
            TakeDamage(closingSpeed >= 10f ? maxHealth : closingSpeed * 5f);
            DamageCar(car, Mathf.Clamp(closingSpeed * .35f, 1f, 7f));
            car.GetComponent<VFX.VehicleBloodSplatterVFX>()?.RegisterZombieRam(transform.position, Vector3.up, closingSpeed * 3.6f);
        }

        protected override void Die()
        {
            currentHealth = 0f;
            Forget(); StopMoving();
            hitCollider.enabled = false;
            agent.enabled = false;
            mecanimController?.TriggerDeath();
            ChangeState(BehaviourState.Dead);
        }

        void LateUpdate()
        {
            // Readable anticipation even when a model has no compatible animation controller.
            if (spawnedVisualInstance == null) return;
            if (IsDead)
            {
                spawnedVisualInstance.transform.localRotation = Quaternion.Euler(-85f * Mathf.Clamp01(stateTime / .6f), 0f, 0f);
                return;
            }
            if (State == BehaviourState.Stagger)
                spawnedVisualInstance.transform.localRotation = Quaternion.Euler(-25f * (1f - Mathf.Clamp01(stateTime / .9f)), 0f, 0f);
            else if (State == BehaviourState.Windup)
                spawnedVisualInstance.transform.localRotation = Quaternion.Euler(-18f * Mathf.Clamp01(stateTime / windupDuration), 0f, 0f);
            else if (State == BehaviourState.Recovery)
                spawnedVisualInstance.transform.localRotation = Quaternion.Euler(20f * (1f - Mathf.Clamp01(stateTime / recoveryDuration)), 0f, 0f);
            else spawnedVisualInstance.transform.localRotation = Quaternion.identity;
        }
    }
}
