using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Огнестрельное оружие выживальщика (пистолет / револьвер).
    /// Хранит характеристики выстрела, текущий магазин патронов и время перезарядки.
    /// </summary>
    [RequireComponent(typeof(PhysicsProp))]
    public sealed class FirearmWeapon : MonoBehaviour
    {
        [Header("Weapon Parameters")]
        [SerializeField] private float damage = 35f;
        [SerializeField] private float range = 55f;
        [SerializeField] private float fireRate = 0.25f;
        [SerializeField] private int maxMagazine = 7;
        [SerializeField] private int currentAmmo = 7;
        [SerializeField] private float reloadDuration = 1.4f;

        [Header("Effects")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0.05f, 0.22f);

        public float Damage => damage;
        public float Range => range;
        public float FireRate => fireRate;
        public int MaxMagazine => maxMagazine;
        public int CurrentAmmo => currentAmmo;
        public float ReloadDuration => reloadDuration;
        public Vector3 MuzzleOffset => muzzleOffset;

        public bool HasAmmo => currentAmmo > 0;

        public bool TryConsumeAmmo()
        {
            if (currentAmmo <= 0) return false;
            currentAmmo--;
            return true;
        }

        public int Reload(int reserveAmmo)
        {
            if (reserveAmmo <= 0) return 0;
            int needed = maxMagazine - currentAmmo;
            int loaded = Mathf.Min(Mathf.Max(0, needed), reserveAmmo);
            currentAmmo += loaded;
            return loaded;
        }

        public void FullReload()
        {
            currentAmmo = maxMagazine;
        }

        public void SetAmmo(int ammo)
        {
            currentAmmo = Mathf.Clamp(ammo, 0, maxMagazine);
        }
    }
}
