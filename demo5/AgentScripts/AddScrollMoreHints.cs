using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class AddScrollMoreHints
{
    const string P="Assets/Prefabs/Settlement/";
    static void Arrow(Transform parent,string name,Vector2 offset,Color color){var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RoundedDownArrow));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=offset;var a=g.GetComponent<RoundedDownArrow>();a.color=color;a.raycastTarget=false;}
    public static string Apply(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
        var root=new GameObject("ScrollMoreHint",typeof(RectTransform),typeof(CanvasGroup),typeof(ScrollMoreIndicator));var rt=(RectTransform)root.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(1,0);rt.anchoredPosition=new Vector2(-13,4);rt.sizeDelta=new Vector2(24,17);var hint=root.GetComponent<ScrollMoreIndicator>();hint.Visibility=root.GetComponent<CanvasGroup>();hint.Visibility.alpha=0;hint.Visibility.blocksRaycasts=false;hint.Visibility.interactable=false;
        Arrow(root.transform,"SoftEdge",new Vector2(0,-1),new Color(.02f,.04f,.04f,.34f));Arrow(root.transform,"Arrow",Vector2.zero,new Color(.98f,.95f,.86f,.55f));var prefab=PrefabUtility.SaveAsPrefabAsset(root,P+"ScrollMoreHint.prefab");Object.DestroyImmediate(root);int count=0;
        foreach(var name in new[]{"CraftWorkPanel","RestWorkPanel"}){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{foreach(var scroll in g.GetComponentsInChildren<ScrollRect>(true)){var old=scroll.viewport.Find("ScrollMoreHint");if(old)Object.DestroyImmediate(old.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scroll.viewport);var h=instance.GetComponent<ScrollMoreIndicator>();h.Scroll=scroll;PrefabUtility.RecordPrefabInstancePropertyModifications(h);count++;}PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return count+" independent subtle down-arrow hints installed; non-interactive, hidden when no unseen content below.";
    }
}
