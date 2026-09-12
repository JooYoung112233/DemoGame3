using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using Live49.Title;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifySaveSlots
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/SaveLoad"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string id)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==id);
    static TitleController Title=>UnityEngine.Object.FindAnyObjectByType<TitleController>();
    static void Check(bool b,string text){if(!b)throw new Exception(text);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),s+"\n");}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow: "+t.name+" / "+t.text);}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(25);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(30);}}
    static async Task Shot(string name){await Task.Delay(250);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(300);}
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(250);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&(string.IsNullOrEmpty(s.weekEvent)&&s.day>0?Hud.IsExploring:State!=null));await Task.Delay(350);
    }
    static async Task Open(bool save){Hud.OpenSaveLoad(save);await Wait(()=>Hud.SaveLoad!=null&&Hud.SaveLoad.IsOpen);await Task.Delay(250);}
    static async Task Select(int slot){B("SaveSlot_"+slot).onClick.Invoke();await Task.Delay(150);Fits(UnityEngine.Object.FindAnyObjectByType<SaveLoadPanel>().transform);}
    static async Task Close(){Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);}
    static async Task SaveNew(int slot){await Select(slot);B("SlotPrimaryAction").onClick.Invoke();await Task.Delay(200);Check(SaveSlots.Read(slot).Readable,"Slot save failed");}
    static async Task LoadSlot(int slot)
    {
        await Select(slot);B("SlotPrimaryAction").onClick.Invoke();await Task.Delay(150);B("ConfirmSlotAction").onClick.Invoke();await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused&&!SceneFlow.OpeningHandoffPending);await Task.Delay(200);
    }
    static async Task ToTitle()
    {
        Hud.OpenPause();await Task.Delay(250);Hud.RequestExit(false);Hud.ConfirmExit();await Wait(()=>Title!=null&&SceneManager.GetActiveScene().name==SceneNames.Title);await Task.Delay(350);
    }
    static void ChooseTitle(string id){var item=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id==id);Title.Confirm(item);}
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"progress.txt"),"");
        string realPath=SaveSystem.SlotPath;string realBytes=File.Exists(realPath)?File.ReadAllText(realPath):null;
        if(!SaveSystem.TryRead(realPath,out var original,out _))Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out original,out _),"No restoration checkpoint");
        var old=SaveSystem.TestSlotPath;
        string isolated=Path.Combine(Folder,"run-"+Guid.NewGuid().ToString("N"),"autosave.json");SaveSystem.TestSlotPath=isolated;
        try
        {
            var s=new JourneyState{day=2,location="L1",bookSeen=true,answeredSoi=true,fuel=2};s.Set("water_checked");s.Set("food_checked");s.Add("water","생수",2);s.Add("packaged_food","포장 식량",2);await Load(s);
            string autoBytes=File.ReadAllText(isolated);await Open(true);await Select(0);Check(!B("SlotPrimaryAction").interactable,"Manual overwrites auto slot");await SaveNew(1);Check(File.ReadAllText(isolated)==autoBytes,"Manual save overwrites automatic");await Shot("01-manual-save");await Close();
            string first=File.ReadAllText(SaveSlots.PathFor(1));State.fuel=4;State.minutes=37;State.Set("slot_marker","changed");await Open(true);await Select(1);B("SlotPrimaryAction").onClick.Invoke();await Task.Delay(150);Check(File.ReadAllText(SaveSlots.PathFor(1))==first,"Overwrite before confirmation");await Shot("02-overwrite-confirmation");B("CancelSlotAction").onClick.Invoke();await Task.Delay(150);Check(File.ReadAllText(SaveSlots.PathFor(1))==first,"Overwrite cancel changed file");B("SlotPrimaryAction").onClick.Invoke();await Task.Delay(120);B("ConfirmSlotAction").onClick.Invoke();await Task.Delay(200);Check(SaveSlots.Read(1).State.fuel==4&&File.ReadAllText(SaveSlots.PathFor(1)+".bak")==first,"Overwrite/backup mismatch");await SaveNew(2);await Close();
            State.cookingUnlocked=true;State.Add("gas","조리용 가스",2,1);State.Add("ingredients","식재료",2);var cook=new CookingSession(State);Check(cook.Start(),"Cooking fixture failed");for(int i=0;i<20;i++)cook.Tick(.1f);cook.Paused=true;await Open(true);await SaveNew(3);await Close();
            await Open(false);await Select(1);B("SlotPrimaryAction").onClick.Invoke();await Task.Delay(150);B("CancelSlotAction").onClick.Invoke();await Task.Delay(100);Check(CampLife.Cooking(State),"Load cancel changed current progress");await LoadSlot(1);Check(State.fuel==4&&State.minutes==37&&State.Value("slot_marker")=="changed"&&!CampLife.Cooking(State),"Manual load did not restore selected state");
            await Open(false);await LoadSlot(3);Check(CampLife.Cooking(State)&&State.Count("gas")==1&&State.Count("ingredients")==1,"Cooking checkpoint lost/double cost");
            await ToTitle();Fits(Title.transform);await Shot("03-title-load-entry");ChooseTitle("load");await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SaveLoadPanel>()?.IsOpen==true);Check(!UnityEngine.Object.FindObjectsByType<Button>().Any(b=>b.name=="SaveMode"),"Title exposes saving");await Shot("04-title-slot-list");await LoadSlot(1);Check(State.minutes==37&&!CampLife.Cooking(State),"Title selected-slot load failed");
            await ToTitle();Check(SaveSlots.Latest().Index==3,"Latest slot selection wrong");ChooseTitle("continue");await Wait(()=>Hud!=null&&Hud.IsExploring&&!SceneFlow.OpeningHandoffPending);Check(CampLife.Cooking(State),"Continue did not load newest manual save");
            // Corrupt only isolated test slots. Recovery never rewrites a manual original.
            var oldTwo=SaveSlots.Read(2).State;Check(SaveSystem.Write(SaveSlots.PathFor(2),Copy(State),out _),"Could not create backup fixture");File.WriteAllText(SaveSlots.PathFor(2),"invalid test file");File.WriteAllText(SaveSlots.PathFor(3),"invalid test file");
            Check(SaveSlots.Read(2).Recoverable&&!SaveSlots.Read(3).Recoverable,"Recovery catalog wrong");await Open(false);await Select(3);Check(!B("SlotPrimaryAction").interactable,"Corrupt slot load enabled");await Select(2);await Shot("05-backup-recovery");await LoadSlot(2);Check(State.minutes==oldTwo.minutes&&!CampLife.Cooking(State)&&File.ReadAllText(SaveSlots.PathFor(2))=="invalid test file","Backup load mutated source/wrong state");
            var current=State;Check(!SaveSystem.QueueLoad(SaveSlots.PathFor(3),out _)&&ReferenceEquals(State,current)&&SaveSystem.Pending==null,"Invalid load changes live state or leaves pending");
            // The pause-menu entry returns to the pause menu, then resumes on the next Escape.
            Hud.OpenPause();await Task.Delay(200);Hud.ShowSaveCard();await Wait(()=>Hud.SaveLoad.IsOpen);Hud.HandleEscape();await Task.Delay(200);Check(Hud.IsPaused&&!Hud.SaveLoad.IsOpen,"Save close skips pause menu");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            SaveSystem.TestSlotPath=Path.Combine(Folder,"empty-"+Guid.NewGuid().ToString("N"),"autosave.json");await ToTitle();Check(!UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Any(i=>i.Id=="continue"),"Continue visible with no save");ChooseTitle("load");await Wait(()=>UnityEngine.Object.FindAnyObjectByType<SaveLoadPanel>()?.IsOpen==true);Check(!B("SlotPrimaryAction").interactable,"Empty slot load enabled");await Shot("06-empty-slot-list");
            Log("PASS slots/UI: auto/manual separation; 3 independent slots; overwrite confirm/cancel; checksum and backup; load cancel; selected slot restored; cooking state/cost preserved; HUD/pause/title entry; latest Continue; corrupt/empty slots; pause ownership.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=isolated;await Load(original);await Shot("07-restored-hud");}
            finally{SaveSystem.TestSlotPath=old;}
            Check((File.Exists(realPath)?File.ReadAllText(realPath):null)==realBytes,"Real autosave was changed by verification");Log("RESTORED existing journey; real save file unchanged.");
        }
        return "PASS save/load slots and real restoration; verification used isolated files only.";
    }
}
