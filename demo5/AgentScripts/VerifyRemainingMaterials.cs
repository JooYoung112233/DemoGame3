using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifyRemainingMaterials {
 static SettlementController C=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(70);}
 static void Bounds(){Canvas.ForceUpdateCanvases();foreach(var t in C.CraftPanel.MaterialGuide.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name+" "+t.preferredHeight);}
 public static async Task<string> Flow(){
  var c=C;var l=c.ArrivalPanel.Loot;var rng=UnityEngine.Random.state;int minute=c.Campaign.MinuteOfDay;var states=(IDictionary)typeof(ExpeditionLootPanel).GetField("states",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(l);int count=states.Count;
  Check(l.MaterialAvailability("wood",out int pending,out int left)&&pending==1&&left==0,"Unknown wood status");Check(!l.MaterialAvailability("unknown-item",out pending,out left),"Unknown item discovered");Check(states.Count==count,"Guidance created search states");
  await Tap(c.Workbench);var p=c.CraftPanel;await Tap(p.Plus);await Tap(p.CostRows[0].GetComponent<Button>());var g=p.MaterialGuide;Check(g.Source.text.Contains("미완료 수색 1곳")&&g.Action.interactable,"Unsearched guidance");Bounds();
  var s=l.State(1);s.Required=3;s.Progress=1;g.Select("wood");Check(g.Source.text.Contains("미완료 수색 1곳"),"Partial lost");
  // Deterministic completed-search fixtures; these do not alter production loot probabilities.
  s.Progress=3;s.Complete=true;s.Loot["wood"]=2;g.Select("wood");Check(g.Source.text.Contains("2개 남겨둠")&&g.Action.interactable,"Leftovers lost");Bounds();await Tap(g.Action);Check(c.ExpeditionPanel.IsOpen,"Revisit route");await Tap(c.ExpeditionPanel.Back);await Tap(c.Workbench);await Tap(p.Plus);await Tap(p.CostRows[0].GetComponent<Button>());Check(g.Source.text.Contains("2개 남겨둠"),"Reopen history");
  s.Loot["wood"]=0;g.Select("wood");Check(!g.Action.interactable&&g.ActionLabel.text=="남은 재료 없음","Exhausted sends expedition");Bounds();
  var box=l.State(0);box.Complete=true;box.Loot["wood"]=1;g.Select("wood");Check(g.Source.text.Contains("1개 남겨둠"),"Player-stored item ignored");box.Loot["wood"]=0;g.Select("wood");
  Check(c.Campaign.MinuteOfDay==minute&&UnityEngine.Random.state.Equals(rng)&&c.InventoryPanel.StockCount("wood")==2,"Guidance mutated time/RNG/stock");return "PASS read-only queries, unknown/partial/leftover/exhausted states, no empty-source expedition, reopened history, player-stored materials, text bounds, no time/RNG/stock changes. Search states were runtime fixtures.";
 }
 public static string LeftoverPreview(){C.ArrivalPanel.Loot.State(1).Loot["wood"]=2;C.CraftPanel.MaterialGuide.Select("wood");Bounds();return "Two remaining wood fixture shown.";}
}
