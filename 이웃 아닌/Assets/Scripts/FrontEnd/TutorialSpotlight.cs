using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Tutorial focus: a dark veil over the whole screen with clear holes (the next click, and where its instruction is written).
 // Code-native mesh, never intercepts clicks: everything under it stays usable.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class TutorialSpotlight : MaskableGraphic {
  readonly List<Rect> holes=new List<Rect>();
  public override bool raycastTarget{get=>false;set{}}
  // Holes in this graphic's local space.
  public void SetHoles(IEnumerable<Rect> rects){holes.Clear();holes.AddRange(rects.Where(r=>r.width>0&&r.height>0));SetVerticesDirty();}
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;
   // Grid on every hole edge; fill each cell that no hole covers.
   var xs=new List<float>{r.xMin,r.xMax};var ys=new List<float>{r.yMin,r.yMax};
   foreach(var h in holes){xs.Add(Mathf.Clamp(h.xMin,r.xMin,r.xMax));xs.Add(Mathf.Clamp(h.xMax,r.xMin,r.xMax));ys.Add(Mathf.Clamp(h.yMin,r.yMin,r.yMax));ys.Add(Mathf.Clamp(h.yMax,r.yMin,r.yMax));}
   xs=xs.Distinct().OrderBy(x=>x).ToList();ys=ys.Distinct().OrderBy(y=>y).ToList();
   for(int i=0;i+1<xs.Count;i++)for(int j=0;j+1<ys.Count;j++){
    var cell=Rect.MinMaxRect(xs[i],ys[j],xs[i+1],ys[j+1]);var mid=cell.center;
    if(holes.Any(h=>h.Contains(mid)))continue;
    int s=vh.currentVertCount;vh.AddVert(new Vector3(cell.xMin,cell.yMin),color,Vector2.zero);vh.AddVert(new Vector3(cell.xMin,cell.yMax),color,Vector2.zero);vh.AddVert(new Vector3(cell.xMax,cell.yMax),color,Vector2.zero);vh.AddVert(new Vector3(cell.xMax,cell.yMin),color,Vector2.zero);
    vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
   }
  }
 }
}
