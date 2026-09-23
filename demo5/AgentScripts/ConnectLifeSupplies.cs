using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Demo5.FrontEnd;
public static class ConnectLifeSupplies {
 const string P="Assets/Prefabs/Settlement/";
 static void Edit(string name,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static SettlementCraftPanel.Cost Cost(string id,int count)=>new SettlementCraftPanel.Cost{MaterialId=id,Count=count};
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve dirty scene first.");
  var water=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-water.png");var cloth=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Inventory/cloth.png");
  Edit("CraftWorkPanel",g=>{
   var c=g.GetComponent<SettlementCraftPanel>();var mats=c.Materials.ToList();
   if(!mats.Any(m=>m.Id=="raw-water"))mats.Add(new SettlementCraftPanel.Material{Id="raw-water",Name="미정수 물",Initial=2,Icon=water});
   if(!mats.Any(m=>m.Id=="bandage"))mats.Add(new SettlementCraftPanel.Material{Id="bandage",Name="붕대",Initial=0,Icon=cloth});c.Materials=mats.ToArray();
   var recipes=c.Recipes.Where(r=>r.Id!="water"&&r.Id!="bandage").ToList();
   recipes.Add(new SettlementCraftPanel.Recipe{Id="water",Name="물 정수",Category=0,Minutes=20,Icon=water,Description="미정수 물 처리\n완성: 물 1개",Costs=new[]{Cost("raw-water",1),Cost("wood",1)}});
   recipes.Add(new SettlementCraftPanel.Recipe{Id="bandage",Name="붕대 제작",Category=0,Minutes=15,Icon=cloth,Description="완성: 붕대 1개\n사용: 체력 +2",Costs=new[]{Cost("cloth",1),Cost("water",1)}});c.Recipes=recipes.ToArray();
  });
  Edit("InventoryPanel",g=>{
   var c=g.GetComponent<SettlementInventoryPanel>();var items=c.Items.Where(i=>i.Id!="raw-water"&&i.Id!="bandage").ToList();
   items.Add(new SettlementInventoryPanel.Item{Id="raw-water",Name="미정수 물",Icon=water,Category=3,Recovery=0,Description="작업대에서 정수할 재료입니다.\n정수 후 조리와 붕대 제작에 사용"});
   items.Add(new SettlementInventoryPanel.Item{Id="bandage",Name="붕대",Icon=cloth,Category=3,Recovery=2,UseCost=1,UseMinutes=10,UseVerb="치료",FieldUsable=true,Description="정착지와 탐험 중 사용하는 치료품\n1개 · 10분 · 체력 +2"});c.Items=items.ToArray();
  });
  Edit("ExpeditionLootPanel",g=>{var c=g.GetComponent<ExpeditionLootPanel>();var drops=c.Sites[0].Drops.Where(d=>d.Id!="raw-water").ToList();drops.Add(new ExpeditionLootPanel.Drop{Id="raw-water",Count=3,Chance=85});c.Sites[0].Drops=drops.ToArray();});
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
  return "Added editable water purification and bandage crafting, new stock/items and raw-water search drop. Existing UI layout/assets retained; field item use is data-driven. New-game raw water 2; balance provisional.";
 }
}
