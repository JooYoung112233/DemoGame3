using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
 // Click without yielding a frame, so a second synchronous click tests the input lock itself.
 static void ClickNow(Button button){Check(button.IsActive()&&button.IsInteractable(),"Unavailable "+button.name);ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
 static Task TapCell(ExpeditionBattlePanel b,bool enemy,int depth,int lane){var p=ExpeditionBattlePanel.Point(enemy,depth,lane);return Tap(b.CellButtons[(enemy?9:0)+lane*3+depth],new Vector2(p.x,-p.y));}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())if(t.horizontalOverflow==HorizontalWrapMode.Wrap&&!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+" "+t.preferredHeight+" / "+t.rectTransform.rect.height+": "+t.text);}
 // Action cards (2026-09-25 two-line slot): each description sits inside its card, is fully generated (Truncate drops a line that does not fit)
 // in at most two lines, and the guard card reads exactly the two lines of GuardDescription (물릴 확률 / 예고 피해).
 static void Cards(ExpeditionBattlePanel battle)
 {
  Canvas.ForceUpdateCanvases();
  foreach(var card in new[]{battle.Melee,battle.Shoot,battle.Guard,battle.Items})
  {
   var d=card.transform.Find("Description").GetComponent<Text>();var r=d.rectTransform;
   Check(-r.anchoredPosition.y+r.rect.height<=((RectTransform)card.transform).rect.height,"Description leaves the card "+card.name+": bottom "+(-r.anchoredPosition.y+r.rect.height)+" / "+((RectTransform)card.transform).rect.height);
   var full=new TextGenerator();full.Populate(d.text,d.GetGenerationSettings(new Vector2(r.rect.width,10000)));
   Check(d.cachedTextGenerator.lineCount==full.lineCount&&full.lineCount<=2,"Card text truncated or over two lines "+card.name+": shown "+d.cachedTextGenerator.lineCount+" / needed "+full.lineCount+": "+d.text);
   if(card==battle.Guard)Check(full.lineCount==2&&d.text==string.Format(battle.GuardDescription,battle.Rules.GuardHitPenalty,battle.Rules.GuardStrikeReduction),"Guard card reads the two GuardDescription lines: "+d.text);
  }
 }
 static async Task Until(Func<bool> done,int milliseconds,string what){var watch=Stopwatch.StartNew();while(!done()){if(watch.ElapsedMilliseconds>milliseconds)throw new Exception("Timeout: "+what);await Task.Delay(30);}}
 // Seed UnityEngine.Random so the next rolls satisfy a condition (hit, crit, miss...). Presentation jitter uses its own generator.
 static void Rig(Func<int[],bool> accept,int count){for(int seed=1;seed<200000;seed++){UnityEngine.Random.InitState(seed);var v=new int[count];for(int i=0;i<count;i++)v[i]=UnityEngine.Random.Range(0,100);if(accept(v)){UnityEngine.Random.InitState(seed);return;}}throw new Exception("No seed");}
 static ExpeditionArrivalPanel Arrival=>Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;

 public static string Rules()
 {
  int r=99;Func<int> roll=()=>r;
  var a=new Adventurer("A","scout","",4,0);var b=new Adventurer("B","medic","",4,0);int ammo=2;
  // Threshold pinned at 4 so two shots still exercise the alarm, arrival, advance and retreat path below (shipped default is 5, checked further down).
  var s=new FieldBattleState(new[]{a,b},1,p=>p==a?ammo:0,p=>{if(p!=a||ammo<=0)return false;ammo--;return true;},roll,new FieldBattleRules{ReinforcementNoise=4});var rules=s.Rules;var e=s.Units[2];
  Check(e.Enemy&&e.Depth==1&&e.Lane==0&&e.Health==rules.EnemyHealth,"Infected starts one row back");
  Check(!s.Move(0,2),"Occupied cell accepted");Check(s.Move(1,0)&&!s.Move(2,0),"Move limit");Check(!s.Attack(2,false),"Melee from rear");
  Check(s.Attack(2,true)&&ammo==1&&s.AmmoSpent==1&&e.Health==rules.EnemyHealth&&s.Noise==rules.ShotNoise,"Miss spends ammo and makes noise");
  Check(!s.Attack(2,true),"Other actor used shared ammo");Check(s.Guard()&&s.Current.Enemy,"Turn order");Check(!s.Attack(2,false),"Enemy input gate");
  Check(s.EnemyStep()&&e.Depth==0&&s.Actor==0&&s.Round==2&&s.Events.Any(v=>v.Kind==BattleEventKind.Advance),"Infected advance before biting");
  var plan=s.PredictIntents().Single();
  Check(plan.Kind==EnemyIntentKind.Attack&&plan.Target==0&&plan.Chance==rules.EnemyHitChance-rules.EnemyDepthPenalty,"Forecast: foremost ally in reach, one row back is harder to bite");
  Check(s.PredictIntents(0,2,0).Single().Chance==rules.EnemyHitChance-2*rules.EnemyDepthPenalty,"Move preview recomputes the bite");
  Check(s.Attack(2,true)&&s.ReinforcementPending&&s.Events.Any(v=>v.Kind==BattleEventKind.Alarm),"Second shot calls a reinforcement");
  Check(s.Guard()&&s.Current.Enemy,"B guards");
  r=0;Check(s.EnemyStep()&&a.Health==3&&s.Units[0].Health==3,"Bite damage persists to the adventurer");
  Check(s.Units.Count==4&&s.Units[3].Reinforcement&&s.Units[3].Depth==2&&s.Reinforcements==1&&s.Events.Any(v=>v.Kind==BattleEventKind.Reinforce),"Reinforcement joins at round start");
  var guardedChance=rules.EnemyHitChance-rules.EnemyDepthPenalty-rules.GuardHitPenalty;Check(s.Guard()&&s.Guard()&&s.PredictIntents().First().Chance==guardedChance,"Guard lowers the forecast bite");
  r=guardedChance;Check(s.EnemyStep()&&a.Health==3&&s.Events.Any(v=>v.Blocked&&!v.Hit),"A guarded miss reads as a block");
  Check(s.EnemyStep()&&s.Units[3].Depth==1&&s.Actor==0,"Reinforcement advances");
  a.Health=s.Units[0].Health=1;var threats=s.RetreatThreats();
  Check(threats.Count==1&&threats[0].Target==0&&threats[0].Chance==rules.RetreatHitChance,"Retreat threat forecast");
  Check(s.Retreat()&&s.Outcome==FieldBattleOutcome.Retreated&&a.Health==1&&s.Events.Any(v=>v.Retreating&&v.Hit),"Parting bite never knocks out");
  Check(!s.Guard()&&!s.EnemyStep()&&!s.Move(0,1),"Terminal mutation");

  a=new Adventurer("A","","",4,0);b=new Adventurer("B","","",4,0);
  s=new FieldBattleState(new[]{a,b},1,p=>5,p=>true,()=>0);
  Check(s.Attack(2,true)&&s.Outcome==FieldBattleOutcome.Victory&&s.Kills==1&&s.Events[0].Critical&&s.Events[0].Damage==rules.ShotDamage+rules.CriticalBonus&&!s.Attack(2,true),"Critical shot kill locks input");
  Check(s.ResultNoise==(rules.ShotNoise*rules.CarriedNoisePercent+50)/100+rules.VictoryNoise,"Result noise = carried gunshots + fight");

  var tough=new FieldBattleRules{EnemyHealth=9};r=99;a=new Adventurer("A","","",4,0);b=new Adventurer("B","","",4,0);
  s=new FieldBattleState(new[]{a,b},1,p=>0,p=>false,roll,tough);s.Guard();s.Guard();s.EnemyStep();
  r=0;Check(s.Attack(2,false)&&s.Units[2].Depth==1&&s.Units[2].Health==9-tough.MeleeDamage-tough.CriticalBonus&&s.Events[0].Pushed,"Critical melee pushes the infected back");
  s.Units[0].Depth=1;s.Units[0].Lane=0;s.Units[1].Depth=0;s.Units[1].Lane=0;s.Units[2].Depth=0;s.Units[2].Lane=0;
  Check(!s.Exposed(0)&&s.Exposed(1)&&s.PredictIntents().Single().Target==1,"Front ally shields the one behind");

  var t=new FieldBattleState(new[]{new Adventurer("A","","",4,0)},1,p=>0,p=>false,()=>99);t.Units[1].Depth=0;t.Units[1].Lane=2;
  Check(t.HitChance(1,false)==0&&t.HitChance(1,true)>0,"Melee lane reach");t.Units[1].Lane=1;Check(t.HitChance(1,false)==t.Rules.MeleeHitChance,"Adjacent lane melee");

  // Review fixes: no alarm on the killing shot; forecast target survives an earlier bite; no sidestep ping-pong; alarm respects the cap.
  var steady=new FieldBattleRules{CriticalChance=0,ReinforcementNoise=4};a=new Adventurer("A","","",4,0);b=new Adventurer("B","","",4,0);
  s=new FieldBattleState(new[]{a,b},1,p=>5,p=>true,()=>0,steady);
  Check(s.Attack(2,true)&&s.Attack(2,true)&&s.Outcome==FieldBattleOutcome.Victory&&!s.ReinforcementPending&&!s.Events.Any(v=>v.Kind==BattleEventKind.Alarm),"Killing shot must not call a reinforcement");
  r=99;a=new Adventurer("A","","",4,0);b=new Adventurer("B","","",4,0);s=new FieldBattleState(new[]{a,b},2,p=>0,p=>false,roll);
  s.Units[0].Depth=0;s.Units[0].Lane=0;s.Units[1].Depth=0;s.Units[1].Lane=2;s.Units[2].Depth=0;s.Units[2].Lane=2;s.Units[3].Depth=0;s.Units[3].Lane=1;
  var forecast=s.PredictIntents();Check(forecast.Single(p=>p.Enemy==2).Target==1&&forecast.Single(p=>p.Enemy==3).Target==0,"Forecast E1->B, E2->A");
  Check(s.Attack(3,false)&&s.Attack(2,false)&&s.Current.Enemy,"Hand over to the infected");
  r=0;s.EnemyStep();Check(b.Health==3,"E1 bit B");s.EnemyStep();Check(s.Events.Single(v=>v.Kind==BattleEventKind.Attack).Target==0,"E2 kept its forecast target after B was hurt");
  s=new FieldBattleState(new[]{new Adventurer("A","","",4,0),new Adventurer("C","","",4,0)},3,p=>0,p=>false,()=>99,new FieldBattleRules{MaxEnemies=3});
  s.Units[0].Depth=0;s.Units[0].Lane=0;s.Units[1].Depth=0;s.Units[1].Lane=1;s.Units[2].Depth=0;s.Units[2].Lane=1;s.Units[3].Depth=0;s.Units[3].Lane=2;s.Units[4].Depth=1;s.Units[4].Lane=1;
  var back=s.PredictIntents().Single(p=>p.Enemy==4);Check(back.Kind==EnemyIntentKind.Shift&&back.Lane==0,"Blocked infected sidesteps toward an open front cell");
  s=new FieldBattleState(new[]{new Adventurer("A","","",4,0)},2,p=>5,p=>true,()=>99,new FieldBattleRules{MaxEnemies=2,ReinforcementNoise=2});
  Check(s.Attack(2,true)&&s.Noise==2&&!s.ReinforcementPending,"No alarm while the infected cap is full");
  // Shipped rules (balance pass 1): two members firing once each stay quiet enough; the third shot calls help. Roll 99 always misses.
  a=new Adventurer("A","","",4,0);b=new Adventurer("B","","",4,0);var shipped=new FieldBattleRules();s=new FieldBattleState(new[]{a,b},1,p=>5,p=>true,()=>99,shipped);
  Check(s.Attack(2,true)&&s.Attack(2,true)&&s.Current.Enemy&&s.Noise==2*shipped.ShotNoise&&!s.ReinforcementPending&&!s.Events.Any(v=>v.Kind==BattleEventKind.Alarm),"Default rules: one shot each calls no reinforcement");
  Check(s.EnemyStep()&&s.Actor==0&&s.Round==2&&!s.ReinforcementPending,"Default rules: still quiet after the enemy phase");
  Check(s.Attack(2,true)&&s.Noise==3*shipped.ShotNoise&&s.ReinforcementPending&&s.Events.Any(v=>v.Kind==BattleEventKind.Alarm),"Default rules: the third shot calls one");

  // Battle items: treat a wounded ally, clamp to max, spend the action, refuse full HP / enemies / failed consumption.
  a=new Adventurer("A","","",4,0){Health=1};b=new Adventurer("B","","",4,0);int used=0;
  s=new FieldBattleState(new[]{a,b},1,p=>0,p=>false,()=>99);
  Check(!s.UseItem(1,2,()=>{used++;return true;})&&used==0,"Full HP cannot be treated");Check(!s.UseItem(2,2,()=>true),"Enemies cannot be treated");
  Check(!s.UseItem(0,2,()=>false)&&a.Health==1&&s.Actor==0,"Failed consumption changes nothing");
  Check(s.UseItem(0,5,()=>{used++;return true;})&&a.Health==4&&s.Units[0].Health==4&&used==1&&s.Actor==1&&s.ItemsUsed==1&&s.Events.Single().Kind==BattleEventKind.Item&&s.Events[0].Damage==3,"Treatment clamps, persists and spends the action");
  s=new FieldBattleState(new[]{new Adventurer("A","","",4,0)},1,p=>0,p=>false,()=>99,new FieldBattleRules{EnemyArmor=1});
  Check(s.ExpectedDamage(1,true)==s.Rules.ShotDamage-1&&s.ArmorOf(1)==1&&s.HitFactors(1,true).First().Key=="기본","Armor lowers damage; hit factors listed");

  a=new Adventurer("A","","",1,0);r=99;s=new FieldBattleState(new[]{a},1,p=>0,p=>false,roll);s.Guard();s.EnemyStep();
  Check(s.Attack(1,false)&&s.Current.Enemy,"Miss hands over the turn");r=0;s.EnemyStep();
  Check(s.Outcome==FieldBattleOutcome.Defeat&&a.Health==0,"Persistent defeat");
  return "PASS: shipped threshold (one shot each = no reinforcement, third shot = one), battle items (treat/clamp/refuse/consume), armor and hit factors, spawn rows, stable forecast target, no alarm on the killing shot or over the cap, blocked sidestep, move limits, front/lane melee reach, ammo+noise on miss, infected advance, live bite forecast and move preview, depth bite penalty, reinforcement alarm/arrival, persistent damage, guard lowers bite chance and reads as a block, retreat bites that cannot knock out, crit kill/push, front ally cover, victory/defeat/retreat terminal guards.";
 }

 // New game -> two adventurers -> first home -> settlement. The intro tutorial gate is lifted for this fixture.
 public static async Task<string> Enter()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */Check(Application.isPlaying,"Play first");SceneManager.LoadScene("PartySelection");await Task.Delay(800);
  // The opening pair is preselected and fixed; older builds let any two be picked.
  var p=Object.FindAnyObjectByType<PartySelectionController>();if(PartySelectionSession.Selected.Count!=2){PartySelectionSession.Clear();p.Refresh();}await Task.Delay(100);
  foreach(var card in p.Cards.Where(x=>x.gameObject.activeInHierarchy&&x.Button.IsInteractable())){if(PartySelectionSession.Selected.Count>=2)break;await Tap(card.Button);}
  await Tap(p.Continue);await Task.Delay(800);
  var h=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(h.Cards[0].Button);await Tap(h.Continue);await Task.Delay(900);
  var c=Object.FindAnyObjectByType<SettlementController>();Check(c&&c.Campaign!=null,"Settlement missing");
  if(c.Introduction)c.Introduction.Restore(10);
  // The tutorial story (SettlementTutorialNarrative) has its own test: mark it read so its Dim never covers this fixture.
  var story=c.GetComponent<SettlementTutorialNarrative>();if(story)story.Restore(new SavedTutorialNarrative{SeenMask=SavedTutorialNarrative.AllSeen,PendingBeat=-1});await Task.Delay(300);
  return "Settlement "+c.Campaign.Home.Name+" · "+string.Join(", ",c.Campaign.Party.Select(x=>x.Name+" "+x.Health+"/"+x.MaxHealth))+" · stock ammo "+c.InventoryPanel.StockCount("ammo");
 }
 public static Task<string> BeginPreview()=>Begin(0);
 // Review fixture: same real path, but always two infected so every feedback layer appears.
 public static Task<string> BeginShowcase()=>Begin(2);
 static async Task<string> Begin(int forcedEnemies)
 {
  var c=Object.FindAnyObjectByType<SettlementController>();Check(c!=null&&c.Campaign!=null,"Open settlement first");
  // Inspection fixture: allocate existing stock, then follow the real departure/search path.
  var people=c.Campaign.Party.ToArray();for(int i=0;i<people.Length;i++)if(c.InventoryPanel.CountFor(people[i],"ammo")==0&&c.InventoryPanel.StockCount("ammo")>=2)Check(c.InventoryPanel.MoveFor(people[i],"ammo",2,true),"Pack ammo");
  await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(!card.Check.gameObject.activeSelf)await Tap(card.Button);
  await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
  var a=c.ArrivalPanel;Check(a.IsOpen,"Departure failed");
  // The ruined-start intro leaves no stock ammo: hand each member two field rounds (same path as picking ammo up on site).
  foreach(var p in a.Participants)if(c.InventoryPanel.CountFor(p,"ammo")==0)Check(c.InventoryPanel.TransferField(p,"ammo",2,true),"Field ammo fixture");
  foreach(var p in a.Participants)if(c.InventoryPanel.CountFor(p,"bandage")==0)Check(c.InventoryPanel.TransferField(p,"bandage",1,true),"Field bandage fixture");var e=a.Encounter;var s=a.Search;int bas=e.BaseChance,max=e.MaximumChance,threshold=e.NoiseThreshold,warnAt=e.WarnSearches,crateTurns=a.Loot.Sites[0].Turns;var oldRandom=UnityEngine.Random.state;
  try{e.BaseChance=e.MaximumChance=100;e.NoiseThreshold=0;e.WarnSearches=1;a.Loot.Sites[0].Turns=3;/* 함께: 2 turns, warned on the first, met on the second */UnityEngine.Random.InitState(5);/* 말 놓기: two pawns on the crate, then 턴 진행 */for(int w=0;w<60&&!FieldPawnTest.Ready(a);w++)await Task.Delay(50);Check(FieldPawnTest.Coop(a,0),"Two pawns on the crate: "+FieldPawnTest.Describe(a));for(int i=0;i<2&&!e.IsOpen;i++){await Task.Delay(250);FieldIdleConfirm.Pass(()=>a.Threat.Planner.TurnButton.onClick.Invoke());await Task.Delay(150);}Check(e.IsOpen,"No encounter");
   // These flows exercise the shared battle UI against the rules' 감염자; creature types are covered by VerifyCreatureBattle.
   e.Battle.NextLineup=new List<BattleCreature>();
   if(forcedEnemies>0){Check(e.Fight.IsInteractable(),"Fight unavailable");e.Battle.Begin(forcedEnemies);await Task.Delay(120);}else await Tap(e.Fight);}
  finally{e.BaseChance=bas;e.MaximumChance=max;e.NoiseThreshold=threshold;e.WarnSearches=warnAt;a.Loot.Sites[0].Turns=crateTurns;UnityEngine.Random.state=oldRandom;}
  var battle=e.Battle;Check(battle.IsOpen&&!a.Main.gameObject.activeSelf&&!a.PawnRoot.gameObject.activeSelf,"Battle input/world gate");Check(battle.Cells.Length==18,"Formation size");
  Check(battle.Presentation&&battle.HudLayer&&battle.FxLayer&&battle.NoiseGauge,"Feel layer wired");
  Check(battle.HudLayer.GetComponentsInChildren<BattlePawnHud>().Length==battle.State.Units.Count,"One HUD per standee");
  Bounds(battle.View);Cards(battle);await Tap(battle.Shoot);battle.Escape();Check(!battle.Aiming&&battle.Ranged,"Shoot selected without aiming");EventSystem.current.SetSelectedGameObject(null);
  // Serialized rules must match the reviewed defaults, or the game silently runs other numbers than the tests and simulation.
  var defaults=new FieldBattleRules();var drift=typeof(FieldBattleRules).GetFields().Where(f=>!Equals(f.GetValue(battle.Rules),f.GetValue(defaults))).Select(f=>f.Name).ToArray();
  Check(drift.Length==0,"Battle rules differ from the reviewed defaults (run SyncBattleRules.Apply or update the defaults): "+string.Join(", ",drift));
  return "Battle open via real departure, search and encounter ("+battle.State.Units.Count(u=>u.Enemy)+" infected), rules = reviewed defaults. Existing stock packed for this inspection fixture; original encounter rates and RNG restored.";
 }
 static async Task PickBandage(ExpeditionBattlePanel b)
 {
  foreach(var slot in b.Drawer.Slots.GetComponentsInChildren<BattleItemSlot>().Where(x=>x.Button.interactable)){if(b.SelectedItem=="bandage")break;await Tap(slot.Button);}
  Check(b.SelectedItem=="bandage","Bandage selectable");
 }
 public static async Task<string> ItemFlow()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");float speed=b.Presentation.Speed,pause=b.ActionPause;b.Presentation.Speed=8;b.ActionPause=.01f;
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");
   int actor=b.State.Actor;var person=b.State.Current.Person;int bandages=a.Inventory.CountFor(person,"bandage");Check(bandages>0,"Bandage fixture");
   await Tap(b.Items);await Task.Delay(150);
   Check(b.ItemMode&&b.Drawer.gameObject.activeSelf&&!b.CellButtons[0].interactable,"Drawer open, board locked");
   Check(b.HideDuringItems.All(g=>!g.activeSelf)&&b.ShowDuringItems.All(g=>g.activeSelf),"UI11 layout swap");Bounds(b.Drawer.gameObject);
   var slots=b.Drawer.Slots.GetComponentsInChildren<BattleItemSlot>();Check(slots.Length==12&&slots.Count(x=>x.Lock.activeSelf)==12-person.BagCapacity,"4x3 bag grid, locked past capacity");
   await PickBandage(b);
   int target=b.ItemTarget;var t=b.State.Units[target];int before=t.Health,after=Math.Min(t.Maximum,before+2);Check(before<t.Maximum,"Default target is hurt");
   Check(b.PreviewBefore.Health.text==before+" / "+t.Maximum&&b.PreviewAfter.Health.text==after+" / "+t.Maximum,"Before -> after preview");
   await Tap(b.Drawer.Cancel);Check(!b.ItemMode&&b.HideDuringItems.All(g=>g.activeSelf)&&a.Inventory.CountFor(person,"bandage")==bandages&&b.State.Actor==actor,"Cancel is free");
   await Task.Delay(150);await Tap(b.Items);await Task.Delay(100);b.Escape();Check(!b.ItemMode,"Esc closes the drawer");
   await Task.Delay(150);await Tap(b.Items);await Task.Delay(150);await PickBandage(b);
   await Tap(b.Drawer.Use);Check(t.Health==after&&t.Person.Health==after&&b.State.ItemsUsed==1&&a.Inventory.CountFor(person,"bandage")==bandages-1,"Heal applied, bandage consumed");
   Check(!b.Drawer.gameObject.activeSelf&&!b.ItemMode&&b.State.Actor!=actor,"Drawer closes, action spent");
   await Until(()=>!b.Busy||b.State.Outcome!=FieldBattleOutcome.Playing,10000,"treatment replay");
   return "PASS: drawer opens (UI11 swap, board locked), 4x3 grid with capacity locks, bandage/target select, before->after preview, cancel/Esc free, use heals +2 and persists, consumes 1, spends the action.";
  }
  finally{b.Presentation.Speed=speed;b.ActionPause=pause;}
 }
 public static async Task<string> AimFlow()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");var beforeRandom=UnityEngine.Random.state;float speed=b.Presentation.Speed,pause=b.ActionPause;b.Presentation.Speed=8;b.ActionPause=.01f;
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");
   if(b.Aiming)b.Escape();int enemy=b.State.Units.FindIndex(u=>u.Enemy&&u.Alive);var u=b.State.Units[enemy];int cell=9+u.Lane*3+u.Depth;
   b.Hover(cell,true);await Task.Delay(120);Check(b.AimTooltip.gameObject.activeSelf&&b.AimTooltip.IntentBlock.activeSelf&&!b.AimArrow.gameObject.activeSelf,"Hover card without aiming");
   if(b.Ranged){await Tap(b.Melee);b.Escape();}await Tap(b.Shoot);await Task.Delay(120);Check(b.Aiming&&b.AimTarget==enemy&&b.AimArrow.gameObject.activeSelf&&b.Reticle.gameObject.activeSelf,"Arrow snaps with brackets");
   Check(b.AimTooltip.AttackBlock.activeSelf&&b.AimTooltip.Chance.text.Contains(b.State.HitChance(enemy,true)+"%")&&b.AimTooltip.Armor.text.StartsWith("방어력")&&b.AimTooltip.Factors.text.Contains("기본"),"Aim card: chance, armor, factors");
   await Tap(b.Shoot);Check(!b.Aiming,"Same card puts the arrow away");
   await Tap(b.Melee);await Task.Delay(120);Check(b.Aiming&&!b.Ranged,"Melee aiming");
   if(!b.State.CanAttack(enemy,false)){int actions=b.State.Actions;await TapCell(b,true,u.Depth,u.Lane);Check(b.State.Actions==actions&&b.AimTooltip.Chance.text=="공격 불가","Unreachable target refused");}
   b.Escape();Check(!b.Aiming,"Esc cancels aiming");
   await Tap(b.Shoot);await Task.Delay(120);Rig(v=>v[0]<b.State.HitChance(enemy,true),2);int before=b.State.Actions;await TapCell(b,true,u.Depth,u.Lane);
   Check(b.State.Actions>before&&!b.Aiming,"Aimed click attacks once");b.Hover(cell,false);
   await Until(()=>!b.Busy||b.State.Outcome!=FieldBattleOutcome.Playing,10000,"shot replay");await Task.Delay(100);Check(!b.AimArrow.gameObject.activeSelf&&!b.Reticle.gameObject.activeSelf,"Aim hidden after the turn");
   return "PASS: hover card, aim arrow snap and brackets, card shows chance/armor/HP/factors, toggle off, melee aim, unreachable refusal, Esc cancel, aimed click attacks once.";
  }
  finally{UnityEngine.Random.state=beforeRandom;b.Presentation.Speed=speed;b.ActionPause=pause;}
 }
 // Review capture: pointer sweep and snap, aimed shot, item drawer and a bandage at real speed.
 public static async Task<string> ShowcaseAimItems()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen,"BeginShowcase first");var s=b.State;var beforeRandom=UnityEngine.Random.state;var log=new List<string>();
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");await Task.Delay(500);
   int enemy=s.Units.FindIndex(u=>u.Enemy&&u.Alive);var target=s.Units[enemy];int cell=9+target.Lane*3+target.Depth;
   b.Hover(cell,true);await Task.Delay(500);await Still(b,"06-hover-card");b.Hover(cell,false);
   await Tap(b.Shoot);b.ScriptedPointer=true;
   var start=b.AimPoint(s.Actor)+new Vector2(160,40);b.ScriptedPointerPosition=start;await Task.Delay(300);
   log.Add("clip4 frames "+await Record(b,"clip4-aim-shot",async()=>{
    var end=b.AimPoint(enemy);var bend=(start+end)*.5f+new Vector2(0,-120);
    for(float t=0;t<=1;t+=.04f){b.ScriptedPointerPosition=(1-t)*(1-t)*start+2*(1-t)*t*bend+t*t*end;await Task.Delay(33);}
    b.ScriptedPointerPosition=end;await Task.Delay(700);await Still(b,"07-aim-card");
    Rig(v=>v[0]<s.HitChance(enemy,true)&&v[1]>=s.Rules.CriticalChance,2);await TapCell(b,true,target.Depth,target.Lane);
    b.ScriptedPointer=false;}));
   await Until(()=>!b.Busy,10000,"after shot");await Task.Delay(300);
   if(s.PlayerTurn)
   {
    await Tap(b.Items);await Task.Delay(700);await PickBandage(b);await Task.Delay(300);await Still(b,"08-item-drawer");
    log.Add("clip5 frames "+await Record(b,"clip5-bandage",async()=>{await Task.Delay(300);await Tap(b.Drawer.Use);}));
   }
   return "Captured to "+Shots+" · "+string.Join(" · ",log);
  }
  finally{b.ScriptedPointer=false;UnityEngine.Random.state=beforeRandom;}
 }
 public static Task<string> VictoryFlow()=>Win(false);
 public static Task<string> ResultPreview()=>Win(true);
 public static Task<string> ResultAfterItems()=>Win(true,false);
 static async Task<string> Win(bool leaveResult,bool checkMove=true)
 {
  var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");
  int turns=a.Rooms.Turns,noise=a.Rooms.Noise,loot=a.Loot.State(0).Loot.Values.Sum(),progress=a.Loot.State(0).Progress;
  var beforeRandom=UnityEngine.Random.state;float pause=b.ActionPause,speed=b.Presentation.Speed;b.ActionPause=.01f;b.Presentation.Speed=8;
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");
   // Clicking the interior of a perspective cell must hit its actual polygon.
   if(checkMove){await TapCell(b,false,1,0);await Until(()=>!b.Busy,4000,"move");Check(b.State.Moved&&b.State.Current.Depth==1,"Move click");}
   int guard=0,confirmedByTap=0;
   while(b.State.Outcome==FieldBattleOutcome.Playing&&guard++<40)
   {
    await Until(()=>!b.Busy||b.State.Outcome!=FieldBattleOutcome.Playing,10000,"enemy phase");if(b.State.Outcome!=FieldBattleOutcome.Playing)break;
    int target=b.State.Units.FindIndex(u=>u.Enemy&&u.Alive);var u=b.State.Units[target];
    await TapCell(b,true,u.Depth,u.Lane);Check(b.SelectedTarget==target,"Target select");
    await Tap(b.Shoot);if(!b.Execute.interactable)await Tap(b.Melee);
    if(!b.Execute.interactable){await Tap(b.Guard);continue;}
    Rig(v=>v[0]<b.State.HitChance(target,b.Ranged),1);
    int actions=b.State.Actions;
    if(guard%2==0){await TapCell(b,true,u.Depth,u.Lane);confirmedByTap++;Check(b.State.Actions>actions,"Second tap did not attack");}
    else{ClickNow(b.Execute);int after=b.State.Actions;Check(after>actions&&b.Busy,"Attack did not resolve");b.Execute.onClick.Invoke();b.CellButtons[9+u.Lane*3+u.Depth].onClick.Invoke();Check(b.State.Actions==after,"Double click actions");}
   }
   // uGUI only raycasts a graphic after its first render, so give the freshly shown result one frame or two.
   await Until(()=>b.Result.activeSelf,8000,"victory result");await Task.Delay(150);
   Check(b.State.Outcome==FieldBattleOutcome.Victory,"No victory: "+b.State.Outcome);Bounds(b.Result);
   Check(b.HudLayer.GetComponentsInChildren<BattlePawnHud>(true).Length>=2,"HUDs exist");
   if(leaveResult)return "Victory result shown from actual attacks. Runtime-only review fixture.";
   int resultNoise=b.State.ResultNoise;
   await Tap(b.ResultContinue);b.ResultContinue.onClick.Invoke();Check(!b.IsOpen&&!a.Encounter.IsOpen&&a.Loot.IsOpen,"Resume loot");
   Check(a.Rooms.Turns==turns+1&&a.Rooms.Noise==noise+resultNoise&&a.Loot.State(0).Loot.Values.Sum()==loot&&a.Loot.State(0).Progress==progress,"Cost/reward duplication");
   Check(!b.FxLayer.GetComponentsInChildren<BattleFloatingText>(true).Any()&&!b.HudLayer.GetComponentsInChildren<BattlePawnHud>(true).Any(),"Battle overlays left behind");
   return "PASS: polygon move click, target select, attack via button and second tap ("+confirmedByTap+"x), double click lock, victory result bounds, exactly one exploration turn, battle noise +"+resultNoise+" carried to exploration, loot/progress preserved, overlays cleaned.";
  }
  finally{UnityEngine.Random.state=beforeRandom;b.ActionPause=pause;b.Presentation.Speed=speed;}
 }
 // The fixture fights in the arcade (room 0, Begin taps Objects[0]). Its way back is the exit, so a battle retreat ends the expedition
 // exactly like the encounter's own retreat (EncounterPanel.FinishBattle -> ArrivalPanel.FinishReturn): travel minutes, no exploration turn.
 // It closes the expedition, so run Enter + BeginPreview again before any later battle flow.
 public static async Task<string> RetreatFlow()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen,"Preview first");Check(a.Rooms.CurrentRoom==0&&b.RetreatsHome,"Fixture fights in the arcade (room 0)");
  var c=Object.FindAnyObjectByType<SettlementController>();var site=c.ExpeditionPanel.Destinations.First(d=>d.Id==c.Campaign.FieldDestination);
  int turns=a.Rooms.Turns,clock=c.Campaign.Day*1440+c.Campaign.MinuteOfDay;float speed=b.Presentation.Speed;b.Presentation.Speed=8;
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");
   var cost=b.Retreat.transform.Find("Cost");Check(cost&&cost.GetComponent<Text>().text==b.RetreatHomeCost,"Retreat cost reads the home return: "+(cost?cost.GetComponent<Text>().text:"no Cost"));
   await Tap(b.Retreat);Bounds(b.RetreatReview);var threats=b.State.RetreatThreats().Count;
   var expected=string.Format(threats==0?b.RetreatHomeQuietBody:b.RetreatHomeThreatBody,threats,b.Rules.RetreatHitChance,a.Rooms.RetreatRoomName,b.Rules.EnemyDamage);
   Check(b.RetreatBody.text==expected&&b.RetreatBody.text.Contains("거점")&&!b.RetreatBody.text.Contains("1턴")&&!b.RetreatBody.text.Contains("적 0마리"),"Retreat body is the home variant: "+b.RetreatBody.text);
   b.Escape();Check(!b.RetreatReview.activeSelf&&b.IsOpen&&a.Rooms.Turns==turns,"Esc retreat cost");
   await Tap(b.Retreat);await Tap(b.RetreatConfirm);await Until(()=>b.Result.activeSelf,8000,"retreat result");await Task.Delay(150);Bounds(b.Result);
   Check(b.State.Units.Where(u=>!u.Enemy).All(u=>u.Health>=1),"Retreat knocked someone out");
   var consequences=b.Result.GetComponent<BattleResultSummary>().Consequences.text;
   Check(b.ResultContinueLabel.text==b.RetreatHomeLabel,"Continue reads the home return: "+b.ResultContinueLabel.text);
   Check(consequences.Contains("거점")&&consequences.Contains(site.OneWayMinutes+"분")&&!consequences.Contains("탐험 1턴")&&!consequences.Contains("총성"),"Result: home return with travel time, no exploration turn or gunfire: "+consequences);
   await Tap(b.ResultContinue);b.ResultContinue.onClick.Invoke();await Task.Delay(600);
   Check(!b.IsOpen&&!a.Encounter.IsOpen&&!a.IsOpen&&!c.Campaign.IsFieldExpedition&&c.Campaign.Stage==JourneyStage.Settlement,"Retreat from the arcade ends the expedition");
   Check(a.Rooms.Turns==turns&&c.Campaign.Day*1440+c.Campaign.MinuteOfDay-clock==site.OneWayMinutes,"Home return spends the travel minutes, not an exploration turn");
   Check(!c.ReturnPanel||c.ReturnPanel.IsOpen,"Return report opens");
   return "PASS: arcade retreat reads '"+b.RetreatHomeCost+"' / home review ("+(threats==0?"quiet":threats+" parting blows")+"), Esc free, nobody knocked out, result '"+b.RetreatHomeLabel+"' without an exploration turn or gunfire, duplicate continue ignored, expedition ended with "+site.OneWayMinutes+" travel minutes and the return report open.";
  }
  finally{b.Presentation.Speed=speed;}
 }

 // ---- Review capture: real Play-mode frames, saved as PNG sequences for stills and GIFs ----
 sealed class Recorder
 {
  public readonly List<Texture2D> Frames=new List<Texture2D>();public readonly List<float> Times=new List<float>();public bool Running;
  public IEnumerator Run(int width,float interval)
  {
   Running=true;float next=0;RenderTexture full=null,small=null;
   while(Running)
   {
    yield return new WaitForEndOfFrame();
    if(Time.unscaledTime<next)continue;next=Time.unscaledTime+interval;
    if(full==null||full.width!=Screen.width||full.height!=Screen.height){if(full)full.Release();full=new RenderTexture(Screen.width,Screen.height,0);}
    int height=Mathf.RoundToInt(width*(float)Screen.height/Screen.width);if(small==null){small=new RenderTexture(width,height,0);}
    ScreenCapture.CaptureScreenshotIntoRenderTexture(full);Upright(full,small);
    var previous=RenderTexture.active;RenderTexture.active=small;var tex=new Texture2D(small.width,small.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,small.width,small.height),0,0);tex.Apply();RenderTexture.active=previous;
    Frames.Add(tex);Times.Add(Time.unscaledTime);
   }
   if(full)Object.Destroy(full);if(small)Object.Destroy(small);
  }
  public int Save(string folder)
  {
   Directory.CreateDirectory(folder);foreach(var f in Directory.GetFiles(folder,"*.png"))File.Delete(f);
   for(int i=0;i<Frames.Count;i++){File.WriteAllBytes(Path.Combine(folder,"frame-"+i.ToString("000")+".png"),Frames[i].EncodeToPNG());Object.Destroy(Frames[i]);}
   File.WriteAllLines(Path.Combine(folder,"times.txt"),Times.Select(t=>t.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture)));
   int n=Frames.Count;Frames.Clear();Times.Clear();return n;
  }
 }
 // CaptureScreenshotIntoRenderTexture is vertically flipped where UVs start at the top (D3D).
 static void Upright(RenderTexture source,RenderTexture target){if(SystemInfo.graphicsUVStartsAtTop)Graphics.Blit(source,target,new Vector2(1,-1),new Vector2(0,1));else Graphics.Blit(source,target);}
 static string Shots=>Path.GetFullPath(Path.Combine(Application.dataPath,"..","Temp","BattleFeelCapture"));
 static Task Still(ExpeditionBattlePanel b,string name)
 {
  var done=new TaskCompletionSource<bool>();b.StartCoroutine(StillRoutine(name,done));return done.Task;
 }
 static IEnumerator StillRoutine(string name,TaskCompletionSource<bool> done)
 {
  yield return new WaitForEndOfFrame();
  var raw=new RenderTexture(Screen.width,Screen.height,0);var rt=new RenderTexture(Screen.width,Screen.height,0);ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);Upright(raw,rt);
  var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=previous;Object.Destroy(raw);Object.Destroy(rt);
  Directory.CreateDirectory(Shots);File.WriteAllBytes(Path.Combine(Shots,name+".png"),tex.EncodeToPNG());Object.Destroy(tex);done.SetResult(true);
 }
 static async Task<int> Record(ExpeditionBattlePanel b,string clip,Func<Task> action)
 {
  var recorder=new Recorder();b.StartCoroutine(recorder.Run(800,1/24f));
  try{await Task.Delay(250);await action();await Until(()=>!b.Busy,20000,"clip "+clip);await Task.Delay(450);}
  catch{recorder.Running=false;await Task.Delay(120);foreach(var f in recorder.Frames)Object.Destroy(f);recorder.Frames.Clear();throw;}
  recorder.Running=false;await Task.Delay(120);return recorder.Save(Path.Combine(Shots,clip));
 }
 // Scripted exchange at real speed: shot, guard, advance; crit melee kill, noisy shots (a third one when the threshold needs it), reinforcement; guard, miss, bite.
 public static async Task<string> Showcase()
 {
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=Arrival;var b=a.Encounter.Battle;Check(b.IsOpen&&b.State.Units.Count(u=>u.Enemy)==2,"BeginShowcase first");var s=b.State;var beforeRandom=UnityEngine.Random.state;var log=new List<string>();
  try
  {
   await Until(()=>!b.Busy,4000,"first turn");await Task.Delay(500);
   int e1=s.Units.FindIndex(u=>u.Enemy),e2=s.Units.FindLastIndex(u=>u.Enemy&&!u.Reinforcement);
   await TapCell(b,true,s.Units[e1].Depth,s.Units[e1].Lane);await Tap(b.Shoot);EventSystem.current.SetSelectedGameObject(null);await Task.Delay(700);
   await Still(b,"01-aim-and-intent");
   log.Add("clip1 frames "+await Record(b,"clip1-shot-guard-advance",async()=>{
    Rig(v=>v[0]<s.HitChance(e1,true)&&v[1]>=s.Rules.CriticalChance,2);await Tap(b.Execute);
    await Until(()=>!b.Busy,8000,"ally 2 turn");await Tap(b.Guard);}));
   await Task.Delay(400);
   int front=s.Units.FindIndex(u=>u.Enemy&&u.Alive&&u.Depth==0);
   await Still(b,"02-bite-forecast");
   int ally=s.Actor;var move=Enumerable.Range(0,9).Select(i=>new{d=i%3,l=i/3}).FirstOrDefault(c=>s.CanMove(c.d,c.l));
   if(move!=null){b.Hover(move.l*3+move.d,true);await Task.Delay(500);await Still(b,"03-move-preview");b.Hover(move.l*3+move.d,false);}
   log.Add("clip2 frames "+await Record(b,"clip2-crit-kill-noise-reinforcement",async()=>{
    int target=s.Units[e1].Alive?e1:front;var t=s.Units[target];
    if(!s.InMeleeReach(target))
     foreach(var o in new[]{new{d=s.Current.Depth-1,l=s.Current.Lane},new{d=s.Current.Depth,l=s.Current.Lane+Math.Sign(t.Lane-s.Current.Lane)}})
      if(s.CanMove(o.d,o.l)){await TapCell(b,false,o.d,o.l);await Until(()=>!b.Busy,4000,"step in");break;}
    await TapCell(b,true,t.Depth,t.Lane);bool melee=s.InMeleeReach(target);await Tap(melee?b.Melee:b.Shoot);
    Rig(v=>v[0]<s.HitChance(target,!melee)&&v[1]<s.Rules.CriticalChance,2);await Tap(b.Execute);log.Add(melee?"melee crit":"shot crit");
    await Until(()=>!b.Busy,8000,"ally 2 second turn");
    int next=s.Units.FindIndex(u=>u.Enemy&&u.Alive);if(next<0)return;await TapCell(b,true,s.Units[next].Depth,s.Units[next].Lane);await Tap(b.Shoot);
    if(!b.Execute.interactable){await Tap(b.Guard);return;}
    Rig(v=>v[0]<s.HitChance(next,true)&&v[1]>=s.Rules.CriticalChance,2);await Tap(b.Execute);
    // Threshold 5: melee + two shots (noise 4) stay quiet. Fire one more that leaves its target standing (noise 6),
    // then let the round turn over so the reinforcement walks in before the '04-reinforced-round' still.
    if(s.Reinforcements>0||s.ReinforcementPending)return;
    await Until(()=>!b.Busy||s.Outcome!=FieldBattleOutcome.Playing,10000,"ally 1 third turn");if(s.Outcome!=FieldBattleOutcome.Playing||!s.PlayerTurn)return;
    int loud=s.Units.FindIndex(u=>u.Enemy&&u.Alive);if(loud<0)return;await TapCell(b,true,s.Units[loud].Depth,s.Units[loud].Lane);await Tap(b.Shoot);
    if(!b.Execute.interactable){await Tap(b.Guard);return;}
    int odds=s.HitChance(loud,true);bool stands=s.Units[loud].Health>s.ExpectedDamage(loud,true);Rig(v=>stands?v[0]<odds&&v[1]>=s.Rules.CriticalChance:v[0]>=odds,2);await Tap(b.Execute);log.Add("extra shot, noise "+s.Noise);
    await Until(()=>!b.Busy||s.Outcome!=FieldBattleOutcome.Playing,10000,"ally 2 guard");if(s.Outcome==FieldBattleOutcome.Playing&&s.PlayerTurn)await Tap(b.Guard);}));
   log.Add("after clip2: noise "+s.Noise+" reinforcements "+s.Reinforcements+" enemies "+s.Units.Count(u=>u.Enemy&&u.Alive));
   await Task.Delay(300);await Still(b,"04-reinforced-round");
   if(s.Outcome==FieldBattleOutcome.Playing)
    log.Add("clip3 frames "+await Record(b,"clip3-guard-miss-bite",async()=>{
     await Tap(b.Guard);await Until(()=>!b.Busy,8000,"ally 2 third turn");
     int far=s.Units.FindLastIndex(u=>u.Enemy&&u.Alive);await TapCell(b,true,s.Units[far].Depth,s.Units[far].Lane);await Tap(b.Shoot);
     if(!b.Execute.interactable){await Tap(b.Guard);return;}
     int chance=s.HitChance(far,true);Rig(v=>v[0]>=chance&&v[1]<55,2);await Tap(b.Execute);}));
   // Finish quickly (not recorded), then keep the result screen for review.
   b.Presentation.Speed=6;b.ActionPause=.01f;int guard=0;
   while(s.Outcome==FieldBattleOutcome.Playing&&guard++<40)
   {
    await Until(()=>!b.Busy||s.Outcome!=FieldBattleOutcome.Playing,10000,"finish");if(s.Outcome!=FieldBattleOutcome.Playing)break;
    int target=s.Units.FindIndex(u=>u.Enemy&&u.Alive);await TapCell(b,true,s.Units[target].Depth,s.Units[target].Lane);
    await Tap(b.Melee);if(!b.Execute.interactable)await Tap(b.Shoot);if(!b.Execute.interactable){await Tap(b.Guard);continue;}
    Rig(v=>v[0]<s.HitChance(target,b.Ranged),1);await Tap(b.Execute);
   }
   await Until(()=>b.Result.activeSelf,8000,"result");b.Presentation.Speed=1;await Task.Delay(400);await Still(b,"05-result");
   return "Captured to "+Shots+" · "+string.Join(" · ",log)+" · outcome "+s.Outcome;
  }
  finally{UnityEngine.Random.state=beforeRandom;b.Presentation.Speed=1;b.ActionPause=.3f;}
 }
}
