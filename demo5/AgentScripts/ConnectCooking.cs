using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class ConnectCooking {
 const string P="Assets/Prefabs/Settlement/";
 static Sprite S(string s)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/"+s+".png");
 static void Edit(string name,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static void Rect(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static T Map<T>(T component,Transform source,Transform target)where T:Component=>target.Find(AnimationUtility.CalculateTransformPath(component.transform,source)).GetComponent<T>();
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve dirty scene first.");
  var food=S("HomeSelection/icon-supplies");var water=S("Settlement/icon-water");var bag=S("Inventory/bag");
  string[] ids={"food","water","can","meal","ration"},names={"식재료","물","통조림","조리한 식사","원정 도시락"};int[] amounts={6,4,2,0,0};Sprite[] icons={food,water,food,food,bag};
  Edit("CraftWorkPanel",g=>{var c=g.GetComponent<SettlementCraftPanel>();var list=c.Materials.ToList();for(int i=0;i<ids.Length;i++)if(!list.Any(m=>m.Id==ids[i]))list.Add(new SettlementCraftPanel.Material{Id=ids[i],Name=names[i],Initial=amounts[i],Icon=icons[i]});c.Materials=list.ToArray();});
  Edit("InventoryPanel",g=>{var c=g.GetComponent<SettlementInventoryPanel>();var list=c.Items.ToList();for(int i=0;i<ids.Length;i++){var item=list.FirstOrDefault(m=>m.Id==ids[i]);if(item==null){item=new SettlementInventoryPanel.Item{Id=ids[i]};list.Add(item);}item.Name=names[i];item.Icon=icons[i];item.Category=1;item.Recovery=i>=3?1:0;item.UseCost=1;item.UseMinutes=10;item.UseVerb="먹기";item.FieldUsable=i==4;item.Description=i<3?"식량 준비에 사용하는 재료입니다.\n수색으로 찾아 가방에 담을 수 있습니다.":i==3?"정착지에서 먹는 식사입니다.\n1개 · 10분 · 체력 +1":"정착지 또는 탐험 중 먹는 도시락입니다.\n1개 · 10분 · 체력 +1";}c.Items=list.ToArray();});
  Edit("CookingPanel",g=>{
   var c=g.GetComponent<SettlementCookingPanel>();var craft=AssetDatabase.LoadAssetAtPath<GameObject>(P+"CraftWorkPanel.prefab").GetComponent<SettlementCraftPanel>();
   c.Workspace=g.transform.Find("Workspace").GetComponent<CanvasGroup>();
   if(!c.OrderScroll){c.OrderScroll=Object.Instantiate(craft.OrderScroll,c.PlanBody.transform.parent);c.OrderScroll.name="OrderScroll";}c.OrderContent=c.OrderScroll.content;c.OrderPrefab=craft.OrderPrefab;
   Rect(c.OrderScroll,24,74,612,276);Rect(c.PlanBody,30,145,600,118);c.PlanBody.alignment=TextAnchor.MiddleCenter;Rect(c.PlanHint,30,368,600,48);c.PlanHint.fontSize=23;
   if(!c.CancelPopup)c.CancelPopup=Object.Instantiate(craft.CancelPopup,g.transform);c.CancelPopup.name="CancelPopup";
   c.CancelYes=Map(craft.CancelYes,craft.CancelPopup.transform,c.CancelPopup.transform);c.CancelNo=Map(craft.CancelNo,craft.CancelPopup.transform,c.CancelPopup.transform);c.CancelMessage=Map(craft.CancelMessage,craft.CancelPopup.transform,c.CancelPopup.transform);c.CancelPopup.SetActive(false);
   c.Ingredients=craft.Materials.Select(m=>new SettlementCookingPanel.Ingredient{Id=m.Id,Name=m.Name,Icon=m.Icon}).ToArray();
   foreach(var m in c.Meals){m.OutputId=m.Id=="ration"?"ration":"meal";foreach(var cost in m.Costs){if(cost.MaterialId=="fuel")cost.MaterialId="wood";if(cost.MaterialId=="wrap")cost.MaterialId="cloth";}m.Use=m.Id=="ration"?"완성 후 창고에 보관\n개인 가방에 넣어 탐험 중 사용":"완성 후 창고에 보관\n식사 1개 · 10분 · 체력 +1";}
   c.Meals.First(m=>m.Id=="stew").Description="물과 목재를 더 써\n세 사람의 식사 준비";
   c.PlanTitle.text="진행 중인 조리";c.PlanBody.text="조리 예약이 없습니다.";c.PlanHint.text="재료는 예약 후 완료 시 소모됩니다.";c.ConfirmLabel.text="담당자 선택";c.ResultUse.text=c.Meals[0].Use;
   g.transform.Find("Workspace/Workshop/CostColumns").GetComponent<Text>().text="사용 가능 / 필요";
  });
  Edit("ExpeditionBagPanel",g=>{var c=g.GetComponent<ExpeditionBagPanel>();if(!c.Use)c.Use=Object.Instantiate(c.Transfer,c.Transfer.transform.parent);c.Use.name="Eat";Rect(c.Use,685,974,550,76);c.Use.GetComponentInChildren<Text>().text="먹기 · 1턴";});
  Edit("ExpeditionLootPanel",g=>{var c=g.GetComponent<ExpeditionLootPanel>();var list=c.Sites[0].Drops.Where(d=>d.Id!="food"&&d.Id!="water"&&d.Id!="can").ToList();list.Add(new ExpeditionLootPanel.Drop{Id="food",Count=2,Chance=85});list.Add(new ExpeditionLootPanel.Drop{Id="water",Count=2,Chance=80});list.Add(new ExpeditionLootPanel.Drop{Id="can",Count=1,Chance=45});c.Sites[0].Drops=list.ToArray();});
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
  return "Cooking queue/cancel UI, shared stock, food items and field eating connected; approved outer layout retained. Initial stock and recovery are provisional Inspector values.";
 }
}
