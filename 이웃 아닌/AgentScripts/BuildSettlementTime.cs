using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSettlementTime {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string sprite){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(sprite!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(sprite);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string text,int size){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.045f,.065f,.06f);t.text=text;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string text){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,text,26).alignment=TextAnchor.MiddleCenter;return b;}
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
  var row=I("TimeWorkRow",null,0,0,1104,124,A+"footer-paper.png");var rc=row.gameObject.AddComponent<TimeWorkRow>();
  rc.Icon=I("Icon",row.transform,22,26,66,66,"Assets/Art/Settlement/icon-bed.png");rc.Icon.preserveAspect=true;
  rc.Title=T("Title",row.transform,112,10,650,42,"작업",26);rc.Detail=T("Detail",row.transform,112,57,650,58,"담당자",18);rc.Result=T("Result",row.transform,786,12,290,100,"완료 예정",24);rc.Result.alignment=TextAnchor.MiddleCenter;
  var element=row.gameObject.AddComponent<LayoutElement>();element.preferredWidth=1104;element.preferredHeight=124;var rowAsset=PrefabUtility.SaveAsPrefabAsset(row.gameObject,P+"TimeWorkRow.prefab");Object.DestroyImmediate(row.gameObject);
  var root=R("SettlementTimePanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<SettlementTimePanel>();c.View=root.gameObject;c.RowPrefab=rowAsset.GetComponent<TimeWorkRow>();c.RestIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-bed.png");c.CompletedIcon=AssetDatabase.LoadAssetAtPath<Sprite>(A+"selected-check.png");
  var dim=I("Dim",root,0,0,1920,1080,null);dim.color=new Color(0,0,0,.72f);dim.raycastTarget=true;
  var paper=I("Paper",root,360,150,1200,748,A+"card-paper.png");
  T("Title",paper.transform,48,30,600,62,"시간 진행",40);c.Clock=T("Clock",paper.transform,700,42,450,42,"",26);c.Clock.alignment=TextAnchor.MiddleRight;
  c.Choices=new[]{B("15Minutes",paper.transform,48,112,250,60,"15분"),B("30Minutes",paper.transform,332,112,250,60,"30분"),B("60Minutes",paper.transform,616,112,250,60,"1시간"),B("NextCompletion",paper.transform,900,112,250,60,"다음 완료까지")};
  c.Preview=T("Preview",paper.transform,48,198,1100,54,"",30);
  var scroll=R("WorkSummaryScroll",paper.transform,48,272,1104,414);c.Scroll=scroll.gameObject.AddComponent<ScrollRect>();c.Scroll.horizontal=false;c.Scroll.movementType=ScrollRect.MovementType.Clamped;c.Scroll.scrollSensitivity=38;
  var viewport=I("Viewport",scroll,0,0,1104,414,null);viewport.color=new Color(1,1,1,.015f);viewport.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();c.Scroll.viewport=viewport.rectTransform;
  c.Content=R("Content",viewport.transform,0,0,1104,414);c.Scroll.content=c.Content;var layout=c.Content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=layout.childForceExpandWidth=false;c.Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  c.Summary=T("SummaryData",root,0,0,1,1,"",12);c.Summary.gameObject.SetActive(false);
  c.Empty=T("Empty",viewport.transform,24,60,1056,160,"진행 중인 작업이 없습니다.\n시설에서 담당자와 작업을 지정하세요.",28);c.Empty.alignment=TextAnchor.MiddleCenter;
  var hint=R("MoreBelow",scroll,1064,378,28,18);hint.gameObject.AddComponent<RoundedDownArrow>().color=new Color(.1f,.12f,.1f,.4f);var group=hint.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;var more=hint.gameObject.AddComponent<ScrollMoreIndicator>();more.Scroll=c.Scroll;more.Visibility=group;
  c.CloseButton=B("Back",root,28,974,320,76,"돌아가기");c.Confirm=B("Advance",root,1572,974,320,76,"시간 진행");c.ConfirmLabel=c.Confirm.GetComponentInChildren<Text>();
  var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,P+"SettlementTimePanel.prefab");Object.DestroyImmediate(root.gameObject);
  var screen=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");try{var old=screen.transform.Find("SettlementTimePanel");if(old)Object.DestroyImmediate(old.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,screen.transform);var owner=screen.GetComponent<SettlementController>();owner.TimePanel=instance.GetComponent<SettlementTimePanel>();owner.TimePanel.HideWhileOpen=new[]{owner.Main.transform.Find("Roster").gameObject,owner.Main.transform.Find("ArrivalNotice").gameObject,owner.Previous.gameObject,owner.Next.gameObject};PrefabUtility.RecordPrefabInstancePropertyModifications(owner.TimePanel);instance.SetActive(false);PrefabUtility.SaveAsPrefabAsset(screen,P+"SettlementScreen.prefab");}finally{PrefabUtility.UnloadPrefabContents(screen);}
  // Existing tool icon and inventory entry are reused; add stock storage for its crafted output.
  var craft=PrefabUtility.LoadPrefabContents(P+"CraftWorkPanel.prefab");try{var cc=craft.GetComponent<SettlementCraftPanel>();if(!cc.Materials.Any(m=>m.Id=="prybar")){var r=cc.Recipes.First(x=>x.Id=="prybar");cc.Materials=cc.Materials.Concat(new[]{new SettlementCraftPanel.Material{Id=r.Id,Name=r.Name,Icon=r.Icon}}).ToArray();}PrefabUtility.SaveAsPrefabAsset(craft,P+"CraftWorkPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(craft);}
  var inventory=PrefabUtility.LoadPrefabContents(P+"InventoryPanel.prefab");try{var ic=inventory.GetComponent<SettlementInventoryPanel>();if(!ic.Items.Any(x=>x.Id=="prybar"))ic.Items=ic.Items.Concat(new[]{new SettlementInventoryPanel.Item{Id="prybar",Name="간이 지렛대",Description="오락기 정비판을 여는 도구입니다.\n수색 담당자의 가방에 넣어주세요.",Category=2,Icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CraftWorkPanel/prybar.png")}}).ToArray();PrefabUtility.SaveAsPrefabAsset(inventory,P+"InventoryPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(inventory);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Time preview/confirmation prefab connected. Existing paper/typography reused; footer baseline 1050.";
 }
}
