using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Demo5.NightRun;
using Object=UnityEngine.Object;
public static class VerifyBagRedesign {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 public static async Task<string> Flow(){
 var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var inv=c.InventoryPanel;var people=c.Campaign.Party.ToArray();Check(a.Begin(people,c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure");await Task.Delay(900);
 Check(inv.TransferField(people[0],"ammo",2,true)&&inv.TransferField(people[0],"bandage",2,true)&&inv.TransferField(people[0],"cloth",4,true),"Fixture items");
 await Tap(a.Cards[0].Button);var b=a.FieldBags;Check(!b.LeftNext.interactable&&!b.RightNext.interactable,"Two member pages");
 await Tap(b.LeftRows.First(r=>r.Label.text==inv.Items.First(i=>i.Id=="ammo").Name).Button);Check(!b.Use.gameObject.activeSelf&&b.Transfer.interactable,"Ammo use hidden");int t=a.Rooms.Turns,n=a.Rooms.Noise;await Tap(b.Transfer);Check(inv.CountFor(people[0],"ammo")==1&&inv.CountFor(people[1],"ammo")==1&&a.Rooms.Turns==t&&a.Rooms.Noise==n,"Transfer conservation/time");
 await Tap(b.LeftRows.First(r=>r.Label.text==inv.Items.First(i=>i.Id=="bandage").Name).Button);Check(b.Use.IsActive()&&b.Use.interactable&&b.Use.GetComponentInChildren<Text>().text.Contains(people[0].Name),"Named use");int h=people[0].Health;await Tap(b.Use);Check(people[0].Health>h&&inv.CountFor(people[0],"bandage")==1&&a.Rooms.Turns==t+1,"Real use");Check(!b.Use.interactable&&b.UseHint.text.Contains("체력이 가득"),"Full health reason");
 Check(((RectTransform)b.Back.transform).anchoredPosition==new Vector2(80,-974)&&((RectTransform)b.Transfer.transform).sizeDelta==new Vector2(410,76),"Common footer");
 return "PASS: actual pointer selection, ammo hides Use, named recipient transfer preserves totals/time/noise, named treatment consumes one and one turn, full-health reason, 2-member page and common footer.";
 }
 public static async Task<string> Six(){
 var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var b=a.FieldBags;b.Close();var people=a.Participants.ToList();var candidates=c.Roster.Candidates;
 for(int i=people.Count;i<6;i++){var d=candidates.First(x=>!people.Any(p=>p.Name==x.DisplayName));var p=new Adventurer(d.DisplayName,"검수","",4,60,6);people.Add(p);var card=Object.Instantiate(a.MemberPrefab,a.MemberContent);card.Portrait.sprite=d.Portrait;a.Cards.Add(card);}
 typeof(ExpeditionArrivalPanel).GetField("people",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,people.ToArray());b.Open(0);Check(b.MemberPageCount==2&&b.LeftCards.Count(x=>x.gameObject.activeSelf)==3,"6 member first page");await Tap(b.RightNext);Check(b.RightCards.Count(x=>x.gameObject.activeSelf)==3&&b.RightCards[3].gameObject.activeSelf&&!b.RightNext.interactable,"second page");await Tap(b.RightCards[4].Button);Check(b.Target==people[4],"Off-page recipient");await Tap(b.LeftRows.First(r=>r.Label.text==c.InventoryPanel.Items.First(i=>i.Id=="bandage").Name).Button);return "PASS: 6-member fixture, 3 visible per page, second-page recipient selected. Runtime fixture only.";
 }
 public static async Task<string> Solo(){var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var b=a.FieldBags;b.Close();typeof(ExpeditionArrivalPanel).GetField("people",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,c.Campaign.Party.Take(1).ToArray());b.Open(0);Check(b.Target==null&&!b.Transfer.interactable&&b.RightRows.Count==0,"Solo transfer");Check(b.MemberPageCount==1&&!b.LeftNext.interactable,"Solo page");await Task.Delay(100);return "PASS: solo has no recipient, no transfer, one page.";}
 public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var b=a.FieldBags;var person=a.Participants[0];person.Health=person.MaxHealth-1;await Tap(b.LeftRows.First(r=>r.Label.text==c.InventoryPanel.Items.First(i=>i.Id=="bandage").Name).Button);return "Treatment preview shown.";}
 public static string Restore(){var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;a.FieldBags.View.SetActive(false);typeof(ExpeditionArrivalPanel).GetField("people",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,c.Campaign.Party.ToArray());while(a.Cards.Count>c.Campaign.Party.Count()){var card=a.Cards[a.Cards.Count-1];a.Cards.RemoveAt(a.Cards.Count-1);Object.Destroy(card.gameObject);}a.FieldBags.Open(0);return "Restored actual two-member party.";}
}
