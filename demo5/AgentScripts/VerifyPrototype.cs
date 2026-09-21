using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyPrototype
{
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static RunState NewRun() { var s = new RunState(() => 0); s.Start(); return s; }
    public static string Rules()
    {
        assertions = 0;
        var s = NewRun();
        Check(!s.Act(0, Order.Move, 3, 3), "Nonadjacent move allowed");
        Check(s.Squad[0].Actions == 2, "Invalid move spent AP");
        Check(!s.Act(0, Order.Move, 1, 3), "Overlapping allies allowed");
        Check(s.Act(1, Order.Move, 1, 2), "Move toward shelf failed");
        Check(s.Act(1, Order.Search, 1, 1), "Search failed");
        Check(s.Supplies == 1 && s.Noise == 1, "Search resources incorrect");
        Check(!s.Act(1, Order.Search, 1, 1), "Exhausted AP allowed action");
        s.EndTurn();
        Check(!s.Act(1, Order.Search, 1, 1), "Repeat loot allowed");
        Check(s.Light == 85 && s.Round == 2, "Round/light incorrect");
        Check(s.Act(1, Order.Search, 2, 2), "Second cache failed");
        Check(s.Act(1, Order.Move, 1, 3), "Return route blocked");
        Check(s.Act(0, Order.Extract, 0, 3), "First extraction failed");
        s.EndTurn();
        Check(s.Act(1, Order.Extract, 1, 3), "Second extraction failed");
        Check(s.Won && s.Supplies == 2 && s.Phase == RunPhase.Debrief, "Win route failed");
        Check(!s.Act(1, Order.Guard, 1, 3), "Action accepted after debrief");

        s = NewRun(); s.Act(0, Order.Extract, 0, 3); s.Act(1, Order.Extract, 1, 3);
        Check(!s.Won && s.Result == "긴급 철수", "Early extraction should be partial");

        s = NewRun(); s.Squad[0].X = 0; s.Squad[0].Y = 1;
        Check(!s.Act(0, Order.Shoot, 3, 1), "Shot passed through shelf");
        s.Squad[0].Y = 0;
        Check(s.Act(0, Order.Shoot, 3, 0), "Clear shot failed");
        Check(s.Ammo == 3 && s.Noise == 3 && s.EnemyAt(3, 0) == null, "Shot rules incorrect");
        s = new RunState(() => .999); s.Start(); s.Squad[0].Y = 0;
        Check(s.Act(0, Order.Shoot, 3, 0), "Missed shot should still execute");
        Check(s.EnemyAt(3, 0).Health == 3 && s.Ammo == 3 && s.Noise == 3 && s.Squad[0].Actions == 0, "Miss failed to consume resources");
        Check(s.HitChance(s.Squad[0], s.Enemies[0], true) == 70, "Ranged distance penalty incorrect");
        var covered = new Piece("covered", 1, 3);
        Check(s.HitChance(new Piece("shooter", 0, 3), covered, true) == 70, "Cover penalty incorrect");
        Check(s.HitChance(s.Squad[0], covered, false) == 85, "Cover must not protect melee");

        s = NewRun(); s.Enemies.Clear(); s.Enemies.Add(new Piece("Test", 0, 2));
        s.Act(0, Order.Guard, 0, 3); s.EndTurn();
        Check(s.Squad[0].Health == 4 && !s.Squad[0].Guarding, "Guard mitigation/reset failed");
        s.EndTurn(); Check(s.Squad[0].Health == 2, "Unguarded damage incorrect");

        s = NewRun(); s.Enemies.Clear(); s.Enemies.Add(new Piece("Test", 0, 2));
        Check(s.Act(0, Order.Lure, 3, 3), "Lure failed"); s.EndTurn();
        Check(s.Squad[0].Health == 5, "Lured enemy attacked instead");
        Check(s.LureX == -1, "Lure failed to expire");

        s = NewRun(); s.Enemies.Clear();
        for (int n = 0; n < 3; n++) { s.Act(0, Order.Lure, 0, 2); s.Act(1, Order.Lure, 1, 2); s.EndTurn(); }
        Check(s.Waves == 1 && s.EnemyAt(3, 0) != null && s.Noise == 0, "Noise reinforcement failed");

        s = NewRun(); s.Enemies.Clear();
        for (int n = 0; n < 7; n++) s.EndTurn();
        Check(s.Light == 0, "Light not clamped");
        s = NewRun(); s.Squad[0].Health = 0; s.Squad[1].Health = 0; s.EndTurn();
        Check(s.Phase == RunPhase.Debrief && s.Result == "원정 실패", "Failure not resolved");
        return assertions + " rule assertions passed, including complete win/retreat/failure paths.";
    }
    static Button FindButton(NightRunView view, string name) => view.GetComponentsInChildren<Button>(true).First(b => b.name == name);
    static void Click(NightRunView v, string name) { var b = FindButton(v, name); Check(b.interactable && b.gameObject.activeInHierarchy, "Inactive button: " + name); b.onClick.Invoke(); }
    public static string PlayThrough()
    {
        assertions = 0;
        var v = UnityEngine.Object.FindAnyObjectByType<NightRunView>();
        Check(v != null, "Scene has no view"); v.Restart();
        Click(v, "Candidate_0"); Click(v, "Candidate_1"); Click(v, "ConfirmParty"); Click(v, "Home_0");
        Check(v.Campaign.Stage == JourneyStage.Settlement, "Settlement not reached");
        int before = v.Campaign.Supplies;
        Click(v, "Depart");
        Click(v, "Deploy"); Click(v, "Survivor_1"); Click(v, "Cell_1_2");
        Click(v, "Order_Search"); Click(v, "Cell_1_1"); Click(v, "EndTurn");
        Click(v, "Cell_2_2"); Click(v, "Order_Move"); Click(v, "Cell_1_3");
        Click(v, "Survivor_0"); Click(v, "Order_Extract"); Click(v, "EndTurn");
        Click(v, "Order_Extract"); Check(v.State.Won, "UI route did not win");
        Check(v.GetComponentsInChildren<Text>(true).All(t => t.font != null), "Missing UI font");
        Click(v, "Deploy");
        Check(v.Campaign.Stage == JourneyStage.Settlement && v.Campaign.Supplies == before + 2, "Return resources not applied");
        Check(!v.Campaign.Return(), "Duplicate settlement reward allowed");
        Check(v.Campaign.Day == 2, "Day did not advance");
        v.Restart();
        return assertions + " Play Mode assertions passed: party choice -> home -> expedition -> return. Reset to selection screen.";
    }
    public static string CampaignRules()
    {
        assertions=0;var c=new CampaignState();
        Check(!c.ConfirmParty(),"Empty party accepted");Check(!c.Depart(),"Departed without settlement");
        c.Toggle(0);Check(!c.ConfirmParty(),"One-person initial party accepted");c.Toggle(1);
        Check(!c.Toggle(2)&&c.Chosen.Count==2,"Third member accepted");c.Toggle(1);c.Toggle(2);
        Check(c.ConfirmParty()&&c.Settle(2),"Choice flow failed");
        Check(c.Supplies==2&&c.Ammo==3&&c.Home.Recovery==3,"Home effects missing");
        c.Candidates[0].Health=2;Check(c.Rest()&&c.Candidates[0].Health==5&&c.Supplies==1,"Rest incorrect");
        Check(c.PrepareAmmo()&&c.Ammo==5&&c.Supplies==0,"Ammo prep incorrect");Check(!c.PrepareAmmo(),"Spent nonexistent supplies");
        Check(c.Depart(()=>0),"Depart failed");Check(!c.Depart(),"Duplicate departure allowed");
        var r=c.ActiveRun;Check(r.Squad[1].Name=="하린"&&r.Squad[1].AimBonus==15&&r.Ammo==5,"Party/gear not transferred");
        Check(!c.Return(),"Unfinished expedition returned");r.Start();r.Act(0,Order.Extract,0,3);r.Act(1,Order.Extract,1,3);
        Check(c.Return()&&c.Ammo==5&&c.Day==2,"Return failed");Check(!c.Return(),"Double reward");
        return assertions+" campaign assertions passed.";
    }
    public static string ShowSettlement()
    {var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Restart();v.ToggleCandidate(0);v.ToggleCandidate(1);v.ConfirmParty();v.ChooseHome(0);return "settlement";}
    public static string ShowCombat()
    {ShowSettlement();var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Depart();v.Begin();return "combat";}
    public static string ShowSelection()
    {var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Restart();v.ToggleCandidate(0);v.ToggleCandidate(1);return "selection";}
}
