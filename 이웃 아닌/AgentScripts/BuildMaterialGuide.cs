using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class BuildMaterialGuide {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string art){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,int size){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");t.fontSize=size;t.color=new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 public static string Run(){if(EditorApplication.isPlaying)throw new System.Exception("Stop first");
  var root=I("CraftMaterialGuide",null,1190,168,660,262,A+"card-paper.png");var c=root.gameObject.AddComponent<CraftMaterialGuide>();c.Icon=I("Icon",root.transform,24,18,48,48,null);c.Icon.preserveAspect=true;c.Title=T("Title",root.transform,88,16,548,52,32);c.Title.text="재료 안내";c.Counts=T("Counts",root.transform,24,76,612,40,27);c.Source=T("Source",root.transform,24,117,612,80,24);T("Hint",root.transform,24,206,340,38,23).text="재료 행을 눌러 확인";
  var im=I("Action",root.transform,404,204,232,46,A+"footer-paper.png");im.color=new Color(.76f,.86f,.68f);im.raycastTarget=true;c.Action=im.gameObject.AddComponent<Button>();c.Action.targetGraphic=im;c.ActionLabel=T("Label",im.transform,8,0,216,46,27);c.ActionLabel.alignment=TextAnchor.MiddleCenter;c.ActionLabel.text="제작법 보기";
  var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,P+"CraftMaterialGuide.prefab");Object.DestroyImmediate(root.gameObject);
  var g=PrefabUtility.LoadPrefabContents(P+"CraftWorkPanel.prefab");try{var w=g.transform.Find("Workspace");var old=w.Find("CraftMaterialGuide");if(old)Object.DestroyImmediate(old.gameObject);var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab,w);g.GetComponent<SettlementCraftPanel>().MaterialGuide=inst.GetComponent<CraftMaterialGuide>();PrefabUtility.SaveAsPrefabAsset(g,P+"CraftWorkPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}
  g=PrefabUtility.LoadPrefabContents(P+"CraftCostRow.prefab");try{var b=g.GetComponent<Button>();if(!b)b=g.AddComponent<Button>();b.targetGraphic=g.GetComponent<Image>();g.GetComponent<Image>().raycastTarget=true;PrefabUtility.SaveAsPrefabAsset(g,P+"CraftCostRow.prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Reusable material guide and clickable cost rows saved.";
 }
}
