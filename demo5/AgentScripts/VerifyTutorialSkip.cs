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

// Disposable campaigns and isolated disk slots. Explicit progress fixtures are
// removed for the initial-only policy; both expedition visits use actual UI.
public static class VerifyTutorialSkip
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    const string Output="아트/리소스검토/";
    static string TestDirectory=>Path.GetFullPath("Temp/TutorialSkipVerification");
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static SettlementTutorialNarrative N=>C?C.Narrative:null;
    static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
    static readonly List<string> checks=new List<string>(),shots=new List<string>();
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static int Clock()=>C.Campaign.Day*1440+C.Campaign.MinuteOfDay;
    static int Stock(string id)=>C.CraftPanel.Materials.Single(m=>m.Id==id).Initial;
    static async Task WaitFor(Func<bool> condition,string error,int attempts=80)
    {for(int i=0;i<attempts&&!condition();i++)await Task.Delay(100);Check(condition(),error);}
    static CampaignSaveData Clone(CampaignSaveData source)
    {var copy=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(source));if(source.ReturnReport==null)copy.ReturnReport=null;return copy;}
    static string Gameplay()
    {var s=CampaignPersistence.Capture(C);s.SavedUtc=null;s.TutorialNarrative=null;s.Activity=null;return JsonUtility.ToJson(s);}
    static string Facts()
    {var s=CampaignPersistence.Capture(C);return JsonUtility.ToJson(new FactSnapshot{Opening=s.Opening,Development=s.Development,Unlocked=s.UnlockedCharacters,Rooms=new[]{s.CorridorVisited,s.StorageUnlocked,s.StorageVisited},Doyun=s.Doyun});}
    [Serializable] sealed class FactSnapshot {public SavedOpeningChapter Opening;public SavedDevelopment Development;public string[] Unlocked;public bool[] Rooms;public SavedNpcStory Doyun;}

    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");checks.Clear();shots.Clear();string failure=null;
        try
        {
            CampaignSaveStore.TestDirectory=TestDirectory;
            await NewGame();await SkipAtArrival();await DiskReload(0);
            await FirstAndSecondVisit();
            await NewGame();await InitialOnly();
            await NewGame();Check(!C.TutorialSkipped&&C.Introduction.Step==0&&N.IsOpen,"Skip leaked into another campaign.");
            checks.Add("A final fresh campaign still shows the ordinary arrival dialogue at step 0; skipping is campaign-local, not a global preference.");
        }
        catch(Exception e){failure=e.ToString();}
        Directory.CreateDirectory(Output);string report=Path.GetFullPath(Output+"tutorial-skip-runtime.json");
        File.WriteAllText(report,Newtonsoft.Json.JsonConvert.SerializeObject(new{result=failure==null?"PASS":"FAIL",checks,screenshots=shots,failure,note="Disposable original-pair campaigns, isolated Temp slots. Raycast clicks exercise initial-only skip, normal preparation, departure/return and revisiting. Later dialogues and their restored saves reject skip; menu entry is absent. Screenshots require separate visual inspection."},Newtonsoft.Json.Formatting.Indented));
        Check(failure==null,failure+"\n"+report);return "PASS: "+report+"\n"+string.Join("\n",checks);
    }
    static async Task NewGame()
    {
        PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");
        await WaitFor(()=>Object.FindAnyObjectByType<PartySelectionController>(),"Party selection missing.");await Task.Delay(150);
        var p=Object.FindAnyObjectByType<PartySelectionController>();
        Check(p.Cards.Count(c=>c.gameObject.activeInHierarchy)==2&&PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"}),"Starting pair changed.");
        await Tap(p.Continue);await WaitFor(()=>Object.FindAnyObjectByType<HomeSelectionController>(),"Home selection missing.");
        var home=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(home.Cards[1].Button);await Tap(home.Continue);
        await WaitFor(()=>C&&C.Campaign!=null&&N&&N.IsOpen&&!N.IsEntering,"Fresh arrival dialogue did not open.");
        Check(!C.TutorialSkipped&&C.Introduction.Step==0&&N.CurrentBeat==0,"New game was already skipped.");
        Check(C.Campaign.Party.All(m=>m.Health==m.MaxHealth),"New game injuries appeared.");
        Check(C.CraftPanel.Materials.All(m=>m.Initial==0)&&C.Campaign.Ammo==0,"Unexplained starting goods.");
    }
    static async Task SkipAtArrival()
    {
        Check(N.Skip,"Apply the skip UI builder first.");
        await Sizes(async wh=>{ValidateSkip(N.Skip);await Shot("tutorial-skip-dialogue-"+wh.x+"x"+wh.y);});
        int time=Clock();string facts=Facts();int ammo=C.Introduction.FoundAmmo;
        await Tap(N.Skip);await WaitFor(()=>C.TutorialSkipped&&!N.IsBlocking&&C.Main.interactable,"Dialogue skip did not release the settlement.");
        Check(C.Introduction.Step==10&&Clock()==time+40,"Full preparation skip must apply 10+10+20 minutes once.");
        Check(Stock("cloth")==1&&Stock("water")==1&&C.Campaign.Ammo==ammo,"Full skip did not grant exactly the real initial discovery.");
        Check(C.Campaign.Party.All(p=>p.Health==p.MaxHealth)&&Facts()==facts,"Skip fabricated injury, chapter progress, facility restoration, characters or room knowledge.");
        Check(!C.Opening.State.FirstReturn&&!C.Opening.State.ClueRead&&!C.Opening.State.Complete,"Skip marked unwitnessed story events completed.");
        Check(C.ArrivalPanel.Threat.ExportSaved()==null&&!C.ArrivalPanel.Threat.Visited,"Skip fabricated a visited site.");
        Check(N.State.PendingBeat==-1&&N.State.LineIndex==0&&N.State.SeenMask==SavedTutorialNarrative.AllSeen,"Skip left a pending tutorial line.");
        Check(C.Exit.IsActive()&&C.Workbench.IsActive()&&C.Cabinet.IsActive()&&C.Development.OpenButton.IsActive()&&C.Journal.IsActive(),"Free-play controls remain tutorial-gated.");
        Check(C.Development.Block(C.CraftPanel.Recipes.Single(r=>r.Id=="nails"))!=null,"Skipping improperly unlocked workbench recipes.");
        Check(C.Roster.Candidates.Where(p=>!p.AvailableAtStart).All(p=>!C.IsCharacterUnlocked(p.Id)),"Skipping unlocked later companions.");
        NoGuide();string after=Gameplay();N.SkipTutorial();await Task.Delay(100);Check(Gameplay()==after,"Repeated skip duplicated discovery or time.");
        checks.Add("Actual arrival dialogue skip applies remaining preparation once (40 minutes, cloth 1, water 1 and configured ammo), keeps full health and all real story/facility/companion/room facts, exposes free-play controls and preserves actual crafting locks. Repeating skip is inert.");
    }
    static async Task DiskReload(int slot)
    {
        string before=Gameplay();var save=CampaignPersistence.Capture(C);
        Check(CampaignSaveStore.Write(slot,save,C,out var error),error);var read=CampaignSaveStore.Read(slot,C);Check(read.CanLoad,read.Error);
        Check(read.Data.TutorialNarrative.Skipped,"Disk save lost the skip flag.");
        await Load(read.Data);await Task.Delay(250);
        Check(C.TutorialSkipped&&!N.IsBlocking&&!N.IsEntering&&Gameplay()==before,"Reload reopens guidance or changes gameplay.");NoGuide();
        checks.Add("Isolated disk write/read and actual scene reload preserve skipped state, resources, clock and gameplay; no fresh arrival fade or tutorial dialogue returns.");
    }
    static async Task Depart()
    {
        await Tap(C.Exit);await WaitFor(()=>C.ExpeditionPanel.IsOpen,"Expedition map did not open.");
        var p=C.ExpeditionPanel;int mall=Array.FindIndex(p.Destinations,d=>d.Id=="mall");Check(mall>=0,"Mall missing.");
        if(p.Current?.Id!="mall")await Tap(p.Markers[mall]);
        foreach(var card in p.Cards.ToArray())if(!card.Check.gameObject.activeSelf)await Tap(card.Button);
        Check(p.Selected.Count==2,"Both original survivors must join the test trip.");
        await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
        await WaitFor(()=>C.ArrivalPanel.IsOpen&&!C.ArrivalPanel.InTransit,"Expedition did not arrive.");await Task.Delay(200);
        Check(C.Campaign.Stage==JourneyStage.Expedition,"Arrival lacks expedition state.");NoGuide();
    }
    static async Task Return()
    {
        var a=C.ArrivalPanel;await Tap(a.Return);Check(a.Popup.activeSelf&&a.ReturnConfirm.gameObject.activeInHierarchy,"Return confirmation missing.");
        await Tap(a.ReturnConfirm);await WaitFor(()=>C.ReturnPanel.IsOpen,"Return report missing.");await Tap(C.ReturnPanel.Back);await Task.Delay(200);
        Check(C.Campaign.Stage==JourneyStage.Settlement&&!N.IsOpen,"Return reopened skipped dialogue.");NoGuide();
    }
    static async Task FirstAndSecondVisit()
    {
        await Depart();var a=C.ArrivalPanel;
        Check(a.Threat.State!=null&&a.Threat.State.Asleep&&!a.Threat.Active&&!a.Threat.Visited&&a.Story.State.VisitCount==1,"Skip changed first-visit threat or NPC progression.");
        Check(!a.Popup.activeSelf,"Tutorial popup opened on first trip.");await Return();
        Check(C.Opening.State.FirstReturn&&C.ArrivalPanel.Threat.Visited,"Actual first return was not recorded.");
        await Depart();a=C.ArrivalPanel;
        Check(a.Threat.Active&&!a.Threat.State.Asleep&&a.Threat.IntroAcknowledged&&!a.Popup.activeSelf,"Second visit board tutorial was not skipped cleanly.");
        Check(a.Story.State.VisitCount==2&&a.Threat.Planner.Active,"Revisit behavior changed with skip.");
        await WaitFor(()=>a.Story.Clue.gameObject.activeInHierarchy,"Skipping board tutorial blocked the hidden NPC.");
        Check(a.Story.ObserveBlock(ExpeditionNpcStory.ObservationId)==FieldPause.None,"Hidden NPC observation blocked after skipped board tutorial.");
        var lesson=typeof(ExpeditionBattlePanel).GetProperty("LessonAvailable",F);Check(lesson!=null&&!(bool)lesson.GetValue(a.Encounter.Battle),"Resident battle lesson remains enabled after skip.");
        await Shot("tutorial-skip-second-visit");await Return();
        Check(C.Opening.State.Enabled&&!C.Opening.State.ClueRead&&!C.Opening.State.Complete,"Tutorial skip corrupted real story state after visits.");
        checks.Add("Actual first departure/return remains the first sleeping-threat visit. Actual second departure wakes the site normally, suppresses board/battle lessons, preserves visit counts and leaves the hidden NPC discoverable; returning still records genuine first-return progress.");
    }
    static async Task Drain(int beat)
    {
        await WaitFor(()=>N.IsOpen&&N.CurrentBeat==beat,"Expected normal dialogue beat "+beat+" did not open.");
        for(int i=0;i<12&&N.IsOpen;i++)await Tap(N.Next);
        Check(!N.IsOpen,"Normal dialogue did not finish.");await Task.Delay(150);
    }
    static async Task InitialOnly()
    {
        await Drain(0);await Tap(C.Introduction.Action);
        await WaitFor(()=>N.IsOpen&&N.CurrentBeat==1,"Next dialogue missing.");
        Check(!N.Skip.gameObject.activeInHierarchy&&!N.View.transform.Find("SkipHint").gameObject.activeInHierarchy,"Skip remains in later dialogue.");
        string before=Gameplay();N.SkipTutorial();Check(!C.TutorialSkipped&&Gameplay()==before,"Later dialogue permits skip.");
        var saved=CampaignPersistence.Capture(C);await Load(saved);
        await WaitFor(()=>N.IsOpen&&N.CurrentBeat==1,"Later dialogue reload missing.");
        Check(!N.Skip.gameObject.activeInHierarchy,"Reload redisplays skip.");
        await Shot("tutorial-skip-later-dialogue-hidden");
        await Drain(1);await Tap(C.GameMenu.OpenButton);
        Check(!C.GameMenu.SkipTutorial||!C.GameMenu.SkipTutorial.gameObject.activeInHierarchy,"Menu still offers skip.");
        await Shot("tutorial-skip-menu-removed");await Tap(C.GameMenu.Resume);
        await Tap(C.Introduction.Action);await WaitFor(()=>N.IsOpen&&N.CurrentBeat==2,"Discovery dialogue missing.");
        Check(!N.Skip.gameObject.activeInHierarchy&&!C.TutorialSkipped,"Discovery reoffers skip.");
        checks.Add("Skip and its hint appear only in the initial beat. Later dialogue, its restored save, discovery and the settlement menu never offer it; a direct late call is inert.");
    }
    static async Task Load(CampaignSaveData save)
    {var previous=C;CampaignPersistence.Prepare(save,C);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&!ReferenceEquals(C,previous)&&C.Campaign!=null&&N,"Settlement save did not reload.");await Task.Delay(200);}
    static void NoGuide()
    {Check(!G.Target&&!G.Banner.activeSelf&&(!G.Marker||!G.Marker.gameObject.activeSelf)&&(!G.Pointer||!G.Pointer.gameObject.activeSelf)&&(!G.Spotlight||!G.Spotlight.gameObject.activeSelf),"Skipped tutorial guide remains visible.");}
    static void ValidateSkip(Button b)
    {Check(b&&b.IsActive()&&b.IsInteractable(),"Skip button unavailable.");Onscreen((RectTransform)b.transform);foreach(var t in b.GetComponentsInChildren<Text>())Fits(t);if(N.IsOpen){foreach(var t in new[]{N.SceneTitle,N.Speaker,N.Body,N.NextLabel,N.ReadingHint})if(t){Fits(t);Onscreen(t.rectTransform);}Onscreen((RectTransform)N.Next.transform);}}
    static void Fits(Text t)
    {Canvas.ForceUpdateCanvases();if(!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+2,"Text clips: "+t.name+" / "+t.text);}
    static void Onscreen(RectTransform r)
    {var canvas=r.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var corners=new Vector3[4];r.GetWorldCorners(corners);foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(cam,corner);Check(p.x>=-1&&p.x<=Screen.width+1&&p.y>=-1&&p.y<=Screen.height+1,"Element leaves screen: "+r.name);}}
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
                if(index<0){var enumType=asm.GetType("UnityEditor.GameViewSizeType");var constructor=sizeType.GetConstructor(F,null,new[]{enumType,typeof(int),typeof(int),typeof(string)},null);var size=constructor.Invoke(new[]{Enum.Parse(enumType,"FixedResolution"),(object)wh.x,wh.y,"Tutorial skip review"});group.GetType().GetMethod("AddCustomSize",F).Invoke(group,new[]{size});index=count;added.Add(index-(int)group.GetType().GetMethod("GetBuiltinCount",F).Invoke(group,null));}
                selected.SetValue(view,index);view.Repaint();await Task.Delay(650);Check(Screen.width==wh.x&&Screen.height==wh.y,"Wrong capture resolution.");await action(wh);
            }
        }
        finally{selected.SetValue(view,original);foreach(var index in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize",F).Invoke(group,new object[]{index});view.Repaint();await Task.Delay(150);}
    }
    static async Task Shot(string name)
    {Directory.CreateDirectory(Output);string temp=Path.GetFullPath("Temp/"+name+"-native.png");if(File.Exists(temp))File.Delete(temp);ScreenCapture.CaptureScreenshot(temp);for(int i=0;i<40&&!File.Exists(temp);i++)await Task.Delay(100);Check(File.Exists(temp),"Missing screenshot.");await Task.Delay(150);string destination=Path.GetFullPath(Output+name+".png");File.Copy(temp,destination,true);shots.Add(destination);}
    public static string Finish()
    {Check(!Application.isPlaying,"Stop Play Mode first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;PartySelectionSession.Clear();return "Tutorial skip verification override cleared.";}
}
