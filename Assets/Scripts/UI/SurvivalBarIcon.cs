using UnityEngine;
using UnityEngine.UI;
namespace RogueDrive.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SurvivalBarIcon : MaskableGraphic
    {
        public int Row;
        static Texture2D atlas;
        public override Texture mainTexture => atlas!=null?atlas:(atlas=Resources.Load<Texture2D>("UI/Survival/vitals_flat_v2"));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            float top=Row==0?118:Row==1?404:691;
            Vector2[] points={new Vector2(.1f,0),new Vector2(.9f,0),new Vector2(1,.12f),new Vector2(1,.88f),new Vector2(.9f,1),new Vector2(.1f,1),new Vector2(0,.88f),new Vector2(0,.12f)};
            Add(vh,r,new Vector2(.5f,.5f),top);
            foreach(var p in points)Add(vh,r,p,top);
            for(int i=0;i<8;i++)vh.AddTriangle(0,i+1,(i+1)%8+1);
        }
        void Add(VertexHelper vh,Rect r,Vector2 p,float top)
        {
            vh.AddVert(new Vector3(r.x+p.x*r.width,r.yMax-p.y*r.height),color,new Vector2((129+p.x*231)/1536f,1-(top+p.y*196)/1024f));
        }
    }
}
