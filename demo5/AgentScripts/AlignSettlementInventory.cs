using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class AlignSettlementInventory {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Pos(Transform p,string name,float x,float y,float w,float h)=>Rect(p.Find(name),x,y,w,h);
 static void Edit(string name,Action<GameObject> change){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{change(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static Image Image(string name,Transform p,float x,float y,float w,float h,string path){var old=p.Find(name);if(old)Object.DestroyImmediate(old.gameObject);var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(p,false);Rect(g.transform,x,y,w,h);var im=g.GetComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text Text(string name,Transform p,float x,float y,float w,float h,int size){var old=p.Find(name);if(old)Object.DestroyImmediate(old.gameObject);var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));g.transform.SetParent(p,false);Rect(g.transform,x,y,w,h);var t=g.GetComponent<Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");t.fontSize=size;t.fontStyle=FontStyle.Normal;t.color=new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 public static string Apply(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
  Edit("InventorySlot",g=>{var icon=(RectTransform)g.transform.Find("Icon");icon.anchorMin=icon.anchorMax=icon.pivot=new Vector2(.5f,1);icon.anchoredPosition=new Vector2(0,-9);icon.sizeDelta=new Vector2(77,65);var name=(RectTransform)g.transform.Find("Name");name.anchorMin=new Vector2(0,1);name.anchorMax=new Vector2(1,1);name.pivot=new Vector2(.5f,1);name.anchoredPosition=new Vector2(0,-87);name.sizeDelta=new Vector2(-14,28);var count=(RectTransform)g.transform.Find("Count");count.anchorMin=count.anchorMax=count.pivot=Vector2.one;count.anchoredPosition=new Vector2(-8,-62);count.sizeDelta=new Vector2(31,28);g.transform.Find("Name").GetComponent<Text>().fontSize=24;g.transform.Find("Count").GetComponent<Text>().fontSize=25;});
  Edit("InventoryMemberCard",g=>{Rect(g.transform,0,0,175,158);Pos(g.transform,"Icon",42.5f,12,90,88);Pos(g.transform,"Name",9,111,157,36);});
  Edit("InventoryPanel",g=>{var c=g.GetComponent<SettlementInventoryPanel>();var w=g.transform.Find("Workspace");g.transform.Find("InputBlocker").GetComponent<Image>().color=new Color(0,0,0,.62f);
   // One outer grid: left 80, middle 776, right 1420, bottom 952. Panel padding 24.
   Rect(c.CloseButton.transform,80,952,410,78);c.CloseButton.name="Back";var label=c.CloseButton.transform.Find("Label").GetComponent<Text>();label.text="돌아가기";label.fontSize=34;Rect(label.transform,65,0,290,78);var icon=Image("BackIcon",c.CloseButton.transform,22,22,26,34,A+"icon-left.png");icon.preserveAspect=true;
   Image("ClockPaper",w,80,65,235,102,A+"count-paper.png");c.HeaderClock=Text("HeaderClock",w,104,69,187,93,32);c.HeaderClock.text="DAY 1\n09:00";
   Image("LocationPaper",w,80,176,235,47,A+"count-paper.png");c.HeaderLocation=Text("HeaderLocation",w,104,177,187,43,29);c.HeaderLocation.text="정착지";
   for(int i=0;i<c.MemberCards.Length;i++)Rect(c.MemberCards[i].transform,386+i*198,65,175,158);Rect(c.PrevMember.transform,299,106,73,72);Pos(c.PrevMember.transform,"Label",6,0,61,72);c.PrevMember.transform.Find("Label").GetComponent<Text>().fontSize=55;Rect(c.NextMember.transform,1570,106,73,72);
   // Paper texture has a transparent inset: compensate so its visible border aligns with the DAY paper.
   Pos(w,"StockPanel",74,242,652,692);Pos(w,"BagPanel",770,242,636,692);Pos(w,"DetailPanel",1416,242,428,692);
   Pos(w,"StockTitlePaper",104,264,315,76);Pos(w,"StockTitleIcon",121,278,48,48);Pos(w,"StockTitle",184,269,225,65);
   Pos(w,"BagHeadingPaper",800,264,410,76);Pos(w,"BagIcon",817,278,48,48);Rect(c.BagTitle.transform,880,269,317,65);Rect(c.Capacity.transform,1276,269,100,65);c.Capacity.fontSize=30;c.Capacity.alignment=TextAnchor.MiddleRight;
   Pos(w,"ToolTitlePaper",1444,264,372,76);Pos(w,"ToolTitleIcon",1461,278,48,48);Pos(w,"ToolTitle",1524,269,280,65);
   for(int i=0;i<4;i++)Rect(c.Tabs[i].transform,104+i*146.5f,356,138.5f,56);
   Rect(c.StockScroll.transform,104,432,592,458);c.StockContent.sizeDelta=new Vector2(578,c.StockContent.sizeDelta.y);c.StockContent.GetComponent<GridLayoutGroup>().cellSize=new Vector2(137,120);c.StockContent.GetComponent<GridLayoutGroup>().spacing=new Vector2(10,12);
   Rect(c.BagScroll.transform,800,432,576,458);c.BagContent.sizeDelta=new Vector2(562,c.BagContent.sizeDelta.y);c.BagContent.GetComponent<GridLayoutGroup>().cellSize=new Vector2(133,120);Rect(c.StockScroll.verticalScrollbar.transform,585,0,7,458);Rect(c.BagScroll.verticalScrollbar.transform,569,0,7,458);Pos(w,"EmptyStock",104,578,578,100);
   Rect(c.ToBag.transform,728,552,40,64);Rect(c.ToStock.transform,728,636,40,64);Pos(c.ToBag.transform,"Label",0,0,40,64);Pos(c.ToStock.transform,"Label",0,0,40,64);c.ToBag.transform.Find("Label").GetComponent<Text>().fontSize=34;c.ToStock.transform.Find("Label").GetComponent<Text>().fontSize=34;
   for(int i=0;i<2;i++){Pos(w,"ToolSlot_"+i,1444+i*194,432,178,142);Pos(w.Find("ToolSlot_"+i),"Empty",9,40,160,62);}
   var toolLabels=w.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Label"&&t.text=="미장착").OrderBy(t=>((RectTransform)t.transform).anchoredPosition.x).ToArray();for(int i=0;i<toolLabels.Length;i++)Rect(toolLabels[i].transform,1444+i*194,583,178,38);
   Pos(w,"DetailTitlePaper",1444,637,372,58);Rect(c.DetailTitle.transform,1460,641,340,50);Rect(c.DetailIcon.transform,1452,711,75,74);Rect(c.Description.transform,1542,704,274,98);Rect(c.Location.transform,1444,788,372,35);
   Pos(w,"Use",1444,831,142,70);Rect(c.Move.transform,1602,831,214,70);Pos(w.Find("Use"),"Label",6,0,130,70);Pos(c.Move.transform,"Label",6,0,202,70);
   Pos(w,"NoticePaper",776,952,1064,78);Rect(c.Notice.transform,800,959,1016,64);
  });
  Edit("SettlementScreen",g=>{var owner=g.GetComponent<SettlementController>();var c=owner.InventoryPanel;var more=new[]{owner.Clock.transform.parent.gameObject,owner.Location.gameObject};c.HideWhileOpen=c.HideWhileOpen.Concat(more).Distinct().ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(c);});
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Inventory alignment saved: 62% dim, aligned headers/grids, grouped upper-left date/location, lower-left 410x78 Back button.";
 }
}
