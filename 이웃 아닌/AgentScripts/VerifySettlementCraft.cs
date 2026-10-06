using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifySettlementCraft
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
    static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow: "+t.name+" ("+t.preferredHeight+"/"+t.rectTransform.rect.height+")");}
    static void Wheel(ScrollRect scroll,float delta){var r=scroll.viewport;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),scrollDelta=new Vector2(0,delta)};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0,"Wheel has no hit target");Check(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.scrollHandler)==scroll.gameObject,"Wrong wheel target");}
    static void Hint(ScrollRect scroll,bool expected){var hint=scroll.viewport.GetComponentInChildren<ScrollMoreIndicator>();Check(hint&&hint.HasMoreBelow==expected,"Incorrect more-below hint on "+scroll.name);Check(!hint.Visibility.blocksRaycasts&&hint.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Hint blocks input");}
    public static async Task<string> ScrollLists(){
        var c=Object.FindAnyObjectByType<SettlementController>();var p=c.CraftPanel;var oldRecipes=p.Recipes;var oldMaterials=p.Materials;var icon=p.Recipes[0].Icon;
        try{
            p.Materials=Enumerable.Range(0,8).Select(i=>new SettlementCraftPanel.Material{Id="qa-"+i,Name="검증용 재료 "+(i+1),Initial=i==7?0:1,Icon=icon}).ToArray();
            var costs=p.Materials.Select(m=>new SettlementCraftPanel.Cost{MaterialId=m.Id,Count=1}).ToArray();p.Recipes=Enumerable.Range(0,12).Select(i=>new SettlementCraftPanel.Recipe{Id="qa-"+i,Name="검증 아이템 "+(i+1),Description="스크롤 검증",Category=0,Minutes=30,Icon=icon,Costs=costs}).ToArray();
            Click(c.Workbench);await Task.Delay(200);Check(p.RecipeRows.Count==12&&p.CostRows.Count==8,"Missing long list rows: "+p.RecipeRows.Count+"/"+p.CostRows.Count+" open="+p.IsOpen+" popup="+c.IsPopupOpen+" rest="+c.WorkPanel.IsOpen+" configured="+p.Recipes.Length);Check(p.RecipeScroll.verticalScrollbar.gameObject.activeSelf&&p.CostScroll.verticalScrollbar.gameObject.activeSelf,"Overflow bars hidden");
            Hint(p.RecipeScroll,true);Hint(p.CostScroll,true);Hint(p.WorkerScroll,false);Hint(p.OrderScroll,false);
            Wheel(p.RecipeScroll,-4);await Task.Delay(120);Check(p.RecipeContent.anchoredPosition.y>0&&Math.Abs(p.CostContent.anchoredPosition.y)<1,"Lists must scroll independently");
            p.RecipeScroll.verticalScrollbar.value=0;await Task.Delay(120);Click(p.RecipeRows[11].Button);Check(p.DetailTitle.text=="검증 아이템 12","Cannot select final recipe");Click(p.WorkerRows[0].Button);Check(!p.Confirm.interactable,"Hidden missing ingredient ignored");
            Hint(p.RecipeScroll,false);Hint(p.CostScroll,true);
            Wheel(p.CostScroll,-4);await Task.Delay(100);Check(p.CostContent.anchoredPosition.y>0,"Ingredient wheel failed");p.CostScroll.verticalScrollbar.value=0;await Task.Delay(100);Check(p.CostRows[7].Count.text=="0 / 1","Final ingredient missing");Bounds(p.View);
            Hint(p.CostScroll,false);Wheel(p.CostScroll,3);await Task.Delay(100);Hint(p.CostScroll,true);
            p.Materials[7].Initial=1;Click(p.RecipeRows[11].Button);Check(p.CostScroll.verticalNormalizedPosition>.99f,"New selection did not reset materials to top");Click(p.WorkerRows[0].Button);Click(p.Confirm);
            Click(c.Workbench);await Task.Delay(150);Click(p.OrderRows[0].Cancel);await Task.Delay(120);Check(p.CancelMessageScroll.content.rect.height>p.CancelMessageScroll.viewport.rect.height,"Refund text must overflow in own scroll");Wheel(p.CancelMessageScroll,-8);await Task.Delay(100);Check(p.CancelMessageScroll.content.anchoredPosition.y>0,"Refund text cannot scroll");Click(p.CancelYes);Check(p.Orders.Count==0&&p.Materials.All(m=>p.Available(m.Id)==1),"Many-material refund failed");
            Click(p.Tabs[1]);await Task.Delay(100);Check(p.RecipeRows.Count==0&&p.CostRows.Count==0&&!p.RecipeScroll.verticalScrollbar.gameObject.activeSelf&&!p.CostScroll.verticalScrollbar.gameObject.activeSelf,"Empty list bars must hide");p.Close();
        }finally{p.Close();p.Recipes=oldRecipes;p.Materials=oldMaterials;}
        Click(c.Workbench);await Task.Delay(150);Check(!p.RecipeScroll.verticalScrollbar.gameObject.activeSelf&&!p.CostScroll.verticalScrollbar.gameObject.activeSelf,"Three visible rows must not show scrollbar");Hint(p.RecipeScroll,false);Hint(p.CostScroll,false);p.Close();
        return "PASS: 12 recipes/8 ingredients, independent wheel input, scrollbar to last recipe, hidden shortage gating, reset on selection, long refund scrolling and complete refund, auto-hide for fitting/empty lists.";
    }
    public static async Task<string> PreviewOverflow(){var c=Object.FindAnyObjectByType<SettlementController>();var p=c.CraftPanel;var original=p.Recipes;try{p.Recipes=Enumerable.Range(0,12).Select(i=>original[i%3]).ToArray();Click(c.Workbench);await Task.Delay(200);Hint(p.RecipeScroll,true);return "Temporary overflow preview; stop Play after capture.";}finally{p.Recipes=original;}}
    public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();Click(c.Workbench);await Task.Delay(200);Click(c.CraftPanel.WorkerRows[0].Button);Bounds(c.CraftPanel.View);return "Craft default preview ready: missing nails, confirm disabled.";}
    public static async Task<string> Flow(){
        var c=Object.FindAnyObjectByType<SettlementController>();var p=c.CraftPanel;int supply=c.Campaign.Supplies,ammo=c.Campaign.Ammo;
        Check(p.IsOpen&&!p.Confirm.interactable&&!c.Main.blocksRaycasts&&p.CostRows.Count==3,"Initial material/modal gate");
        Click(p.Tabs[1]);await Task.Delay(80);Check(p.DetailTitle.text=="침대 수리","Repair tab");Click(p.Tabs[2]);await Task.Delay(80);Check(p.DetailTitle.text=="작업대 개선","Upgrade tab");Click(p.Tabs[0]);await Task.Delay(80);
        Click(p.RecipeRows[1].Button);await Task.Delay(80);Check(!p.Confirm.interactable,"No worker chosen yet");Click(p.WorkerRows[0].Button);Click(p.Plus);Check(p.QuantityText.text=="2"&&p.CostRows[0].Count.text=="3 / 2","Quantity cost");Click(p.Plus);Click(p.Plus);Check(!p.Confirm.interactable&&p.CostRows[0].Count.text=="3 / 4","Insufficient gate");Click(p.Minus);Click(p.Minus);Bounds(p.View);
        Click(p.Confirm);Check(!p.IsOpen&&p.Orders.Count==1&&p.Available("scrap")==1,"Reservation not exact");Check(c.Members[0].Status.text=="예약","Member status not refreshed");Bounds(c.Main.gameObject);
        Click(c.Bed);await Task.Delay(160);Check(!c.WorkPanel.Rows[0].Button.interactable,"Craft worker can rest simultaneously");Click(c.WorkPanel.Rows[1].Button);Click(c.WorkPanel.Confirm);
        Click(c.Workbench);await Task.Delay(160);Check(p.OrderRows.Count==1&&!p.WorkerRows[0].Button.interactable&&!p.WorkerRows[1].Button.interactable,"Queue / shared worker gate");Bounds(p.View);
        Click(p.OrderRows[0].Cancel);await Task.Delay(100);Check(p.CancelPopup.activeSelf&&!p.Workspace.interactable,"Nested confirmation input");Bounds(p.CancelPopup);Click(p.CancelNo);Check(p.Orders.Count==1&&p.Available("scrap")==1,"Declined cancellation mutated state");
        Click(p.OrderRows[0].Cancel);await Task.Delay(100);Click(p.CancelYes);await Task.Delay(100);Check(p.Orders.Count==0&&p.Available("scrap")==3&&p.WorkerRows[0].Button.interactable&&!p.WorkerRows[1].Button.interactable,"Refund/shared availability");
        Click(p.RecipeRows[1].Button);await Task.Delay(80);Click(p.WorkerRows[0].Button);Click(p.Confirm);Click(c.Workbench);await Task.Delay(180);
        Check(p.Orders.Count==1&&p.Available("scrap")==2,"Re-registration after refund");Check(c.Campaign.Supplies==supply&&c.Campaign.Ammo==ammo&&c.Clock.text.EndsWith("09:00"),"Registration changed time/global supplies");Bounds(p.View);
        return "PASS: all categories, quantity/cost/shortage gates, selection reset, exact reservation, rest/craft mutual exclusion, nested cancel/keep, full refund, re-register, text bounds and no time/global resource changes.";
    }
}
