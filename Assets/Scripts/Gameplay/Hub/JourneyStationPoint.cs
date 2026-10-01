using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    public sealed class JourneyStationPoint : MonoBehaviour, IGarageInteractable
    {
        public enum Kind { Workbench, Trader }
        [SerializeField] JourneyServiceStation station;
        [SerializeField] Kind kind;
        public void Configure(JourneyServiceStation owner,Kind point){station=owner;kind=point;}
        public string GetPromptText()=>kind==Kind.Workbench?"[E] Верстак — осмотреть и улучшить машину":"[E] Приёмка — продать добычу";
        public bool CanInteract()=>station!=null&&station.Visited;
        public void Interact(GaragePlayerController player)
        {
            if(!CanInteract())return;
            JourneyStationExperienceUI.Open(station,kind==Kind.Workbench?JourneyStationExperienceUI.Page.Workshop:JourneyStationExperienceUI.Page.Trader);
        }
    }
}
