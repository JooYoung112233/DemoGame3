using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyFieldBattle
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static async Task Tap(Button button,Vector2? local=null)
 {
  Check(button.IsActive()&&button.IsInteractable(),"Unavailable "+button.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)button.transform;
  var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(local??r.rect.center)),button=PointerEventData.InputButton.Left};
  var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked "+button.name+" by "+(hits.Count==0?"none":hits[0].gameObject.name));
  ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);await Task.Delay(70);
 }
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+" "+t.preferredHeight+" / "+t.rectTransform.rect.height+": "+t.text);}
 public static string Rules()
 {
  var a=new Adventurer("A","scout","",4,0);var b=new Adventurer("B","medic","",4,0);int ammo=2;
  var s=new FieldBattleState(new[]{a,b},1,p=>p==a?ammo:0,p=>{if(p!=a||ammo<=0)return false;ammo--;return true;},()=>99);
  Check(!s.Move(0,2),"Occupied cell accepted");Check(s.Move(1,0)&&!s.Move(2,0),"Move limit");Check(!s.Attack(2,false),"Melee from rear");
  Check(s.Attack(2,true)&&ammo==1&&s.AmmoSpent==1&&s.Units[2].Health==3,"Miss/ammo");Check(!s.Attack(2,true),"Other actor used shared ammo");
  Check(s.Guard()&&s.Current.Enemy,"Turn order");Check(!s.Attack(2,false)&&s.EnemyStep()&&s.Actor==0,"Enemy input gate");
  Check(s.Retreat()&&!s.Guard()&&!s.EnemyStep(),"Terminal mutation");
  s=new FieldBattleState(new[]{a,b},1,p=>5,p=>true,()=>0);Check(s.Attack(2,true)&&s.Outcome==FieldBattleOutcome.Victory&&!s.Attack(2,true),"Victory/double action");
  a=new Adventurer("A","","",1,0);s=new FieldBattleState(new[]{a},1,p=>0,p=>false,()=>0);
  s.Guard();s.EnemyStep();Check(a.Health==1,"Guard damage");s.Attack(1,false);s.EnemyStep();Check(s.Outcome==FieldBattleOutcome.Defeat&&a.Health==0,"Persistent defeat");
  return "PASS: occupied/multi-move restrictions, front melee, personal ammunition, miss consumption, turn order, guard, persistent damage, victory/defeat/retreat terminal guards.";
 }
 public static async Task<string> BeginPreview()
 {
  var c=Object.FindAnyObjectByType<SettlementController>();Check(c!=null&&c.Campaign!=null,"Open settlement first");
  // Inspection fixture: allocate existing stock, then follow the real departure/search path.
  var people=c.Campaign.Party.ToArray();for(int i=0;i<people.Length;i++)if(c.InventoryPanel.CountFor(people[i],"ammo")==0)Check(c.InventoryPanel.MoveFor(people[i],"ammo",2,true),"Pack ammo");
  await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(!card.Check.gameObject.activeSelf)await Tap(card.Button);
  await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
  var a=c.ArrivalPanel;var e=a.Encounter;var s=a.Search;int bas=e.BaseChance,max=e.MaximumChance,threshold=e.NoiseThreshold;var oldRandom=UnityEngine.Random.state;
  try{e.BaseChance=e.MaximumChance=100;e.NoiseThreshold=0;UnityEngine.Random.InitState(5);await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);await Tap(s.Paces[1]);for(int i=0;i<2;i++){await Tap(s.Choose);await Tap(s.Confirm);}Check(e.IsOpen,"No encounter");await Tap(e.Fight);}
  finally{e.BaseChance=bas;e.MaximumChance=max;e.NoiseThreshold=threshold;UnityEngine.Random.state=oldRandom;}
  var battle=e.Battle;Check(battle.IsOpen&&!a.Main.gameObject.activeSelf&&!a.PawnRoot.gameObject.activeSelf,"Battle input/world gate");Check(battle.Cells.Length==18,"Formation size");Bounds(battle.View);await Tap(battle.Shoot);EventSystem.current.SetSelectedGameObject(null);
  return "Battle open via real departure, search and encounter. Existing stock packed for this inspection fixture; original encounter rates and RNG restored.";
 }
 public static Task<string> VictoryFlow()=>Win(false);
 public static Task<string> ResultPreview()=>Win(true);
 static async Task<string> Win(bool leaveResult)
 {
  var a=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");
  int turns=a.Rooms.Turns,loot=a.Loot.State(0).Loot.Values.Sum(),progress=a.Loot.State(0).Progress;
  var beforeRandom=UnityEngine.Random.state;float pause=b.ActionPause;b.ActionPause=.01f;
  try
  {
   // Clicking the interior of a perspective cell must hit its actual polygon.
   var move=ExpeditionBattlePanel.Point(false,1,0);await Tap(b.CellButtons[1],new Vector2(move.x,-move.y));Check(b.State.Moved,"Move click");
   await Tap(b.Shoot);int guard=0;
   while(b.State.Outcome==FieldBattleOutcome.Playing&&guard++<20)
   {
    if(b.Busy){await Task.Delay(80);continue;}
    int target=b.State.Units.FindIndex(u=>u.Enemy&&u.Alive);var u=b.State.Units[target];int cell=9+u.Lane*3+u.Depth;
    var point=ExpeditionBattlePanel.Point(true,u.Depth,u.Lane);await Tap(b.CellButtons[cell],new Vector2(point.x,-point.y));
    await Tap(b.Shoot);if(!b.Execute.interactable){await Tap(b.Melee);}
    Check(b.Execute.interactable,"No valid attack");
    int seed=0;for(;seed<100;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(0,100)<b.State.HitChance(target,b.Ranged))break;}UnityEngine.Random.InitState(seed);
    int actions=b.State.Actions;await Tap(b.Execute);b.Execute.onClick.Invoke();Check(b.State.Actions<=actions+3,"Double click actions");await Task.Delay(120);
   }
   Check(b.State.Outcome==FieldBattleOutcome.Victory&&b.Result.activeSelf,"No victory result");Bounds(b.Result);
   if(leaveResult)return "Victory result shown from actual attacks. Runtime-only review fixture.";
   await Tap(b.ResultContinue);b.ResultContinue.onClick.Invoke();Check(!b.IsOpen&&!a.Encounter.IsOpen&&a.Loot.IsOpen,"Resume loot");
   Check(a.Rooms.Turns==turns+1&&a.Loot.State(0).Loot.Values.Sum()==loot&&a.Loot.State(0).Progress==progress,"Cost/reward duplication");
   return "PASS: polygon clicks, movement, attack target/confirm, double click lock, victory result text, exactly one exploration turn, preserved loot and search progress.";
  }
  finally{UnityEngine.Random.state=beforeRandom;b.ActionPause=pause;}
 }
 public static async Task<string> RetreatFlow()
 {
  var a=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");int turns=a.Rooms.Turns;
  await Tap(b.Retreat);Bounds(b.RetreatReview);b.Escape();Check(!b.RetreatReview.activeSelf&&b.IsOpen&&a.Rooms.Turns==turns,"Esc retreat cost");
  await Tap(b.Retreat);await Tap(b.RetreatConfirm);Check(b.Result.activeSelf,"Retreat result");Bounds(b.Result);await Tap(b.ResultContinue);b.ResultContinue.onClick.Invoke();await Task.Delay(2400);
  Check(a.Rooms.CurrentRoom==1&&a.Rooms.Turns==turns+1&&!b.IsOpen&&!a.Encounter.IsOpen,"Retreat destination/cost");return "PASS: retreat cancel/Esc free, confirm and result, duplicate continue ignored, actual corridor transition costs exactly one turn.";
 }
}
