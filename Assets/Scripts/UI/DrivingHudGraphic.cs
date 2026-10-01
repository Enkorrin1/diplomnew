using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>Resolution-independent artwork for the driving instruments.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DrivingHudGraphic : MaskableGraphic
    {
        public enum Shape { Panel, Dial, Car, Storm, Fuel, Pause }
        public Shape Artwork;
        [Range(0, 1)] public float Value;
        public Color Accent = new Color(.91f, .72f, .40f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 P(float x, float y) => new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
            void Line(float x1, float y1, float x2, float y2, float width, Color c)
            {
                Vector2 a = P(x1,y1), b = P(x2,y2);
                Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized * width * .5f;
                int k = vh.currentVertCount;
                vh.AddVert(a-n,c,Vector2.zero); vh.AddVert(a+n,c,Vector2.zero);
                vh.AddVert(b+n,c,Vector2.zero); vh.AddVert(b-n,c,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2); vh.AddTriangle(k,k+2,k+3);
            }
            void Box(float x,float y,float w,float h,Color c)
            {
                int k=vh.currentVertCount;
                vh.AddVert(P(x,y),c,Vector2.zero);vh.AddVert(P(x+w,y),c,Vector2.zero);
                vh.AddVert(P(x+w,y+h),c,Vector2.zero);vh.AddVert(P(x,y+h),c,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
            if (Artwork == Shape.Panel)
            {
                float cx=8f/r.width, cy=8f/r.height;
                Vector2[] points={P(cx,0),P(1-cx,0),P(1,cy),P(1,1-cy),P(1-cx,1),P(cx,1),P(0,1-cy),P(0,cy)};
                vh.AddVert(r.center,color,Vector2.zero);
                foreach(var point in points)vh.AddVert(point,color,Vector2.zero);
                for(int i=0;i<8;i++)vh.AddTriangle(0,i+1,(i+1)%8+1);
                Line(cx,1,1-cx,1,1,new Color(.70f,.82f,.85f,.24f));
                Line(cx,0,1-cx,0,1,new Color(0,0,0,.3f));
            }
            else if (Artwork == Shape.Dial)
            {
                for(int i=0;i<41;i++)
                {
                    float a=(220f-i*260f)*Mathf.Deg2Rad;
                    float inner=i%5==0?.40f:.44f;
                    Color c=i/40f<=Value ? Accent : new Color(.57f,.67f,.70f,.38f);
                    if(i==0)c=Accent;
                    Line(.5f+Mathf.Cos(a)*inner,.5f+Mathf.Sin(a)*inner,.5f+Mathf.Cos(a)*.49f,.5f+Mathf.Sin(a)*.49f,i%5==0?2f:1.3f,c);
                }
            }
            else if (Artwork == Shape.Car)
            {
                Color glass=new Color(color.r,color.g,color.b,.28f);
                Line(.30f,.12f,.26f,.76f,1.5f,color);Line(.26f,.76f,.35f,.91f,1.5f,color);
                Line(.35f,.91f,.65f,.91f,1.5f,color);Line(.65f,.91f,.74f,.76f,1.5f,color);
                Line(.74f,.76f,.70f,.12f,1.5f,color);Line(.70f,.12f,.30f,.12f,1.5f,color);
                Box(.34f,.48f,.32f,.22f,glass);Line(.34f,.70f,.66f,.70f,2,color);
                Line(.34f,.35f,.66f,.35f,1.5f,color);Line(.30f,.80f,.70f,.80f,1,color);
            }
            else if (Artwork == Shape.Storm)
            {
                Line(.5f,.94f,.06f,.13f,2,color);Line(.06f,.13f,.94f,.13f,2,color);Line(.94f,.13f,.5f,.94f,2,color);
                Line(.56f,.7f,.4f,.48f,2,color);Line(.4f,.48f,.59f,.48f,2,color);Line(.59f,.48f,.45f,.28f,2,color);
            }
            else if(Artwork == Shape.Fuel)
            {
                Line(.18f,.12f,.18f,.87f,2,color);Line(.18f,.87f,.62f,.87f,2,color);Line(.62f,.87f,.62f,.12f,2,color);
                Line(.10f,.12f,.70f,.12f,2,color);Line(.24f,.62f,.56f,.62f,2,color);
                Line(.62f,.51f,.80f,.51f,2,color);Line(.80f,.51f,.80f,.27f,2,color);Line(.80f,.27f,.94f,.27f,2,color);
                Line(.94f,.27f,.94f,.69f,2,color);Line(.94f,.69f,.78f,.84f,2,color);
            }
            else if(Artwork == Shape.Pause)
            { Box(.22f,.2f,.17f,.6f,color);Box(.61f,.2f,.17f,.6f,color); }
        }
    }
}
