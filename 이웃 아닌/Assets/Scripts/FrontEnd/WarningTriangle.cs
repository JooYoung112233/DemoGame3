using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Code-native warning sign (a filled upward triangle); the '!' is a child Text so it stays editable. Never intercepts clicks.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class WarningTriangle : MaskableGraphic {
  public override bool raycastTarget{get=>false;set{}}
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;
   vh.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector3(r.center.x,r.yMax),color,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin),color,Vector2.zero);
   vh.AddTriangle(0,1,2);
  }
 }
}
