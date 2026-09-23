using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class FixPopupContentAlignment {
 const string P="Assets/Prefabs/Settlement/";
 static void Edit(string n,Action<Transform> f){var g=PrefabUtility.LoadPrefabContents(P+n+".prefab");try{f(g.transform);PrefabUtility.SaveAsPrefabAsset(g,P+n+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static void R(Transform p,string path,float x,float y,float w,float h){var r=(RectTransform)p.Find(path);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void T(Transform p,string path,float x,float y,float w,float h,TextAnchor a){R(p,path,x,y,w,h);p.Find(path).GetComponent<Text>().alignment=a;}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  Edit("CraftWorkPanel",t=>{
   var w=t.Find("Workspace/Workshop");T(w,"Duration",36,630,408,40,TextAnchor.MiddleLeft);
   var o=t.Find("Workspace/OrdersPaper");T(o,"Count",490,4,140,46,TextAnchor.MiddleRight);T(o,"Empty",30,145,600,118,TextAnchor.MiddleCenter);T(o,"Hint",30,368,600,48,TextAnchor.MiddleLeft);
   var c=t.GetComponent<Demo5.FrontEnd.SettlementCraftPanel>();R(o,c.OrderScroll.transform.name,24,74,612,276);c.OrderContent.sizeDelta=new Vector2(594,c.OrderContent.sizeDelta.y);if(c.OrderScroll.verticalScrollbar)R(c.OrderScroll.transform,c.OrderScroll.verticalScrollbar.transform.name,604,0,8,276);
   R(w,"ItemIcon",593,96,132,152);
  });
  Edit("CraftOrderRow",t=>{((RectTransform)t).sizeDelta=new Vector2(594,118);});
  Edit("RestWorkPanel",t=>{
   var p=t.Find("TaskPaper");T(p,"EffectLabel",32,744,250,46,TextAnchor.MiddleLeft);T(p,"Effect",318,744,306,46,TextAnchor.MiddleRight);
   T(p,"DurationLabel",83,679,335,46,TextAnchor.MiddleLeft);T(p,"Duration",430,679,194,46,TextAnchor.MiddleRight);R(p,"ClockIcon",31,684,36,36);
   foreach(Transform child in p){if(child.name=="Rule"){var r=(RectTransform)child;if(Mathf.Approximately(-r.anchoredPosition.y,753))r.anchoredPosition=new Vector2(r.anchoredPosition.x,-735);}}
  });
  Edit("CraftOrderRow",t=>{R(t,"Cancel",482,28.5f,92,61);T(t,"Title",87,9,375,43,TextAnchor.MiddleLeft);T(t,"Member",87,52,205,40,TextAnchor.MiddleLeft);T(t,"Status",295,52,167,40,TextAnchor.MiddleRight);R(t,"ProgressTrack",87,96,375,6);R(t,"Icon",14,30,58,58);});
  foreach(var n in new[]{"CraftRecipeRow","CraftWorkerRow","CraftCostRow","WorkAssigneeRow"})Edit(n,t=>{var r=(RectTransform)t;foreach(Transform child in t){if(child.name!="Icon"&&child.name!="Portrait"&&child.name!="Selected")continue;var a=(RectTransform)child;a.anchoredPosition=new Vector2(a.anchoredPosition.x,-(r.rect.height-a.rect.height)/2);}});
  AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Content insets and row icon centers saved.";
 }
}
