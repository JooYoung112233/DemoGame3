using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Editable vector UI glyph; no baked text or raster dependency.
    public sealed class BattleShieldIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            Vector2[] p={new Vector2(.5f,1),new Vector2(.94f,.83f),new Vector2(.87f,.39f),new Vector2(.72f,.16f),new Vector2(.5f,0),new Vector2(.28f,.16f),new Vector2(.13f,.39f),new Vector2(.06f,.83f)};
            vh.AddVert(new Vector2(r.xMin+r.width*.5f,r.yMin+r.height*.52f),color,Vector2.zero);
            foreach(var v in p)vh.AddVert(new Vector2(r.xMin+r.width*v.x,r.yMin+r.height*v.y),color,Vector2.zero);
            for(int i=0;i<p.Length;i++)vh.AddTriangle(0,i+1,(i+1)%p.Length+1);
        }
    }
}
