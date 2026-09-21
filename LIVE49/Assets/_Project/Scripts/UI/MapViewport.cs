using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Live49.UI
{
    public class MapViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public RectTransform Content;
        public Action Changed;
        public float Zoom {get;private set;}=1;
        public Vector2 Pan=>Content.anchoredPosition;
        RectTransform View=>(RectTransform)transform;
        Vector2 _last;
        public void OnBeginDrag(PointerEventData e)
        {if(e.button!=PointerEventData.InputButton.Left)return;e.eligibleForClick=false;RectTransformUtility.ScreenPointToLocalPointInRectangle(View,e.position,e.pressEventCamera,out _last);}
        public void OnDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(View,e.position,e.pressEventCamera,out var p);
            PanBy(p-_last);_last=p;e.eligibleForClick=false;
        }
        public void OnEndDrag(PointerEventData e){e.eligibleForClick=false;}
        public void OnScroll(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(View,e.position,e.enterEventCamera,out var p);
            ZoomAt(Zoom*Mathf.Pow(1.15f,e.scrollDelta.y),p);e.Use();
        }
        public void StepZoom(float factor)=>ZoomAt(Zoom*factor,new Vector2(View.rect.width*.5f,-View.rect.height*.5f));
        public void ZoomAt(float value,Vector2 pivot)
        {
            float next=Mathf.Clamp(value,1,2.5f);
            Content.anchoredPosition=pivot-(pivot-Content.anchoredPosition)*(next/Zoom);
            Zoom=next;Content.localScale=Vector3.one*Zoom;Clamp();Changed?.Invoke();
        }
        public void PanBy(Vector2 delta){Content.anchoredPosition+=delta;Clamp();Changed?.Invoke();}
        public void Focus(Vector2 point)
        {Content.anchoredPosition=new Vector2(View.rect.width*.5f,-View.rect.height*.5f)-new Vector2(point.x,-point.y)*Zoom;Clamp();Changed?.Invoke();}
        public void ResetView(){Zoom=1;Content.localScale=Vector3.one;Content.anchoredPosition=Vector2.zero;Changed?.Invoke();}
        void Clamp()
        {
            var p=Content.anchoredPosition;
            p.x=Mathf.Clamp(p.x,View.rect.width-Content.rect.width*Zoom,0);
            p.y=Mathf.Clamp(p.y,0,Content.rect.height*Zoom-View.rect.height);
            Content.anchoredPosition=p;
        }
    }
}
