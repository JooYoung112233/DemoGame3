using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class MapPinGraphic:MaskableGraphic {
  protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();var points=new List<Vector2>();for(int i=0;i<=40;i++){float a=Mathf.Lerp(225,-45,i/40f)*Mathf.Deg2Rad;points.Add(new Vector2(50+43*Mathf.Cos(a),46-43*Mathf.Sin(a)));}points.Add(new Vector2(50,125));var r=rectTransform.rect;vh.AddVert(new Vector3(r.xMin+r.width*.5f,r.yMax-r.height*.48f),color,Vector2.zero);foreach(var p in points)vh.AddVert(new Vector3(r.xMin+p.x/100*r.width,r.yMax-p.y/130*r.height),color,Vector2.zero);for(int i=0;i<points.Count;i++)vh.AddTriangle(0,i+1,(i+1)%points.Count+1);}
 }
}
