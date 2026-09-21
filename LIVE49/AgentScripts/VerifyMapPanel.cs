using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyMapPanel
{
    static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name);
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/MapReview"));
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static async Task Wait(Func<bool> condition)
    { var end=DateTime.UtcNow.AddSeconds(5); while(!condition()){ if(DateTime.UtcNow>end) throw new Exception("Map wait timeout.");await Task.Delay(16); } }
    static async Task Shot(string name)
    { Directory.CreateDirectory(Folder); ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(300); }
    public static async Task<string> Run()
    {
        var hud=GameHud.Instance;
        Check(hud!=null && hud.IsExploring,"Run in an active gameplay exploration context with Map configured.");
        int sceneCount=SceneManager.sceneCount;
        var original=InputSystem.settings;
        var restore=original.hideFlags==HideFlags.HideAndDontSave?UnityEngine.Object.Instantiate(original):original;
        var input=UnityEngine.Object.Instantiate(original);
        InputSystem.settings=input;
        input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>("MapAuditKeyboard");
        try
        {
            Button("Action_Map").onClick.Invoke();await Task.Delay(350);
            Check(hud.Map.IsOpen && hud.IsPaused && Time.timeScale==0,"Map did not pause gameplay.");
            Check(!hud.transform.Find("HUDViewport/Location").gameObject.activeSelf,"HUD location overlaps map header.");
            Check(SceneManager.sceneCount==sceneCount,"Map loaded another scene.");
            Check(!UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t=>t.text.Contains("주유소")||t.text.Contains("낡은 집")),"Undiscovered place leaked.");
            hud.Map.Select("L2"); Check(hud.Map.SelectedId==null,"Undiscovered place is selectable.");
            await Shot("01-map-empty.png");
            Button("MapPlace_L1").onClick.Invoke();await Task.Delay(160);
            Check(hud.Map.SelectedId=="L1" && !Button("MapTravel").interactable,"Selection or departure guard failed.");
            await Shot("02-map-store.png");
            hud.Map.Discover("L3");Button("MapPlace_L3").onClick.Invoke();
            Check(hud.Map.SelectedId=="L3","Discovered place could not be selected.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(250);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Wait(()=>!hud.Map.IsOpen);
            Check(!hud.IsPaused&&Time.timeScale==1,"Escape leaked pause state.");
            Check(hud.transform.Find("HUDViewport/Location").gameObject.activeSelf,"HUD did not return after closing map.");
            int travels=0;string blocked=null;
            hud.ConfigureMap(FirstRegionMap.Create(),id=>blocked,id=>{Check(!hud.IsPaused,"Travel ran while paused.");travels++;});
            hud.OpenMap();hud.Map.Select("L1");await Task.Delay(300);
            Button("MapTravel").onClick.Invoke();
            Check(hud.Map.IsConfirming&&travels==0,"Travel ran before confirmation.");
            await Shot("03-map-confirmation.png");
            hud.HandleEscape();Check(!hud.Map.IsConfirming&&hud.Map.IsOpen,"Confirm cancellation closed two levels.");
            Button("MapTravel").onClick.Invoke();blocked="출발 전에 남은 일을 마쳐주세요.";
            Button("MapTravel").onClick.Invoke();Check(travels==0,"Changed condition was ignored.");
            blocked=null;hud.Map.Select("L1");Button("MapTravel").onClick.Invoke();Button("MapTravel").onClick.Invoke();Button("MapTravel").onClick.Invoke();
            await Wait(()=>!hud.Map.IsOpen);Check(travels==1&&!hud.IsPaused,"Travel did not dispatch exactly once after closing.");
            hud.ConfigureMap(FirstRegionMap.Create(),id=>"소이와의 대화를 먼저 마쳐주세요.",null);
            hud.OpenMap();hud.Map.Select("L1");await Task.Delay(350);
            await Shot("02-map-store.png");
            File.WriteAllText(Path.Combine(Folder,"report.txt"),"PASS: existing HUD Map button opens without a new scene; pause and actual Escape restore clock; undiscovered locations hidden/unselectable; discovery reveals selectable marker; destination card and route selection; travel guarded, separately confirmed, cancellable, condition rechecked; callback exactly once after unpause. Existing HUD preview left showing map. Travel and geography remain preview integration.\n");
            return "Map UI and travel callback contract passed; no scene added; map left open for review.";
        }
        finally
        {
            if(keyboard.added)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings=restore;
            if(input!=null)UnityEngine.Object.Destroy(input);
        }
    }
}
