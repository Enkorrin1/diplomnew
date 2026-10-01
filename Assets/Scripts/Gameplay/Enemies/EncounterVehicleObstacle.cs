using UnityEngine;
using UnityEngine.AI;

namespace RogueDrive.Gameplay
{
    /// <summary>Fits navigation avoidance to the actual car hull, including while parked.</summary>
    [RequireComponent(typeof(NavMeshObstacle))]
    public sealed class EncounterVehicleObstacle : MonoBehaviour
    {
        void Awake()
        {
            var hull = GetComponent<BoxCollider>();
            var obstacle = GetComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            if (hull != null) { obstacle.center = hull.center; obstacle.size = hull.size; }
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = .5f;
        }
    }
}
