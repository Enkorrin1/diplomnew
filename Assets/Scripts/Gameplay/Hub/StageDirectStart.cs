using System.Collections;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Prepared garage actors used only when Stage1 is launched directly.</summary>
    public sealed class StageDirectStart : MonoBehaviour
    {
        [SerializeField] ArcadeCarController car;
        [SerializeField] GaragePlayerController player;
        [SerializeField] GarageDriveOutController driving;
        public ArcadeCarController Car => car;

        public void Configure(ArcadeCarController vehicle, GaragePlayerController pedestrian, GarageDriveOutController controller)
        { car = vehicle; player = pedestrian; driving = controller; }

        IEnumerator Start()
        {
            // Inventory and workshop Awake/Start must finish before boarding hides the pedestrian.
            yield return null;
            // A network scene load already carries the shared car and both players.
            // Solo boarding would detach and enable a second camera/audio listener.
            var session = RogueDrive.Gameplay.Coop.CoopSession.Instance;
            if (session != null && session.Manager != null && session.Manager.IsListening)
                yield break;
            if (car == null || player == null || driving == null)
            {
                Debug.LogError("[StageDirectStart] Prepared driving rig is incomplete.", this);
                yield break;
            }
            driving.StartDriveOut(player, car.gameObject);
            player.LockCursor(true);
        }
    }
}
