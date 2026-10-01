using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Character health is independent of the vehicle. Food/water remain legacy save fields.</summary>
    public sealed class PlayerFieldNeeds : MonoBehaviour
    {
        [SerializeField,Range(0,100)] float health=100,food=100,water=100;
        void Start() { if (GetComponent<RogueDrive.UI.PlayerVitalsHUD>() == null) gameObject.AddComponent<RogueDrive.UI.PlayerVitalsHUD>(); }
        public float Health=>health;
        public bool IsDead=>health<=0f;
        public event System.Action Died;
        public float Food=>food;
        public float Water=>water;
        public string Summary=>$"Здоровье {health:0}%";
        public static PlayerFieldNeeds For(GaragePlayerController player)
        {return player==null?null:player.GetComponent<PlayerFieldNeeds>()??player.gameObject.AddComponent<PlayerFieldNeeds>();}
        // Kept for older callers; hunger and thirst are not part of the survival loop.
        public void Advance(float seconds)
        { }
        public void TakeDamage(float amount)
        {
            if(IsDead||amount<=0f||float.IsNaN(amount)||float.IsInfinity(amount))return;
            if(gameObject.activeInHierarchy&&(WorkshopServiceZone.Find(transform.position)?.Safe??false))return;
            var run=FindFirstObjectByType<GameRunController>();
            if(run!=null&&run.IsGameOver)return;

            var downed = GetComponent<PlayerDownedState>();
            if (health - amount <= 0f && downed != null && !downed.IsDowned)
            {
                health = 1f;
                downed.EnterDowned();
                return;
            }

            health=Mathf.Max(0f,health-amount);
            if(!IsDead)return;
            ForceDie();
        }

        public void ForceDie()
        {
            health = 0f;
            GetComponent<GaragePlayerController>()?.SetMovementLocked(true);
            var run = FindFirstObjectByType<GameRunController>();
            run?.ReportPlayerDeath();
            Died?.Invoke();
        }
        public bool Heal(float amount){if(IsDead||health>=99.99f||amount<=0f||float.IsNaN(amount)||float.IsInfinity(amount))return false;health=Mathf.Min(100,health+amount);return true;}
        public bool Eat(float amount){if(food>=99.99f)return false;food=Mathf.Min(100,food+amount);return true;}
        public bool Drink(float amount){if(water>=99.99f)return false;water=Mathf.Min(100,water+amount);return true;}
    }
}
