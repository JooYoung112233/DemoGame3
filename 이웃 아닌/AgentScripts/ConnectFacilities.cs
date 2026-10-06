using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
public static class ConnectFacilities
{
 const string P="Assets/Prefabs/Settlement/";
 static void Edit(string name,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static Button Improve(Transform parent,Button source){var old=parent.Find("FacilityImprove");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);var b=UnityEngine.Object.Instantiate(source,parent);b.name="FacilityImprove";var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(1390,-32);r.sizeDelta=new Vector2(450,72);b.onClick=new Button.ButtonClickedEvent();var label=b.GetComponentInChildren<Text>();label.text="시설 개선";label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(16,0);label.rectTransform.offsetMax=new Vector2(-16,0);return b;}
 static Text Heading(GameObject g)=>g.transform.Find("PopupHeading").GetComponentInChildren<Text>(true);
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve dirty scene first");
  Edit("CraftWorkPanel",g=>{var c=g.GetComponent<SettlementCraftPanel>();var r=c.Recipes.Where(x=>x.Id!="upgrade-cooker").ToList();r.Add(new SettlementCraftPanel.Recipe{Id="upgrade-cooker",Name="조리대 개선",Description="조리대 Lv.1 → Lv.2",Category=2,Minutes=60,Icon=c.Recipes.First(x=>x.Id=="upgrade-bench").Icon,Costs=new[]{new SettlementCraftPanel.Cost{MaterialId="wood",Count=3},new SettlementCraftPanel.Cost{MaterialId="scrap",Count=2},new SettlementCraftPanel.Cost{MaterialId="nails",Count=2}}});c.Recipes=r.ToArray();});
  var root=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");
  try{
   var c=root.GetComponent<SettlementController>();var rest=c.WorkPanel;var cook=c.CookingPanel;
   rest.Improve=Improve(rest.View.transform,rest.CloseButton);rest.FacilityHeading=Heading(rest.View);
   cook.Improve=Improve(cook.Workspace.transform,cook.Back);cook.FacilityHeading=Heading(cook.gameObject);
   PrefabUtility.SaveAsPrefabAsset(root,P+"SettlementScreen.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");return "Connected bed/cooker improvement entries, level headings and editable cooker upgrade recipe. Existing paper/button assets reused.";
 }
}

