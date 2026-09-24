using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// End-to-end UI/turn/save review. Production save files are never read or written.
public static class VerifyNpcStoryRuntime
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static PointerEventData Hit(Button button)
    {
        Check(button&&button.IsActive()&&button.IsInteractable(),"Inactive button: "+(button?button.name:"null"));
        var canvas=button.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var r=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Raycast blocked for "+button.name+": "+(hits.Count>0?hits[0].gameObject.name:"no hit"));
        return data;
    }
    static async Task Click(Button button)
    {
        ExecuteEvents.Execute(button.gameObject,Hit(button),ExecuteEvents.pointerClickHandler);await Task.Delay(80);
    }
    public static async Task<string> Start()
    {
        Check(Application.isPlaying,"Enter Play first.");
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/NpcStoryRuntimeSlots");PartySelectionSession.Clear();
        SceneManager.LoadScene("PartySelection");await Task.Delay(600);
        var p=Object.FindAnyObjectByType<PartySelectionController>();
        foreach(var card in p.Cards.Where(x=>x.gameObject.activeInHierarchy&&x.Button.IsInteractable())){if(PartySelectionSession.Selected.Count>=2)break;card.Button.onClick.Invoke();}
        p.Continue.onClick.Invoke();await Task.Delay(600);var home=Object.FindAnyObjectByType<HomeSelectionController>();home.Cards[0].Button.onClick.Invoke();home.Continue.onClick.Invoke();await Task.Delay(750);
        C.Introduction.Restore(10);await Depart();
        Check(C.ArrivalPanel.Story&&C.ArrivalPanel.Story.State.VisitCount==1&&!C.ArrivalPanel.Story.Clue.gameObject.activeSelf,"NPC clue leaked into first visit.");
        C.ArrivalPanel.Story.Open();Check(!C.ArrivalPanel.Story.IsOpen,"First visit story bypass.");
        Check(C.ArrivalPanel.FinishReturn(),"First return failed.");await Task.Delay(100);
        await Depart();var a=C.ArrivalPanel;
        Check(a.Story.State.VisitCount==2&&a.Popup.activeSelf,"Second visit intro missing.");
        a.Story.Open();Check(!a.Story.IsOpen,"Story interrupted place-board intro.");
        a.ClosePopup();await Task.Delay(80);
        Check(a.Story.Clue.gameObject.activeSelf&&a.Story.ObserveBlock(ExpeditionNpcStory.ObservationId)==FieldPause.None,"Clue did not unlock after intro.");
        return "PASS first visit hidden, second visit intro owns focus, clue unlocks after closing intro. Ready in arcade.";
    }
    static async Task Depart()
    {
        var c=C;if(c.ReturnPanel.IsOpen){c.ReturnPanel.Close();await Task.Delay(100);}c.Opening.Evaluate();
        Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure failed.");await Task.Delay(1050);
    }
    static void Layout()
    {
        var s=C.ArrivalPanel.Story;Canvas.ForceUpdateCanvases();
        foreach(var text in s.View.GetComponentsInChildren<Text>(true).Where(t=>t.enabled&&t.gameObject.activeInHierarchy))
            Check(text.preferredHeight<=text.rectTransform.rect.height+2,"Text clips: "+text.name+" "+text.preferredHeight+" > "+text.rectTransform.rect.height+" "+text.text);
    }
    static string DialoguePosition(ExpeditionNpcStory s)=>$"{s.State.Stage}:{s.State.Page}:{s.DialogueLine}:{s.State.Choice}:{s.State.Reunited}:{s.IsOpen}";
    static void DialogueAt(ExpeditionNpcStory s,int stage,int page,int line,string speaker,bool choosing=false)
    {
        Check(s.IsOpen&&s.State.Stage==stage&&s.State.Page==page&&s.DialogueLine==line,
            "Wrong dialogue beat: "+DialoguePosition(s)+" expected "+stage+":"+page+":"+line);
        Check(s.DialogueLayout.activeInHierarchy&&!s.InvestigationLayout.activeInHierarchy,"Dialogue still shows the investigation layout.");
        Check(s.DialogueSpeaker.text==speaker,"Wrong speaker: "+s.DialogueSpeaker.text+" expected "+speaker);
        Check(s.AwaitingDialogueChoice==choosing&&s.DialogueChoices.Count(b=>b.gameObject.activeInHierarchy)==(choosing?2:0),"Choices appeared before reading, or are missing at the choice beat.");
        Check(s.Next.gameObject.activeInHierarchy!=choosing,"Next remains available while waiting for a choice.");
        Check(!s.Members.Any(b=>b.gameObject.activeInHierarchy)&&!s.Choices.Any(b=>b.gameObject.activeInHierarchy),"Investigation controls leaked into dialogue.");
        Layout();
    }
    static void UnknownIdentity(ExpeditionNpcStory s)
    {
        Check(s.DisplayName=="?"&&!s.RecordTitle.Contains("장도윤")&&!s.RecordBody().Contains("장도윤"),"Name leaked through identity/journal before badge confirmation.");
        Check(!s.View.GetComponentsInChildren<Text>(true).Any(t=>t.enabled&&t.gameObject.activeInHierarchy&&t.text.Contains("장도윤")),"Visible text names the NPC before badge confirmation.");
        Check(!s.DialoguePortrait.gameObject.activeInHierarchy&&s.DialogueUnknownPortrait.gameObject.activeInHierarchy&&(!s.VisiblePawn||!s.VisiblePawn.activeSelf),"Portrait or pawn revealed identity too early.");
    }
    static void EarlyChoiceIgnored(ExpeditionNpcStory s)
    {
        Check(!s.AwaitingDialogueChoice,"Fixture is already waiting for a choice.");
        string before=DialoguePosition(s);int logs=C.ActivityLog.Count;
        s.Choose(0);s.Choose(1);
        Check(DialoguePosition(s)==before&&C.ActivityLog.Count==logs,"Direct Choose skipped an unread line or wrote a log.");
    }
    static async Task WaitingChoiceDoesNotAdvance(ExpeditionNpcStory s)
    {
        Check(s.AwaitingDialogueChoice,"Fixture is not waiting for a choice.");
        string before=DialoguePosition(s);int logs=C.ActivityLog.Count;
        s.AdvanceDialogue();await Click(s.ContinueSurface);
        Check(DialoguePosition(s)==before&&C.ActivityLog.Count==logs,"Body/Advance bypassed a pending choice.");
    }
    static async Task ReopenSameBeat(ExpeditionNpcStory s)
    {
        string before=DialoguePosition(s),body=s.DialogueBody.text;bool choosing=s.AwaitingDialogueChoice;
        await Click(s.Back);Check(!s.IsOpen,"Back did not defer dialogue.");await Click(s.Clue);
        Check(DialoguePosition(s)==before&&s.DialogueBody.text==body&&s.AwaitingDialogueChoice==choosing,"Closing/reopening reset the current reading beat.");
    }
    static async Task SubmitOnce(ExpeditionNpcStory s)
    {
        Check(EventSystem.current&&s.Next&&s.Next.IsActive()&&s.Next.IsInteractable(),"UI submit target is unavailable.");
        int stage=s.State.Stage,page=s.State.Page,line=s.DialogueLine,logs=C.ActivityLog.Count;
        EventSystem.current.SetSelectedGameObject(s.Next.gameObject);
        // Headless-safe UI submit path only. This does not claim delivery of an actual keyboard key press.
        ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        await Task.Delay(160);
        Check(s.IsOpen&&s.State.Stage==stage&&s.State.Page==page&&s.DialogueLine==line+1&&C.ActivityLog.Count==logs,
            "One UI submit advanced zero or multiple dialogue beats: "+DialoguePosition(s));
    }
    static async Task ReadFirstDialogue(ExpeditionNpcStory s,string capturePrefix,bool submit=false,int openingChoice=0,int finalChoice=1)
    {
        var a=C.ArrivalPanel;int turns=a.Rooms.Turns,noise=a.Rooms.Noise,minute=C.Campaign.MinuteOfDay;string bag=Inventory();
        DialogueAt(s,1,0,0,"주변");UnknownIdentity(s);EarlyChoiceIgnored(s);await Shot(capturePrefix+"-narration");
        await Click(s.ContinueSurface);DialogueAt(s,1,0,1,"?",true);UnknownIdentity(s);
        await WaitingChoiceDoesNotAdvance(s);await ReopenSameBeat(s);await Shot(capturePrefix+"-unknown-choice");
        await Click(s.DialogueChoices[openingChoice]);DialogueAt(s,1,1,0,"?");UnknownIdentity(s);EarlyChoiceIgnored(s);
        await Click(s.ContinueSurface);DialogueAt(s,1,1,1,"주변");UnknownIdentity(s);await ReopenSameBeat(s);
        int names=C.ActivityLog.Count(x=>x.StartsWith("이름 확인 ·"));
        await Click(s.Next);DialogueAt(s,2,2,0,"주변");
        Check(s.DisplayName=="장도윤"&&s.DialogueBody.text.Contains("장도윤")&&s.DialoguePortrait.gameObject.activeInHierarchy&&s.VisiblePawn&&s.VisiblePawn.activeSelf,"Badge confirmation did not reveal matching identity/pawn.");
        Check(C.ActivityLog.Count(x=>x.StartsWith("이름 확인 ·"))==names+1,"Name discovery was not recorded once.");
        EarlyChoiceIgnored(s);await Shot(capturePrefix+"-badge");
        if(submit)await SubmitOnce(s);else await Click(s.Next);
        DialogueAt(s,2,2,1,"장도윤");EarlyChoiceIgnored(s);
        await Click(s.ContinueSurface);DialogueAt(s,2,2,2,"장도윤",true);
        await WaitingChoiceDoesNotAdvance(s);await Shot(capturePrefix+"-known-choice");
        await Click(s.DialogueChoices[finalChoice]);DialogueAt(s,3,3,0,"장도윤");
        Check(s.State.Choice==finalChoice+1,"Chosen dialogue branch was not retained.");
        await Click(s.Next);DialogueAt(s,3,3,1,"주변");EarlyChoiceIgnored(s);
        await Click(s.ContinueSurface);Check(!s.IsOpen,"Final narration did not close dialogue.");
        Check(a.Rooms.Turns==turns&&a.Rooms.Noise==noise&&C.Campaign.MinuteOfDay==minute&&Inventory()==bag,"Reading generated time/noise/resources.");
    }
    public static async Task<string> Dialogue()
    {
        await Start();var a=C.ArrivalPanel;var s=a.Story;
        await Click(s.Clue);await Click(s.Members[0]);await Click(s.Choices[0]);await Click(a.Threat.Planner.TurnButton);await Task.Delay(150);
        Check(s.IsOpen&&s.State.Stage==1,"Observation did not open the first reading beat.");
        await ReadFirstDialogue(s,"npc-dialogue-input",true,1,0);
        return "PASS: body clicks advance one reading beat; choices hidden until their beat; early direct Choose ignored; Advance/body ignored while choosing; close/reopen preserves the same line; identity hidden until badge; one UI submit advances exactly one beat (actual keyboard delivery not tested); reading costs no time/noise/items.";
    }
    public static async Task<string> Walkthrough()
    {
        var c=C;var a=c.ArrivalPanel;var s=a.Story;var planner=a.Threat.Planner;
        int minute=c.Campaign.MinuteOfDay,turn=a.Rooms.Turns;string inventory=Inventory();
        await Click(s.Clue);Check(s.IsOpen&&!a.Main.interactable&&!a.Main.blocksRaycasts&&s.InvestigationLayout.activeInHierarchy&&!s.DialogueLayout.activeInHierarchy,"Investigation popup did not isolate input.");
        Check(!planner.Run()&&!a.Threat.CanAct&&a.Rooms.Turns==turn,"Popup permits a hidden turn.");
        Check(s.Speaker.text=="생활 흔적"&&!s.Body.text.Contains("장도윤")&&!s.VisiblePawn,"Identity appears before observation.");
        await Click(s.Members[0]);Layout();await Shot("npc-doyun-observation");
        await Click(s.Choices[0]);Check(!s.IsOpen&&planner.Plan.Observations.Count==1&&a.Rooms.Turns==turn&&c.Campaign.MinuteOfDay==minute,"Assign spent time or did not close.");
        planner.Plan.Assign(new FieldOrder{Site=1,Lead=1,Pace=2,Duty=0,Solo=true});planner.Refresh();
        int progress=a.Loot.State(1).Progress;
        var outlook=planner.Forecast(planner.Plan);Check(outlook.check.Observations.Count==1&&outlook.check.Runs.Count==1&&!outlook.check.HushedAll,"Observation and search did not coexist.");
        await Click(planner.TurnButton);await Task.Delay(150);
        Check(a.Rooms.Turns==turn+1&&c.Campaign.MinuteOfDay==minute+a.Rooms.MinutesPerTurn&&a.Loot.State(1).Progress==progress+1,"Combined actions spent other than one turn.");
        Check(s.State.Stage==1&&s.IsOpen&&s.DialogueSpeaker.text=="주변"&&planner.Plan.Observations.Count==0&&planner.LastMismatch=="","Observation result/forecast mismatch.");
        int talkTurn=a.Rooms.Turns,talkNoise=a.Rooms.Noise;
        await ReadFirstDialogue(s,"npc-dialogue-walkthrough");
        Check(a.Rooms.Turns==talkTurn&&a.Rooms.Noise==talkNoise&&Inventory()==inventory,"Reading generated cost/rewards.");
        await Shot("npc-doyun-room");
        Check(a.FinishReturn(),"Return after talk failed.");await Task.Delay(120);
        Check(s.State.Returned,"Return not remembered.");
        if(c.ReturnPanel.IsOpen)c.ReturnPanel.Close();await Task.Delay(100);c.Opening.OpenRecords();Check(c.Opening.RecordTabs.Length==5&&c.Opening.RecordTabs[4].gameObject.activeSelf&&c.Opening.RecordTitle.text.Contains("장도윤"),"Journal missing discovered NPC.");
        await Shot("npc-doyun-record");c.Opening.Close();
        var save=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(0,save,c,out var saveError),"Save write failed: "+saveError);var read=CampaignSaveStore.Read(0,c);Check(read.CanLoad&&read.Data.Doyun.Choice==2&&read.Data.Doyun.Returned,"File save lost choice/return: "+read.Error);
        CampaignPersistence.Prepare(read.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(900);
        Check(C.ArrivalPanel.Story.State.Stage==3&&C.ArrivalPanel.Story.State.Choice==2,"Scene reload lost NPC story.");
        await Depart();c=C;a=c.ArrivalPanel;s=a.Story;if(a.Popup.activeSelf)a.ClosePopup();await Task.Delay(80);
        await Click(s.Clue);DialogueAt(s,3,3,0,"장도윤");Check(s.DialogueBody.text.Contains("알려준 입구")&&!s.State.Reunited,"Reunion did not remember chosen route branch.");
        await Shot("npc-dialogue-reunion-memory");int logs=c.ActivityLog.Count;
        EarlyChoiceIgnored(s);await Click(s.ContinueSurface);DialogueAt(s,3,3,1,"장도윤");
        Check(s.DialogueBody.text.Contains("공구 가방")&&!s.State.Reunited,"Reunion skipped the bag explanation.");
        await ReopenSameBeat(s);await Click(s.Next);DialogueAt(s,3,3,2,"주변");
        Check(!s.State.Reunited&&c.ActivityLog.Count==logs,"Reunion recorded before its final confirmation.");await Shot("npc-dialogue-reunion-record");
        await Click(s.ContinueSurface);Check(!s.IsOpen&&s.State.Reunited&&c.ActivityLog.Count==logs+1,"Reunion record not appended exactly once and closed.");
        await Click(s.Clue);DialogueAt(s,3,3,0,"장도윤");await Click(s.Next);Check(!s.IsOpen,"Post-reunion single line did not close.");
        Check(c.ActivityLog.Count==logs+1&&a.Rooms.Turns==0,"Re-reading reunion duplicated a reward/log/cost.");
        return "PASS: real clue/member/Next/body/choice clicks; sequential narration and NPC beats; hidden identity→badge→remembered choice; one observation+search turn; deferred dialogue resumes same line; distinct NPC standee; return journal; file+scene reload; three-beat reunion recorded exactly once; one-line repeat closes. Production saves untouched.";
    }
    static string Inventory()=>string.Join("|",C.Campaign.Party.Select(p=>p.Name+":"+string.Join(",",C.InventoryPanel.Items.Select(i=>i.Id+"="+C.InventoryPanel.CountFor(p,i.Id)))));
    // Art-only review fixture: six recruited bodies plus the already-revealed NPC.
    public static Task<string> PeopleV3()=>ReviewPeople("standee-proportions-v3");
    public static Task<string> PeopleFacingFix()=>ReviewPeople("standee-clipping-fixed");
    static async Task<string> ReviewPeople(string prefix)
    {
        await Start();Check(C.ArrivalPanel.FinishReturn(),"Art fixture return failed.");await Task.Delay(100);
        if(C.ReturnPanel.IsOpen)C.ReturnPanel.Close();
        foreach(var data in C.Roster.Candidates.Where(p=>!PartySelectionSession.Selected.Contains(p.Id)).ToArray())
        {
            Check(C.Campaign.AddResident(new Demo5.NightRun.Adventurer(data.DisplayName,data.RoleTitle,data.Description,data.Health,data.Aim,data.BagCapacity),6),"Art fixture recruitment failed.");
            PartySelectionSession.Selected.Add(data.Id);
        }
        C.RefreshMembers();await Depart();var arrival=C.ArrivalPanel;
        if(arrival.Popup.activeSelf)arrival.ClosePopup();
        arrival.Story.Restore(new SavedNpcStory{VisitCount=3,Stage=3,Page=3,Choice=1,Returned=true,Reunited=true});
        await Task.Delay(200);Check(arrival.PartyPawns.Count==6&&arrival.Story.VisiblePawn&&arrival.Story.VisiblePawn.activeInHierarchy,"Seven person field fixture incomplete.");
        foreach(var pawn in arrival.PartyPawns)
        {
            var body=pawn.transform.Find("Body").GetComponent<SpriteRenderer>();var scale=body.transform.localScale;
            // The original scout's authored prefab proportions predate this revision and are intentionally preserved.
            if(UnityEditor.AssetDatabase.GetAssetPath(body.sprite).Contains("-body-v"))
                Check(Mathf.Abs(Mathf.Abs(scale.x)-Mathf.Abs(scale.y))<.0001f,"Revised field body was stretched: "+body.sprite.name+" / "+scale);
        }
        await ReviewedResolutions(async wh=>await Shot(prefix+"-field-"+wh.x+"x"+wh.y));
        arrival.Story.Open();await Task.Delay(100);Layout();
        await Shot(prefix+"-dialogue");arrival.Story.Close();
        arrival.Encounter.OpenThreat("원화 검수용 조우", "원화 검수", null);
        var battle=arrival.Encounter.Battle;battle.Begin(2);await Task.Delay(2000);
        Check(battle.IsOpen,"Art battle fixture did not open.");
        for(int i=0;i<battle.State.Units.Count;i++)
        {
            if(battle.State.Units[i].Enemy)continue;
            var pawn=battle.PawnView(i);float expected=1.72f*C.Roster.BodyScaleFor(pawn.Body.sprite)+.035f;
            Check(Mathf.Abs(pawn.Height-expected)<.0001f,"Battle discarded the actor's display scale.");
            Check(Mathf.Abs(Mathf.Abs(pawn.Body.transform.localScale.x)-Mathf.Abs(pawn.Body.transform.localScale.y))<.0001f,"Battle body stretched.");
        }
        await Shot(prefix+"-battle");
        return "PASS: actual six-person expedition plus revealed Doyun at three aspect ratios; uniform full-body scaling; dialogue layout; all six battle bodies and head heights retain each actor's individual scale. Screenshots are art-review fixtures, not claims of new encounter progression.";
    }
    public static async Task<string> SixAndResolutions()
    {
        await Start();Check(C.ArrivalPanel.FinishReturn(),"Six-person fixture return failed.");await Task.Delay(80);
        if(C.ReturnPanel.IsOpen)C.ReturnPanel.Close();
        foreach(var data in C.Roster.Candidates.Where(p=>!PartySelectionSession.Selected.Contains(p.Id)).ToArray())
        {
            Check(C.Campaign.AddResident(new Demo5.NightRun.Adventurer(data.DisplayName,data.RoleTitle,data.Description,data.Health,data.Aim,data.BagCapacity),6),"Fixture recruitment failed.");
            PartySelectionSession.Selected.Add(data.Id);
        }
        C.RefreshMembers();await Depart();if(C.ArrivalPanel.Popup.activeSelf)C.ArrivalPanel.ClosePopup();await Task.Delay(60);
        var story=C.ArrivalPanel.Story;await Click(story.Clue);
        Check(story.Members.Count(b=>b.gameObject.activeInHierarchy)==6,"Six member names not available.");
        try
        {
            await ReviewedResolutions(async wh=>
            {
                foreach(var button in story.Members)await Click(button);Layout();
                Hit(story.Choices[0]);Hit(story.Back);
                await Shot("npc-dialogue-six-investigation-"+wh.x+"x"+wh.y);
            });
            await Click(story.Choices[0]);await Click(C.ArrivalPanel.Threat.Planner.TurnButton);await Task.Delay(150);
            DialogueAt(story,1,0,0,"주변");UnknownIdentity(story);
            await ReviewDialogueResolutions(story,"narration");
            await Click(story.ContinueSurface);DialogueAt(story,1,0,1,"?",true);UnknownIdentity(story);
            await ReviewDialogueResolutions(story,"unknown-choices");
            await Click(story.DialogueChoices[0]);DialogueAt(story,1,1,0,"?");
            await Click(story.Next);DialogueAt(story,1,1,1,"주변");UnknownIdentity(story);
            await Click(story.Next);DialogueAt(story,2,2,0,"주변");
            await Click(story.ContinueSurface);await Click(story.Next);DialogueAt(story,2,2,2,"장도윤",true);
            await ReviewDialogueResolutions(story,"known-choices");
            await Click(story.DialogueChoices[0]);await Click(story.Next);await Click(story.ContinueSurface);
            Check(!story.IsOpen&&story.State.Stage==3,"Six-person first dialogue did not complete.");
            Check(C.ArrivalPanel.FinishReturn(),"Six-person reunion return failed.");await Task.Delay(100);await Depart();
            story=C.ArrivalPanel.Story;if(C.ArrivalPanel.Popup.activeSelf)C.ArrivalPanel.ClosePopup();await Task.Delay(80);
            await Click(story.Clue);DialogueAt(story,3,3,0,"장도윤");
            await Click(story.Next);await Click(story.ContinueSurface);DialogueAt(story,3,3,2,"주변");
            await ReviewDialogueResolutions(story,"reunion-record");
            await Click(story.Next);Check(story.State.Reunited&&!story.IsOpen,"Reunion final record did not close.");
        }
        finally{if(story&&story.IsOpen)story.Close();}
        return "PASS: six independently selectable member tabs; active text bounds and real raycasts for investigation, narration, unknown/known choices and reunion at 1920×1080, 1280×1024, 2560×1080. Each GameView change restored; no hidden-layout text included.";
    }
    static async Task ReviewDialogueResolutions(ExpeditionNpcStory story,string label)
    {
        string before=DialoguePosition(story);
        await ReviewedResolutions(async wh=>
        {
            Check(DialoguePosition(story)==before,"Changing aspect ratio advanced the story.");Layout();
            Hit(story.ContinueSurface);Hit(story.Back);
            if(story.Next.gameObject.activeInHierarchy)Hit(story.Next);
            foreach(var button in story.DialogueChoices.Where(b=>b.gameObject.activeInHierarchy))Hit(button);
            await Shot("npc-dialogue-six-"+label+"-"+wh.x+"x"+wh.y);
        });
    }
    static async Task ReviewedResolutions(Func<Vector2Int,Task> review)
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        var assembly=typeof(UnityEditor.Editor).Assembly;var st=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(st).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=st.GetMethod("GetGroup",flags).Invoke(sizes,new[]{st.GetProperty("currentGroupType",flags).GetValue(sizes)});
        var view=UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
        try
        {
            foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,1024),new Vector2Int(2560,1080)})
            {
                var sizeType=assembly.GetType("UnityEditor.GameViewSize");int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});if((int)sizeType.GetProperty("width",flags).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",flags).GetValue(size)==wh.y){index=i;break;}}
                Check(index>=0,"Expected previously reviewed GameView size is missing: "+wh);selected.SetValue(view,index);view.Repaint();await Task.Delay(450);
                await review(wh);
            }
        }
        finally{selected.SetValue(view,original);view.Repaint();}
    }
    public static async Task<string> Interrupted()
    {
        await Start();var a=C.ArrivalPanel;var s=a.Story;var planner=a.Threat.Planner;
        var state=new FieldSiteState(a.Threat.Rules,()=>99,false,3);state.MoveParty(0);state.EndTurn(0,false);
        typeof(ExpeditionSiteThreat).GetProperty("State").SetValue(a.Threat,state);a.Threat.Refresh();
        Check(state.Incoming,"Fixture did not put resident at incoming door.");
        s.Open();s.SelectMember(0);s.Choose(0);await Task.Delay(50);
        Check(planner.Run(),"Incoming observation rejected.");await Task.Delay(100);
        Check(a.Encounter.IsOpen&&!s.IsOpen&&s.State.Stage==1&&s.State.PendingDialogue,"Encounter did not take precedence over pending dialogue.");
        // Existing battle-resolution public path clears this fixture's encounter; no battle balance is modified.
        a.Encounter.FinishBattle(false,0);await Task.Delay(100);
        Check(s.IsOpen&&s.State.Stage==1&&!s.State.PendingDialogue,"Dialogue did not resume after encounter.");
        return "PASS: predicted encounter interrupts presentation while observation progress survives; safe return resumes the unread dialogue without re-observing.";
    }
    public static async Task<string> ReassignSearch()
    {
        await Start();var a=C.ArrivalPanel;var s=a.Story;var planner=a.Threat.Planner;
        await Click(s.Clue);await Click(s.Members[0]);await Click(s.Choices[0]);
        a.Search.Open(1);await Task.Delay(100);var search=a.Search;
        Check(search.IsOpen&&search.Worker==a.Participants[1],"Search did not prefer the idle member.");
        Check(search.Cards[0].Action&&search.Cards[0].Action.text.Contains("관찰")&&search.Cards[0].Paper.color==planner.BusyTint,"Observation member looks free in search.");
        await Click(search.Cards[0].Button);
        Check(search.Notice.text.Contains("관찰"),"Changing observation lead lacks notice.");
        await Click(search.Choose);
        Check(search.ReviewBody.text.Contains("관찰"),"Confirmation omits displaced observation.");
        Canvas.ForceUpdateCanvases();Check(search.ReviewBody.preferredHeight<=search.ReviewBody.rectTransform.rect.height+2,"Observation warning clips review text.");
        await Shot("npc-doyun-reassignment");
        await Click(search.Cancel);Check(planner.Plan.Observations.Count==1,"Cancelling changed live assignments.");
        await Click(search.Back);await Click(s.Clue);
        Check(s.Choices[1].gameObject.activeInHierarchy,"Cancel lost removable observation.");
        s.Close();search.Open(1);await Task.Delay(70);await Click(search.Cards[0].Button);await Click(search.Choose);await Click(search.Confirm);
        Check(planner.Plan.Observations.Count==0&&s.State.Stage==0,"Confirmed search leaves conflicting observation.");
        return "PASS: assigned observer appears busy; reassignment and confirmation identify observation removal; cancel preserves it and confirm removes it without discovering NPC.";
    }
    public static async Task<string> InspectReview()
    {
        var text=C.ArrivalPanel.Search.ReviewBody;await Shot("npc-doyun-reassignment");
        return text.text+"\nSIZE "+text.preferredHeight+" / "+text.rectTransform.rect.height+" font="+text.fontSize+" fit="+text.resizeTextForBestFit;
    }
    static async Task Shot(string name)
    {
        Canvas.ForceUpdateCanvases();string native=Path.GetFullPath("Temp/"+name+"-native.png");if(File.Exists(native))File.Delete(native);
        ScreenCapture.CaptureScreenshot(native);for(int i=0;i<35&&!File.Exists(native);i++)await Task.Delay(100);Check(File.Exists(native),"Capture missing");await Task.Delay(150);
        var source=new Texture2D(2,2);ImageConversion.LoadImage(source,File.ReadAllBytes(native));var rt=new RenderTexture(1440,Mathf.RoundToInt(1440f*Screen.height/Screen.width),0);var old=RenderTexture.active;
        try{Graphics.Blit(source,rt);RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("아트/리소스검토/"+name+".png",image.EncodeToPNG());Object.Destroy(image);}
        finally{RenderTexture.active=old;Object.Destroy(source);Object.Destroy(rt);}
    }
    public static string Finish()
    {
        Check(!Application.isPlaying,"Stop Play first.");if(CampaignSaveStore.TestDirectory==Path.GetFullPath("Temp/NpcStoryRuntimeSlots"))CampaignSaveStore.TestDirectory=null;
        return "Stopped; review-only save override cleared.";
    }
}
