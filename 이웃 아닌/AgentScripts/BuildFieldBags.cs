using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildFieldBags {
 const string P="Assets/Prefabs/Settlement/";
 static void Rect(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  var g=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P+"ExpeditionLootPanel.prefab"));g.name="ExpeditionBagPanel";
  try{
   var old=g.GetComponent<ExpeditionLootPanel>();var c=g.AddComponent<ExpeditionBagPanel>();c.View=g;c.Workspace=old.Workspace;c.SlotPrefab=old.SlotPrefab;c.MemberPrefab=old.MemberPrefab;c.LeftItems=old.FieldContent;c.RightItems=old.BagContent;c.RightTitle=old.BagTitle;c.RightCapacity=old.Capacity;c.DetailTitle=old.DetailTitle;c.Description=old.Description;c.Quantity=old.Quantity;c.Message=old.Message;c.DetailIcon=old.DetailIcon;c.Back=old.Back;c.Minus=old.Minus;c.Plus=old.Plus;c.Max=old.Max;c.Transfer=old.Transfer;c.EmptyLeft=old.Empty;
   var main=c.Workspace.transform;old.Title.text="원정대 · 개인 가방";main.Find("Hint").GetComponent<Text>().text="함께 온 대원끼리 물건을 나눌 수 있습니다.";
   c.LeftTitle=main.Find("FieldHeading").GetComponent<Text>();Rect(main.Find("FieldHeadingPaper"),120,426,280,65);Rect(c.LeftTitle,135,426,255,65);c.LeftTitle.fontSize=27;c.RightTitle.fontSize=27;
   c.LeftCapacity=Object.Instantiate(c.RightCapacity,main);c.LeftCapacity.name="LeftCapacity";Rect(c.LeftCapacity,405,430,130,60);c.LeftCapacity.fontSize=27;
   c.LeftMembers=old.Members;var leftView=(RectTransform)c.LeftMembers.parent;leftView.name="LeftMembers";Rect(leftView,120,252,424,132);c.LeftMembers.sizeDelta=new Vector2(424,126);
   var rightView=Object.Instantiate(leftView,main);rightView.name="RightMembers";Rect(rightView,604,252,565,132);c.RightMembers=rightView.GetComponent<ScrollRect>().content;c.RightMembers.sizeDelta=new Vector2(565,126);
   c.EmptyLeft.text="가방이 비어 있습니다.";c.EmptyLeft.fontSize=26;c.EmptyRight=Object.Instantiate(c.EmptyLeft,main);c.EmptyRight.name="EmptyRight";Rect(c.EmptyRight,624,625,525,110);
   c.Description.fontSize=24;c.Message.fontSize=24;
   Rect(c.Back,28,974,410,76);Rect(c.Transfer,1482,974,410,76);c.Back.GetComponentInChildren<Text>().text="돌아가기";c.Transfer.GetComponentInChildren<Text>().fontSize=29;
   Object.DestroyImmediate(old.LeaveReview);Object.DestroyImmediate(old);
   PrefabUtility.SaveAsPrefabAsset(g,P+"ExpeditionBagPanel.prefab");
  }finally{Object.DestroyImmediate(g);}
  var root=PrefabUtility.LoadPrefabContents(P+"ExpeditionArrivalPanel.prefab");try{var old=root.transform.Find("ExpeditionBagPanel");if(old)Object.DestroyImmediate(old.gameObject);var node=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P+"ExpeditionBagPanel.prefab"),root.transform);root.GetComponent<ExpeditionArrivalPanel>().FieldBags=node.GetComponent<ExpeditionBagPanel>();node.SetActive(false);root.GetComponent<ExpeditionArrivalPanel>().Fade.transform.SetAsLastSibling();PrefabUtility.SaveAsPrefabAsset(root,P+"ExpeditionArrivalPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Field bag panel saved with reusable item and member prefabs; symmetric footer baseline 1050.";
 }
}
