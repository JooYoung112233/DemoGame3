using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Bundled with the existing verification classes by tools/build-revalidation.ps1.
public static class RevalidateFirstPass
{
    static string Folder=>Path.GetFullPath(Application.dataPath+"/../Screenshots/Revalidation-2026-09-13");
    static GameHud Hud=>GameHud.Instance;
    static JourneyState S=>SaveSystem.Current;
    static List<string> failures=new List<string>(),runtimeErrors=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Log(string m)=>File.AppendAllText(Folder+"/results.txt",DateTime.UtcNow.ToString("s")+" "+m+"\n");
    static string Hash(string p){if(!File.Exists(p))return "absent";using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)));}
    static async Task Wait(Func<bool> f){var end=DateTime.UtcNow.AddSeconds(20);while(!f()){if(DateTime.UtcNow>end)throw new Exception("UI timeout");await Task.Delay(50);}}
    static Button B(string id)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==id);
    static async Task Click(string id){Check(B(id).interactable,"Disabled "+id);B(id).onClick.Invoke();await Task.Delay(250);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var e)&&SaveSystem.QueueLoad(out e),e);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(400);}
    static async Task Title(){var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone);SaveSystem.Current=null;}
    static async Task Shot(string n){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Folder+"/"+n+".png");await Task.Delay(250);}
    static void Fit(Transform r){Canvas.ForceUpdateCanvases();foreach(var t in r.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Suite(string name,Func<Task<string>> run)
    {
        Log("START "+name);
        try{Log("PASS "+name+" — "+await run());}
        catch(Exception e){failures.Add(name+": "+e.Message);Log("FAIL "+name+" — "+e);}
    }
    static void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors.Add(type+": "+message+"\n"+stack);}
    static async Task<string> SaveAndArt()
    {
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder+"/save-ui/"+Guid.NewGuid().ToString("N")+"/auto.json";
        try
        {
            var natural=JsonUtility.FromJson<JourneyState>(File.ReadAllText(Path.GetFullPath(Application.dataPath+"/../Screenshots/FirstPassJourney/completed-state.json")));
            await Load(natural);Hud.HandleEscape();await Task.Delay(250);await Click("Button_설정");Hud.Settings.RequestSaveLoad();await Task.Delay(250);await Click("SaveMode");await Click("SaveSlot_1");
            string autoHash=Hash(SaveSystem.SlotPath);await Click("SlotPrimaryAction");Check(SaveSlots.Read(1).Readable&&Hash(SaveSystem.SlotPath)==autoHash,"Manual/automatic separation");
            string first=Hash(SaveSlots.PathFor(1));await Click("SlotPrimaryAction");Check(Hash(SaveSlots.PathFor(1))==first,"Overwrite before confirmation");await Click("CancelSlotAction");Check(Hash(SaveSlots.PathFor(1))==first,"Cancel changed save");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");Check(Hash(SaveSlots.PathFor(1)+".bak")==first,"Backup missing");
            await Click("SaveSlot_0");Check(!B("SlotPrimaryAction").interactable,"Manual overwrite of autosave enabled");
            await Click("LoadMode");await Click("SaveSlot_1");await Click("SlotPrimaryAction");await Click("CancelSlotAction");Check(S.regionId=="region02"&&S.Count("camera")==1,"Load cancel changes state");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);await Task.Delay(500);Check(JourneyTutorial.Completed(S).All(x=>x)&&S.bandit.stage==4&&S.Count("first_travel_photo")==1,"Loaded milestones lost");
            string slot=SaveSlots.PathFor(1);File.WriteAllText(slot,"deliberately invalid isolated verification file");Check(SaveSlots.Read(1).Recoverable,"Backup recovery not offered");Hud.OpenSaveLoad(false);await Task.Delay(300);await Click("SaveSlot_1");Fit(Hud.SaveLoad.transform);await Shot("save-backup-recovery");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);Check(File.ReadAllText(slot).StartsWith("deliberately invalid"),"Recovery overwrites damaged original");
            var before=S;Check(!SaveSystem.QueueLoad(slot,out _)&&ReferenceEquals(S,before)&&SaveSystem.Pending==null,"Invalid save changes live state");
            foreach(string id in new[]{"L8","L9","L10"})
            {
                var s=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(natural));s.regionId="region01";s.location="L1";s.exploringPlace=id;s.inStore=false;await Load(s);
                var stage=GameObject.Find("JourneyStage").GetComponent<Image>();Check(stage.sprite.name=="region-"+id,"Missing/wrong stage "+id);Check(Hud.InteractionRoot.Find("Location").GetComponentInChildren<HudIcon>().Symbol==HudIcon.Kind.Map,"Outside location icon");Fit(Hud.InteractionRoot);await Shot(id+"-stage");
                Hud.OpenSaveLoad(false);await Task.Delay(250);await Click("SaveSlot_0");Check(GameObject.Find("SaveIllustration").GetComponent<Image>().sprite==stage.sprite,"Save thumbnail "+id);Fit(Hud.SaveLoad.transform);await Title();
            }
            return "settings manual save/load, overwrite/load cancellation, checksum rejection, backup recovery, six milestones and 3 stage/thumbnail identities";
        }
        finally{await Title();SaveSystem.TestSlotPath=old;}
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/results.txt","");failures.Clear();runtimeErrors.Clear();
        Check(SceneManager.GetActiveScene().name==SceneNames.Title&&S==null&&string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Start from fresh Title play mode");
        var paths=Enumerable.Range(0,4).Select(SaveSlots.PathFor).SelectMany(p=>new[]{p,p+".bak"}).ToArray();var hashes=paths.ToDictionary(p=>p,Hash);
        Application.logMessageReceived+=OnLog;
        try
        {
            await Suite("first-week model",()=>Task.FromResult(VerifyFirstWeek.Model()));
            await Suite("region travel model",()=>Task.FromResult(VerifyRegionTravel.Model()));
            await Suite("camp life model",()=>Task.FromResult(VerifyCampLife.Model()));
            await Suite("tutorial model + UI",VerifyTutorialStructure.Run);
            await Suite("search model + UI",VerifySearchRuntime.Run);
            await Suite("kitchen runtime",VerifyKitchenPresentation.Run);
            await Suite("bandit model + UI",VerifyBanditEncounter.Run);
            await Suite("save recovery + art",SaveAndArt);
        }
        finally
        {
            await Title();SaveSystem.TestSlotPath=null;Application.logMessageReceived-=OnLog;
            foreach(var p in paths)if(Hash(p)!=hashes[p])failures.Add("Real save changed: "+p);
            File.WriteAllText(Folder+"/runtime-errors.txt",string.Join("\n\n",runtimeErrors));
            if(runtimeErrors.Count>0)failures.Add(runtimeErrors.Count+" runtime error logs");
            Log("RESTORED Title, slot override cleared; checked automatic/manual slots and backups");
            Log(failures.Count==0?"COMPLETE PASS — 8 suites, no runtime error logs, all 8 real slot/backup hashes unchanged":"COMPLETE FAIL — "+string.Join("; ",failures));
        }
        return failures.Count==0?"PASS 8 revalidation suites":string.Join("; ",failures);
    }
}
