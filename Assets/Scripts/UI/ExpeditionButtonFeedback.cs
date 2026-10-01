using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ExpeditionButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        Button button;
        Vector3 scale;
        bool hovered, selected, pressed;
        bool? lastInteractable;
        void Awake() { button = GetComponent<Button>(); scale = transform.localScale; }
        void OnDisable() { transform.localScale = scale; hovered = selected = pressed = false; }
        void Update()
        {
            bool interactable = button.IsInteractable();
            if (lastInteractable != interactable)
            {
                lastInteractable = interactable;
                var color = !interactable ? LowPolyUi.Muted : button.colors.normalColor.grayscale > .42f ? LowPolyUi.Ink : LowPolyUi.Paper;
                foreach (var text in button.GetComponentsInChildren<Text>(true)) text.color = color;
            }
            float target = interactable ? pressed ? .985f : hovered || selected ? 1.012f : 1 : 1;
            transform.localScale = Vector3.Lerp(transform.localScale, scale * target, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) => pressed = true;
        public void OnPointerUp(PointerEventData data) => pressed = false;
        public void OnSelect(BaseEventData data) => selected = true;
        public void OnDeselect(BaseEventData data) => selected = false;
    }
}
