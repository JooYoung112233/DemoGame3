using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyBagPanel
{
    static GameHud Hud=>GameHud.Instance;
    static Button Button(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/BagReview"));
    static void Check(bool condition,string error){if(!condition)throw new Exception(error);}
    static async Task Wait(Func<bool> ready)
    {var end=DateTime.UtcNow.AddSeconds(5);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Bag wait timed out.");await Task.Delay(16);}}
    static async Task Shot(string file){ScreenCapture.CaptureScreenshot(Path.Combine(Folder,file));await Task.Delay(350);}
    static BagContents Fixture()
    {
        var data=BagContents.Opening("수혁","소이");data.InventoryKnown=true;
        // UI fixtures only: no gameplay resource award or balancing decision.
        string[] names={"생수","통조림","붕대","손전등","건전지","담요","스케치북","사진"};
        data.Items=names.Select((name,i)=>new BagContents.Item{Id="test_"+i,Name=name,Quantity=i+1,
            Group=i<2?BagContents.Category.Food:i<6?BagContents.Category.Supplies:BagContents.Category.Keepsakes,
            Description=i==0?"밀봉된 생수 한 병. 마실 물을 챙겨 두었어요.":"물건을 선택하면 이곳에서 설명을 읽을 수 있어요."}).ToArray();
        data.Members[0].Hunger=new BagContents.Gauge{Current=68,Maximum=100};
        data.Members[0].Water=new BagContents.Gauge{Current=42,Maximum=100};
        data.Members[0].Health=new BagContents.Gauge{Current=90,Maximum=100};
        data.Members[0].Stamina=new BagContents.Gauge{Current=36,Maximum=80};
        data.Members[1].Hunger=new BagContents.Gauge{Current=40,Maximum=60};
        data.Members[1].Water=new BagContents.Gauge{Current=33,Maximum=60};
        data.Members[1].Health=new BagContents.Gauge{Current=70,Maximum=80};
        data.Members[1].Stamina=new BagContents.Gauge{Current=26,Maximum=50};return data;
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);
        var original=InputSystem.settings;
        var restore=original.hideFlags==HideFlags.HideAndDontSave?UnityEngine.Object.Instantiate(original):original;
        var input=UnityEngine.Object.Instantiate(original);InputSystem.settings=input;
        input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>("BagAuditKeyboard");
        try
        {
            Check(Hud.Bag.IsOpen&&Hud.IsPaused&&Time.timeScale==0&&AudioListener.pause,"Opening bag did not own pause.");
            int scenes=SceneManager.sceneCount;
            Check(!Hud.transform.Find("HUDViewport/ActionDock").gameObject.activeSelf,"HUD overlaps bag.");
            Check(Hud.Bag.GetComponentsInChildren<TMP_Text>().Count(t=>t.text=="—")==8,"Unknown survival values are not distinct from zero.");
            Hud.OpenMap();Check(!Hud.Map.IsOpen,"Map opened over bag.");
            var fixture=Fixture();Hud.ConfigureBag(fixture);await Task.Delay(150);
            Check(Hud.Bag.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("BagItem_"))==6,"Bag pagination failed.");
            Button("BagItem_test_0").onClick.Invoke();Check(Hud.Bag.SelectedId=="test_0","Item selection failed.");
            var water=(RectTransform)Hud.Bag.transform.Find("MemberStatus/Status_soi/Gauge_수분/Fill");
            Check(water!=null&&Mathf.Abs(water.rect.width-236*33f/60)<.01,"Individual maximum is ignored.");
            await Shot("02-fixture-items.png");
            Button("BagNext").onClick.Invoke();await Task.Delay(100);Check(Hud.Bag.Page==1&&Hud.Bag.SelectedId==null,"Next page kept stale details.");
            Button("BagItem_test_6").onClick.Invoke();Check(Hud.Bag.SelectedId=="test_6","Second page selection failed.");
            Button("BagFilter_1").onClick.Invoke();await Task.Delay(100);
            Check(Hud.Bag.Page==0&&Hud.Bag.SelectedId==null&&Hud.Bag.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("BagItem_"))==2,"Category filter failed.");
            Button("BagItem_test_0").onClick.Invoke();fixture.Items[0].Quantity=0;Hud.ConfigureBag(fixture);await Task.Delay(100);
            Check(Hud.Bag.SelectedId==null&&!Hud.Bag.GetComponentsInChildren<Button>().Any(b=>b.name=="BagItem_test_0"),"Depleted item remains selectable.");
            var empty=BagContents.Opening("?","소이");empty.InventoryKnown=true;Hud.ConfigureBag(empty);await Task.Delay(100);
            Check(Hud.Bag.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="?"),"Unknown member name leaks identity.");
            Check(!Hud.Bag.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("BagItem_")),"Empty bag has stale item buttons.");
            await Shot("03-empty-filter.png");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(250);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Wait(()=>!Hud.Bag.IsOpen);
            Check(!Hud.IsPaused&&Time.timeScale==1&&!AudioListener.pause,"Actual Escape leaked pause.");
            Time.timeScale=.75f;AudioListener.pause=true;
            Hud.ConfigureBag(BagContents.Opening("수혁","소이"));Hud.Bag.SetFilter(0);
            Button("Action_Bag").onClick.Invoke();await Task.Delay(350);
            ExecuteEvents.Execute(Button("CloseBag").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            await Wait(()=>!Hud.Bag.IsOpen);
            Check(Time.timeScale==.75f&&AudioListener.pause,"Close did not restore original clock/audio.");
            Time.timeScale=1;AudioListener.pause=false;
            Check(SceneManager.sceneCount==scenes&&SceneManager.GetActiveScene().name=="01_Game","Bag changed scenes.");
            Button("Action_Bag").onClick.Invoke();await Task.Delay(350);
            var button=Button("BagFilter_2");var rt=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Bag pointer input blocked.");
            Canvas.ForceUpdateCanvases();
            Check(!Hud.Bag.GetComponentsInChildren<TMP_Text>().Any(t=>t.isTextOverflowing),"Visible text overflows.");
            await Shot("01-opening-bag.png");
            string report="PASS: real HUD bag action; eight unknown gauges; fixture filtering and paging; selection and zero-quantity update; separate character maxima; empty/unknown state and unrevealed name; map blocked while bag open; actual Escape and native submit close; restored original time scale and audio; pointer raycast; no text overflow or new scene. Fixture data removed. Actual opening bag left open. 02-fixture-items.png contains temporary UI test values, not starting inventory or balance.\n";
            File.WriteAllText(Path.Combine(Folder,"report.txt"),report);return report;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Folder,"report.txt"),"FAIL: "+e);throw;}
        finally
        {
            if(keyboard.added)InputSystem.RemoveDevice(keyboard);InputSystem.settings=restore;
            if(input!=null)UnityEngine.Object.Destroy(input);
        }
    }
}
