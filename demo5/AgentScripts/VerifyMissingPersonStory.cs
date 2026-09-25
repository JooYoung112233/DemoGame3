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
public static class VerifyMissingPersonStory
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 const string Output="아트/리소스검토/";
 static string TestDirectory=>Path.GetFullPath("Temp/MissingPersonVerification");
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static SettlementTutorialNarrative N=>C?C.Narrative:null;
 static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
 static MissingPersonStory S=>C.MissingPerson;
 static readonly List<string> checks=new List<string>(),shots=new List<string>();
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static async Task WaitFor(Func<bool> f,string message,int attempts=100){for(int i=0;i<attempts&&!f();i++)await Task.Delay(100);Check(f(),message);}
 static CampaignSaveData Clone(CampaignSaveData s)=>JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(s));
 public static async Task<string> Run()
 {
  Check(Application.isPlaying,"Play first");CampaignSaveStore.TestDirectory=TestDirectory;checks.Clear();shots.Clear();string failure=null;
  try{
   PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await WaitFor(()=>Object.FindAnyObjectByType<PartySelectionController>(),"party");await Task.Delay(200);
   await Tap(Object.FindAnyObjectByType<PartySelectionController>().Continue);await WaitFor(()=>Object.FindAnyObjectByType<HomeSelectionController>(),"home");
   var h=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(h.Cards[1].Button);await Tap(h.Continue);
   await WaitFor(()=>N&&N.IsOpen&&!N.IsEntering,"opening");Check(S&&!S.State.Found,"story missing or fabricated");
   Check(N.Body.text.Contains("이금례"),"opening lacks purpose");await Sizes(async wh=>{Fits(N.Body);await Shot("missing-person-opening-"+wh.x+"x"+wh.y);});
   await Tap(N.Skip);Check(C.TutorialSkipped&&!S.State.Found,"skip fabricated story");
   await Tap(C.Journal);C.Opening.SelectRecord(0);Check(C.Opening.RecordBody.text.Contains("이금례"),"initial record");await Tap(C.Opening.RecordsBack);
   await Tap(C.Exit);var p=C.ExpeditionPanel;await WaitFor(()=>p.IsOpen,"map");int mall=Array.FindIndex(p.Destinations,d=>d.Id=="mall");if(p.Current?.Id!="mall")await Tap(p.Markers[mall]);
   foreach(var card in p.Cards.ToArray())if(!card.Check.gameObject.activeSelf)await Tap(card.Button);
   await Tap(p.Pack);await Tap(C.PackingPanel.Ready);await Tap(C.PackingPanel.Depart);
   await WaitFor(()=>C.ArrivalPanel.IsOpen&&!C.ArrivalPanel.InTransit,"arrive");var a=C.ArrivalPanel;
   /* 말 놓기: pawns at the door, then 턴 진행 */await WaitFor(()=>FieldPawnTest.Ready(a),"board ready");Check(await FieldPawnTest.Move(a,1),"everyone at the corridor door: "+FieldPawnTest.Describe(a));await WaitFor(()=>a.Rooms.CurrentRoom==1&&!a.InTransit,"corridor");
   await WaitFor(()=>FieldPawnTest.Ready(a),"board ready");Check(FieldPawnTest.Detail(a,5)&&a.Search.IsOpen&&a.Search.ReadOnly,"the 07 window only shows");await Tap(a.Search.Back);
   Check(FieldPawnTest.Coop(a,5,1),"two pawns on the crate, 망보기: "+FieldPawnTest.Describe(a));/* 망보기: 2 turns */
   Check(await FieldPawnTest.Turn(a),"first search turn");await Encounters();
   Check(!a.Loot.State(5).Complete&&!S.State.Found,"partial search fabricated clue");
   await WaitFor(()=>FieldPawnTest.Ready(a),"board ready");if(FieldPawnTest.Check(a)?.RunFor(5)==null)Check(FieldPawnTest.Lead(a,0,5),"a pawn back on the crate");Check(await FieldPawnTest.Turn(a),"second search turn");await Encounters();await WaitFor(()=>a.Loot.IsOpen,"loot");
   Check(S.State.Found&&!S.IsOpen,"discovery must wait for loot close");
   await Tap(a.Loot.Back);if(a.Loot.LeaveReview.activeSelf)await Tap(a.Loot.LeaveConfirm);
   await WaitFor(()=>S.IsOpen,"discovery dialogue");int turns=a.Rooms.Turns,noise=a.Rooms.Noise;
   await Tap(S.Next);await Sizes(async wh=>{Fits(S.Body);Fits(S.NextLabel);await Shot("missing-person-discovery-"+wh.x+"x"+wh.y);});
   await Tap(S.Next);await Tap(S.Next);Check(!S.IsOpen&&a.Rooms.Turns==turns&&a.Rooms.Noise==noise,"reading changed turn/noise");
   await WaitFor(()=>FieldPawnTest.Ready(a),"board ready");Check(await FieldPawnTest.Move(a,0),"everyone at the arcade door: "+FieldPawnTest.Describe(a));await WaitFor(()=>a.Rooms.CurrentRoom==0&&!a.InTransit,"back");
   await Tap(a.Return);await Tap(a.ReturnConfirm);await WaitFor(()=>C.ReturnPanel.IsOpen,"return report");await Tap(C.ReturnPanel.Back);await WaitFor(()=>S.IsOpen,"return story");
   await Tap(S.Next);Check(S.State.ReturnLine==1,"return index");var saved=CampaignPersistence.Capture(C);
   Check(CampaignSaveStore.Write(0,saved,C,out var error),error);var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);await Load(read.Data);await WaitFor(()=>S.IsOpen,"return resume");Check(S.State.ReturnLine==1&&S.Body.text.Contains("보관표"),"wrong resumed line");
   await Tap(S.Next);await Tap(S.Next);Check(S.State.Discussed&&!S.IsOpen,"return finish");await Tap(C.Journal);C.Opening.SelectRecord(0);
   Check(C.Opening.RecordBody.text.Contains("미란 세탁소")&&C.Opening.RecordBody.text.Contains("아직 모른다"),"record overstates evidence");
   await Sizes(async wh=>{Fits(C.Opening.RecordBody);await Shot("missing-person-record-"+wh.x+"x"+wh.y);});await Tap(C.Opening.RecordsBack);
   var done=CampaignPersistence.Capture(C);await Load(done);await Task.Delay(300);Check(S.State.Found&&S.State.Discussed&&!S.IsOpen,"duplicate return");
   var legacy=Clone(done);legacy.Version=18;legacy.MissingPerson=null;CampaignPersistence.Upgrade(legacy,C);Check(legacy.Version==CampaignPersistence.CurrentVersion&&!legacy.MissingPerson.Found,"legacy fabricated clue");
   var malformed=Clone(done);malformed.MissingPerson.ReturnLine=3;bool rejected=false;try{CampaignPersistence.Validate(malformed,C);}catch{rejected=true;}Check(rejected,"bad line accepted");
   checks.Add("Actual fresh game -> initial-only skip -> record -> corridor -> partial/full search -> deferred dialogue -> return -> disk save/reload mid-dialogue -> permanent record passed.");
   checks.Add("Reading preserves turns/noise; partial search and tutorial skip do not grant clue; completed return is not repeated. Version 18 upgrades without inferring discovery; invalid return line rejected.");
  }catch(Exception e){failure=e.ToString();}
  File.WriteAllText(Output+"missing-person-runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{result=failure==null?"PASS":"FAIL",checks,shots,failure},Newtonsoft.Json.Formatting.Indented));
  Check(failure==null,failure);return string.Join("\n",checks);
 }
 static async Task Encounters(){for(int i=0;i<25&&C.ArrivalPanel.Encounter.IsOpen;i++){await Tap(C.ArrivalPanel.Encounter.Wait);await Tap(C.ArrivalPanel.Encounter.Confirm);await Task.Delay(200);}Check(!C.ArrivalPanel.Encounter.IsOpen,"encounter persists");}
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
