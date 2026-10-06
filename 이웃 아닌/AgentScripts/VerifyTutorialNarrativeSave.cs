using System;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

// Stopped-editor fixtures. Uses only DTOs, the actual catalog and isolated Temp slots.
public static class VerifyTutorialNarrativeSave
{
    static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    static T Copy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    static SettlementController Catalog()=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
    static CampaignSaveData Save(SettlementController c)
    {
        var members=c.Roster.Candidates.Select(p=>new SavedMember {
            Id=p.Id,Name=p.DisplayName,Role=p.RoleTitle,Description=p.Description,
            Health=p.Id=="medic"?0:p.Health-1,Maximum=p.Health,Aim=p.Aim,Capacity=p.BagCapacity,
            Bag=p.Id=="scout"?new[]{new SavedCount{Id="cloth",Count=2}}:Array.Empty<SavedCount>()
        }).ToArray();
        return new CampaignSaveData {
            Version=17,SavedUtc="2026-09-25T00:00:00Z",HomeId="garage",Day=c.VisitorPanel.FirstDay,Minute=c.VisitorPanel.ArrivalMinute,
            Ammo=2,IntroductionStep=0,Opening=new SavedOpeningChapter{Enabled=true},Development=new SavedDevelopment(),
            Members=members,Materials=c.CraftPanel.Materials.Select(m=>new SavedCount{Id=m.Id,Count=m.Id=="prybar"?0:20}).ToArray(),
            Rest=Array.Empty<SavedRest>(),Craft=Array.Empty<SavedCraft>(),Cooking=Array.Empty<SavedCooking>(),
            Visitor=new SavedVisitor{VisitDay=c.VisitorPanel.FirstDay,RecruitId="guard",Recruited=true,Dismissed=true,
                Stock=c.VisitorPanel.Goods.Select(g=>new SavedCount{Id=g.Id,Count=Math.Max(0,g.Initial-1)}).ToArray()},
            Inspected=new[]{"mall.arcade.table"},Searches=new[]{new SavedSearch{Id="mall.arcade.table",Progress=1,Required=1,Complete=true,Opened=true,
                Loot=new[]{new SavedCount{Id="cloth",Count=1},new SavedCount{Id="wood",Count=2}}}},
            SiteBoards=new[]{new SavedSiteBoard{Id="mall",Danger=2,Gauge=3,Remembered="mall.corridor",IntroShown=true,ResidentLessonShown=true}},
            Doyun=new SavedNpcStory{Stage=3,Page=3,Choice=2,VisitCount=3,MetVisit=2,Returned=true,Reunited=true},
            UnlockedCharacters=members.Select(m=>m.Id).OrderBy(id=>id).ToArray(),Activity=new[]{"기존 활동 기록"},TutorialNarrative=null
        };
    }
    static string Gameplay(CampaignSaveData data)
    {
        var normalized=Copy(data);normalized.Version=18;normalized.TutorialNarrative=null;
        return JsonUtility.ToJson(normalized);
    }
    static void UpgradeCheck(CampaignSaveData s,SettlementController c,int expectedMask,string label)
    {
        string before=Gameplay(s);CampaignPersistence.Upgrade(s,c);
        Check(s.Version==18&&s.TutorialNarrative.SeenMask==expectedMask&&s.TutorialNarrative.PendingBeat==-1&&s.TutorialNarrative.LineIndex==0,"Wrong migrated dialogue context: "+label);
        Check(Gameplay(s)==before,"Migration altered gameplay, resources, time, jobs or history: "+label);
        string once=JsonUtility.ToJson(s);CampaignPersistence.Upgrade(s,c);
        Check(once==JsonUtility.ToJson(s),"Repeated loading changed progress: "+label);
    }
    public static string Migration()
    {
        Check(CampaignPersistence.CurrentVersion==18,"Expected save v18.");var c=Catalog();
        foreach(int step in new[]{0,1,2,3,4,5,10})
        {
            var s=Save(c);s.IntroductionStep=step;UpgradeCheck(s,c,(1<<Math.Min(4,step))-1,"intro "+step);
        }
        var first=Save(c);first.IntroductionStep=10;first.Opening.FirstReturn=true;UpgradeCheck(first,c,31,"first return");
        var bench=Save(c);bench.IntroductionStep=10;bench.Opening.FirstReturn=true;bench.Development.Workbench=true;UpgradeCheck(bench,c,63,"bench");
        var clue=Save(c);clue.Opening.FirstReturn=clue.Opening.ClueRead=true;clue.Development.Workbench=true;UpgradeCheck(clue,c,127,"clue");
        var tool=Save(c);tool.Opening.FirstReturn=tool.Opening.ClueRead=true;tool.Materials.Single(m=>m.Id=="prybar").Count=1;UpgradeCheck(tool,c,255,"warehouse prybar");
        tool=Save(c);tool.Opening.FirstReturn=tool.Opening.ClueRead=true;tool.Members.Single(m=>m.Id=="scout").Bag=new[]{new SavedCount{Id="prybar",Count=1}};UpgradeCheck(tool,c,255,"carried prybar");
        var unlocked=Save(c);unlocked.Opening.FirstReturn=unlocked.Opening.ClueRead=true;unlocked.StorageUnlocked=true;UpgradeCheck(unlocked,c,255,"storage already unlocked without a carried prybar");
        var survey=Save(c);survey.Opening.FirstReturn=survey.Opening.ClueRead=survey.Opening.SurveyReturned=true;UpgradeCheck(survey,c,511,"survey return");
        var complete=Save(c);complete.Opening.FirstReturn=complete.Opening.ClueRead=complete.Opening.SurveyReturned=complete.Opening.Complete=true;UpgradeCheck(complete,c,SavedTutorialNarrative.AllSeen,"completed opening");
        var disabled=Save(c);disabled.Opening.Enabled=false;UpgradeCheck(disabled,c,SavedTutorialNarrative.AllSeen,"legacy opening disabled");
        var jobs=Save(c);jobs.IntroductionStep=10;jobs.Opening.FirstReturn=true;jobs.Development.Workbench=jobs.Development.Cooker=true;
        var craft=c.CraftPanel.Recipes.Single(r=>r.Id=="nails");var cooking=c.CookingPanel.Meals.Single(r=>r.Id=="warm");
        jobs.Craft=new[]{new SavedCraft{MemberId="mechanic",RecipeId=craft.Id,Quantity=2,Minutes=7,
            Reserved=craft.Costs.GroupBy(x=>x.MaterialId).Select(g=>new SavedCount{Id=g.Key,Count=g.Sum(x=>x.Count)*2}).ToArray()}};
        jobs.Cooking=new[]{new SavedCooking{MemberId="cook",RecipeId=cooking.Id,OutputId=cooking.OutputId,OutputCount=cooking.Servings,Quantity=1,Minutes=11,
            Reserved=cooking.Costs.GroupBy(x=>x.MaterialId).Select(g=>new SavedCount{Id=g.Key,Count=g.Sum(x=>x.Count)}).ToArray()}};
        jobs.Rest=new[]{new SavedRest{MemberId="guard",Name="짧은 휴식",Minutes=13,Recovery=1}};
        jobs.ReturnReport=new SavedReport{Destination="폐상가",Body="이전 귀환 기록",Minutes=40,
            Members=new[]{new SavedReportMember{Id="scout",Maximum=3,Before=2,After=1}},Items=new[]{new SavedReportItem{Id="cloth",Before=0,After=2}}};
        UpgradeCheck(jobs,c,63,"active jobs, injuries, dead member and old report");
        return "PASS v17 -> v18: seven introduction steps, six contextual milestones, disabled/completed campaigns; no resource/time/order/health/search/NPC/unlock/history changes; repeated loading is idempotent.";
    }
    static CampaignSaveData Current(SettlementController c)
    {
        var s=Save(c);CampaignPersistence.Upgrade(s,c);
        s.TutorialNarrative=new SavedTutorialNarrative{SeenMask=63,PendingBeat=6,LineIndex=1};return s;
    }
    public static string Refusals()
    {
        var c=Catalog();var narrative=c.GetComponent<SettlementTutorialNarrative>();
        Check(narrative&&narrative.Beats!=null&&narrative.Beats.Length==SavedTutorialNarrative.BeatCount&&narrative.Beats.All(b=>b!=null&&b.Lines!=null&&b.Lines.Length>0),"Apply the narrative builder before validating actual dialogue bounds.");
        CampaignPersistence.Validate(Current(c),c);int count=0;
        Action<CampaignSaveData>[] invalid={
            s=>s.TutorialNarrative=null,
            s=>s.TutorialNarrative.SeenMask=-1,
            s=>s.TutorialNarrative.SeenMask=SavedTutorialNarrative.AllSeen+1,
            s=>s.TutorialNarrative.PendingBeat=-2,
            s=>s.TutorialNarrative.PendingBeat=SavedTutorialNarrative.BeatCount,
            s=>s.TutorialNarrative.LineIndex=-1,
            s=>s.TutorialNarrative.LineIndex=8,
            s=>s.TutorialNarrative.PendingBeat=-1,
            s=>s.TutorialNarrative.SeenMask|=1<<s.TutorialNarrative.PendingBeat,
            s=>s.TutorialNarrative.LineIndex=narrative.Beats[s.TutorialNarrative.PendingBeat].Lines.Length
        };
        foreach(var mutate in invalid)
        {
            var s=Current(c);mutate(s);bool refused=false;
            try{CampaignPersistence.Upgrade(s,c);}catch(InvalidOperationException){refused=true;}
            Check(refused,"Invalid dialogue record accepted at case "+count);count++;
        }
        for(int beat=0;beat<SavedTutorialNarrative.BeatCount;beat++)
        {
            var s=Current(c);s.TutorialNarrative=new SavedTutorialNarrative{SeenMask=(1<<beat)-1,PendingBeat=beat,LineIndex=narrative.Beats[beat].Lines.Length-1};
            CampaignPersistence.Validate(s,c);var copy=s.TutorialNarrative.Clone();copy.SeenMask=SavedTutorialNarrative.AllSeen;
            Check(copy.SeenMask!=s.TutorialNarrative.SeenMask,"Clone shares mutable dialogue state.");
        }
        return "PASS ten invalid dialogue records rejected including a line past the actual beat; all eleven pending beats accept their actual last line; export-state clones independent.";
    }
    public static string Disk()
    {
        string previous=CampaignSaveStore.TestDirectory;
        try
        {
            CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/TutorialNarrativeSlots");var c=Catalog();var old=Save(c);
            Check(CampaignSaveStore.Write(0,old,c,out string error),"Legacy write: "+error);
            string raw=File.ReadAllText(CampaignSaveStore.SlotPath(0));var loaded=CampaignSaveStore.Read(0,c);
            Check(loaded.CanLoad&&loaded.Data.Version==18&&loaded.Data.TutorialNarrative.SeenMask==0,"Legacy disk load: "+loaded.Error);
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(0))==raw,"Reading rewrote the legacy save.");
            var pending=Current(c);Check(CampaignSaveStore.Write(1,pending,c,out error),"Current write: "+error);
            loaded=CampaignSaveStore.Read(1,c);
            Check(loaded.CanLoad&&JsonUtility.ToJson(loaded.Data)==JsonUtility.ToJson(pending),"Current mid-dialogue roundtrip: "+loaded.Error);
            string intact=File.ReadAllText(CampaignSaveStore.SlotPath(1));pending.TutorialNarrative.LineIndex=8;
            Check(!CampaignSaveStore.Write(1,pending,c,out error),"Invalid replacement accepted.");
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(1))==intact,"Invalid replacement damaged the good save.");
            return "PASS real isolated v17/v18 disk roundtrip preserves pending beat/line; reading leaves legacy slot unchanged; invalid replacement preserves good slot.";
        }
        finally{CampaignSaveStore.TestDirectory=previous;}
    }
    public static string Run()
    {
        Check(!Application.isPlaying,"Run these DTO fixtures while stopped.");
        return Migration()+"\n"+Refusals()+"\n"+Disk();
    }
}
