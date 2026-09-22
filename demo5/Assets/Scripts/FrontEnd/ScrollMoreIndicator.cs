using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class ScrollMoreIndicator:MonoBehaviour
    {
        public ScrollRect Scroll;
        public CanvasGroup Visibility;
        [Min(0)] public float RemainingThreshold=2;
        public bool HasMoreBelow {get;private set;}
        void OnEnable(){Canvas.willRenderCanvases+=Refresh;Refresh();}
        void OnDisable(){Canvas.willRenderCanvases-=Refresh;HasMoreBelow=false;if(Visibility)Visibility.alpha=0;}
        void LateUpdate()=>Refresh();
        public void Refresh(){
            HasMoreBelow=false;
            if(Scroll&&Scroll.isActiveAndEnabled&&Scroll.vertical&&Scroll.content&&Scroll.viewport){
                var viewport=Scroll.viewport;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,Scroll.content);
                HasMoreBelow=bounds.size.y>viewport.rect.height+RemainingThreshold&&bounds.min.y<viewport.rect.yMin-RemainingThreshold;
            }
            if(Visibility){Visibility.alpha=HasMoreBelow?1:0;Visibility.blocksRaycasts=false;Visibility.interactable=false;}
        }
    }
}
