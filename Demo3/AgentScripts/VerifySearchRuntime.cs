using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifySearchRuntime
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/SearchRuntime"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static JourneyState Fixture()=>new JourneyState{day=1,location="L1",inStore=true,answeredSoi=true,bookSeen=true};
    static void Valid(JourneyState s){Check(s.Valid()&&Copy(s).Valid(),"Search checkpoint invalid: "+s.search?.phase);}
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),s+"\n");}
    static void CheckTwo(JourneyState s,string grade){Check(SearchSession.Check(s,0,grade)&&SearchSession.Check(s,1,grade),"Check pair");Check(!SearchSession.Check(s,1,grade),"Repeated check");Check(SearchSession.Checkpoint(s),"Checkpoint");Valid(s);}
    public static string Model()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");
        var s=Fixture();Check(SearchSession.Begin(s,"L1",0,true,false,()=>.99f),"Begin focus");Valid(s);Check(s.minutes==10&&s.Count("water")==0,"Premature loot");CheckTwo(s,"miss");Check(s.Count("water")==2,"Miss lost mandatory water");
        Check(!SearchSession.Checkpoint(s)&&!SearchSession.Check(s,2,"great"),"Checkpoint repeated / early third check");
        Check(SearchSession.Continue(s)&&!SearchSession.Continue(s)&&s.minutes==15,"Repeated time cost");SearchSession.Check(s,2,"miss");Check(SearchSession.Finish(s,"complete"),"Finish");Check(s.search.risk==.95f&&s.Count("water")==2,"Failure risk / failed extra roll");Check(!SearchSession.Finish(s,"complete"),"Duplicate reward");Valid(s);SearchSession.Acknowledge(s);Check(!SearchSession.Active(s),"Quiet result close");
        Check(SearchSession.Begin(s,"L1",1,false,false,()=>.99f),"Quick food");Check(s.Count("packaged_food")==2&&s.minutes==20&&RegionExploration.Discovered(s,"L2")&&RegionExploration.Discovered(s,"L3"),"Food or map guaranteed flow");Valid(s);SearchSession.Acknowledge(s);
        Check(!SearchSession.Begin(s,"L1",0,true),"Completed spot reopened");
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>.59f);CheckTwo(s,"great");SearchSession.Continue(s);SearchSession.Check(s,2,"great");SearchSession.Finish(s,"complete");Check(s.Count("water")==3,"Great bonus not applied");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>0);CheckTwo(s,"good");Check(SearchSession.Finish(s,"stopped")&&s.minutes==10&&s.Count("water")==2,"Checkpoint stop loss or extras");SearchSession.Acknowledge(s);Check(s.search.phase=="warning","Noise missing");Check(!RegionExploration.Travel(s,"L2"),"Travel during noise");SearchSession.Decide(s,true);Check(s.Value("search.caution.region01")=="1","Caution not recorded");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>0);s=Copy(s);SearchSession.Finish(s,"interrupted");Check(s.Count("water")==0&&s.minutes==10&&!s.Has("water_checked"),"Interrupted early paid loot");SearchSession.Acknowledge(s);SearchSession.Decide(s,false);Check(!SearchSession.FocusAvailable(s,"L1",0)&&SearchSession.Begin(s,"L1",0,false),"Interrupted recovery blocked");Check(s.Count("water")>=2,"Quick recovery lost required water");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>.99f);CheckTwo(s,"good");s=Copy(s);SearchSession.Finish(s,"interrupted");Check(s.Count("water")==2&&s.minutes==10,"Interrupted secured item lost");Valid(s);
        var bad=Copy(s);bad.search.rolls[0]=float.NaN;Check(!bad.Valid(),"NaN accepted");bad=Copy(s);bad.search.centers=null;Check(!bad.Valid(),"Missing centers accepted");
        Check(SearchSession.Grade(.60f,.60f,false)=="great"&&SearchSession.Grade(.69f,.60f,false)=="good"&&SearchSession.Grade(.74f,.60f,false)=="miss"&&SearchSession.Grade(.74f,.60f,true)=="good","Grading/slow mode");
        s=Fixture();s.Set("water_checked");s.Set("food_checked");s.inStore=false;s.exploringPlace="L2";SearchSession.Begin(s,"L2",0,false,false,()=>.99f);Check(s.fuel==5&&s.minutes==5,"L2 fuel/cost mismatch");SearchSession.Acknowledge(s);SearchSession.Begin(s,"L2",1,false,false,()=>.99f);Check(RegionExploration.Completed(s,"L2")&&RegionExploration.Discovered(s,"L4")&&s.minutes==10,"Region unlock mismatch");Valid(s);
        Log("PASS model: guaranteed first water/food, quick and focus time, optional loot bonus, misses and noise, checkpoint stop, duplicate prevention, interruption before/after securing, quick recovery, region fuel and discovery, malformed saves, slow grading.");return "PASS search model";
    }
    static async Task Wait(Func<bool> f,string why){var end=DateTime.UtcNow.AddSeconds(20);while(!f()){if(DateTime.UtcNow>end)throw new Exception("Timeout "+why);await Task.Delay(30);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Click(string n){B(n).onClick.Invoke();await Task.Delay(90);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(500);}
    static void Fit(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Shot(string n){await Task.Delay(120);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n+".png"));await Task.Delay(250);}
    static async Task AdvanceTo(float t){while(Hud.Search.Elapsed<t&&State.search.phase=="playing"){Hud.Search.Tick(.1f);await Task.Delay(18);}}
    public static async Task<string> Run()
    {
        Model();var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            await Load(Fixture());await Click("StoreWater");Check(Hud.SearchOpen,"Store action not connected");Fit(Hud.Search.transform);await Shot("01-search-choice");
            await Click("FocusSearch");State.search.noiseRoll=.99f;State.search.rolls=new[]{.99f,.99f};
            float elapsed=Hud.Search.Elapsed;Hud.Search.Strike();Check(State.search.checks==0,"Input outside check counted");Hud.HandleEscape();await Task.Delay(250);Hud.Search.Tick(1);Check(Hud.IsPaused&&Hud.Search.Elapsed==elapsed,"ESC failed to freeze");Check(B("Button_설정")!=null,"Pause settings unavailable");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused,"resume");await Task.Delay(150);
            await AdvanceTo(State.search.schedule[0]+.4f);Check(GameObject.Find("SkillDial")!=null&&GameObject.Find("SkillDial").GetComponent<CanvasRenderer>()!=null,"No rendered preview ring");Fit(Hud.Search.transform);await Shot("02-skill-preview");
            await AdvanceTo(State.search.schedule[0]+.8f+(State.search.slow?1.9f:1.25f)*State.search.centers[0]);Hud.Search.Strike();await Task.Delay(120);Check(State.search.checks==1,"Strike not registered");await Shot("03-check-feedback");
            await AdvanceTo(8.2f);await Wait(()=>State.search.phase=="checkpoint","checkpoint");Check(State.Count("water")==2&&State.minutes==10,"Checkpoint water/cost");Fit(Hud.Search.transform);await Shot("04-secured-water");
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var error),error);await Load(saved);Check(Hud.SearchOpen&&State.search.phase=="result"&&State.search.reason=="interrupted"&&State.Count("water")==2,"Reload duplicated/lost checkpoint");Fit(Hud.Search.transform);await Shot("05-restored-search");await Click("AcknowledgeSearch");if(SearchSession.Active(State))await Click("StayAfterSearch");Check(!Hud.SearchOpen&&!Hud.IsPaused,"Result did not close");
            await Click("StoreFood");await Click("QuickSearch");Fit(Hud.Search.transform);Check(State.Has("food_checked")&&RegionExploration.Discovered(State,"L2"),"Quick food progression");await Shot("06-quick-food");await Click("AcknowledgeSearch");if(SearchSession.Active(State))await Click("StayAfterSearch");
            var noisy=Fixture();noisy.Set("water_checked");noisy.Set("food_checked");noisy.inStore=false;noisy.exploringPlace="L2";SearchSession.Begin(noisy,"L2",0,false,false,()=>0);await Load(noisy);await Click("AcknowledgeSearch");Fit(Hud.Search.transform);await Shot("07-noise-choice");await Click("ReturnAfterSearch");await Wait(()=>!RegionExploration.Outside(State)&&Hud.IsExploring,"return actual camper");Check(State.location=="L1"&&State.fuel==5,"Camper moved during return");
            var region=Fixture();region.Set("water_checked");region.Set("food_checked");region.inStore=false;region.exploringPlace="L3";await Load(region);await Click("InspectRegion_0");await Click("SlowSearch");await Click("FocusSearch");State.search.noiseRoll=.999f;State.search.rolls=new[]{.99f,.99f};
            await AdvanceTo(8.8f);await Wait(()=>State.search.phase=="checkpoint","region checkpoint");await Click("ContinueSearch");await AdvanceTo(14.2f);Check(State.search.phase=="result"&&State.search.checks==3&&State.minutes==15&&PlaceInspection.Checked(State,"L3",0),"Full focus region flow");Fit(Hud.Search.transform);await Shot("08-full-focus-result");await Click("AcknowledgeSearch");
            await Load(Fixture());await Click("StoreWater");string goodPath=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder;await Click("QuickSearch");Check(Hud.Search.SaveFailed,"Save error did not stop play");int stock=State.Count("water");SaveSystem.TestSlotPath=goodPath;await Click("RetrySearchSave");Check(!Hud.Search.SaveFailed&&State.Count("water")==stock,"Save retry granted twice");
            Log("PASS live: actual store buttons, choice/preview/checkpoint/result/noise layout, timed strike and ignored early input, ESC pause/settings/resume, real save reload after checkpoint, quick food unlock, walking return to original parking, save failure/retry without duplicate loot. Timer advanced through the same Tick method via CLI; human timing difficulty remains playtest work.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Task.Delay(350);}if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore title");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real autosave changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS search runtime";
    }
}
