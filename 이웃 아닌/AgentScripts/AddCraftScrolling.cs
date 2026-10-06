using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class AddCraftScrolling
{
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static ScrollRect Wrap(RectTransform content,string name,bool text=false){
        if(content.parent.name==name)return content.parent.GetComponent<ScrollRect>();
        var xy=content.anchoredPosition;var size=content.sizeDelta;int sibling=content.GetSiblingIndex();var viewport=Rect(name,content.parent,xy.x,-xy.y,size.x,size.y);viewport.SetSiblingIndex(sibling);viewport.gameObject.AddComponent<RectMask2D>();var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
        content.SetParent(viewport,false);content.anchoredPosition=Vector2.zero;
        if(text){content.sizeDelta=new Vector2(size.x-16,size.y);var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=36;
        var rail=Rect("Scrollbar",viewport,size.x-7,4,7,size.y-8);var image=rail.gameObject.AddComponent<Image>();image.color=new Color(.6f,.59f,.5f,.2f);
        var handle=Rect("Handle",rail,0,0,7,48);var hi=handle.gameObject.AddComponent<Image>();hi.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");var sb=rail.gameObject.AddComponent<Scrollbar>();sb.targetGraphic=hi;sb.handleRect=handle;sb.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=sb;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    public static string Apply(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
        const string path="Assets/Prefabs/Settlement/CraftWorkPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var c=g.GetComponent<SettlementCraftPanel>();c.RecipeScroll=Wrap(c.RecipeContent,"RecipeViewport");c.CostScroll=Wrap(c.CostContent,"CostViewport");c.CancelMessageScroll=Wrap(c.CancelMessage.rectTransform,"RefundViewport",true);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Independent recipe, ingredient and refund scroll areas added; existing positions and row sizes retained, scrollbars auto-hide.";
    }
}
