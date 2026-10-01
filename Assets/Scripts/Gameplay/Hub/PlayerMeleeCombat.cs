using UnityEngine;
using RogueDrive.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Local on-foot actions; targets are resolved once, at impact.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PlayerMeleeCombat : MonoBehaviour
    {
        GaragePlayerController player;
        PlayerPocketInventory pocket;
        MeleeWeapon swingingWeapon;
        float elapsed, readyAt, shoveReadyAt;
        bool pending, shove, impacted;
        public int SuccessfulStrikes { get; private set; }
        public int SuccessfulShoves { get; private set; }
        public bool IsAttacking => pending;

        void Start()
        {
            player = GetComponent<GaragePlayerController>();
            pocket = GetComponent<PlayerPocketInventory>();
            EnsureStarterWeapon();
        }

        public void EnsureStarterWeapon()
        {
            if (GetComponentInChildren<MeleeWeapon>(true) != null) return;
            if (pocket == null) pocket = GetComponent<PlayerPocketInventory>();
            var prefab = Resources.Load<GameObject>("Combat/Crowbar");
            if (prefab == null) return;
            var item = Instantiate(prefab, transform.position + transform.forward, Quaternion.identity).GetComponent<PhysicsProp>();
            var identity = item.GetComponent<GarageCheckpointItem>() ?? item.gameObject.AddComponent<GarageCheckpointItem>();
            if (string.IsNullOrEmpty(identity.id)) identity.id = "combat_crowbar";
            pocket.TryStorePhysical(item);
        }

        bool CanAct() => isActiveAndEnabled && player != null && player.isActiveAndEnabled
            && !player.IsMovementLocked && player.PlayerCamera != null
            && GetComponent<PlayerFieldNeeds>()?.IsDead != true && Time.timeScale > 0f
            && !InventoryWindowUI.BlockGameplayInput && Cursor.lockState == CursorLockMode.Locked
            && GetComponent<PlayerHandsInventory>()?.HasItem != true
            && FindFirstObjectByType<GameRunController>()?.IsGameOver != true
            && WorkshopServiceZone.Find(transform.position)?.Safe != true;

        public bool TryAttack(bool push = false)
        {
            if (!CanAct() || pending || Time.time < readyAt || (push && Time.time < shoveReadyAt)) return false;
            swingingWeapon = pocket.ActivePhysical != null ? pocket.ActivePhysical.GetComponent<MeleeWeapon>() : null;
            if (!push && swingingWeapon == null) return false;
            shove = push; pending = true; impacted = false; elapsed = 0f;
            readyAt = Time.time + (push ? .55f : .8f);
            if (push) shoveReadyAt = Time.time + 2f;
            return true;
        }

        void Update()
        {
            if (!CanAct()) { pending = false; return; }
            if (Input.GetMouseButtonDown(0)) TryAttack();
            if (Input.GetKeyDown(KeyCode.V)) TryAttack(true);
            if (!pending) return;
            if (!shove && (swingingWeapon == null || pocket.ActivePhysical == null
                || pocket.ActivePhysical.GetComponent<MeleeWeapon>() != swingingWeapon)) { pending = false; return; }
            elapsed += Time.deltaTime;
            if (!impacted && elapsed >= (shove ? .12f : .25f)) { impacted = true; ResolveImpact(); }
            if (elapsed >= (shove ? .55f : .8f)) pending = false;
        }

        void ResolveImpact()
        {
            var camera = player.PlayerCamera.transform;
            float reach = shove ? 1.6f : swingingWeapon.Reach;
            EnemyBase best = null;
            float nearest = float.MaxValue;
            foreach (var collider in Physics.OverlapSphere(camera.position, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead || WorkshopServiceZone.Find(enemy.transform.position)?.Safe == true) continue;
                Vector3 point = collider.ClosestPoint(camera.position);
                Vector3 delta = point - camera.position;
                if (delta.sqrMagnitude >= nearest || Vector3.Angle(camera.forward, delta) > 45f) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(camera.position, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(enemy.transform)) { blocked = true; break; }
                if (blocked) continue;
                best = enemy; nearest = delta.sqrMagnitude;
            }
            if (best == null) return;
            if (shove)
            {
                if (best is EncounterZombie walker && walker.TryShove(transform.position)) SuccessfulShoves++;
            }
            else { best.TakeDamage(swingingWeapon.Damage); SuccessfulStrikes++; }
            RogueDrive.Audio.AudioManager.Instance?.PlayImpact();
        }

        void LateUpdate()
        {
            if (pocket == null || pocket.ActivePhysical == null || !pocket.ActivePhysical.gameObject.activeInHierarchy) return;
            var weapon = pocket.ActivePhysical.GetComponent<MeleeWeapon>();
            if (weapon == null) return;
            float swing = pending ? Mathf.Sin(Mathf.Clamp01(elapsed / (shove ? .55f : .8f)) * Mathf.PI) : 0f;
            weapon.transform.localRotation = Quaternion.Euler(10f + swing * 70f, -20f - swing * 60f, 5f - swing * 35f);
            weapon.transform.localPosition += new Vector3(-swing * .18f, 0f, swing * .12f);
        }

        void OnDisable() { pending = false; swingingWeapon = null; }
    }
}
