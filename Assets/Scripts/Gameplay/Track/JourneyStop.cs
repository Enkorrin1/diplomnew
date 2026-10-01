using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>One visit per run; rewards remain physical objects and are never respawned here.</summary>
    public sealed class JourneyStop : MonoBehaviour, IGarageInteractable
    {
        [SerializeField] string title, supplies, directions;
        [SerializeField] float roadDistance, side;
        [SerializeField] bool safe;
        bool announced, visited;
        ArcadeCarController car;
        float nextCheck;
        public bool Visited => visited;
        public string Title => title;
        public void Configure(string name, string contents, string instructions, float distance, float turnSide, bool shelter=false)
        { title=name;supplies=contents;directions=instructions;roadDistance=distance;side=turnSide;safe=shelter; }
        void Update()
        {
            if(Time.time<nextCheck||StageRoute.Instance==null)return;
            nextCheck=Time.time+.4f;
            var route=StageRoute.Instance;
            float remaining=roadDistance-route.Progress;
            if(!announced&&remaining>180&&remaining<370)
            {
                announced=true;
                GarageInteractionUI.Instance?.ShowNotification($"{title} — съезд {(side>0?"направо":"налево")}\n{supplies}",6f);
            }
            if(car==null)car=FindFirstObjectByType<ArcadeCarController>();
            if(!visited&&car!=null&&(car.transform.position-transform.position).sqrMagnitude<40*40)
            {
                visited=true;
                GarageInteractionUI.Instance?.ShowNotification(safe
                    ? "СТО Северная: вы в укрытии.\nРемонт и торговля — через осмотр капота."
                    : $"{title}\nПрипасы нужно забрать вручную. Буря продолжает приближаться.",6f);
            }
        }
        public string GetPromptText()=>"[E] Прочитать: "+title;
        public bool CanInteract()=>true;
        public void Interact(GaragePlayerController player)=>GarageInteractionUI.Instance?.ShowNotification(title+"\n"+directions,9f);
    }
}
