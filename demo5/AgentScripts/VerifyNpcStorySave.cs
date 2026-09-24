using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// v15 story persistence only: disposable data, an inactive screen clone, and isolated Temp slots.
public static class VerifyNpcStorySave
{
    const string Invalid="탐험 인물 기록이 올바르지 않습니다.";
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static T Copy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    static bool Same(object a,object b)=>JsonUtility.ToJson(a)==JsonUtility.ToJson(b);
    static void Owner(object target,SettlementController owner)=>target.GetType().GetField("owner",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,owner);
    static SettlementController Catalog()=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
    static CampaignSaveData Save(SettlementController catalog,int version=15)
    {
        return new CampaignSaveData{
            Version=version,SavedUtc="2026-09-25T00:00:00Z",HomeId="garage",Day=1,Minute=600,Ammo=0,IntroductionStep=10,
            Development=new SavedDevelopment{Bed=true,Workbench=true,Cooker=true,Warehouse=1},Opening=new SavedOpeningChapter(),
            Members=catalog.Roster.Candidates.Take(2).Select(p=>new SavedMember{Id=p.Id,Name=p.DisplayName,Role=p.RoleTitle,Description=p.Description,Health=p.Health,Maximum=p.Health,Aim=p.Aim,Capacity=p.BagCapacity,Bag=Array.Empty<SavedCount>()}).ToArray(),
            Materials=catalog.CraftPanel.Materials.Select(m=>new SavedCount{Id=m.Id,Count=0}).ToArray(),
            Rest=Array.Empty<SavedRest>(),Craft=Array.Empty<SavedCraft>(),Cooking=Array.Empty<SavedCooking>(),Visitor=new SavedVisitor(),
            CorridorVisited=true,Inspected=new[]{"mall.arcade.crate"},
            Searches=new[]{new SavedSearch{Id="mall.arcade.crate",Progress=1,Required=1,Pace=0,Complete=true,Opened=true,Loot=Array.Empty<SavedCount>()}},
            SiteBoards=new[]{new SavedSiteBoard{Id="mall",Danger=2,Gauge=3,Remembered="mall.corridor",IntroShown=true,ResidentLessonShown=true}},
            Activity=new[]{"기존 수색 기록"},Doyun=new SavedNpcStory()
        };
    }
    static SavedNpcStory[] Stages()=>new[]{
        new SavedNpcStory(),
        new SavedNpcStory{VisitCount=4},
        new SavedNpcStory{Stage=1,Page=0,VisitCount=2,MetVisit=2,PendingDialogue=true},
        new SavedNpcStory{Stage=1,Page=0,VisitCount=2,MetVisit=2},
        new SavedNpcStory{Stage=1,Page=1,VisitCount=2,MetVisit=2},
        new SavedNpcStory{Stage=2,Page=2,VisitCount=2,MetVisit=2},
        new SavedNpcStory{Stage=3,Choice=1,Page=3,VisitCount=2,MetVisit=2},
        new SavedNpcStory{Stage=3,Choice=2,Page=3,VisitCount=2,MetVisit=2,Returned=true},
        new SavedNpcStory{Stage=3,Choice=2,Page=3,VisitCount=3,MetVisit=2,Returned=true,Reunited=true}
    };
    static string WithoutStory(CampaignSaveData save)
    {
        string json=Regex.Replace(JsonUtility.ToJson(save),@",?""Doyun"":\{[^{}]*\}","");
        Check(!json.Contains("\"Doyun\""),"Could not build a real legacy payload without the story field.");return json;
    }

    public static string Migration()
    {
        Check(CampaignPersistence.CurrentVersion==15,"Expected save v15.");var catalog=Catalog();
        foreach(int version in new[]{13,14})
        {
            var original=Save(catalog,version);var expected=Copy(original);expected.Version=15;expected.Doyun=new SavedNpcStory();
            var raw=JsonUtility.FromJson<CampaignSaveData>(WithoutStory(original));CampaignPersistence.Upgrade(raw,catalog);
            Check(Same(raw,expected),"v"+version+" migration altered existing inventory/search/board/lesson/opening/activity state.");
            Check(Same(raw.Doyun,new SavedNpcStory()),"An ordinary old visit inferred an NPC meeting or name.");
        }
        // Old versions must discard the unknown future DTO, even if a foreign tool injected one.
        var injected=Save(catalog,14);injected.Doyun=Stages().Last();CampaignPersistence.Upgrade(injected,catalog);
        Check(Same(injected.Doyun,new SavedNpcStory()),"v14 migration accepted an NPC history that v14 never stored.");
        foreach(var story in Stages())
        {
            var save=Save(catalog);save.Doyun=story;CampaignPersistence.Validate(save,catalog);
            var again=Copy(save);CampaignPersistence.Upgrade(again,catalog);Check(Same(again,save),"Current story JSON roundtrip changed a stage.");
            Check(again.Activity.SequenceEqual(new[]{"기존 수색 기록"}),"Migration added story spoilers to activity.");
        }
        return "PASS v13/v14 raw migration to v15; site board/resident lesson/search/inventory preserved; no inferred NPC knowledge; "+Stages().Length+" valid story JSON states preserved.";
    }

    public static string Refusals()
    {
        var catalog=Catalog();var cases=new (string name,Action<CampaignSaveData> corrupt)[]{
            ("missing DTO",s=>s.Doyun=null),("stage -1",s=>s.Doyun.Stage=-1),("stage 4",s=>s.Doyun.Stage=4),
            ("choice -1",s=>s.Doyun.Choice=-1),("choice 3",s=>s.Doyun.Choice=3),("choice before completion",s=>s.Doyun.Choice=1),
            ("completion without choice",s=>{s.Doyun.Stage=3;s.Doyun.Page=3;s.Doyun.VisitCount=s.Doyun.MetVisit=2;}),
            ("page -1",s=>s.Doyun.Page=-1),("page 4",s=>s.Doyun.Page=4),
            ("unobserved page 1",s=>s.Doyun.Page=1),
            ("observed page 2",s=>{s.Doyun.Stage=1;s.Doyun.Page=2;s.Doyun.VisitCount=s.Doyun.MetVisit=2;}),
            ("named page 1",s=>{s.Doyun.Stage=2;s.Doyun.Page=1;s.Doyun.VisitCount=s.Doyun.MetVisit=2;}),
            ("completed page 2",s=>{s.Doyun.Stage=3;s.Doyun.Choice=1;s.Doyun.Page=2;s.Doyun.VisitCount=s.Doyun.MetVisit=2;}),
            ("unobserved pending dialogue",s=>s.Doyun.PendingDialogue=true),
            ("pending second page",s=>{s.Doyun.Stage=1;s.Doyun.Page=1;s.Doyun.VisitCount=s.Doyun.MetVisit=2;s.Doyun.PendingDialogue=true;}),
            ("pending after name",s=>{s.Doyun.Stage=2;s.Doyun.Page=2;s.Doyun.VisitCount=s.Doyun.MetVisit=2;s.Doyun.PendingDialogue=true;}),
            ("pending after completion",s=>{s.Doyun.Stage=3;s.Doyun.Choice=1;s.Doyun.Page=3;s.Doyun.VisitCount=s.Doyun.MetVisit=2;s.Doyun.PendingDialogue=true;}),
            ("negative visit",s=>s.Doyun.VisitCount=-1),("unbounded visit",s=>s.Doyun.VisitCount=1000001),("negative met visit",s=>s.Doyun.MetVisit=-1),
            ("met after current visit",s=>{s.Doyun.Stage=1;s.Doyun.VisitCount=2;s.Doyun.MetVisit=3;}),
            ("met on first visit",s=>{s.Doyun.Stage=1;s.Doyun.VisitCount=s.Doyun.MetVisit=1;}),
            ("observed without meeting",s=>{s.Doyun.Stage=1;s.Doyun.VisitCount=2;}),
            ("meeting before observation",s=>{s.Doyun.VisitCount=s.Doyun.MetVisit=2;}),
            ("returned before completion",s=>{s.Doyun.Stage=1;s.Doyun.VisitCount=s.Doyun.MetVisit=2;s.Doyun.Returned=true;}),
            ("reunited before return",s=>{s.Doyun=Stages().First(story=>story.Stage==3);s.Doyun.Reunited=true;})
        };
        foreach(var item in cases)
        {
            var save=Save(catalog);item.corrupt(save);string error=null;
            try{CampaignPersistence.Validate(save,catalog);}catch(InvalidOperationException e){error=e.Message;}
            Check(error==Invalid,item.name+" accepted or failed for another reason: "+error);
        }
        return "PASS "+cases.Length+" invalid story range/choice/visit/return/reunion records refused with a spoiler-free error.";
    }

    [Serializable] sealed class Envelope{public string Magic,Payload,Hash;}
    static void RawSlot(string payload)
    {
        string hash;using(var sha=SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        File.WriteAllText(CampaignSaveStore.SlotPath(0),JsonUtility.ToJson(new Envelope{Magic="NEIGHBOR-NOT-SAVE",Payload=payload,Hash=hash}),new UTF8Encoding(false));
    }
    public static string Disk()
    {
        var catalog=Catalog();string previous=CampaignSaveStore.TestDirectory;
        string path=Path.GetFullPath(Path.Combine("Temp","NpcStorySaveVerification",Guid.NewGuid().ToString("N")));
        try
        {
            CampaignSaveStore.TestDirectory=path;Directory.CreateDirectory(path);RawSlot(WithoutStory(Save(catalog,14)));
            var legacy=CampaignSaveStore.Read(0,catalog);Check(legacy.CanLoad&&legacy.Data.Version==15&&Same(legacy.Data.Doyun,new SavedNpcStory()),"Real v14 slot migration: "+legacy.Error);
            foreach(var story in Stages())
            {
                var save=Save(catalog);save.Doyun=story;Check(CampaignSaveStore.Write(0,save,catalog,out var error),error);
                var read=CampaignSaveStore.Read(0,catalog);Check(read.CanLoad&&Same(read.Data,save),"Story file roundtrip: "+read.Error);
            }
            string intact=File.ReadAllText(CampaignSaveStore.SlotPath(0));var bad=Save(catalog);bad.Doyun.Stage=4;
            Check(!CampaignSaveStore.Write(0,bad,catalog,out var rejected)&&rejected==Invalid,"Invalid story overwrote a good slot.");
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(0))==intact,"Rejected story changed the existing save.");
            Check(CampaignSaveStore.Latest(catalog)?.CanLoad==true,"Continue cannot discover a valid story slot.");
            return "PASS raw v14 file, "+Stages().Length+" v15 story file roundtrips, invalid-write protection, Continue discovery; only isolated Temp slots: "+path;
        }
        finally{CampaignSaveStore.TestDirectory=previous;}
    }

    public static string Integration()
    {
        var ids=PartySelectionSession.Selected.ToArray();var pendingParty=PartySelectionSession.Pending;var pendingSave=CampaignPersistence.Pending;
        var root=new GameObject("NPC save integration fixture");root.SetActive(false);
        try
        {
            var catalog=Catalog();var save=Save(catalog);var clone=Object.Instantiate(catalog.gameObject,root.transform);var c=clone.GetComponent<SettlementController>();
            typeof(SettlementController).GetProperty("Campaign").SetValue(c,CampaignState.RestoreSettled(save));
            PartySelectionSession.Selected.Clear();PartySelectionSession.Selected.AddRange(save.Members.Select(p=>p.Id));
            Owner(c.CraftPanel,c);Owner(c.WorkPanel,c);Owner(c.CookingPanel,c);Owner(c.InventoryPanel,c);Owner(c.ReturnPanel,c);Owner(c.VisitorPanel,c);Owner(c.ArrivalPanel,c);
            c.Opening.State=new SavedOpeningChapter();c.VisitorPanel.View.SetActive(false);c.VisitorPanel.FirstDay=100000;
            c.CraftPanel.RestoreSaved(save);c.WorkPanel.RestoreSaved(save);c.CookingPanel.RestoreSaved(save);c.InventoryPanel.RestoreSaved(save);c.ReturnPanel.RestoreSaved(save);c.VisitorPanel.Restore(save.Visitor);
            c.ArrivalPanel.Rooms.RestoreSaved(save);c.ArrivalPanel.Loot.RestoreSaved(save);c.ArrivalPanel.Threat.RestoreSaved(save.SiteBoards[0]);
            var old=c.ArrivalPanel.GetComponent<ExpeditionNpcStory>();if(old)Object.DestroyImmediate(old);
            Check(Same(CampaignPersistence.Capture(c).Doyun,new SavedNpcStory()),"Optional missing story component did not produce a fresh DTO.");
            CampaignPersistence.Prepare(save,c);CampaignPersistence.ApplyPending(c);
            Check(!c.ArrivalPanel.GetComponent<ExpeditionNpcStory>()&&CampaignPersistence.Pending==null,"Optional absent component failed restore or was implicitly spawned.");
            var story=c.ArrivalPanel.gameObject.AddComponent<ExpeditionNpcStory>();
            foreach(var sample in Stages())
            {
                var input=Copy(sample);story.Restore(input);var exported=story.Export();Check(Same(exported,sample),"Component restore/export changed a story stage.");
                Check(sample.Stage>=2?story.DisplayName=="장도윤":story.DisplayName=="?"&&!story.RecordTitle.Contains("장도윤"),"A restored stage exposed a name before it was learned.");
                input.VisitCount+=9;exported.Page=99;Check(Same(story.Export(),sample),"Component shares the caller's saved DTO.");
                var captured=CampaignPersistence.Capture(c);Check(Same(captured.Doyun,sample),"Campaign Capture omitted story progress.");
                story.Restore(new SavedNpcStory());Check(Same(captured.Doyun,sample),"Capture snapshot changed with later runtime state.");
                CampaignPersistence.Prepare(captured,c);CampaignPersistence.ApplyPending(c);
                Check(Same(story.Export(),sample)&&CampaignPersistence.Pending==null,"ApplyPending did not restore the exact story state.");
                Check(c.ActivityLog.SequenceEqual(captured.Activity),"Story restore injected an activity entry.");
                Check(c.Campaign.Party.Count()==2&&c.InventoryPanel.Items.All(i=>c.Campaign.Party.All(p=>c.InventoryPanel.CountFor(p,i.Id)==0)),"Story restore recruited someone or granted items.");
            }
            return "PASS optional component fallback, component snapshot isolation, Campaign Capture/ApplyPending for all stages, no disappearance of recorded progress, no new activity/recruits/items; inactive clone only.";
        }
        finally
        {
            Object.DestroyImmediate(root);PartySelectionSession.Selected.Clear();PartySelectionSession.Selected.AddRange(ids);PartySelectionSession.Pending=pendingParty;
            typeof(CampaignPersistence).GetProperty("Pending").SetValue(null,pendingSave);
        }
    }

    public static string Run()=>Migration()+"\n"+Refusals()+"\n"+Disk()+"\n"+Integration();
}
