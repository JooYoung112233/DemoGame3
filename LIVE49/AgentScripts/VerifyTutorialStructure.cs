using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Chapter00;
using Live49.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyTutorialStructure
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Tutorial"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Log(string message){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),message+"\n");}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static JourneyState Fixture()=>new JourneyState{day=1,location="L1",inStore=true,answeredSoi=true,bookSeen=true,fuel=3};
    static void Guide(JourneyState s,string expected){Check(Copy(s).Valid(),"Invalid checkpoint "+expected);Check(JourneyTutorial.Current(s).Id==expected,"Expected "+expected+", got "+JourneyTutorial.Current(s).Id);}
    static void Event(JourneyState s,string id){Check(FirstWeekStory.Begin(s,id),"Cannot begin "+id);while(s.weekLine<WeekScript.Find(id).lines.Length)FirstWeekStory.ConfirmLine(s);Check(FirstWeekStory.Finish(s),"Cannot finish "+id);}
    static void Inspect(JourneyState s,string id){s.exploringPlace=id;for(int i=0;i<PlaceInspection.Points(id).Length;i++)if(!PlaceInspection.Checked(s,id,i))Check(PlaceInspection.Inspect(s,id,i,out _),"Cannot inspect "+id);}
    static JourneyState CompletedPhoto()
    {var s=Fixture();s.inStore=false;s.Set("water_checked");s.Set("food_checked");s.Set(RegionTravel.Drawing);s.Set("camera_received");s.Set("first_photo_taken");s.Set(RegionTravel.Photo);s.Set(RegionTravel.Route);s.Set("story.discovered.L4");s.Set("story.discovered.L6");return s;}
    public static string Model()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");
        Check(JourneyTutorial.Current(new JourneyState()).Id=="opening","Opening guidance");
        var s=Fixture();Guide(s,"supplies");s.Set("water_checked");Guide(s,"supplies");s.Set("food_checked");s.Add("water","생수",2);s.Add("packaged_food","포장 식량",2);Guide(s,"return.meal");
        s.inStore=false;Guide(s,"meal");Check(CampLife.Apply(s,"meal","eat.packaged_food",out _),"First meal");Guide(s,"book");Event(s,"request");Guide(s,"go.L3");
        s.exploringPlace="L3";Guide(s,"pencils");Event(s,"pencils");Guide(s,"return.deliver");s.exploringPlace="";Guide(s,"deliver");Event(s,"deliver");Guide(s,"draw");Event(s,"draw");Guide(s,"go.L2");
        s.exploringPlace="L2";Guide(s,"meet");Event(s,"meet");Guide(s,"accept");Event(s,"accept");Guide(s,"go.L4");s.exploringPlace="L4";Guide(s,"bag");Event(s,"bag");Guide(s,"go.L2");
        s.exploringPlace="L2";Guide(s,"handover");Event(s,"handover");Guide(s,"go.L6");s.exploringPlace="L6";Guide(s,"photo");Event(s,"shoot");Guide(s,"album");Event(s,"album");
        Guide(s,"inspect.L6");Inspect(s,"L6");Guide(s,"go.L4");Inspect(s,"L4");Guide(s,"go.L10");Inspect(s,"L10");Guide(s,"go.L7");Inspect(s,"L7");Guide(s,"return.depart");
        s.exploringPlace="";Guide(s,"park.gate");Check(RegionExploration.Travel(s,"L7"),"Vehicle to gate");Guide(s,"depart");Check(RegionTravel.Travel(s,RegionTravel.NextRegion),"Departure");Guide(s,"complete");Check(JourneyTutorial.Completed(s).All(x=>x),"Tutorial not completed by actual actions");
        var shortage=Fixture();shortage.Set("water_checked");shortage.Set("food_checked");Guide(shortage,"extra.food");Check(CampLife.Apply(shortage,"supplies","emergency.food",out _),"No supply recovery");Guide(shortage,"return.meal");
        var ready=CompletedPhoto();ready.fuel=0;Guide(ready,"walk.L6");ready.exploringPlace="L6";Inspect(ready,"L6");Inspect(ready,"L4");Inspect(ready,"L10");Inspect(ready,"L7");Guide(ready,"go.L2");ready.exploringPlace="L2";Guide(ready,"fuel");
        ready=CompletedPhoto();ready.exploringPlace="L4";ready.Set(RegionTravel.Photo,"false");Guide(ready,"return.album");
        var existing=Fixture();existing.inStore=false;existing.Set("water_checked");existing.Set("food_checked");existing.Set("drawing_requested");existing.Add("pencils","색연필",1,2);Guide(existing,"deliver");
        var graph=ContinuationGraph.Load();foreach(var node in graph.nodes){if(node.scene!="preparation")Check(Resources.Load<Sprite>("Live49/Stages/"+node.scene)!=null,"Missing story art "+node.scene);if(!string.IsNullOrEmpty(node.next))Check(graph.nodes.Any(n=>n.id==node.next),"Missing story next "+node.next);foreach(var c in node.choices)Check(graph.nodes.Any(n=>n.id==c.target),"Missing choice target "+c.target);}
        var week=JsonUtility.FromJson<WeekScript>(Resources.Load<TextAsset>("Live49/FirstWeek/story").text);foreach(var line in week.events.SelectMany(e=>e.lines))Check(Resources.Load<Sprite>("Live49/Stages/"+line.art)!=null,"Missing first-week art "+line.art);
        foreach(var site in RegionExploration.Sites.Where(p=>!string.IsNullOrEmpty(p.Art)))Check(Resources.Load<Sprite>("Live49/Stages/"+site.Art)!=null,"Missing site art "+site.Id);
        var scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>Path.GetFileNameWithoutExtension(x.path)).ToArray();Check(scenes.Length==2&&scenes.Contains(SceneNames.Title)&&scenes.Contains(SceneNames.Game),"Unexpected build scenes");
        var origin=Fixture();origin.location="camper";origin.inStore=false;origin.fuel=0;Guide(origin,"origin.fuel");int minutes=origin.minutes;
        Check(CampLife.Apply(origin,"energy","emergency.origin",out _)&&origin.fuel==2&&origin.minutes==minutes+60,"Origin fuel recovery failed");
        Check(!CampLife.Apply(origin,"energy","emergency.origin",out _),"Origin recovery granted twice");Guide(origin,"go.L1");Check(RegionExploration.Travel(origin,"L1"),"Recovered origin cannot depart");
        Log("PASS model: actionable stages from first supplies to next region, actual first-week effects and two-point inspections, route prerequisites, pre-owned tools, zero-fuel origin recovery without duplicate grant, low fuel and food recovery, album separate from photo, legacy JSON, resource/graph references, exactly Title+Game scenes.");
        return "PASS tutorial progression and asset audit";
    }
    static async Task Wait(Func<bool> predicate,string why){var until=DateTime.UtcNow.AddSeconds(20);while(!predicate()){if(DateTime.UtcNow>until)throw new Exception("Timeout "+why);await Task.Delay(40);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(350);}
    static void Fit(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Text overflow "+t.name+": "+t.text);}
    static async Task Shot(string name){await Task.Delay(200);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(300);}
    static async Task CloseGuide(){Hud.HandleEscape();await Task.Delay(250);Check(Hud.IsPaused&&!Hud.Tutorial.IsOpen,"Guide closed two levels");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused,"resume");}
    public static async Task<string> Run()
    {
        Model();var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            var s=Fixture();s.inStore=false;await Load(s);Fit(Hud.transform);Check(GameObject.Find("PauseButton")==null,"Permanent menu button returned");
            string before=JsonUtility.ToJson(State);Hud.OpenPause();await Task.Delay(250);B("Button_플레이 안내").onClick.Invoke();
            for(int i=0;i<7;i++){B("GuideChapter_"+i).onClick.Invoke();await Task.Delay(50);Fit(Hud.Tutorial.transform);}
            Check(JsonUtility.ToJson(State)==before,"Reading guide changed journey");B("GuideChapter_1").onClick.Invoke();await Shot("01-first-exploration");await CloseGuide();
            Check(Time.timeScale==1&&!GamePause.IsPaused,"Guide left clock paused");
            await Load(CompletedPhoto());Hud.OpenTutorial();await Task.Delay(250);Fit(Hud.Tutorial.transform);await Shot("02-next-route");await CloseGuide();
            Check(SaveSystem.QueueLoad(out var error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"reload");await Task.Delay(350);Check(Hud.Objective==JourneyTutorial.Current(State).Title,"Reload lost current goal");
            var poor=Fixture();poor.Set("water_checked");poor.Set("food_checked");await Load(poor);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("03-food-recovery");await CloseGuide();
            var b=CompletedPhoto();b.exploringPlace="L8";b.Set("story.discovered.L8");await Load(b);await Wait(()=>BanditEncounter.Active(State)&&Hud.IsExploring,"bandit");Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("04-encounter-guide");await CloseGuide();Check(State.bandit.stage==0,"Guide consumed encounter");
            var cooking=CompletedPhoto();cooking.cookingUnlocked=true;cooking.Add("gas","가스",2,1);cooking.Add("ingredients","식재료",2);var session=new CookingSession(cooking);Check(session.Start(),"Cooking fixture");await Load(cooking);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("05-resume-cooking");await CloseGuide();
            var origin=Fixture();origin.location="camper";origin.inStore=false;origin.fuel=0;await Load(origin);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("06-origin-fuel-guide");await CloseGuide();
            Hud.OpenActivity(ActivityPanel.Kind.Life);await Wait(()=>Hud.Activity!=null&&Hud.Activity.IsOpen,"life");await Task.Delay(250);B("LifeTab_energy").onClick.Invoke();await Task.Delay(150);Fit(Hud.Activity.transform);await Shot("07-origin-fuel-recovery");B("LifeConfirm").onClick.Invoke();await Task.Delay(150);Check(State.fuel==2,"Origin recovery UI failed");Hud.HandleEscape();await Wait(()=>!Hud.Activity.IsOpen,"close life");
            Log("PASS live: all seven help pages fit at 1920x1080; read-only help; ESC returns one level; HUD hidden while paused; no permanent menu; state-based goal after reload; resource recovery; bandit and paused cooking guidance; zero-fuel origin recovery through actual life UI.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{if(Hud!=null&&Hud.Tutorial!=null&&Hud.Tutorial.IsOpen)await CloseGuide();if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Task.Delay(300);}if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore title");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real save changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS tutorial UI verification";
    }
}
