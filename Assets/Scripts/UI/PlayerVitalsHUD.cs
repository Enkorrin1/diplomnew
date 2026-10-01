using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.UI;
namespace RogueDrive.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerVitalsHUD : MonoBehaviour
    {
        PlayerFieldNeeds needs;
        GaragePlayerController player;
        Canvas canvas;
        RectTransform safeArea;
        SurvivalStatusBar health,food;
        Rect lastSafeArea;
        void Awake()
        {
            needs=GetComponent<PlayerFieldNeeds>();player=GetComponent<GaragePlayerController>();
            var root=new GameObject("SurvivalHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=48;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            safeArea=LowPolyUi.Rect(root.transform,"SafeArea",Vector2.zero,Vector2.zero);
            health=SurvivalStatusBar.Create(safeArea,0,new Vector2(26,154),350);
            food=SurvivalStatusBar.Create(safeArea,1,new Vector2(26,84),350);
            foreach(var bar in new[]{health,food}) {var rt=(RectTransform)bar.transform;rt.anchorMin=rt.anchorMax=Vector2.zero;}
            ApplySafeArea();Refresh(true);
        }
        void ApplySafeArea()
        {
            var area=Screen.safeArea;lastSafeArea=area;
            safeArea.anchorMin=new Vector2(area.x/Screen.width,area.y/Screen.height);
            safeArea.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        void LateUpdate(){if(lastSafeArea!=Screen.safeArea)ApplySafeArea();Refresh(false);}
        void Refresh(bool instant)
        {
            if(needs==null)return;
            // Menus show their own larger bars; cinematics remain unobstructed.
            bool visible=gameObject.activeInHierarchy && (player==null||!player.IsMovementLocked) &&
                !InventoryWindowUI.BlockGameplayInput;
            canvas.enabled=visible;
            health.Render(needs.Health,instant);food.Render(needs.Food,instant);
        }
        void OnDestroy(){if(canvas!=null)Destroy(canvas.gameObject);}
    }
}
