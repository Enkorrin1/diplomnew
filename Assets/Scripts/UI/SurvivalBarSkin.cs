using UnityEngine;
using UnityEngine.UI;
namespace RogueDrive.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SurvivalBarSkin : MaskableGraphic
    {
        public int Row;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float cut=7;
            Vector2[] points={new Vector2(cut,0),new Vector2(r.width-cut,0),new Vector2(r.width,-cut),new Vector2(r.width,-r.height+cut),new Vector2(r.width-cut,-r.height),new Vector2(cut,-r.height),new Vector2(0,-r.height+cut),new Vector2(0,-cut)};
            var ink=new Color32(34,48,57,245);
            vh.AddVert(new Vector3(r.x+r.width/2,r.yMax-r.height/2),ink,Vector2.zero);
            foreach(var p in points)vh.AddVert(new Vector3(r.x+p.x,r.yMax+p.y),ink,Vector2.zero);
            for(int i=0;i<8;i++)vh.AddTriangle(0,i+1,(i+1)%8+1);
        }
    }
}
