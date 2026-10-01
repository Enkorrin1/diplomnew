using System.Collections;
using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Entry scenes keep their existing standalone rigs. Streamed scenes contain no rig.</summary>
    public sealed class JourneyStreamEntry : MonoBehaviour
    {
        [SerializeField] JourneyStreamCatalog catalog;
        [SerializeField] int segmentIndex;
        [SerializeField] GameObject world;
        [SerializeField] StageRoute localRoute;
        public void Configure(JourneyStreamCatalog data, int index, GameObject root, StageRoute route)
        { catalog = data; segmentIndex = index; world = root; localRoute = route; }

        public void Begin(ArcadeCarController car)
        {
            if (SeamlessJourneyStream.Instance != null || catalog == null || car == null) return;
            SeamlessJourneyStream.Begin(this, catalog, segmentIndex, world, localRoute, car);
        }

        IEnumerator Start()
        {
            while (SeamlessJourneyStream.Instance == null)
            {
                if (world != null && world.activeInHierarchy && GarageSceneExitCinematic.Pending == null)
                {
                    // The offline rig can awaken before NGO spawns the shared car.
                    // Bind streaming to the actual expedition actor on both peers.
                    var car = Coop.CoopSession.Instance?.Busy == true
                        ? Coop.CoopVehicle.Instance != null && Coop.CoopVehicle.Instance.IsSpawned
                            ? Coop.CoopVehicle.Instance.GetComponent<ArcadeCarController>() : null
                        : FindFirstObjectByType<ArcadeCarController>();
                    if (car != null) { Begin(car); yield break; }
                }
                yield return null;
            }
        }
    }
}
