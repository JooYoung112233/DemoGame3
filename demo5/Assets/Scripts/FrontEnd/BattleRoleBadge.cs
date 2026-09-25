using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Single-colour, resolution-independent role symbols for the existing ink/paper interface.
    // The role is descriptive; the creature's Attack/Gait still own all combat behaviour.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleRoleBadge : MaskableGraphic
    {
        public CreatureRole Role;
        public void SetRole(CreatureRole role)
        {
            if (Role == role) return;
            Role = role; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (Role)
            {
                case CreatureRole.Defender:
                    Polygon(vh, new Vector2(.5f,.96f),new Vector2(.91f,.8f),new Vector2(.83f,.36f),
                        new Vector2(.5f,.04f),new Vector2(.17f,.36f),new Vector2(.09f,.8f));
                    break;
                case CreatureRole.Assault:
                    Polygon(vh,new Vector2(.35f,.3f),new Vector2(.64f,.84f),new Vector2(.92f,.97f),
                        new Vector2(.85f,.65f),new Vector2(.47f,.23f));
                    Line(vh,new Vector2(.2f,.44f),new Vector2(.65f,.15f),.1f);
                    Line(vh,new Vector2(.25f,.07f),new Vector2(.43f,.32f),.12f);
                    break;
                case CreatureRole.Ranged:
                    Arc(vh,new Vector2(.5f,.5f),.28f,0,360,.07f);
                    Line(vh,new Vector2(.5f,.02f),new Vector2(.5f,.31f),.085f);
                    Line(vh,new Vector2(.5f,.69f),new Vector2(.5f,.98f),.085f);
                    Line(vh,new Vector2(.02f,.5f),new Vector2(.31f,.5f),.085f);
                    Line(vh,new Vector2(.69f,.5f),new Vector2(.98f,.5f),.085f);
                    Disc(vh,new Vector2(.5f,.5f),.055f);
                    break;
                case CreatureRole.Disruptor:
                    Line(vh,new Vector2(.5f,.97f),new Vector2(.97f,.5f),.075f);
                    Line(vh,new Vector2(.97f,.5f),new Vector2(.5f,.03f),.075f);
                    Line(vh,new Vector2(.5f,.03f),new Vector2(.03f,.5f),.075f);
                    Line(vh,new Vector2(.03f,.5f),new Vector2(.5f,.97f),.075f);
                    Line(vh,new Vector2(.5f,.7f),new Vector2(.5f,.43f),.105f);
                    Disc(vh,new Vector2(.5f,.29f),.065f);
                    break;
                case CreatureRole.Support:
                    // Broadcast waves rather than a medical cross: current support calls reinforcements.
                    Disc(vh,new Vector2(.18f,.18f),.095f);
                    Arc(vh,new Vector2(.18f,.18f),.32f,0,90,.095f);
                    Arc(vh,new Vector2(.18f,.18f),.57f,0,90,.095f);
                    Arc(vh,new Vector2(.18f,.18f),.78f,0,90,.095f);
                    break;
                default:
                    for(int i=0;i<3;i++)
                    {
                        float x=.13f+i*.28f;
                        Polygon(vh,new Vector2(x,.08f),new Vector2(x+.02f,.72f),new Vector2(x+.2f,.96f),new Vector2(x+.12f,.27f));
                    }
                    break;
            }
        }
        Vector2 Point(Vector2 p)
        {
            var r=rectTransform.rect;float size=Mathf.Min(r.width,r.height);
            return r.center+(p-Vector2.one*.5f)*size;
        }
        void Polygon(VertexHelper vh,params Vector2[] points)
        {
            int start=vh.currentVertCount;
            foreach(var p in points)vh.AddVert(Point(p),color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=(b-a).normalized,side=new Vector2(-d.y,d.x)*width*.5f;
            Polygon(vh,a-side,a+side,b+side,b-side);
        }
        void Arc(VertexHelper vh,Vector2 center,float radius,float from,float to,float width)
        {
            int steps=Mathf.Max(8,Mathf.CeilToInt((to-from)/15));
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(from,to,(float)i/steps)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/steps)*Mathf.Deg2Rad;
                var da=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Polygon(vh,center+da*(radius-width*.5f),center+da*(radius+width*.5f),center+db*(radius+width*.5f),center+db*(radius-width*.5f));
            }
        }
        void Disc(VertexHelper vh,Vector2 center,float radius)
        {
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                Polygon(vh,center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);
            }
        }
    }
}
