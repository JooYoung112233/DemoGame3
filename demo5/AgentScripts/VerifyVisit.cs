using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
public static class VerifyVisit
{
    static int count;
    static void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
    static CampaignState Setup(){var c=new CampaignState();c.Toggle(0);c.Toggle(1);c.ConfirmParty();c.Settle(0);c.Depart(()=>0);return c;}
    public static string Rules()
    {
        count=0;var c=Setup();var v=new ExplorationRun(c.ActiveRun,()=>0);
        Check(v.Phase==VisitPhase.Explore,"Must start exploring");Check(!v.Attack(0,true),"Attack outside combat");
        Check(v.Search(0,SearchPace.Normal,0,PartnerDuty.Watch)&&v.Phase==VisitPhase.Explore,"First search not safe");
        Check(v.Minutes==10&&v.Progress[0]==38&&v.Loot==1,"Search effects");
        Check(v.Search(0,SearchPace.Normal,0,PartnerDuty.Watch)&&v.Phase==VisitPhase.Encounter,"Random encounter missing");
        Check(!v.Search(1,SearchPace.Normal,0,PartnerDuty.Watch),"Search during encounter");Check(v.BeginCombat(),"Battle start");
        Check(v.Lanes==3&&v.Actor==0,"3x3 initial deployment");Check(v.Move(1,0),"Position move");Check(!v.Move(2,0),"Moved twice");Check(!v.Attack(0,false),"Rear melee");
        Check(v.Attack(0,true)&&v.Actor==1,"Individual turn progression");Check(v.Attack(1,true)&&v.Phase==VisitPhase.Result,"Victory");
        Check(v.Ammo==4,"Ammo consumption");Check(v.Resume()&&v.Progress[0]==76,"Return to exploration loses progress");
        Check(v.Search(0,SearchPace.Thorough,0,PartnerDuty.Watch)&&v.Progress[0]==100&&v.Stock[0]==0&&v.Loot==2,"Finite site contents");Check(!v.Search(0,SearchPace.Normal,0,PartnerDuty.Watch),"Exhausted site farming");
        Check(v.ChangeLanes(4)&&v.Lanes==4,"4x3 support");Check(v.Return()&&c.Return(),"Settlement return");Check(!v.Return()&&!c.Return(),"Duplicate reward");Check(c.Supplies==4&&c.Ammo==4,"Campaign settlement");
        c=Setup();v=new ExplorationRun(c.ActiveRun,()=>.99);Check(!v.CanLight(1)&&v.CanLight(0),"Flashlight ownership");v.Search(0,SearchPace.Thorough,0,PartnerDuty.Light);Check(v.Battery==95&&v.Progress[0]==75,"Light support");
        v.PreviewEncounter();v.BeginCombat();Check(v.Attack(0,true)&&v.Foes[0].Health==3&&v.Ammo==5,"Miss consumes ammo");
        c=Setup();v=new ExplorationRun(c.ActiveRun,()=>0);v.ChangeLanes(4);v.PreviewEncounter();v.BeginCombat();Check(!v.ChangeLanes(3)&&v.Lanes==4,"Board changed during combat");
        v.Run.Squad[0].Health=1;v.Run.Squad[1].Health=1;v.Guard();v.Guard();Check(v.Phase==VisitPhase.Result,"Defeat");Check(v.Resume()&&c.Return()&&c.Stage==JourneyStage.Lost,"Defeat settlement");
        return count+" exploration / combat checks passed";
    }
    static void Click(NightRunView v,string name){var b=v.GetComponentsInChildren<Button>(true).First(x=>x.name==name);Check(b.gameObject.activeInHierarchy&&b.interactable,"Unavailable "+name);b.onClick.Invoke();}
    public static string UI()
    {
        count=0;var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Restart();Click(v,"Candidate_0");Click(v,"Candidate_1");Click(v,"ConfirmParty");Click(v,"Home_0");Click(v,"Depart");
        Check(v.Visit.Phase==VisitPhase.Explore,"Depart skips exploration");Click(v,"SearchPace_1");Click(v,"PartnerDuty_2");Click(v,"CommitSearch");Check(v.Visit.Battery==95,"UI light support");
        Click(v,"PreviewEncounter");Click(v,"FightEncounter");Check(v.Visit.Phase==VisitPhase.Combat,"Combat UI");Click(v,"Formation_0_1_0");Check(v.Visit.Moved,"Board click");Click(v,"VisitShoot");Click(v,"EnemyTarget_0");Check(v.Visit.Ammo==5,"Target click");
        v.Restart();return count+" UI flow checks passed";
    }
    public static string Explore(){var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Restart();v.ToggleCandidate(0);v.ToggleCandidate(1);v.ConfirmParty();v.ChooseHome(0);v.Depart();return "exploration";}
    public static string Combat(){Explore();var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.EncounterPreview();v.FightVisit();return "3x3 combat";}
    public static string Combat4(){Explore();var v=UnityEngine.Object.FindAnyObjectByType<NightRunView>();v.Visit.ChangeLanes(4);v.EncounterPreview();v.FightVisit();return "4x3 combat";}
}
