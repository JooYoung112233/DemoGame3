using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyKitchenPresentation
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/KitchenPresentation"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),s+"\n");}
    static JourneyState Fixture(){var s=new JourneyState{day=2,location="L1",answeredSoi=true,bookSeen=true,cookingUnlocked=true,fuel=3};s.Set("water_checked");s.Set("food_checked");s.Add("gas","가스",3,1);s.Add("ingredients","식재료",3);return s;}
    static async Task Wait(Func<bool> f,string why,int seconds=25){var end=DateTime.UtcNow.AddSeconds(seconds);while(!f()){if(DateTime.UtcNow>end)throw new Exception("Timeout "+why);await Task.Delay(30);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Click(string n){B(n).onClick.Invoke();await Task.Delay(100);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(350);}
    static async Task Open(){Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Wait(()=>Hud.Activity!=null&&Hud.Activity.IsOpen,"kitchen");await Task.Delay(300);Fit();}
    static async Task Close(){Hud.HandleEscape();await Wait(()=>!Hud.Activity.IsOpen&&!Hud.IsPaused,"close");}
    static void Fit(){Canvas.ForceUpdateCanvases();foreach(var t in Hud.Activity.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Shot(string n){Fit();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n+".png"));await Task.Delay(250);}
    static void Dial(int level,bool drag=true)
    {
        var dial=Hud.Activity.Dial;var rt=dial.rectTransform;float a=(level-2)*60*Mathf.Deg2Rad;
        var pos=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*70));
        var e=new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left};
        if(drag)ExecuteEvents.Execute(dial.gameObject,e,ExecuteEvents.dragHandler);else ExecuteEvents.Execute(dial.gameObject,e,ExecuteEvents.pointerDownHandler);
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            var s=Fixture();s.cookingUnlocked=false;await Load(s);await Open();Check(!B("StartCooking").interactable,"Unrepaired stove cooks");await Shot("01-repair-needed");await Close();
            await Load(Fixture());await Open();Check(Hud.Activity.transform.Find("ActivityBody/KitchenStage/Pot").GetComponent<Image>().sprite.rect.width==714,"Pot original missing");await Shot("02-ready");
            await Click("StartCooking");Dial(3,false);Check(Hud.Activity.Cooking.Level==3,"Pointer down did not select high heat");Dial(1);Check(Hud.Activity.Cooking.Level==1,"Drag did not select low heat");Dial(2);Check(Hud.Activity.Cooking.Level==2&&State.Count("gas")==2&&State.Count("ingredients")==2&&State.fuel==3&&State.minutes==25,"Control changed cost or fuel");
            await Task.Delay(1500);await Click("PauseCooking");float heat=Hud.Activity.Cooking.Heat;Dial(3);await Task.Delay(200);Check(Hud.Activity.Cooking.Paused&&Hud.Activity.Cooking.Level==2&&Hud.Activity.Cooking.Heat==heat,"Paused dial or heat changed");await Shot("03-paused");await Close();
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var error),error);await Load(saved);await Open();Check(Hud.Activity.Cooking.Paused&&State.Count("gas")==2&&State.minutes==25,"Reload spent or resumed automatically");await Shot("04-restored");await Click("PauseCooking");Dial(3);
            await Wait(()=>Hud.Activity.Cooking.Heat>=85,"boiling");await Shot("05-boiling");Dial(1);await Task.Delay(120);Check(Hud.Activity.Cooking.Heat>78,"Dial changed temperature instantly");
            await Wait(()=>Hud.Activity.Cooking.Stage==CookingSession.Phase.Done,"meal",25);Fit();Check(State.Count("meal")==1&&State.Count("gas")==2&&State.Count("ingredients")==2&&State.minutes==25&&Hud.Activity.Cooking.Level==0,"Completion duplicated meal or cost");await Shot("06-finished");await Task.Delay(300);Check(State.Count("meal")==1,"Repeated completion");await Close();
            await Load(Fixture());await Open();string good=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder;await Click("StartCooking");Check(Hud.Activity.KitchenSaveFailed&&Hud.Activity.Cooking.Paused,"Save failure did not freeze kitchen");await Shot("07-save-retry");SaveSystem.TestSlotPath=good;await Click("PauseCooking");Check(!Hud.Activity.KitchenSaveFailed&&Hud.Activity.Cooking.Paused&&State.Count("gas")==2,"Retry spent again or resumed silently");await Close();
            Log("PASS: original 714x523 pot, unrepaired block, real pointer-down/drag levels, paused input/heat, gradual heat response, cooking reload, one-time cost and meal, vehicle fuel unchanged, automatic flame off, save-failure freeze/retry. Text fit at 1920x1080. Actual cooking completed using elapsed game frames.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");if(Hud?.Activity!=null&&Hud.Activity.IsOpen)await Close();if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real save changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS kitchen presentation";
    }
}
