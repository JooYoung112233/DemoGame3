using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class RedesignFieldBags {
 static Font font;static Sprite paper;static Color cream=new Color(.95f,.92f,.82f),ink=new Color(.05f,.07f,.06f);
 static void R(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static RectTransform Node(string n,Transform p,float x,float y,float w,float h){var go=new GameObject(n,typeof(RectTransform));go.transform.SetParent(p,false);R(go.transform,x,y,w,h);return (RectTransform)go.transform;}
 static Text Text(string n,Transform p,float x,float y,float w,float h,string value,int size=25){var t=Node(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.color=cream;t.alignment=TextAnchor.MiddleLeft;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;}
 static Button Button(string n,Transform p,float x,float y,float w,float h,string label){var r=Node(n,p,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.sprite=paper;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;var t=Text("Label",r,0,0,w,h,label,26);t.color=ink;t.alignment=TextAnchor.MiddleCenter;return b;}
 static void Label(Text t,float x,float y,float w,float h,int size,Color color){R(t.transform,x,y,w,h);t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.verticalOverflow=VerticalWrapMode.Overflow;}
 static void ButtonRect(Button b,float x,float y,float w,float h,int size){R(b.transform,x,y,w,h);var t=b.GetComponentInChildren<Text>();R(t.transform,8,0,w-16,h);t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.verticalOverflow=VerticalWrapMode.Overflow;}
 static void Items(RectTransform content,float x,float y){var scroll=content.GetComponentInParent<ScrollRect>();R(scroll.transform,x,y,502,446);R(content,0,0,480,446);var grid=content.GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(126,126);grid.spacing=new Vector2(24,20);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.childAlignment=TextAnchor.UpperCenter;
 if(scroll.verticalScrollbar){R(scroll.verticalScrollbar.transform,488,0,12,446);scroll.verticalScrollbar.handleRect.sizeDelta=Vector2.zero;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;}scroll.verticalNormalizedPosition=1;}
 static void Members(RectTransform content,float x){var scroll=content.GetComponentInParent<ScrollRect>();R(scroll.transform,x,221,426,132);R(content,0,0,426,126);scroll.horizontal=false;scroll.vertical=false;content.anchoredPosition=Vector2.zero;var layout=content.GetComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.UpperCenter;layout.spacing=12;var fit=content.GetComponent<ContentSizeFitter>();if(fit)fit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;}
 public static string Live(){var b=Object.FindAnyObjectByType<ExpeditionBagPanel>(FindObjectsInactive.Include);var w=b.Workspace.transform;foreach(var t in w.Cast<Transform>().Where(t=>t.name=="Section").ToArray())t.SetAsFirstSibling();Members(b.LeftMembers,144);Members(b.RightMembers,718);Canvas.ForceUpdateCanvases();return "Panel backgrounds moved behind both member lists; 3 cards fit fully.";}
 public static string Run(){const string path="Assets/Prefabs/Settlement/ExpeditionBagPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);try{
 var b=go.GetComponent<ExpeditionBagPanel>();var w=b.Workspace.transform;font=b.LeftTitle.font;paper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
 var prev=w.Find("BagRedesign");if(prev)Object.DestroyImmediate(prev.gameObject);var extra=Node("BagRedesign",w,0,0,1920,1080);
 foreach(var n in new[]{"MainPaper","TitlePaper","Title"})w.Find(n).gameObject.SetActive(false);
 Label(w.Find("Hint").GetComponent<Text>(),670,38,1170,60,25,cream);w.Find("Hint").GetComponent<Text>().text="가방에서 물품 선택 → 사용하거나 동료에게 전달";
 var panels=w.Cast<Transform>().Where(t=>t.name=="Section").ToArray();R(panels[0],80,144,550,800);R(panels[1],654,144,550,800);R(panels[2],1228,144,612,800);foreach(var panel in panels)panel.SetAsFirstSibling();
 Text("LeftSelectorTitle",extra,104,160,480,44,"가방 A · 대원 선택",27);Text("RightSelectorTitle",extra,678,160,480,44,"가방 B · 대원 선택",27);
 Members(b.LeftMembers,144);Members(b.RightMembers,718);b.MembersPerPage=3;
 b.LeftPrevious=Button("LeftPrevious",extra,98,249,38,62,"‹");b.LeftNext=Button("LeftNext",extra,582,249,38,62,"›");b.RightPrevious=Button("RightPrevious",extra,672,249,38,62,"‹");b.RightNext=Button("RightNext",extra,1156,249,38,62,"›");
 b.LeftPage=Text("LeftPage",extra,144,352,410,38,"",19);b.RightPage=Text("RightPage",extra,718,352,410,38,"",19);b.LeftPage.alignment=b.RightPage.alignment=TextAnchor.MiddleCenter;
 R(w.Find("FieldHeadingPaper"),104,402,326,58);R(w.Find("BagHeadingPaper"),678,402,326,58);
 Label(b.LeftTitle,118,402,298,58,24,ink);Label(b.RightTitle,692,402,298,58,24,ink);Label(b.LeftCapacity,450,402,156,58,25,cream);Label(b.RightCapacity,1024,402,156,58,25,cream);b.LeftCapacity.alignment=b.RightCapacity.alignment=TextAnchor.MiddleRight;
 Items(b.LeftItems,104,480);Items(b.RightItems,678,480);Label(b.EmptyLeft,125,610,460,100,25,cream);Label(b.EmptyRight,699,610,460,100,25,cream);
 R(w.Find("DetailTitlePaper"),1252,162,560,62);Label(b.DetailTitle,1270,162,520,62,28,ink);R(b.DetailIcon.transform,1254,263,104,126);b.DetailIcon.preserveAspect=true;Label(b.Description,1382,250,426,163,23,cream);
 Text("UseHeading",extra,1254,429,540,42,"사용 · 가방 주인",27);b.UseHint=Text("UseHint",extra,1254,478,544,102,"",21);ButtonRect(b.Use,1254,598,558,64,27);
 w.Find("QuantityHeading").GetComponent<Text>().text="전달 수량";Label(w.Find("QuantityHeading").GetComponent<Text>(),1254,688,540,42,27,cream);
 ButtonRect(b.Minus,1254,745,64,58,30);Label(b.Quantity,1332,745,100,58,28,cream);b.Quantity.alignment=TextAnchor.MiddleCenter;ButtonRect(b.Plus,1446,745,64,58,30);ButtonRect(b.Max,1532,745,142,58,25);Label(b.Message,1254,827,558,88,22,cream);
 ButtonRect(b.Back,80,974,410,76,30);ButtonRect(b.Transfer,1442,974,410,76,25);b.Transfer.GetComponent<Image>().sprite=paper;
 go.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);go.transform.Find("PopupHeading").GetComponentInChildren<Text>().text="원정대 · 개인 가방";
 PrefabUtility.SaveAsPrefabAsset(go,path);AssetDatabase.SaveAssets();return "Saved symmetric bags, 3-person paging, contextual use panel and named transfer footer.";
 }finally{PrefabUtility.UnloadPrefabContents(go);}}
}
