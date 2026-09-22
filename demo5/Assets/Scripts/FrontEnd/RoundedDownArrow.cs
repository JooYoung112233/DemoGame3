using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Vector UI shape: no font glyph or small raster texture to blur when scaled.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedDownArrow:MaskableGraphic
    {
        static void Quad(List<Vector2> points,Vector2 control,Vector2 end){var start=points[points.Count-1];for(int i=1;i<=8;i++){float t=i/8f;points.Add((1-t)*(1-t)*start+2*(1-t)*t*control+t*t*end);}}
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();var p=new List<Vector2>{new Vector2(18,8),new Vector2(82,8)};Quad(p,new Vector2(95,8),new Vector2(95,20));Quad(p,new Vector2(95,25),new Vector2(88,32));p.Add(new Vector2(61,60));Quad(p,new Vector2(50,72),new Vector2(39,60));p.Add(new Vector2(12,32));Quad(p,new Vector2(5,25),new Vector2(5,20));Quad(p,new Vector2(5,8),new Vector2(18,8));p.RemoveAt(p.Count-1);
            var r=rectTransform.rect;vh.AddVert(new Vector3(r.center.x,r.center.y),color,Vector2.zero);foreach(var q in p)vh.AddVert(new Vector3(r.xMin+q.x/100*r.width,r.yMax-q.y/70*r.height),color,Vector2.zero);for(int i=0;i<p.Count;i++)vh.AddTriangle(0,i+1,(i+1)%p.Count+1);
        }
    }
}
