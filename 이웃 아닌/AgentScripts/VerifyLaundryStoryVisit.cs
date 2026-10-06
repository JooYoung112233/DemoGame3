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
public static class VerifyLaundryStoryVisit
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 const string Output="아트/리소스검토/";
 static string TestDirectory=>Path.GetFullPath("Temp/LaundryStoryVerification");
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static SettlementTutorialNarrative N=>C?C.Narrative:null;
 static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
 static LaundryStoryVisit V=>C.Laundry;
 static SavedMissingPerson S=>C.MissingPerson.State;
 static readonly List<string> checks=new List<string>(),shots=new List<string>();
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static async Task WaitFor(Func<bool> f,string message,int attempts=100){for(int i=0;i<attempts&&!f();i++)await Task.Delay(100);Check(f(),message);}
 static CampaignSaveData Clone(CampaignSaveData s)=>JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(s));
 static int Clock()=>C.Campaign.Day*1440+C.Campaign.MinuteOfDay;
 static int Index()=>Array.FindIndex(C.ExpeditionPanel.Destinations,d=>d.Id=="laundry");
 static string Mall()=>JsonUtility.ToJson(C.ArrivalPanel.Story.State)+JsonUtility.ToJson(C.ArrivalPanel.Threat.ExportSaved())+C.ArrivalPanel.Loot.SearchSummary();
 public static async Task<string> Reunion()
 {
  Check(Application.isPlaying,"Play first");CampaignSaveStore.TestDirectory=TestDirectory;
  var catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
  var read=CampaignSaveStore.Read(0,catalog);Check(read.CanLoad,read.Error);var fixture=Clone(read.Data);fixture.MissingPerson.MetGeumrye=false;fixture.MissingPerson.ReunionComplete=false;fixture.MissingPerson.MiranQuestions=0;
  CampaignPersistence.Prepare(fixture,catalog);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&C.Campaign!=null&&V,"reunion fixture");await Task.Delay(200);
  string mall=Mall();await Tap(C.Exit);var p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);foreach(var card in p.Cards.ToArray())if(card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(p.Cards[0].Button);await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
  await Tap(V.Next);Check(V.AskWhere.GetComponentInChildren<Text>().text.Contains("어디"),"meeting exposed before whereabouts");await Tap(V.AskWhere);await Tap(V.Next);await Tap(V.Next);Check(!S.MetGeumrye,"hearsay counted as meeting");await Tap(V.AskWhere);Check(!S.MetGeumrye,"invitation counted as sighting");await Tap(V.Next);Check(S.MetGeumrye&&!S.ReunionComplete,"sighting state");await Tap(V.Next);Check(V.Speaker.text=="이금례"&&V.Body.text.Contains("전해 줘요"),"absent Haein branch");await Tap(V.Return);await Tap(C.ReturnPanel.Back);
  while(C.MissingPerson.IsOpen)await Tap(C.MissingPerson.Next);
  var partial=CampaignPersistence.Capture(C);Check(CampaignSaveStore.Write(1,partial,C,out var err),err);var partialRead=CampaignSaveStore.Read(1,C);Check(partialRead.CanLoad,partialRead.Error);await Load(partialRead.Data);Check(S.MetGeumrye&&!S.ReunionComplete,"partial save fabricated resolution");
  await DepartOnly();await Tap(V.Next);await Tap(V.AskWhere);int clock=Clock();await Tap(V.Next);await Tap(V.Next);Check(V.Speaker.text=="이금례"&&V.Body.text.Contains("쪽지"),"reunion opening");
  await Sizes(async wh=>{Fits(V.Body);Fits(V.Context);await Shot("geumrye-reunion-"+wh.x+"x"+wh.y);});
  int count=0;while(V.Next.gameObject.activeInHierarchy&&count++<15){Fits(V.Body);await Tap(V.Next);}Check(count<15&&S.ReunionComplete,"reunion not completed");Check(Clock()==clock,"reunion consumed time");
  int logCount=C.ActivityLog.Count(x=>x.Contains("해인과 금례가 다시 만났다"));Check(logCount==1,"completion duplicated");await Tap(V.Return);await Tap(C.ReturnPanel.Back);await Tap(C.Journal);C.Opening.SelectRecord(0);
  Check(C.Opening.RecordBody.text.Contains("현재 거처")&&!C.Opening.RecordBody.text.Contains("아직 남아"),"resolved record inaccurate");await Sizes(async wh=>{Fits(C.Opening.RecordBody);await Shot("geumrye-reunion-record-"+wh.x+"x"+wh.y);});await Tap(C.Opening.RecordsBack);
  var done=CampaignPersistence.Capture(C);Check(CampaignSaveStore.Write(2,done,C,out err),err);var disk=CampaignSaveStore.Read(2,C);Check(disk.CanLoad,disk.Error);await Load(disk.Data);Check(S.ReunionComplete,"reunion lost on reload");
  var old=Clone(done);old.Version=20;CampaignPersistence.Upgrade(old,C);Check(old.MissingPerson.RouteKnown&&old.MissingPerson.MetMiran&&!old.MissingPerson.MetGeumrye&&!old.MissingPerson.ReunionComplete,"v20 upgrade fabricated reunion");
  var bad=Clone(done);bad.MissingPerson.MetGeumrye=false;bool rejected=false;try{CampaignPersistence.Validate(bad,C);}catch{rejected=true;}Check(rejected,"invalid resolution accepted");
  await DepartOnly();await Tap(V.Next);Check(V.AskWhere.GetComponentInChildren<Text>().text.Contains("안부"),"repeat visit not updated");await Tap(V.AskWhere);await Tap(V.Next);Check(V.Body.text.Contains("주소"),"repeat does not remember address");await Tap(V.Next);await Tap(V.Return);await Tap(C.ReturnPanel.Back);Check(C.ActivityLog.Count(x=>x.Contains("해인과 금례가 다시 만났다"))==1&&Mall()==mall,"repeat grants or mall changed");
  File.WriteAllText(Output+"geumrye-reunion-runtime.txt","PASS: known-Miran fixture from actual prior campaign; location answer gate; invitation vs direct sighting; solo messenger; early return and isolated disk reload; Haein reunion; reading free; record bounds at 1920x1080/1280x800; persistent resolution/revisit; v20 migration and invalid state rejection; no duplicate completion or mall changes.");
  return "PASS: reunion branches, partial/complete disk reload, revisit, migration and both resolutions.";
 }
 public static async Task<string> Relay()
 {
  Check(Application.isPlaying,"Play first");CampaignSaveStore.TestDirectory=TestDirectory;
  var catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
  var read=CampaignSaveStore.Read(0,catalog);Check(read.CanLoad,read.Error);var fixture=Clone(read.Data);
  fixture.MissingPerson.MiranQuestions=1;fixture.MissingPerson.MetGeumrye=false;fixture.MissingPerson.ReunionComplete=false;
  fixture.MissingPerson.NewsShared=false;fixture.MissingPerson.NewsMessengerId=null;fixture.MissingPerson.NewsLine=0;
  CampaignPersistence.Prepare(fixture,catalog);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&C.Campaign!=null&&V,"relay fixture");await Task.Delay(200);
  C.MissingPerson.QueueNews("scout");Check(string.IsNullOrEmpty(S.NewsMessengerId),"hearsay queues direct witness");
  await Tap(C.Exit);var p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);foreach(var card in p.Cards.ToArray())if(card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(p.Cards[0].Button);await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
  await Tap(V.Next);await Tap(V.AskWhere);await Tap(V.Next);Check(S.MetGeumrye&&!S.ReunionComplete,"witness before relay");
  await Tap(V.Return);Check(C.ReturnPanel.IsOpen&&!C.MissingPerson.IsOpen&&S.NewsMessengerId=="scout","relay overlaps return report");
  await Tap(C.ReturnPanel.Back);await WaitFor(()=>C.MissingPerson.IsOpen,"relay did not open after report");
  int before=Clock();Check(C.MissingPerson.Speaker.text==C.Campaign.Party.First().Name&&C.MissingPerson.Body.text.Contains("직접"),"wrong witness");
  await Tap(C.MissingPerson.Next);Check(S.NewsLine==1&&C.MissingPerson.Speaker.text.Contains("해인"),"Haein response");
  await Sizes(async wh=>{Fits(C.MissingPerson.Body);Fits(C.MissingPerson.Title);await Shot("geumrye-news-relay-"+wh.x+"x"+wh.y);});
  var mid=CampaignPersistence.Capture(C);Check(CampaignSaveStore.Write(1,mid,C,out var error),error);var disk=CampaignSaveStore.Read(1,C);Check(disk.CanLoad,disk.Error);await Load(disk.Data);await WaitFor(()=>C.MissingPerson.IsOpen,"relay resume");Check(S.NewsLine==1&&C.MissingPerson.Body.text.Contains("걱정"),"relay lost read position");
  await Tap(C.MissingPerson.Next);await Tap(C.MissingPerson.Next);Check(S.NewsShared&&!S.ReunionComplete&&string.IsNullOrEmpty(S.NewsMessengerId)&&Clock()==before,"relay fabricated meeting or time");
  var done=CampaignPersistence.Capture(C);await Load(done);await Task.Delay(200);Check(!C.MissingPerson.IsOpen&&S.NewsShared,"relay repeated after load");C.MissingPerson.QueueNews("scout");Check(string.IsNullOrEmpty(S.NewsMessengerId),"relay repeated after another witness");
  var old=Clone(done);old.Version=21;CampaignPersistence.Upgrade(old,C);Check(old.MissingPerson.MetGeumrye&&!old.MissingPerson.NewsShared&&string.IsNullOrEmpty(old.MissingPerson.NewsMessengerId),"migration invented relay");
  var bad=Clone(done);bad.MissingPerson.NewsLine=3;bool rejected=false;try{CampaignPersistence.Validate(bad,C);}catch{rejected=true;}Check(rejected,"invalid relay index accepted");
  File.WriteAllText(Output+"geumrye-news-relay-runtime.txt","PASS: actual solo visit and witness; return-report ordering; Haein response; both resolutions; isolated disk mid-dialogue reload; no repeat, time or reunion grant; v21 migration and invalid index rejection. Save mid-dialogue is a verification API fixture, not a new in-dialogue save button.");
  return "PASS: solo witness news relay, report ordering, persistent line and completion, migration and both resolutions.";
 }
 public static async Task<string> Followup()
 {
  Check(Application.isPlaying,"Play first");CampaignSaveStore.TestDirectory=TestDirectory;
  var catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
  var read=CampaignSaveStore.Read(2,catalog);Check(read.CanLoad,read.Error);CampaignPersistence.Prepare(read.Data,catalog);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&C.Campaign!=null&&V,"followup fixture");await Task.Delay(200);Check(S.ReunionComplete,"completed fixture required");
  await Tap(C.Exit);var p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);
  foreach(var card in p.Cards.ToArray())if(card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(p.Cards[0].Button);
  Check(p.Unknown.text.Contains("재회 완료")&&p.Description.text.Contains("안부")&&p.Hint.text.Contains("누구나"),"completed map guidance");
  await Sizes(async wh=>{Fits(p.Unknown);Fits(p.Description);Fits(p.Hint);await Shot("laundry-followup-map-"+wh.x+"x"+wh.y);});
  S.ReunionComplete=false;p.Refresh();Check(p.Unknown.text.Contains("재회 대기")&&p.Hint.text.Contains("해인 없이도"),"solo pending guidance");
  await Tap(p.Cards[1].Button);Check(p.Hint.text.Contains("해인 동행"),"Haein selection hint");await Tap(p.Cards[1].Button);
  S.ReunionComplete=true;p.Refresh();await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
  Check(V.Body.text.Contains("안부")&&V.Context.text.Contains("안부"),"return greeting still searches");
  await Tap(V.Next);await Tap(V.AskWhere);await Tap(V.Next);Check(V.Body.text.Contains("해인이가")&&V.Body.text.Contains("전해 줘요"),"solo visitor mistaken for Haein");
  await Sizes(async wh=>{Fits(V.Body);Fits(V.Context);await Shot("laundry-followup-solo-"+wh.x+"x"+wh.y);});
  await Tap(V.Return);await Tap(C.ReturnPanel.Back);
  File.WriteAllText(Output+"laundry-followup-runtime.txt","PASS: completed save fixture; completed/pending map descriptions; party hints; solo return greeting and Geumrye dialogue; text bounds at 1920x1080 and 1280x800. No user saves written.");
  return "PASS: post-reunion map, party guidance and solo revisit at both resolutions.";
 }
 public static async Task<string> HudSix()
 {
  Check(Application.isPlaying&&C&&S.RouteKnown,"Run Edges first");
  foreach(var data in C.Roster.Candidates.Where(p=>!PartySelectionSession.Selected.Contains(p.Id)).ToArray()){
   if(C.Campaign.Party.Count()>=6)break;
   Check(C.Campaign.AddResident(data.CreateAdventurer(),6),"six-member layout fixture");PartySelectionSession.Selected.Add(data.Id);
  }
  C.RefreshMembers();await DepartOnly();await Tap(V.Next);
  Check(V.Hud&&V.Hud.Cards.Count==6,"six HUD cards missing");
  await Sizes(async wh=>{
   foreach(var text in new[]{V.Heading,V.Context,V.Body,V.Hint,V.Hud.Clock,V.Hud.Route,V.Hud.MemberCount}){Fits(text);Onscreen(text.rectTransform);}
   foreach(var button in new[]{V.Return,V.AskWhere,V.AskLetter}){Fits(button.GetComponentInChildren<Text>());Onscreen((RectTransform)button.transform);}
   V.Hud.MemberScroll.horizontalNormalizedPosition=0;await Task.Delay(180);Check(V.Hud.MemberScroll.horizontalScrollbar.gameObject.activeInHierarchy,"six-member scrollbar hidden");await Shot("laundry-hud-six-left-"+wh.x+"x"+wh.y);
   V.Hud.MemberScroll.horizontalNormalizedPosition=1;await Task.Delay(180);Onscreen((RectTransform)V.Hud.Cards[5].transform);
   var bar=V.Hud.MemberScroll.horizontalScrollbar;var rail=(RectTransform)bar.transform;var corners=new Vector3[4];bar.handleRect.GetWorldCorners(corners);foreach(var corner in corners){var local=rail.InverseTransformPoint(corner);Check(local.x>=rail.rect.xMin-1&&local.x<=rail.rect.xMax+1,"scroll handle exceeds rail");}
   await Shot("laundry-hud-six-right-"+wh.x+"x"+wh.y);
  });
  await Tap(V.Return);await Tap(C.ReturnPanel.Back);
  File.WriteAllText(Output+"laundry-hud-six.txt","PASS: explicit six-resident layout fixture; original-size cards, visible scroll rail, sixth card visible at right edge; texts/actions onscreen at 1920x1080 and 1280x800. No user campaign modified.");
  return "PASS six-member HUD, scrolling, text bounds and both resolutions.";
 }
 public static async Task<string> Edges()
 {
  Check(Application.isPlaying,"Play first");CampaignSaveStore.TestDirectory=TestDirectory;
  var catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
  var read=CampaignSaveStore.Read(0,catalog);Check(read.CanLoad,read.Error);var fixture=Clone(read.Data);fixture.MissingPerson.MetMiran=false;fixture.MissingPerson.MiranQuestions=0;
  CampaignPersistence.Prepare(fixture,catalog);SceneManager.LoadScene("Settlement");await WaitFor(()=>C&&C.Campaign!=null&&V,"load fixture");await Task.Delay(200);
  int before=Clock();await DepartOnly();await Tap(V.Return);await Tap(C.ReturnPanel.Back);Check(!S.MetMiran&&S.MiranQuestions==0&&Clock()==before+40,"early exit fabricated meeting or lost travel cost");
  await DepartOnly();for(int i=0;i<4;i++)await Tap(V.Next);await Tap(V.AskWhere);await Tap(V.Return);await Tap(C.ReturnPanel.Back);
  Check(S.MetMiran&&S.MiranQuestions==1,"answer interrupted by departure lost evidence");await Tap(C.Journal);C.Opening.SelectRecord(0);Check(C.Opening.RecordBody.text.Contains("아직 묻지 않았다"),"unasked letter revealed");await Tap(C.Opening.RecordsBack);
  var save=CampaignPersistence.Capture(C);await Load(save);Check(S.MiranQuestions==1,"partial knowledge not saved");
  await DepartOnly();await Tap(V.Next);await Sizes(async wh=>{Check(V.Speaker.text=="대화 선택","choice mislabeled as speech");await Shot("laundry-choices-"+wh.x+"x"+wh.y);});await Tap(V.Return);await Tap(C.ReturnPanel.Back);
  File.WriteAllText(Output+"laundry-story-edge-cases.txt","PASS: early departure before introduction leaves identity unknown; travel charged each direction. Leaving during a heard answer records that answer only; reload preserves partial knowledge. Choices are not spoken NPC dialogue. Fixture starts from the actual end-to-end saved campaign with contact state explicitly reset.");
  return "PASS: early departure, partial answer evidence, partial-state reload and current choice screenshots.";
 }
 static async Task DepartOnly(){await Tap(C.Exit);var p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);foreach(var card in p.Cards.ToArray())if(!card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);Check(V.IsVisit,"visit missing");}
 public static async Task<string> Run()
 {
  Check(Application.isPlaying&&C&&S.Discussed&&!S.RouteKnown,"Run VerifyMissingPersonStory first to obtain actual clue");
  CampaignSaveStore.TestDirectory=TestDirectory;checks.Clear();shots.Clear();string failure=null;
  try{
   var baseline=CampaignPersistence.Capture(C);string mall=Mall();int start=Clock();
   var noClue=Clone(baseline);noClue.MissingPerson=new SavedMissingPerson();await Load(noClue);await Tap(C.Exit);
   Check(!C.ExpeditionPanel.Markers[Index()].gameObject.activeInHierarchy,"unearned marker visible");Check(!V.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations[Index()]),"unearned departure allowed");Check(Clock()==start,"failed departure spent time");await Tap(C.ExpeditionPanel.Back);await Load(baseline);
   await Tap(C.Exit);var p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);Check(p.PackLabel.text=="표찰 주소 확인"&&!p.Current.Accessible,"route gate missing");await Tap(p.Pack);Check(V.IsOpen&&!S.RouteKnown,"route granted early");
   await Tap(V.Next);await Tap(V.Next);Check(S.RouteKnown&&p.IsOpen&&Clock()==start,"route review time/unlock");
   await Sizes(async wh=>{Fits(p.Unknown);Fits(p.PlaceName);await Shot("laundry-map-"+wh.x+"x"+wh.y);});
   foreach(var card in p.Cards.ToArray())if(!card.Check.gameObject.activeSelf)await Tap(card.Button);
   await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
   Check(V.IsVisit&&Clock()==start+20&&!C.ArrivalPanel.IsOpen&&!S.MetMiran,"visit lifecycle");
   bool blocked=false;try{CampaignPersistence.Capture(C);}catch(InvalidOperationException){blocked=true;}Check(blocked,"field save should be blocked");
   await Tap(V.Next);Check(V.Speaker.text=="?"&&!S.MetMiran,"identity revealed early");await Tap(V.Next);await Tap(V.Next);Check(V.Speaker.text=="황미란"&&S.MetMiran,"introduction not revealed");await Tap(V.Next);
   await Sizes(async wh=>{Fits(V.Body);Fits(V.Context);await Shot("laundry-choices-"+wh.x+"x"+wh.y);});
   await Tap(V.AskWhere);Check(S.MiranQuestions==0,"question marked before reading");await Tap(V.Next);await Tap(V.Next);Check(S.MiranQuestions==1,"where answer not recorded");
   await Tap(V.AskLetter);await Tap(V.Next);await Sizes(async wh=>{Fits(V.Body);await Shot("laundry-letter-"+wh.x+"x"+wh.y);});await Tap(V.Next);
   Check(S.MiranQuestions==3&&Clock()==start+20,"dialogue consumed time");await Tap(V.Return);Check(C.ReturnPanel.IsOpen&&Clock()==start+40&&Mall()==mall,"return altered mall or cost");await Tap(C.ReturnPanel.Back);
   await Tap(C.Journal);C.Opening.SelectRecord(0);Check(C.Opening.RecordBody.text.Contains("수신함")&&C.Opening.RecordBody.text.Contains("직접 만나지는"),"incorrect evidence");await Sizes(async wh=>{Fits(C.Opening.RecordBody);await Shot("laundry-record-"+wh.x+"x"+wh.y);});await Tap(C.Opening.RecordsBack);
   var done=CampaignPersistence.Capture(C);Check(CampaignSaveStore.Write(0,done,C,out var error),error);var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);await Load(read.Data);Check(S.RouteKnown&&S.MetMiran&&S.MiranQuestions==3,"disk lost visit");
   var old=Clone(done);old.Version=19;CampaignPersistence.Upgrade(old,C);Check(old.MissingPerson.Found&&old.MissingPerson.Discussed&&!old.MissingPerson.RouteKnown&&!old.MissingPerson.MetMiran,"migration fabricated contact");
   var invalid=Clone(done);invalid.MissingPerson.RouteKnown=false;bool rejected=false;try{CampaignPersistence.Validate(invalid,C);}catch{rejected=true;}Check(rejected,"impossible contact accepted");
   await Tap(C.Exit);p=C.ExpeditionPanel;await Tap(p.Markers[Index()]);foreach(var card in p.Cards.ToArray())if(card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(p.Cards[0].Button);await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
   Check(V.Speaker.text=="황미란"&&V.Body.text.Contains("다시"),"revisit repeats unknown greeting");await Tap(V.Next);await Tap(V.AskLetter);await Tap(V.Next);Check(!V.Body.text.Contains("저는"),"absent medic speaks");await Tap(V.Next);await Tap(V.Return);await Tap(C.ReturnPanel.Back);Check(Mall()==mall,"revisit mutates mall");
   checks.Add("Actual prior clue -> manual address review -> map unlock -> packing -> visit -> unknown/name reveal -> two questions -> return -> record -> isolated disk reload -> solo revisit passed.");
   checks.Add("20 minutes each direction; reading free; field save blocked; no mall visit/loot changes; legacy v19 preserves existing clue without fabricated route; invalid contact rejected.");
  }catch(Exception e){failure=e.ToString();}
  File.WriteAllText(Output+"laundry-story-runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{result=failure==null?"PASS":"FAIL",checks,shots,failure},Newtonsoft.Json.Formatting.Indented));Check(failure==null,failure);return string.Join("\n",checks);
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
    {Check(!Application.isPlaying,"Stop Play Mode first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;PartySelectionSession.Clear();return "Missing person verification override cleared.";}
}
