using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Disposable UI journey. No production saves are used. Arrival through first-return
// reconstruction uses actual clicks; later narrative conditions are labelled data fixtures.
public static class VerifyTutorialStory
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    const string Output="아트/리소스검토/";
    static string TestDirectory=>Path.GetFullPath("Temp/TutorialStoryVerification");
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static SettlementTutorialNarrative N=>C?C.GetComponent<SettlementTutorialNarrative>():null;
    static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
    static readonly List<string> checks=new List<string>(),shots=new List<string>();
    static void Check(bool v,string m){if(!v)throw new Exception(m);}
    static int Stock(string id)=>C.CraftPanel.Materials.Single(m=>m.Id==id).Initial;
    static int Clock()=>C.Campaign.Day*1440+C.Campaign.MinuteOfDay;
    static async Task WaitFor(Func<bool> condition,string error,int attempts=70)
    {for(int i=0;i<attempts&&!condition();i++)await Task.Delay(100);Check(condition(),error);}
    static string Gameplay()
    {
        var s=CampaignPersistence.Capture(C);s.SavedUtc=null;s.TutorialNarrative=null;s.Activity=null;
        return JsonUtility.ToJson(s);
    }
    static CampaignSaveData CloneSave(CampaignSaveData source)
    {
        var copy=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(source));
        // JsonUtility turns a null inline class into an empty instance. A fixture
        // cloned before the first trip must retain the absence of a return report.
        if(source.ReturnReport==null)copy.ReturnReport=null;
        return copy;
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");checks.Clear();shots.Clear();string failure=null;
        try
        {
            CampaignSaveStore.TestDirectory=TestDirectory;
            await NewGame();await ArrivalAndDeparture();var departure=CloneSave(CampaignPersistence.Capture(C));
            await FirstTripAndReconstruction();await LaterDialogueFixtures();await LegacyRestStageFixtures(departure);
        }
        catch(Exception e){failure=e.ToString();}
        Directory.CreateDirectory(Output);string report=Path.GetFullPath(Output+"tutorial-story-runtime.json");
        File.WriteAllText(report,Newtonsoft.Json.JsonConvert.SerializeObject(new{result=failure==null?"PASS":"FAIL",checks,screenshots=shots,failure,note="Disposable original-two campaign and isolated disk slots. Actual raycast clicks from full-health arrival through staging, first search/return and storage/workbench reconstruction; no mandatory injury or rest. Later tool/survey/completion and legacy rest-stage saves are explicit state fixtures, not played injuries or a second expedition. Reading-dialogue checks compare saved gameplay fields; screenshot fit checks do not replace visual inspection."},Newtonsoft.Json.Formatting.Indented));
        Check(failure==null,failure+"\n"+report);return "PASS: "+report+"\n"+string.Join("\n",checks);
    }
    public static async Task<string> VerifyLegacyRestStages()
    {
        Check(Application.isPlaying&&C&&C.Campaign!=null,"Enter Play Mode in the played test settlement first.");
        CampaignSaveStore.TestDirectory=TestDirectory;checks.Clear();
        // Slot 0 is the isolated, actual discovery save written by this verifier.
        // This entry reruns only explicit legacy fixtures after the real journey passed.
        var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);
        Check(read.Data.IntroductionStep==2&&read.Data.ReturnReport==null,"Expected isolated pre-expedition discovery save.");
        var departure=CloneSave(read.Data);departure.IntroductionStep=5;
        departure.TutorialNarrative=new SavedTutorialNarrative{SeenMask=31,PendingBeat=-1,LineIndex=0};
        await LegacyRestStageFixtures(departure);
        string report=Path.GetFullPath(Output+"tutorial-legacy-rest-stages-runtime.json");
        Directory.CreateDirectory(Output);
        File.WriteAllText(report,Newtonsoft.Json.JsonConvert.SerializeObject(new{result="PASS",checks,note="Legacy fixtures only, based on the isolated discovery save. Full fresh journey is verified separately by Run."},Newtonsoft.Json.Formatting.Indented));
        return "PASS: "+report+"\n"+string.Join("\n",checks);
    }
    static async Task NewGame()
    {
        PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await WaitFor(()=>Object.FindAnyObjectByType<PartySelectionController>(),"Party selection missing.");await Task.Delay(150);
        var p=Object.FindAnyObjectByType<PartySelectionController>();
        Check(p.Cards.Count(c=>c.gameObject.activeInHierarchy)==2,"Only the original pair should be visible.");
        Check(PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"}),"Original pair changed.");
        await Tap(p.Continue);await WaitFor(()=>Object.FindAnyObjectByType<HomeSelectionController>(),"Home selection missing.");
        var home=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(home.Cards[1].Button);await Tap(home.Continue);
        await WaitFor(()=>C&&C.Campaign!=null&&N,"Settlement did not initialize after home selection.");
        Check(N.IsEntering&&N.IsBlocking&&!N.IsOpen,"Fresh-game arrival skipped its transition or opened dialogue before the fade.");
        Check(N.ArrivalFade&&N.ArrivalFade.gameObject.activeInHierarchy&&N.ArrivalFade.alpha>=.9f,"Fresh-game transition did not begin from black.");
        ValidateArrivalBlock();string enteringGameplay=Gameplay();
        C.Introduction.Act();Check(Gameplay()==enteringGameplay,"Hidden main action can bypass the arrival fade.");
        int arrivalWidth=Screen.width,arrivalHeight=Screen.height;
        await Shot("tutorial-opening-black-"+arrivalWidth+"x"+arrivalHeight);
        await WaitFor(()=>N&&N.IsEntering&&N.ArrivalFade.alpha>.15f&&N.ArrivalFade.alpha<.8f,"Arrival transition never visibly faded from black.",25);
        ValidateArrivalBlock();Check(Screen.width==arrivalWidth&&Screen.height==arrivalHeight,"Capture changed resolution during the fade.");
        await Shot("tutorial-opening-fade-"+arrivalWidth+"x"+arrivalHeight);
        await WaitFor(()=>N&&N.IsOpen&&!N.IsEntering,"Arrival dialogue did not open after the fade.");
        Check(Gameplay()==enteringGameplay,"Arrival fade changed gameplay time, health, stock or progression.");
        Check(C.Introduction.Step==0&&N.CurrentBeat==0,"Wrong opening beat/state.");
        Check(C.CraftPanel.Materials.All(m=>m.Initial==0)&&C.Campaign.Ammo==0,"Arrival granted unexplained stock.");
        Check(C.Campaign.Party.All(m=>m.Health==m.MaxHealth),"Fresh arrival must not injure either survivor to manufacture a rest lesson.");
        Check(!N.Beats[0].ShowPartyCondition&&(!N.HealthView||!N.HealthView.activeInHierarchy),"Opening still presents a manufactured injury summary.");
        foreach(var beat in N.Beats.Take(5))foreach(var line in beat.Lines)
            Check(!new[]{"긁혔","긁었","다친 몸","무릎이 쑤","팔이 따가","큰 상처"}.Any(term=>(line.Text??"").Contains(term)),"Opening claims an injury that did not happen: "+line.Text);
        string before=Gameplay();C.Introduction.Act();await Task.Delay(120);
        Check(Gameplay()==before,"Hidden main action can bypass open dialogue.");
        await Sizes(async wh=>{ValidateDialogue();await Shot("tutorial-story-arrival-"+wh.x+"x"+wh.y);});
        checks.Add("Actual original-pair/home selection starts at full health, fades from black with HUD/main/guide blocked, and opens the arrival conversation without invented injuries. Fade and dialogue do not change health/time/stock, and main actions cannot bypass either.");
    }
    static async Task ArrivalAndDeparture()
    {
        await Drain(0);await WaitFor(()=>G.Target==C.Introduction.Action,"No survey target after arrival.");
        await Sizes(async wh=>{ValidateGuide();await Shot("tutorial-story-purpose-"+wh.x+"x"+wh.y);});
        int time=Clock();await Next();await Beat(1);
        Check(C.Introduction.Step==1&&Clock()==time+10,"Survey action time/state mismatch.");
        Check(Stock("cloth")==0&&Stock("water")==0&&C.Campaign.Ammo==0,"Survey silently gave search items.");
        await Drain(1);time=Clock();await Next();await Beat(2);
        Check(C.Introduction.Step==2&&Clock()==time+10,"Search action time/state mismatch.");
        Check(Stock("cloth")==1&&Stock("water")==1&&C.Campaign.Ammo==C.Introduction.FoundAmmo,"Search did not give exactly the documented goods.");
        await Tap(N.Next);Check(N.IsOpen&&N.CurrentBeat==2&&N.CurrentLine==1,"Discovery needs a resumable second line.");
        ValidateDialogue();await Sizes(async wh=>{ValidateDialogue();await Shot("tutorial-story-found-"+wh.x+"x"+wh.y);});
        string gameplay=Gameplay();var saved=CampaignPersistence.Capture(C);
        Check(saved.TutorialNarrative.PendingBeat==2&&saved.TutorialNarrative.LineIndex==1,"Capture lost pending dialogue line.");
        Check(CampaignSaveStore.Write(0,saved,C,out var error),error);var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);
        var priorController=C;CampaignPersistence.Prepare(read.Data,C);SceneManager.LoadScene("Settlement");
        await WaitFor(()=>C&&!ReferenceEquals(C,priorController)&&C.Campaign!=null&&N,"Saved settlement did not initialize.");
        Check(!N.IsEntering&&(!N.ArrivalFade||!N.ArrivalFade.gameObject.activeInHierarchy||N.ArrivalFade.alpha<=.001f),"Loading an in-progress save started a fresh-game fade.");
        await WaitFor(()=>C&&!ReferenceEquals(C,priorController)&&N&&N.IsOpen&&N.CurrentBeat==2,"Pending discovery dialogue did not reopen in the reloaded scene.");
        Check(!N.IsEntering&&(!N.ArrivalFade||!N.ArrivalFade.gameObject.activeInHierarchy||N.ArrivalFade.alpha<=.001f),"Loading an in-progress dialogue replayed the fresh-game fade.");
        Check(N.CurrentLine==1&&Gameplay()==gameplay,"Disk reload lost dialogue position or changed gameplay stock/time.");
        await Drain(2);Check(Stock("cloth")==1&&Stock("water")==1&&C.Campaign.Ammo==C.Introduction.FoundAmmo,"Reading/reloading awarded discovery twice.");
        await WaitFor(()=>G&&G.Target==C.Introduction.Action,"No floor-clearing target after discovery.");
        await Sizes(async wh=>{
            ValidateGuide();
            foreach(var t in new[]{C.NoticeTitle,C.NoticeBody}){
                Check(t&&t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text),"Staging notice lacks its visible explanation.");
                Fits(t);Onscreen(t.rectTransform);
            }
            await Shot("tutorial-concept-staging-"+wh.x+"x"+wh.y);
        });
        time=Clock();await Next();await Beat(4);
        Check(C.Introduction.Step==5&&Clock()==time+20,"Clearing the floor must take 20 minutes and proceed directly to the expedition need.");
        Check(C.Campaign.Party.All(m=>m.Health==m.MaxHealth),"Opening preparation unexpectedly damaged a survivor.");
        Check(C.WorkPanel.Orders.Count==0&&!C.WorkPanel.IsOpen&&!C.TimePanel.IsOpen,"Fresh introduction imposed a rest order or recovery screen.");
        Check(!N.Beats[4].ShowPartyCondition&&(!N.HealthView||!N.HealthView.activeInHierarchy),"Departure dialogue still presents an injury/recovery summary.");
        await Sizes(async wh=>{ValidateDialogue();await Shot("tutorial-opening-expedition-reason-"+wh.x+"x"+wh.y);});
        await Drain(4);Check(!C.InventoryPanel.IsOpen&&G.Target==C.Exit,"Prepared shelter must lead to the actual settlement exit without mandatory rest or inventory detours.");await ConceptShot("expedition-exit");
        checks.Add("Survey/search/floor clearing use real 10/10/20 minute actions. Discovery gives cloth 1, water 1 and configured ammo once. Mid-discovery line 1 survives a disk reload without rewards/time duplication.");
        checks.Add("Fresh introduction proceeds through steps 0, 1 and 2 directly to step 5. Both survivors stay at full health, no rest is assigned, and the departure conversation leads to the real settlement exit.");
    }
    static async Task FirstTripAndReconstruction()
    {
        for(int i=0;i<12&&!C.PackingPanel.IsOpen;i++)await Next();Check(C.PackingPanel.IsOpen&&C.ExpeditionPanel.Selected.Count==2,"Actual two-person packing was not reached.");
        await ConceptShot("packing");
        await Next();await Next();await WaitFor(()=>!C.ArrivalPanel.InTransit,"Travel did not complete.");await Task.Delay(180);
        var a=C.ArrivalPanel;Check(C.Campaign.Stage==JourneyStage.Expedition,"Not in expedition after departure.");
        for(int i=0;i<35&&!a.Loot.IsOpen;i++)
        {
            if(a.Encounter.IsOpen){await Tap(a.Encounter.Wait);await Tap(a.Encounter.Confirm);}
            else await Next();
        }
        Check(a.Loot.IsOpen&&a.Loot.State(0).Complete,"Real first-crate search did not finish.");
        Check(a.Loot.TakeAll&&a.Loot.TakeAll.IsInteractable(),"First crate cannot be collected.");
        await Tap(a.Loot.TakeAll);Check(a.Loot.FieldRows.Count==0,"First crate should fit the original two bags.");
        await Next();await Next();await Next();Check(C.ReturnPanel.IsOpen,"Actual return result missing.");
        for(int i=0;i<8&&C.ReturnPanel.IsOpen;i++)await Next();
        await Beat(5);Check(C.Opening.State.FirstReturn,"Narrative opened without a real first return.");
        await Sizes(async wh=>{ValidateDialogue();await Shot("tutorial-story-first-return-"+wh.x+"x"+wh.y);});await Drain(5);
        var trace=new List<string>();bool capturedWorkbench=false;
        for(int i=0;i<110&&!C.Opening.State.ClueRead;i++)
        {
            if(N.IsOpen){await Drain(N.CurrentBeat);continue;}
            if(!capturedWorkbench&&G.Target==C.Workbench){await ConceptShot("workbench");capturedWorkbench=true;}
            trace.Add(G.Guidance);await Next();
        }
        Check(capturedWorkbench,"Reconstruction did not guide the player to the actual workbench.");
        Check(C.Development.State.Warehouse==1&&C.Development.State.Workbench&&C.Opening.State.ClueRead,"Real reconstruction did not reach the management note: "+string.Join(" > ",trace.TakeLast(12)));
        if(C.Opening.IsOpen)await Tap(C.Opening.Back);
        await Beat(7);await Drain(7);
        checks.Add("Real first expedition, guided crate search, take-all, return/deposit and warehouse/workbench reconstruction lead to dialogue and the management note. No materials or unlock flags were seeded on this journey.");
        checks.Add("Every guided journey click checks visible guide text fit. Four actual guidance points (staging, exit, packing and workbench) are captured at 1920×1080 and 1280×800 with readable onscreen explanations and real target controls.");
    }
    static async Task LegacyRestStageFixtures(CampaignSaveData departure)
    {
        var baseline=CampaignPersistence.Capture(C);
        try
        {
            foreach(int oldStep in new[]{3,4})
            {
                // Explicit legacy fixture: these injuries and existing rest orders are not
                // created on the real fresh-game journey above, nor granted by a dialogue.
                var fixture=CloneSave(departure);
                fixture.IntroductionStep=oldStep;
                foreach(var member in fixture.Members)member.Health=Math.Max(1,member.Maximum-1);
                fixture.Rest=fixture.Members.Select((m,i)=>new SavedRest{MemberId=m.Id,Name="짧은 휴식",Minutes=45+i*15,Recovery=1}).ToArray();
                fixture.TutorialNarrative=new SavedTutorialNarrative{SeenMask=7,PendingBeat=3,LineIndex=1};
                Check(CampaignSaveStore.Write(1,fixture,C,out var error),error);
                var read=CampaignSaveStore.Read(1,C);Check(read.CanLoad,read.Error);
                var expected=CloneSave(read.Data);
                expected.IntroductionStep=5;expected.SavedUtc=null;expected.TutorialNarrative=null;expected.Activity=null;
                string expectedGameplay=JsonUtility.ToJson(expected);
                var priorController=C;CampaignPersistence.Prepare(read.Data,C);SceneManager.LoadScene("Settlement");
                await WaitFor(()=>C&&!ReferenceEquals(C,priorController)&&C.Campaign!=null&&N,"Legacy rest-stage save did not load.");
                Check(!N.IsEntering,"Legacy save unexpectedly replayed the fresh-game fade.");
                await Beat(3);
                Check(C.Introduction.Step==5&&N.CurrentLine==1,"Legacy rest step or pending dialogue line did not resume correctly: "+oldStep);
                Check(Gameplay()==expectedGameplay,"Loading legacy step "+oldStep+" changed saved health, rest orders, inventory or time beyond the step remap.");
                await Tap(N.Next);await Beat(4);
                Check(Gameplay()==expectedGameplay,"Finishing a legacy pending line changed health, orders or time.");
                await Drain(4);C.Opening.Evaluate();
                Check(C.Opening.CurrentAction==3,"All-resting legacy party was sent to an unavailable departure instead of its existing work completion.");
                Check(C.WorkPanel.Orders.Count==fixture.Rest.Length&&C.TimePanel.NextCompletion()==45,"Legacy rest orders were discarded or changed.");
                Check(Gameplay()==expectedGameplay,"Legacy departure guidance silently healed a survivor or completed existing orders.");
            }
            checks.Add("Explicit legacy disk-save fixtures for old steps 3 and 4 remap only the introduction step to 5. Existing injuries, every rest order, remaining minutes and pending beat 3 line 1 survive loading; reading does not heal or advance time. An all-assigned party is directed to its existing work completion instead of an unavailable departure.");
        }
        finally
        {
            var priorController=C;CampaignPersistence.Prepare(baseline,C);SceneManager.LoadScene("Settlement");
            await WaitFor(()=>C&&!ReferenceEquals(C,priorController)&&N&&C.Opening.State.ClueRead,"Could not restore played state after legacy fixtures.");
        }
    }
    static async Task LaterDialogueFixtures()
    {
        var baseline=CampaignPersistence.Capture(C);
        try
        {
            foreach(int beat in new[]{8,9,10})
            {
                // These late conditions are deliberately seeded: they test the observer/dialogue,
                // not second-expedition gameplay or the tool's crafting chain.
                C.Introduction.Restore(10);C.Opening.State.Enabled=true;C.Opening.State.FirstReturn=true;C.Opening.State.ClueRead=true;
                C.Opening.State.SurveyReturned=beat>=9;C.Opening.State.Complete=beat==10;
                C.Development.State.Warehouse=1;C.Development.State.Workbench=true;
                C.CraftPanel.Materials.Single(m=>m.Id=="prybar").Initial=1;
                N.Restore(new SavedTutorialNarrative{SeenMask=(1<<beat)-1,PendingBeat=-1,LineIndex=0});
                await Beat(beat);await Drain(beat);
            }
            Check(!G.Banner.activeSelf&&!G.Target,"Completed tutorial left action guidance onscreen.");
            checks.Add("Explicit data fixtures for completed tool, surveyed-room return and completed opening each show only the matching unseen dialogue. Reading changes no gameplay, grants no unlock/material/time, and dismissed dialogue stays closed.");

            // The player may return empty, lose a tool after opening the door, or finish
            // facilities early. Dialogue must describe that state instead of inventing it.
            FixtureContext(5);C.Development.State.Workbench=false;FixtureStock("wood",0);FixtureStock("scrap",0);
            string emptyReturn=await ProbeLine(5,"{return_line}");
            Check(emptyReturn.Contains("목재")&&emptyReturn.Contains("고철")&&emptyReturn.Contains("없")&&!emptyReturn.Contains("확인했"),"Empty return invents a stock discovery: "+emptyReturn);
            FixtureContext(8);FixtureStock("prybar",0);
            var doorSave=CampaignPersistence.Capture(C);doorSave.StorageUnlocked=true;C.ArrivalPanel.Rooms.RestoreSaved(doorSave);
            string openedDoor=await ProbeLine(8,"{door_line}");
            Check(openedDoor.Contains("문")&&openedDoor.Contains("열")&&!openedDoor.Contains("만들었"),"Opened door without a tool claims a new crafted tool: "+openedDoor);
            string carry=await ProbeLine(8,"{carry_line}");
            Check(!carry.Contains("지렛대"),"Already open door still insists a missing prybar is owned/packed: "+carry);
            var living=new Dictionary<string,string>();
            foreach(bool bed in new[]{false,true})foreach(bool cooker in new[]{false,true})
            {
                FixtureContext(10);C.Development.State.Bed=bed;C.Development.State.Cooker=cooker;
                string line=await ProbeLine(10,"{living_line}");living.Add(bed+"/"+cooker,line);
                if(!bed&&!cooker)Check(line.Contains("쉴 곳")&&line.Contains("먹을 곳")&&line.Contains("손보"),"Both missing living facilities are not reflected by final goal: "+line);
                if(bed&&!cooker)Check(line.Contains("잘 곳은 마련")&&line.Contains("조리할 곳을 손보"),"Final goal does not acknowledge completed bed and missing cooker: "+line);
                if(!bed&&cooker)Check(line.Contains("먹을 곳은 갖")&&line.Contains("침대도 손보"),"Final goal does not acknowledge completed cooker and missing bed: "+line);
                if(bed&&cooker)Check(line.Contains("쉴 곳과 먹을 곳은 갖")&&line.Contains("물자")&&!line.Contains("손보"),"Completed facilities are described as still requiring restoration: "+line);
            }
            Check(living.Values.Distinct().Count()==4,"Final goal did not distinguish all four bed/cooker states.");
            foreach(string id in new[]{"scout","medic"})
            {
                FixtureContext(8);FixtureStock("prybar",1);
                foreach(var p in C.Campaign.Party)p.Health=CampaignPersistence.MemberId(C,p)==id?0:p.MaxHealth;
                var original=C.Campaign.Party.Single(p=>CampaignPersistence.MemberId(C,p)==id);
                int index=Array.FindIndex(N.Beats[8].Lines,l=>l.SpeakerId==id);Check(index>=0,"Expected named line missing: "+id);
                await ProbeLine(8,index,()=>Check(N.Speaker.text==original.Name&&N.Portrait.sprite==C.Roster.Candidates.Single(p=>p.Id==id).Portrait,"Health-zero survivor's line/portrait was reassigned to another character: "+id));
            }
            checks.Add("Bounded state fixtures: empty return does not invent an observed cache; an unlocked door without a prybar does not claim tool ownership; final needs distinguish all four bed/cooker states; both health-zero named survivors retain their own speaker and portrait. Each dialogue is read with all gameplay fields unchanged.");
        }
        finally
        {
            var priorController=C;CampaignPersistence.Prepare(baseline,C);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&!ReferenceEquals(C,priorController)&&N&&C.Opening.State.ClueRead,"Could not restore played state in a new scene after fixture probes.");
        }
    }
    static void FixtureContext(int beat)
    {
        C.Introduction.Restore(10);C.Opening.State.Enabled=true;C.Opening.State.FirstReturn=true;C.Opening.State.ClueRead=beat>=7;
        C.Opening.State.SurveyReturned=beat>=9;C.Opening.State.Complete=beat==10;
        C.Development.State.Warehouse=1;C.Development.State.Workbench=beat>=6;
    }
    static void FixtureStock(string id,int count)
    {
        var data=CampaignPersistence.Capture(C);foreach(var member in data.Members)foreach(var item in member.Bag.Where(x=>x.Id==id))item.Count=0;
        C.InventoryPanel.RestoreSaved(data);C.CraftPanel.Materials.Single(m=>m.Id==id).Initial=count;
    }
    static Task<string> ProbeLine(int beat,string token)
    {
        int line=Array.FindIndex(N.Beats[beat].Lines,l=>(l.Text??"").Contains(token));Check(line>=0,"Dynamic story token missing from prefab: "+token);
        return ProbeLine(beat,line,null);
    }
    static async Task<string> ProbeLine(int beat,int line,Action inspect)
    {
        N.Restore(new SavedTutorialNarrative{SeenMask=(1<<beat)-1,PendingBeat=-1,LineIndex=0});await Beat(beat);
        string before=Gameplay();for(int i=0;i<line;i++)await Tap(N.Next);
        Check(N.IsOpen&&N.CurrentBeat==beat&&N.CurrentLine==line,"Did not reach requested conditional dialogue line.");
        string text=N.Body.text;Check(!text.Contains("{")&&!text.Contains("}"),"Unresolved narrative placeholder: "+text);inspect?.Invoke();
        await Drain(beat);Check(Gameplay()==before,"Conditional line changed gameplay: "+beat+"/"+line);return text;
    }
    static async Task Beat(int expected)
    {await WaitFor(()=>N&&N.IsOpen&&!N.IsEntering,"Expected narrative beat "+expected+" did not open.");Check(N.CurrentBeat==expected,"Expected beat "+expected+", got "+N.CurrentBeat);ValidateDialogue();}
    static async Task Drain(int beat)
    {
        await Beat(beat);string before=Gameplay();int clicks=0;
        while(N.IsOpen&&N.CurrentBeat==beat&&clicks++<12){ValidateDialogue();await Tap(N.Next);}
        Check(!N.IsOpen,"Dialogue did not finish: "+beat);await Task.Delay(180);
        Check(!N.IsOpen,"Dismissed dialogue reopened without a state change: "+beat);
        Check((N.Export().SeenMask&(1<<beat))!=0&&N.Export().PendingBeat==-1,"Dismissed beat not marked read: "+beat);
        Check(Gameplay()==before,"Reading dialogue changed gameplay state: "+beat);
    }
    static void ValidateDialogue()
    {
        Check(N&&N.IsOpen&&N.IsBlocking&&!N.IsEntering&&N.View.activeInHierarchy,"Narrative view is not visible or is still covered by the opening fade.");
        Check(!N.ArrivalFade||!N.ArrivalFade.gameObject.activeInHierarchy||N.ArrivalFade.alpha<=.001f,"Opening fade still obscures the dialogue.");
        Check(!G.Banner.activeSelf&&!G.Target&&(!G.Marker||!G.Marker.gameObject.activeSelf)&&(!G.Pointer||!G.Pointer.gameObject.activeSelf)&&(!G.Spotlight||!G.Spotlight.gameObject.activeSelf),"Action guide competes with narrative.");
        Check(!C.Main.interactable&&!C.Main.blocksRaycasts,"Main input remains available under narrative.");
        Check(N.Next&&N.Next.IsActive()&&N.Next.IsInteractable(),"Dialogue has no usable continuation button.");
        foreach(var t in new[]{N.SceneTitle,N.Speaker,N.Body,N.NextLabel}){Check(t&&!string.IsNullOrWhiteSpace(t.text),"Empty narrative text.");Fits(t);}
        if(N.Portrait&&N.Portrait.isActiveAndEnabled)Check(N.Portrait.sprite&&N.Portrait.preserveAspect,"Narrative portrait is missing/stretched.");
        if(N.Facts&&N.Facts.activeInHierarchy)foreach(var t in N.FactLabels.Where(t=>t&&t.gameObject.activeInHierarchy))Fits(t);
        if(N.HealthView&&N.HealthView.activeInHierarchy)ValidatePartyHealth();
        foreach(var r in new[]{N.Body.rectTransform,N.Speaker.rectTransform,N.NextLabel.rectTransform,(RectTransform)N.Next.transform})Onscreen(r);
    }
    static void ValidateArrivalBlock()
    {
        Check(N&&N.IsEntering&&N.IsBlocking&&!N.IsOpen,"Arrival is not exclusively blocking before dialogue.");
        Check(!C.Main.interactable&&!C.Main.blocksRaycasts&&C.Main.alpha<=.001f,"Main input or HUD remains active beneath the opening fade.");
        Check(N.ArrivalFade&&N.ArrivalFade.blocksRaycasts,"Arrival fade does not block background pointer input.");
        Check(!G.Banner.activeSelf&&!G.Target&&(!G.Marker||!G.Marker.gameObject.activeSelf)&&(!G.Pointer||!G.Pointer.gameObject.activeSelf)&&(!G.Spotlight||!G.Spotlight.gameObject.activeSelf),"Action guide appears before the opening story.");
    }
    static void ValidatePartyHealth()
    {
        var people=C.Campaign.Party.ToArray();
        Check(N.HealthLabels!=null&&N.HealthBars!=null&&N.HealthLabels.Length>=people.Length&&N.HealthBars.Length>=people.Length,"Narrative health summary lacks a row for each starting survivor.");
        Onscreen((RectTransform)N.HealthView.transform);
        for(int i=0;i<people.Length;i++)
        {
            var person=people[i];var label=N.HealthLabels[i];var bar=N.HealthBars[i];
            Check(label&&label.gameObject.activeInHierarchy&&bar&&bar.gameObject.activeInHierarchy,"Narrative health summary row is hidden.");
            string compact=label.text.Replace(" ","").Replace("\n","");
            Check(compact.Contains(person.Name)&&compact.Contains(person.Health+"/"+person.MaxHealth),"Narrative health label does not match its actual survivor: "+label.text);
            Fits(label);Onscreen(label.rectTransform);Onscreen(bar.rectTransform);
            var slots=bar.GetComponentInChildren<SegmentedHealthGraphic>();
            Check(slots&&slots.isActiveAndEnabled&&slots.Current==person.Health&&slots.Maximum==person.MaxHealth,"Narrative segmented health differs from gameplay health: "+person.Name);
        }
    }
    static void ValidateGuide()
    {
        Check(G&&G.Target&&!string.IsNullOrWhiteSpace(G.Guidance),"Missing post-story purpose/target.");
        if(!G.Banner.activeSelf){
            foreach(var t in new[]{C.NoticeTitle,C.NoticeBody}){Check(t&&t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text),"Visible notice lacks a purpose.");Fits(t);Onscreen(t.rectTransform);}
            return;
        }
        foreach(var t in new[]{G.Title,G.Instruction}){Check(t&&!string.IsNullOrWhiteSpace(t.text),"Visible guide lacks a name or explanation.");Fits(t);Onscreen(t.rectTransform);}
        Onscreen((RectTransform)G.Banner.transform);
    }
    static async Task ConceptShot(string name)
    {
        await WaitFor(()=>G&&G.Target&&G.Banner.activeSelf,"Concept explanation is missing: "+name);
        await Sizes(async wh=>{ValidateGuide();await Shot("tutorial-concept-"+name+"-"+wh.x+"x"+wh.y);});
    }
    static async Task Next(){await WaitFor(()=>G&&G.Target,"No actual next target: "+G?.Guidance);Check(!N.IsBlocking,"A guide action was requested before arrival/dialogue finished.");ValidateGuide();await Tap(G.Target);}
    static void Fits(Text t){Canvas.ForceUpdateCanvases();if(!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+2,"Text clips: "+t.name+" / "+t.text+" / "+t.preferredHeight+" > "+t.rectTransform.rect.height);}
    static void Onscreen(RectTransform r)
    {
        var canvas=r.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var corners=new Vector3[4];r.GetWorldCorners(corners);
        foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(cam,corner);Check(p.x>=-1&&p.x<=Screen.width+1&&p.y>=-1&&p.y<=Screen.height+1,"Dialogue element leaves screen: "+r.name);}
    }
    static async Task Tap(Button b)
    {
        await Task.Delay(75);Canvas.ForceUpdateCanvases();Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable button "+(b?b.name:"destroyed"));
        var canvas=b.GetComponentInParent<Canvas>();var rect=(RectTransform)b.transform;var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        if(b.GetComponent<SettlementFacilityFocus>())for(int y=1;y<10&&(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b);y++)for(int x=1;x<10;x++)
        {e.position=RectTransformUtility.WorldToScreenPoint(cam,rect.TransformPoint(new Vector2(rect.rect.xMin+rect.rect.width*x/10,rect.rect.yMin+rect.rect.height*y/10)));hits.Clear();EventSystem.current.RaycastAll(e,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b)break;}
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Hit blocked: "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));
        ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(140);
    }
    static async Task Sizes(Func<Vector2Int,Task> action)
    {
        var asm=typeof(Editor).Assembly;var type=asm.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=type.GetMethod("GetGroup",F).Invoke(sizes,new[]{type.GetProperty("currentGroupType",F).GetValue(sizes)});
        var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",F);int original=(int)selected.GetValue(view);
        var sizeType=asm.GetType("UnityEditor.GameViewSize");var added=new List<int>();
        try
        {
            foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)})
            {
                int count=(int)group.GetType().GetMethod("GetTotalCount",F).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",F).Invoke(group,new object[]{i});if((int)sizeType.GetProperty("width",F).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",F).GetValue(size)==wh.y){index=i;break;}}
                if(index<0){var enumType=asm.GetType("UnityEditor.GameViewSizeType");var constructor=sizeType.GetConstructor(F,null,new[]{enumType,typeof(int),typeof(int),typeof(string)},null);var size=constructor.Invoke(new[]{Enum.Parse(enumType,"FixedResolution"),(object)wh.x,wh.y,"Tutorial story review"});group.GetType().GetMethod("AddCustomSize",F).Invoke(group,new[]{size});index=count;added.Add(index-(int)group.GetType().GetMethod("GetBuiltinCount",F).Invoke(group,null));}
                selected.SetValue(view,index);view.Repaint();await Task.Delay(650);Check(Screen.width==wh.x&&Screen.height==wh.y,"Wrong capture resolution.");await action(wh);
            }
        }
        finally{selected.SetValue(view,original);foreach(var index in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize",F).Invoke(group,new object[]{index});view.Repaint();await Task.Delay(150);}
    }
    static async Task Shot(string name)
    {
        Directory.CreateDirectory(Output);string temp=Path.GetFullPath("Temp/"+name+"-native.png");if(File.Exists(temp))File.Delete(temp);
        ScreenCapture.CaptureScreenshot(temp);for(int i=0;i<40&&!File.Exists(temp);i++)await Task.Delay(100);Check(File.Exists(temp),"Missing screenshot.");await Task.Delay(150);
        string destination=Path.GetFullPath(Output+name+".png");File.Copy(temp,destination,true);shots.Add(destination);
    }
    public static string Finish(){Check(!Application.isPlaying,"Stop Play Mode first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;PartySelectionSession.Clear();return "Tutorial story verification override cleared.";}
}
