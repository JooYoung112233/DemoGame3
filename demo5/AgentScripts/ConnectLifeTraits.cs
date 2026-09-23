using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class ConnectLifeTraits
{
 static void Place(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static Text Effect(Text duration){var parent=duration.transform.parent;var old=parent.Find("TraitEffect");var t=old?old.GetComponent<Text>():Object.Instantiate(duration,parent);t.name="TraitEffect";Place(t,480,624,576,56);t.fontSize=23;t.alignment=TextAnchor.MiddleLeft;t.text="시설 기본 · 담당 기본";var icon=parent.Find("DurationIcon");if(!icon){var g=new GameObject("DurationIcon",typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);icon=g.transform;}var im=icon.GetComponent<Image>();Place(im,36,637,28,28);im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/RestWorkPanel/icon-clock.png");im.preserveAspect=true;im.raycastTarget=false;im.color=new Color(.94f,.88f,.72f);Place(duration,80,624,364,56);duration.fontSize=25;return t;}
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve dirty scene first");
  var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
  foreach(var p in roster.Candidates){p.CookingTimePercent=p.Id=="cook"?80:100;p.CraftTimePercent=p.Id=="mechanic"?80:100;}EditorUtility.SetDirty(roster);
  foreach(string name in new[]{"CookingPanel","CraftWorkPanel"}){string path="Assets/Prefabs/Settlement/"+name+".prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var cook=g.GetComponent<SettlementCookingPanel>();if(cook){cook.TraitEffect=Effect(cook.Duration);foreach(var meal in cook.Meals)meal.WorkerSpeedAllowed=meal.Id=="warm"||meal.Id=="stew";}else{var craft=g.GetComponent<SettlementCraftPanel>();craft.TraitEffect=Effect(craft.Duration);}PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
  AssetDatabase.SaveAssets();return "Editable cook/mechanic time traits and separate facility/worker indicators connected. Warm/stew enabled; packing/canned excluded.";
 }
}
