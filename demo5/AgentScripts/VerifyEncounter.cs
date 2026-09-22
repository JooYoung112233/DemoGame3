using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyEncounter {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}




 static async Task<SettlementController> Depart(){var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(!card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);return c;}
 static async Task Tick(ExpeditionSearchPanel s){await Tap(s.Choose);await Tap(s.Confirm);s.Confirm.onClick.Invoke();}
 static async Task Select(ExpeditionLootPanel l,string name,bool bag=false){await Tap((bag?l.BagRows:l.FieldRows).First(r=>r.Label.text==name).Button);}

 public static async Task<string> Flow(){var c=await Depart();var a=c.ArrivalPanel;var e=a.Encounter;var s=a.Search;int baseChance=e.BaseChance,maxChance=e.MaximumChance,wait=e.WaitChance,threshold=e.NoiseThreshold;
 try{e.BaseChance=e.MaximumChance=100;e.NoiseThreshold=0;
 await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);await Tap(s.Paces[1]);await Tick(s);Check(e.Warned&&e.Banner.activeSelf&&!e.IsOpen&&e.Rolls==0,"First turn must warn, not encounter");int rolls=e.Rolls;await Tap(s.Back);await Tap(a.Objects[0]);Check(e.Rolls==rolls,"UI rolled encounter");await Tick(s);Check(e.IsOpen&&!s.IsOpen&&!a.Loot.IsOpen&&!a.Main.blocksRaycasts&&a.Loot.State(0).Complete,"Encounter did not suspend completed search");Check(!e.Fight.interactable,"Unimplemented battle enabled");Bounds(e.View);int turn=a.Rooms.Turns;int count=a.Loot.State(0).Loot.Values.Sum();e.Escape();Check(e.IsOpen,"Escape skipped encounter");a.Rooms.AskMove();Check(!a.Popup.activeSelf,"Room bypass");await Tap(e.Wait);Bounds(e.Review);await Tap(e.Cancel);Check(a.Rooms.Turns==turn&&e.IsOpen,"Cancel charged/resolved");
 e.WaitChance=0;int seed=0;for(;seed<100;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(0,100)>=20)break;}UnityEngine.Random.InitState(seed);await Tap(e.Wait);await Tap(e.Confirm);e.Confirm.onClick.Invoke();Check(e.IsOpen&&a.Rooms.Turns==turn+1&&!e.Review.activeSelf,"Failed wait state/double charge");e.WaitChance=100; // prior failure lowers chance to 85; choose a passing seed.
 for(seed=0;seed<100;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(0,100)<85)break;}UnityEngine.Random.InitState(seed);await Tap(e.Wait);await Tap(e.Confirm);Check(!e.IsOpen&&a.Loot.IsOpen&&e.Cooldown==2&&a.Rooms.Turns==turn+2,"Successful wait did not resume loot");Check(a.Loot.State(0).Loot.Values.Sum()==count,"Encounter rerolled rewards");await Tap(a.Loot.Back);if(a.Loot.LeaveReview.activeSelf)await Tap(a.Loot.LeaveConfirm);
 await Tap(a.Objects[1]);await Tap(s.Cards[0].Button);await Tap(s.Paces[2]);await Tick(s);await Tick(s);Check(!e.IsOpen&&e.Cooldown==0,"Grace period failed");await Tick(s);Check(!e.IsOpen&&e.Warned,"Grace must return to warning first");await Tap(a.Loot.Back);if(a.Loot.LeaveReview.activeSelf)await Tap(a.Loot.LeaveConfirm);
 await Tap(a.Objects[2]);await Tap(s.Cards[0].Button);await Tap(s.Paces[2]);await Tick(s);Check(e.IsOpen&&!a.Loot.State(2).Complete,"Incomplete search encounter missing");int progress=a.Loot.State(2).Progress;turn=a.Rooms.Turns;await Tap(e.Retreat);await Tap(e.Confirm);e.Confirm.onClick.Invoke();Check(a.InTransit&&a.Rooms.Turns==turn+1,"Retreat not one movement turn");await Task.Delay(2400);Check(a.Rooms.CurrentRoom==1&&!e.IsOpen&&a.Loot.State(2).Progress==progress,"Retreat state lost");await Tap(a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);await Task.Delay(2400);await Tap(a.Objects[2]);Check(s.IsOpen&&a.Loot.State(2).Progress==progress,"Resume interrupted search");await Tap(s.Back);
 return "PASS: warning first, one roll per search, no UI reroll, input/Esc gate, cancel free, wait fail/success one turn, reward preservation, cooldown, retreat one turn, interrupted search preserved, text bounds.";
 }finally{e.BaseChance=baseChance;e.MaximumChance=maxChance;e.WaitChance=wait;e.NoiseThreshold=threshold;}}
 public static async Task<string> Preview(){var c=await Depart();var a=c.ArrivalPanel;var e=a.Encounter;int b=e.BaseChance,m=e.MaximumChance,t=e.NoiseThreshold;try{e.BaseChance=e.MaximumChance=100;e.NoiseThreshold=0;await Tap(a.Objects[0]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[1]);await Tick(a.Search);await Tick(a.Search);Bounds(e.View);EventSystem.current.SetSelectedGameObject(null);return "Encounter review fixture open. Rates restored to defaults.";}finally{e.BaseChance=b;e.MaximumChance=m;e.NoiseThreshold=t;}}
}
