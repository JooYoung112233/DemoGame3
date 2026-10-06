using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class ConnectSearchTool {
 static void Edit(string name,Action<GameObject> action){string p="Assets/Prefabs/Settlement/"+name+".prefab";var g=PrefabUtility.LoadPrefabContents(p);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,p);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  Edit("ExpeditionLootPanel",g=>g.GetComponent<ExpeditionLootPanel>().Sites[2].RequiredTool="prybar");
  Edit("InventoryPanel",g=>g.GetComponent<SettlementInventoryPanel>().Items.First(i=>i.Id=="prybar").Description="오락기 정비판을 여는 도구입니다.\n수색 담당자의 가방에 넣어주세요.");
  Edit("ExpeditionArrivalPanel",g=>g.GetComponent<ExpeditionArrivalPanel>().ObjectDescriptions[2]="오락기 정비판이 단단히 닫혀 있다.\n지렛대로 열면 안쪽을 수색할 수 있다.");
  Edit("ExpeditionSearchPanel",g=>{
   var c=g.GetComponent<ExpeditionSearchPanel>();var old=c.Equipment.transform.parent.Find("RequiredToolIcon");var node=old?old.gameObject:new GameObject("RequiredToolIcon",typeof(RectTransform),typeof(Image));node.transform.SetParent(c.Equipment.transform.parent,false);
   var rect=(RectTransform)node.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(48,-807);rect.sizeDelta=new Vector2(30,30);
   c.ToolIcon=node.GetComponent<Image>();c.ToolIcon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CraftWorkPanel/prybar.png");c.ToolIcon.preserveAspect=true;c.ToolIcon.raycastTarget=false;
   c.Equipment.rectTransform.anchoredPosition=new Vector2(94,-805);c.Equipment.rectTransform.sizeDelta=new Vector2(556,38);c.Equipment.fontSize=20;c.Cost.fontSize=22;c.Cost.rectTransform.sizeDelta=new Vector2(604,78);
  });AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Arcade machine service panel requires worker-carried prybar on first search; reusable tool, opened state persists.";
 }
}
