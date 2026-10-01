using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public sealed class JourneyStationConsole:MonoBehaviour,IGarageInteractable
    {
        [SerializeField] JourneyServiceStation station;
        public void Configure(JourneyServiceStation value)=>station=value;
        public string GetPromptText()=>station!=null?station.GetPromptText():"СТО";
        public bool CanInteract()=>station!=null;
        public void Interact(GaragePlayerController player)=>station?.Interact(player);
    }
}
