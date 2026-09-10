using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Solid road furniture survives impact; light debris can be cleared.</summary>
    public sealed class TrackObstacle : MonoBehaviour
    {
        bool consumed;
        [SerializeField] private bool breakOnImpact;
        float nextImpactTime;

        public void Configure(bool breakable) => breakOnImpact = breakable;

        void Awake()
        {
            if (!breakOnImpact)
                foreach (Collider hitbox in GetComponents<Collider>()) hitbox.isTrigger = false;
        }

        public bool TryConsume()
        {
            if (consumed || Time.time < nextImpactTime)
                return false;

            nextImpactTime = Time.time + 0.75f;
            if (breakOnImpact)
            {
                consumed = true;
                gameObject.SetActive(false);
            }
            return true;
        }
    }
}
