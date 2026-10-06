using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildCookingUI {
 const string P="Assets/Prefabs/Settlement/";
 static Sprite S(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/"+path+".png");
 static void R(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static SettlementCraftPanel.Cost Cost(string id,int n)=>new SettlementCraftPanel.Cost{MaterialId=id,Count=n};
 static SettlementCookingPanel.Meal Meal(string id,string name,string desc,string use,int cat,int mins,int servings,Sprite icon,params SettlementCraftPanel.Cost[] costs)=>new SettlementCookingPanel.Meal{Id=id,Name=name,Description=desc,Use=use,Category=cat,Minutes=mins,Servings=servings,Icon=icon,Costs=costs};
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P+"CraftWorkPanel.prefab"));PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);g.name="CookingPanel";
  try{
   var old=g.GetComponent<SettlementCraftPanel>();var c=g.AddComponent<SettlementCookingPanel>();
   c.Back=old.CloseButton;c.Minus=old.Minus;c.Plus=old.Plus;c.Confirm=old.Confirm;c.Tabs=old.Tabs;c.Title=old.DetailTitle;c.Description=old.DetailDescription;c.Quantity=old.QuantityText;c.Duration=old.Duration;c.ConfirmLabel=old.ConfirmLabel;c.Icon=old.DetailIcon;c.RecipeContent=old.RecipeContent;c.WorkerContent=old.WorkerContent;c.CostContent=old.CostContent;c.RecipeScroll=old.RecipeScroll;c.WorkerScroll=old.WorkerScroll;c.CostScroll=old.CostScroll;
   c.RecipePrefab=old.RecipePrefab;c.WorkerPrefab=old.WorkerPrefab;c.CostPrefab=old.CostPrefab;
   var guide=old.MaterialGuide;c.ResultTitle=guide.Title;c.ResultCount=guide.Counts;c.ResultUse=guide.Source;c.ResultIcon=guide.Icon;Object.DestroyImmediate(guide.Action.gameObject);Object.DestroyImmediate(guide.transform.Find("Hint").gameObject);Object.DestroyImmediate(guide);R(c.ResultUse,24,117,612,108);c.ResultUse.fontSize=26;
   var jobs=old.EmptyOrders.transform.parent;Object.DestroyImmediate(old.OrderScroll.gameObject);Object.DestroyImmediate(old.OrderCount.gameObject);c.PlanTitle=jobs.Find("Title").GetComponent<Text>();R(jobs.Find("Heading"),0,-20,395,70);R(c.PlanTitle,20,-15,355,64);c.PlanTitle.fontSize=34;c.PlanBody=old.EmptyOrders;R(c.PlanBody,30,98,600,230);c.PlanBody.fontSize=30;c.PlanBody.alignment=TextAnchor.UpperLeft;c.PlanHint=jobs.Find("Hint").GetComponent<Text>();R(c.PlanHint,30,352,600,66);c.PlanHint.fontSize=23;
   Object.DestroyImmediate(old.CancelPopup);Object.DestroyImmediate(old);
   g.transform.Find("PopupHeading").GetComponentInChildren<Text>().text="정착지 · 식량 준비";
   var workshop=g.transform.Find("Workspace/Workshop");workshop.Find("Heading").GetComponent<Text>().text="조리대";workshop.Find("QuantityLabel").GetComponent<Text>().text="준비 횟수";workshop.Find("WorkerHeading").GetComponent<Text>().text="담당자 배정";workshop.Find("CostColumns").GetComponent<Text>().text="시안 재고 / 필요";
   string[] labels={"조리","휴대식","간편식"};for(int i=0;i<c.Tabs.Length;i++){var b=c.Tabs[i];Object.DestroyImmediate(b.transform.Find("Icon").gameObject);var t=b.transform.Find("Label").GetComponent<Text>();R(t,8,0,146,72);t.text=labels[i];t.fontSize=32;}
   var supply=S("HomeSelection/icon-supplies");var water=S("Settlement/icon-water");var wood=S("CraftWorkPanel/wood");var cloth=S("Inventory/cloth");var bag=S("Inventory/bag");
   c.Ingredients=new[]{new SettlementCookingPanel.Ingredient{Id="food",Name="식재료",PreviewCount=6,Icon=supply},new SettlementCookingPanel.Ingredient{Id="water",Name="물",PreviewCount=4,Icon=water},new SettlementCookingPanel.Ingredient{Id="fuel",Name="조리 연료",PreviewCount=3,Icon=wood},new SettlementCookingPanel.Ingredient{Id="meal",Name="조리한 식사",PreviewCount=2,Icon=supply},new SettlementCookingPanel.Ingredient{Id="wrap",Name="포장 재료",PreviewCount=2,Icon=cloth},new SettlementCookingPanel.Ingredient{Id="can",Name="통조림",PreviewCount=2,Icon=supply}};
   c.Meals=new[]{Meal("warm","따뜻한 한 끼","재료를 익혀\n두 사람의 식사 준비","정착지 식사용\n완성 후 공용 식량에 보관",0,20,2,supply,Cost("food",2),Cost("water",1),Cost("fuel",1)),Meal("stew","넉넉한 스튜","물과 연료를 더 써\n세 사람의 식사 준비","정착지 식사용\n한 번에 세 사람의 식사 준비",0,30,3,supply,Cost("food",2),Cost("water",2),Cost("fuel",2)),Meal("ration","원정 도시락","준비한 식사를 싸서\n밖에서 먹을 도시락","탐험 휴대용\n출발 전 개인 가방에 넣기",1,10,1,bag,Cost("meal",1),Cost("wrap",1)),Meal("canned","통조림 한 끼","불과 물 없이\n바로 먹을 한 끼","간편 식사용\n연료를 아끼고 빠르게 준비",2,0,1,supply,Cost("can",1))};
   // Initial editor labels also show honest preview data, never a production stock claim.
   c.Title.text="따뜻한 한 끼";c.Description.text=c.Meals[0].Description;c.ResultTitle.text="따뜻한 한 끼";c.ResultCount.text="완성 예상  2인분";c.ResultUse.text=c.Meals[0].Use;c.ResultIcon.sprite=supply;c.PlanTitle.text="준비할 식사";c.PlanBody.text="조리법과 수량을 고른 뒤\n담당자를 선택해 주세요.";c.PlanHint.text="UI 미리보기 · 실제 물자는 소모하지 않습니다.";
   var asset=PrefabUtility.SaveAsPrefabAsset(g,P+"CookingPanel.prefab");
   var screen=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");try{var previous=screen.transform.Find("CookingPanel");if(previous)Object.DestroyImmediate(previous.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,screen.transform);var owner=screen.GetComponent<SettlementController>();owner.CookingPanel=instance.GetComponent<SettlementCookingPanel>();foreach(var t in owner.Stock.GetComponentsInChildren<Text>(true))if(t.text=="비축 물자"||t.text=="물자비축")t.text="식량 준비";instance.SetActive(false);PrefabUtility.SaveAsPrefabAsset(screen,P+"SettlementScreen.prefab");}finally{PrefabUtility.UnloadPrefabContents(screen);}
  }finally{Object.DestroyImmediate(g);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Cooking UI prefab connected to Stock. Preview-only recipes, stock, assignment and confirmation; warehouse unchanged.";
 }
}
