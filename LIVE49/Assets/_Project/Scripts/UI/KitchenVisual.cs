using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Live49.UI
{
    // Three physical detents preserve the existing recipe rules; pointer and keyboard use the same selection.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StoveDial : MaskableGraphic, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public Action<int> Changed;
        public bool Interactive;
        int _level;
        public void Display(int level,bool interactive){_level=level;Interactive=interactive;SetVerticesDirty();}
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)SelectPointer(e);}
        public void OnDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)SelectPointer(e);}
        void SelectPointer(PointerEventData e)
        {
            if(!Interactive||!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p))return;
            p-=rectTransform.rect.center;if(p.sqrMagnitude<225)return;
            float angle=Mathf.Atan2(p.x,p.y)*Mathf.Rad2Deg;
            Changed?.Invoke(Mathf.Clamp(Mathf.RoundToInt((angle+60)/60)+1,1,3));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var c=rectTransform.rect.center;
            KitchenMesh.Ellipse(vh,c,new Vector2(78,78),new Color32(20,24,22,255));
            KitchenMesh.Ellipse(vh,c,new Vector2(65,65),new Color32(67,69,60,255));
            KitchenMesh.Ellipse(vh,c+new Vector2(-3,4),new Vector2(58,58),new Color32(86,84,69,255));
            for(int i=1;i<=3;i++){float a=(i-2)*60*Mathf.Deg2Rad;var d=new Vector2(Mathf.Sin(a),Mathf.Cos(a));KitchenMesh.Line(vh,c+d*85,c+d*96,4,i==_level?PanelUI.Gold:PanelUI.Muted);}
            float angle=(_level==0?-130:(_level-2)*60)*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
            KitchenMesh.Line(vh,c+dir*20,c+dir*54,7,Interactive?PanelUI.Cream:PanelUI.Muted);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KitchenVisual : MaskableGraphic
    {
        public bool OverPot;
        float _heat,_clock;int _level;bool _active;
        public void Show(float heat,int level,bool active,float clock){_heat=heat;_level=level;_active=active;_clock=clock;SetVerticesDirty();}
        Vector2 P(float x,float y)=>new Vector2(rectTransform.rect.xMin+x,rectTransform.rect.yMax-y);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(!OverPot)
            {
                KitchenMesh.Box(vh,P(55,451),new Vector2(1010,244),new Color32(50,57,53,255));
                KitchenMesh.Box(vh,P(55,565),new Vector2(1010,130),new Color32(40,45,41,255));
                KitchenMesh.Line(vh,P(75,566),P(1045,566),2,new Color32(106,109,87,255));
                KitchenMesh.Ellipse(vh,P(560,517),new Vector2(278,43),new Color32(22,27,23,255));
                KitchenMesh.Ellipse(vh,P(560,512),new Vector2(225,32),new Color32(65,72,62,255));
                if(_active&&_level>0)
                    for(int i=0;i<21;i++)
                    {float x=357+i*20,y=521+9*Mathf.Cos(i*.3f);float h=15+_level*8+Mathf.Sin(_clock*5+i*1.7f)*4;
                        var p=P(x,y);KitchenMesh.Triangle(vh,p+new Vector2(-8,0),p+new Vector2(8,0),p+new Vector2(2,h),new Color32(80,152,178,210));
                        KitchenMesh.Triangle(vh,p+new Vector2(-4,0),p+new Vector2(4,0),p+new Vector2(0,h*.65f),new Color32(170,204,179,240));}
                return;
            }
            if(!_active)return;
            float bubbling=Mathf.InverseLerp(53,94,_heat);
            for(int i=0;i<12;i++)
            {
                float pulse=Mathf.Repeat(_clock*(.7f+bubbling)+i*.37f,1);
                if(i>=Mathf.CeilToInt(bubbling*12))continue;
                float a=i*2.399f,r=35+(i%4)*30;
                var p=P(326+Mathf.Cos(a)*r*1.4f,199+Mathf.Sin(a)*r*.62f);
                KitchenMesh.Ellipse(vh,p,new Vector2(3+pulse*9,2+pulse*4),new Color(1,.89f,.58f,(1-pulse)*.55f));
            }
            float steam=Mathf.InverseLerp(48,100,_heat);
            for(int i=0;i<5;i++)for(int j=0;j<9;j++)
            {
                float height=j*8+Mathf.Repeat(_clock*16+i*11,24);
                float x=213+i*56+Mathf.Sin(_clock*.8f+j*.55f+i)*10;
                KitchenMesh.Line(vh,P(x,115-height),P(x+Mathf.Sin(j+_clock)*4,108-height),2.5f,new Color(.92f,.89f,.76f,steam*(1-height/100)*.26f));
            }
        }
    }
    static class KitchenMesh
    {
        public static void Triangle(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Color color){int n=v.currentVertCount;v.AddVert(a,color,Vector2.zero);v.AddVert(b,color,Vector2.zero);v.AddVert(c,color,Vector2.zero);v.AddTriangle(n,n+1,n+2);}
        public static void Ellipse(VertexHelper v,Vector2 center,Vector2 size,Color color){for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Triangle(v,center,center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),size),center+Vector2.Scale(new Vector2(Mathf.Cos(b),Mathf.Sin(b)),size),color);}}
        public static void Box(VertexHelper v,Vector2 top,Vector2 size,Color color){var b=top+new Vector2(size.x,0);var c=top+new Vector2(size.x,-size.y);var d=top+new Vector2(0,-size.y);Triangle(v,top,b,c,color);Triangle(v,top,c,d,color);}
        public static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color color){var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Triangle(v,a-n,a+n,b+n,color);Triangle(v,a-n,b+n,b-n,color);}
    }
}
