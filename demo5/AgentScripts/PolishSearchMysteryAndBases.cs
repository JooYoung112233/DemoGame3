using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class PolishSearchMysteryAndBases {
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop and preserve dirty scene");
  const string path="Assets/Prefabs/Settlement/SettlementScreen.prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try{var a=g.GetComponent<SettlementController>().ArrivalPanel;var s=a.GetComponent<ExpeditionSearchStatus>();s.UnknownMarks=new Text[s.Labels.Length];s.SiteIcons=new Image[s.Labels.Length];
   for(int i=0;i<s.Labels.Length;i++){
    if(!s.Labels[i])continue;var b=a.Objects[i];var h=b.GetComponent<SettlementHotspot>();if(h){h.SuppressLabel=true;h.Label.SetActive(false);}
    var icon=b.transform.Find("Icon").GetComponent<Image>();s.SiteIcons[i]=icon;icon.enabled=false;
    var old=b.transform.Find("UnknownMark");if(old)Object.DestroyImmediate(old.gameObject);
    var t=new GameObject("UnknownMark",typeof(RectTransform),typeof(Text)).GetComponent<Text>();t.transform.SetParent(b.transform,false);
    var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
    t.font=s.Labels[i].font;t.fontSize=46;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.05f,.075f,.07f);t.text="?";t.raycastTarget=false;s.UnknownMarks[i]=t;
   }
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  EditBases("Assets/Prefabs/Settlement/FieldPawn.prefab");EditBases("Assets/Prefabs/Settlement/SettlementWorld.prefab");
  AssetDatabase.SaveAssets();return "Unknown search marks, single status labels, and layered pawn bases saved.";
 }
 static void EditBases(string path){var g=PrefabUtility.LoadPrefabContents(path);try{
  foreach(var source in g.GetComponentsInChildren<SpriteRenderer>(true)){
   if(source.name!="Base")continue;var parent=source.transform.parent;
   foreach(string name in new[]{"BaseShadow","BaseFoot","BaseBevel","BaseNeck"}){var old=parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);}
   var original=source.transform.localScale;var pos=source.transform.localPosition;int order=source.sortingOrder;
   // Existing oval artwork, separate editable renderer layers. Character artwork remains unchanged.
   Layer("BaseShadow",source,original,new Vector3(.05f,-.12f,.02f),new Vector3(1.16f,1.12f,1),new Color(.02f,.025f,.02f,.32f),order-4);
   Layer("BaseFoot",source,original,new Vector3(0,-.09f,0),new Vector3(1.04f,1,1),new Color(.31f,.28f,.22f,1),order-3);
   Layer("BaseBevel",source,original,new Vector3(0,-.055f,0),new Vector3(1.02f,1,1),new Color(.67f,.61f,.49f,1),order-2);
   Layer("BaseNeck",source,original,new Vector3(0,-.025f,0),new Vector3(.91f,1,1),new Color(.82f,.77f,.66f,1),order-1);
   source.color=new Color(.97f,.92f,.79f,1);
  }
  PrefabUtility.SaveAsPrefabAsset(g,path);
 }finally{PrefabUtility.UnloadPrefabContents(g);}}
 static void Layer(string name,SpriteRenderer source,Vector3 scale,Vector3 offset,Vector3 multiplier,Color color,int order){
  var s=new GameObject(name,typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();s.transform.SetParent(source.transform.parent,false);s.transform.localPosition=source.transform.localPosition+offset;s.transform.localScale=Vector3.Scale(scale,multiplier);s.sprite=source.sprite;s.sharedMaterial=source.sharedMaterial;s.sortingLayerID=source.sortingLayerID;s.sortingOrder=order;s.color=color;
 }
}
