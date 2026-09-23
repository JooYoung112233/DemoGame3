using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // One visible slot per health point; capacity and noise gauges stay continuous.
    public sealed class SegmentedHealthGraphic : MaskableGraphic
    {
        public int Current, Maximum, After;
        public float Gap=3;
        public Color Empty=new Color(.08f,.10f,.10f,1), Damage=new Color(.94f,.75f,.43f,1);
        public static void Set(Image source,int current,int maximum,int after=-1)
        {
            if(!source)return;
            var child=source.transform.Find("HealthSlots");
            var slots=child?child.GetComponent<SegmentedHealthGraphic>():null;
            if(!slots)
            {
                var go=new GameObject("HealthSlots",typeof(RectTransform),typeof(CanvasRenderer));
                go.transform.SetParent(source.transform,false);
                var r=(RectTransform)go.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                slots=go.AddComponent<SegmentedHealthGraphic>();slots.raycastTarget=false;
            }
            source.enabled=false;
            maximum=Mathf.Max(0,maximum);current=Mathf.Clamp(current,0,maximum);after=after<0?current:Mathf.Clamp(after,0,current);
            if(slots.Current==current&&slots.Maximum==maximum&&slots.After==after&&slots.color==source.color)return;
            slots.Current=current;slots.Maximum=maximum;slots.After=after;slots.color=source.color;slots.SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Maximum<=0)return;
            var r=rectTransform.rect;
            // Wrap unusually large maxima instead of turning every slot into an unreadable sliver.
            int columns=Mathf.Min(Maximum,Mathf.Max(1,Mathf.FloorToInt(r.width/8)));
            int rows=Mathf.CeilToInt((float)Maximum/columns);
            float gap=Mathf.Min(Gap,r.width/(columns*3f));
            float w=(r.width-gap*(columns-1))/columns,h=(r.height-Mathf.Min(2,Gap)*(rows-1))/rows;
            for(int i=0;i<Maximum;i++)
            {
                float x=r.xMin+(i%columns)*(w+gap),y=r.yMax-(i/columns)*(h+Mathf.Min(2,Gap));
                Color c=i>=Current?Empty:i>=After?Damage:color;int n=vh.currentVertCount;
                vh.AddVert(new Vector3(x,y-h),c,Vector2.zero);vh.AddVert(new Vector3(x,y),c,Vector2.zero);
                vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y-h),c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
