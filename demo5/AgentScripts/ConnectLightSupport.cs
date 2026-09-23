using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class ConnectLightSupport {
 static void Edit(string n,Action<GameObject> edit){string p="Assets/Prefabs/Settlement/"+n+".prefab";var g=PrefabUtility.LoadPrefabContents(p);try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,p);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");var icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Inventory/flashlight.png");
  Edit("CraftWorkPanel",g=>{var c=g.GetComponent<SettlementCraftPanel>();if(!c.Materials.Any(x=>x.Id=="flashlight"))c.Materials=c.Materials.Concat(new[]{new SettlementCraftPanel.Material{Id="flashlight",Name="손전등",Icon=icon,Initial=0}}).ToArray();if(!c.Recipes.Any(x=>x.Id=="flashlight"))c.Recipes=c.Recipes.Concat(new[]{new SettlementCraftPanel.Recipe{Id="flashlight",Name="손전등",Category=0,Minutes=30,Icon=icon,Description="남은 부품으로 손전등을 조립합니다.\n동료가 휴대하면 조명 지원이 가능합니다.",Costs=new[]{new SettlementCraftPanel.Cost{MaterialId="scrap",Count=2},new SettlementCraftPanel.Cost{MaterialId="nails",Count=1}}}}).ToArray();});
  Edit("InventoryPanel",g=>{var c=g.GetComponent<SettlementInventoryPanel>();if(!c.Items.Any(x=>x.Id=="flashlight"))c.Items=c.Items.Concat(new[]{new SettlementInventoryPanel.Item{Id="flashlight",Name="손전등",Category=3,Icon=icon,Description="수색 담당자 외 동료가 휴대하면\n조명 지원을 할 수 있습니다.\n현재는 재사용 도구입니다."}}).ToArray();});
  AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Flashlight craft recipe, stock and portable inventory item connected using existing icon.";
 }
}
