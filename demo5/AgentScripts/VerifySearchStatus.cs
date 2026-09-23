using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifySearchStatus
{
 static SettlementController Owner()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static async Task Tap(Button b){await Task.Delay(70);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Begin(){var c=Owner();Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Expedition failed");for(int i=0;i<40&&c.ArrivalPanel.InTransit;i++)await Task.Delay(100);Check(!c.ArrivalPanel.InTransit,"Travel not finished");}
 public static async Task<string> Flow(){
  var c=Owner();var loot=c.ArrivalPanel.Loot;Check(loot.ExportSearches().Length==0,"Need fresh state");await Tap(c.Exit);Bounds(c.ExpeditionPanel.View);Check(c.ExpeditionPanel.Unknown.text.Contains("미수색 "+loot.Sites.Count(s=>s.Room>=0))&&loot.ExportSearches().Length==0,"Guidance created states");await Tap(c.ExpeditionPanel.Back);await Begin();var a=c.ArrivalPanel;var labels=a.GetComponent<ExpeditionSearchStatus>().Labels;await Task.Delay(150);Check(labels.Where(t=>t).All(t=>t.text=="미수색"),"Initial badges");
  // Deterministic fixture for reward transfer; it changes only this Play instance's drop chance.
  for(int d=0;d<loot.Sites[0].Drops.Length;d++)loot.Sites[0].Drops[d].Chance=d==0?100:0;Check(loot.Advance(0,0,a.Participants[0]),"Search completion");Check(loot.StatusKind(0)==2,"Remaining state");Check(loot.Advance(1,1,a.Participants[0]),"Partial search");Check(loot.StatusKind(1)==1&&loot.StatusKind(2)==0,"Partial/unknown states");await Tap(a.Objects[0]);Check(loot.IsOpen,"Badge blocked object click");int rounds=0;while(loot.FieldRows.Count>0&&rounds++<30){await Tap(loot.FieldRows[0].Button);if(!loot.Transfer.interactable){await Tap(loot.Cards[1].Button);await Tap(loot.FieldRows[0].Button);}await Tap(loot.Max);await Tap(loot.Transfer);}Check(loot.FieldRows.Count==0&&loot.StatusKind(0)==3,"Depleted state");await Tap(loot.Back);await Task.Delay(100);Check(labels[0].text=="비어 있음"&&labels[1].text.Contains("수색 중")&&labels[2].text=="미수색","Badge update");Check(!loot.Advance(0,0,a.Participants[0]),"Depleted reroll");
  await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/SearchStatusVerification");Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);var saved=CampaignSaveStore.Read(0,c);Check(saved.CanLoad,saved.Error);CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();loot=c.ArrivalPanel.Loot;Check(loot.StatusKind(0)==3&&loot.StatusKind(1)==1&&loot.StatusKind(2)==0,"Restore classification");await Tap(c.Exit);Check(c.ExpeditionPanel.Unknown.text=="미수색 "+(loot.Sites.Count(s=>s.Room>=0)-2)+" · 진행 1\n물품 남음 0 · 비어 있음 1","Plan summary");Bounds(c.ExpeditionPanel.View);await Tap(c.ExpeditionPanel.Back);await Begin();Check(loot.StatusKind(0)==3&&loot.State(1).Progress==1&&!loot.Advance(0,0,c.ArrivalPanel.Participants[0]),"Revisit resets or rerolls");Bounds(c.ArrivalPanel.Main.gameObject);return "PASS: guidance has no state/random side effects; live remaining/progress/depleted badges; actual item pickup, no reroll; file restore/revisit preserve progress; plan summary and field text bounds.";
 }
 public static async Task<string> PlanPreview(){var c=Owner();await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);await Tap(c.ReturnPanel.Back);await Tap(c.Exit);Bounds(c.ExpeditionPanel.View);return "Plan summary ready.";}
}
