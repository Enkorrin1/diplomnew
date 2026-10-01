using UnityEngine;
using Image=UnityEngine.UI.Image;
using Text=UnityEngine.UI.Text;
namespace RogueDrive.UI
{
    public sealed class SurvivalStatusBar : MonoBehaviour
    {
        Image fill,trail;
        Text title,value;
        Color normal;
        string caption;
        float shown=-1, delayed=1;
        public float DisplayedPercent {get;private set;}
        public static SurvivalStatusBar Create(Transform parent,int kind,Vector2 position,float width)
        {
            var rt=LowPolyUi.Rect(parent,"Status_"+kind,position,new Vector2(width,width*228f/1384f));
            var skin=rt.gameObject.AddComponent<SurvivalBarSkin>();skin.Row=kind;skin.raycastTarget=false;
            var view=rt.gameObject.AddComponent<SurvivalStatusBar>();
            view.caption=kind==0?"ЗДОРОВЬЕ":kind==1?"СЫТОСТЬ":"ВОДА";
            view.normal=kind==0?new Color32(209,105,88,255):kind==1?LowPolyUi.Amber:LowPolyUi.Water;
            float h=rt.sizeDelta.y;
            var icon=LowPolyUi.Rect(rt,"GeneratedIcon",new Vector2(6,-6),new Vector2(h-12,h-12)).gameObject.AddComponent<SurvivalBarIcon>();
            icon.Row=kind;icon.raycastTarget=false;
            var well=LowPolyUi.Rect(rt,"MeterWell",new Vector2(width*.205f,-h*.55f),new Vector2(width*.764f,h*.245f)).gameObject.AddComponent<Image>();
            well.color=new Color32(15,26,32,255);well.raycastTarget=false;
            view.title=LowPolyUi.Label(rt,"Caption",view.caption,new Vector2(width*.245f,-h*.205f),new Vector2(width*.49f,h*.255f),width<380?14:17,LowPolyUi.Paper);
            view.title.font=LowPolyUi.HeadingFont??view.title.font;
            view.value=LowPolyUi.Label(rt,"Value","",new Vector2(width*.745f,-h*.205f),new Vector2(width*.203f,h*.255f),width<380?14:17,LowPolyUi.Paper);
            view.value.alignment=TextAnchor.MiddleRight;
            view.title.verticalOverflow=VerticalWrapMode.Overflow;
            view.value.verticalOverflow=VerticalWrapMode.Overflow;
            view.trail=Fill(rt,"LossTrail",width,h,new Color32(238,213,178,255));
            view.fill=Fill(rt,"LiveFill",width,h,view.normal);
            // Subtle graduation marks make small changes legible without cluttering the label.
            for(int i=1;i<4;i++)
            {
                var tick=LowPolyUi.Rect(rt,"Tick",new Vector2(width*(.205f+.764f*i/4),-h*.55f),new Vector2(1,h*.245f)).gameObject.AddComponent<Image>();
                tick.color=new Color(0,0,0,.22f);tick.raycastTarget=false;
            }
            return view;
        }
        static Image Fill(Transform parent,string name,float w,float h,Color color)
        {
            var result=LowPolyUi.Rect(parent,name,new Vector2(w*.205f,-h*.55f),new Vector2(w*.764f,h*.245f)).gameObject.AddComponent<Image>();
            result.sprite=LowPolyUi.Sprite("bar");result.type=Image.Type.Filled;result.fillMethod=Image.FillMethod.Horizontal;
            result.fillOrigin=0;result.color=color;result.raycastTarget=false;return result;
        }
        public void Render(float percent,bool instant=false)
        {
            percent=Mathf.Clamp(percent,0,100);DisplayedPercent=percent;float target=percent/100;
            if(shown<0||instant){shown=target;delayed=target;}
            shown=Mathf.MoveTowards(shown,target,Time.unscaledDeltaTime*1.5f);
            delayed=Mathf.MoveTowards(delayed,target,Time.unscaledDeltaTime*.35f);
            fill.fillAmount=shown;trail.fillAmount=Mathf.Max(shown,delayed);
            bool low=percent<=25;
            fill.color=low?Color.Lerp(normal,LowPolyUi.Danger,.55f+.15f*Mathf.Sin(Time.unscaledTime*3f)):normal;
            value.text=$"{Mathf.CeilToInt(percent)}%";value.color=low?LowPolyUi.Danger:LowPolyUi.Paper;
            title.text=caption+(low?" !":"");
        }
    }
}
