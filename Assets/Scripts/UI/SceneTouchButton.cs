using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace RogueDrive.UI
{
    public sealed class SceneTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private readonly HashSet<int> pointers = new HashSet<int>();
        public bool Pressed => pointers.Count > 0;
        public void OnPointerDown(PointerEventData data) => pointers.Add(data.pointerId);
        public void OnPointerUp(PointerEventData data) => pointers.Remove(data.pointerId);
        public void OnPointerExit(PointerEventData data) => pointers.Remove(data.pointerId);
        void OnDisable() => pointers.Clear();
    }
}
