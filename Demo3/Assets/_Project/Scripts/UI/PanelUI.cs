using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    public static class PanelUI
    {
        public static readonly Color Cream=new Color32(242,228,196,255), Gold=new Color32(180,145,89,255), Muted=new Color32(185,166,135,255), Ink=new Color32(31,28,23,255);
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        public static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color,bool hit=false)
        {var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=hit;return r;}
        public static TMP_Text Text(Transform p,TMP_FontAsset font,string name,string value,float x,float y,float w,float h,float size,Color? color=null)
        {var t=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color??Cream;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMin=size-4;t.fontSizeMax=size;return t;}
        public static Button Button(Transform p,TMP_FontAsset font,string name,string label,float x,float y,float w,float h,Action action)
        {
            var r=Box(p,name,x,y,w,h,new Color(Gold.r,Gold.g,Gold.b,.20f),true);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            var colors=b.colors;colors.highlightedColor=colors.selectedColor=new Color(1.3f,1.2f,1.05f);colors.fadeDuration=.12f;b.colors=colors;
            Text(r,font,"Text",label,22,0,w-44,h,24).alignment=TextAlignmentOptions.Center;
            b.onClick.AddListener(()=>{Core.FreshInput.DiscardPending();if(b.interactable)action();});return b;
        }
        public static void Clear(Transform parent)
        {foreach(Transform child in parent){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}}
    }
    public sealed class ExplorationOnly : MonoBehaviour
    {
        CanvasGroup _group;
        void Awake(){_group=gameObject.AddComponent<CanvasGroup>();}
        void Update(){bool show=GameHud.Instance!=null&&GameHud.Instance.IsExploring&&!GameHud.Instance.IsPaused&&!GameHud.Instance.SearchOpen;_group.alpha=show?1:0;_group.blocksRaycasts=_group.interactable=show;}
    }
}
