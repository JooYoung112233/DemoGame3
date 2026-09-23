using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
public static class ConnectHousing
{
 const string P="Assets/Prefabs/Settlement/";
 static SettlementCraftPanel.Cost Cost(string id,int n)=>new SettlementCraftPanel.Cost{MaterialId=id,Count=n};
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve dirty scene first");
  var craft=PrefabUtility.LoadPrefabContents(P+"CraftWorkPanel.prefab");
  try{
   var c=craft.GetComponent<SettlementCraftPanel>();var list=c.Recipes.Where(r=>r.Id!="open-side-room"&&r.Id!="prepare-side-room").ToList();var icon=c.Recipes.First(r=>r.Id=="upgrade-bench").Icon;
   list.Add(new SettlementCraftPanel.Recipe{Id="open-side-room",Name="옆방 통로 연결",Description="막힌 통로를 정리합니다.",Category=2,Minutes=45,Icon=icon,Costs=new[]{Cost("wood",2),Cost("scrap",2)}});
   list.Add(new SettlementCraftPanel.Recipe{Id="prepare-side-room",Name="옆방 거주 준비",Description="연결된 옆방을 정리합니다.",Category=2,Minutes=90,Icon=icon,Costs=new[]{Cost("wood",4),Cost("cloth",3),Cost("nails",2)}});
   c.Recipes=list.ToArray();PrefabUtility.SaveAsPrefabAsset(craft,P+"CraftWorkPanel.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(craft);}
  var root=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");
  try{
   var c=root.GetComponent<SettlementController>();var old=c.Main.transform.Find("HousingButton");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   var b=UnityEngine.Object.Instantiate(c.GameMenu.OpenButton,c.Main.transform);b.name="HousingButton";b.onClick=new Button.ButtonClickedEvent();
   var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(480,-26);r.sizeDelta=new Vector2(280,66);
   var t=b.GetComponentInChildren<Text>();t.text="거주 공간  2 / 3";t.fontSize=25;t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(12,0);t.rectTransform.offsetMax=new Vector2(-12,0);
   c.CraftPanel.HousingButton=b;c.CraftPanel.HousingLabel=t;
   PrefabUtility.SaveAsPrefabAsset(root,P+"SettlementScreen.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");return "Two sequential side-room jobs, current/capacity HUD and housing shortcut connected; original background preserved.";
 }
}
