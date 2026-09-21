using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyCityMap
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview"));
    static Button B(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static void Check(bool value,string text){if(!value)throw new Exception(text);}
    static async Task Shot(string name){await Task.Delay(350);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(350);}
    static void Fits(Transform root)
    {Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Text overflow: "+t.text);}
    static Vector2 ScreenPoint(RectTransform rt,Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(p));
    static void Hit(Button button)
    {
        var rt=(RectTransform)button.transform;var e=new PointerEventData(EventSystem.current){position=ScreenPoint(rt,rt.rect.center)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pointer cannot reach "+button.name);
        ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    static async Task EscapeKey()
    {
        var original=InputSystem.settings;
        var restore=original.hideFlags==HideFlags.HideAndDontSave?UnityEngine.Object.Instantiate(original):original;
        var input=UnityEngine.Object.Instantiate(original);InputSystem.settings=input;
        input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(250);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(150);}
        finally{if(keyboard.added)InputSystem.RemoveDevice(keyboard);InputSystem.settings=restore;if(input!=null)UnityEngine.Object.Destroy(input);}
    }
    public static async Task<string> Run()
    {
        var hud=GameHud.Instance;Check(hud!=null&&hud.IsExploring,"Needs live exploration.");
        if(!hud.Map.IsOpen)hud.OpenMap();await Task.Delay(350);
        var map=hud.Map;var view=map.View;int scenes=SceneManager.sceneCount;
        string state=JsonUtility.ToJson(SaveSystem.Current);
        try
        {
            Check(Time.timeScale==0&&hud.IsPaused,"Map must pause gameplay.");
            Check(!map.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("주유소")||t.text.Contains("낡은 집")),"Undiscovered name leaked.");
            string selected=map.SelectedId;map.Select("L2");Check(map.SelectedId==selected,"Undiscovered selection allowed.");
            Hit(B("MapPlace_L1"));Check(map.SelectedId=="L1","Native pointer selection failed.");
            Check(!B("MapTravel").interactable,"Current location should remain blocked.");
            Fits(map.transform);await Shot("01-city-map.png");
            var marker=(RectTransform)B("MapPlace_L1").transform;
            float originalWidth=Vector2.Distance(ScreenPoint(marker,Vector2.zero),ScreenPoint(marker,new Vector2(44,0)));
            // EventSystem scroll coordinates, including Canvas scale, must preserve the point under the pointer.
            var vr=(RectTransform)view.transform;var pivot=new Vector2(610,-396);
            var e=new PointerEventData(EventSystem.current){position=ScreenPoint(vr,pivot),scrollDelta=new Vector2(0,3)};
            Vector2 before=(pivot-view.Pan)/view.Zoom;
            ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.scrollHandler);Canvas.ForceUpdateCanvases();
            Check(view.Zoom>1,"Wheel event did not zoom.");
            Check(Vector2.Distance((pivot-view.Pan)/view.Zoom,before)<.1f,"Wheel lost the point under the pointer.");
            float width=Vector2.Distance(ScreenPoint(marker,Vector2.zero),ScreenPoint(marker,new Vector2(44,0)));
            Check(Mathf.Abs(width-originalWidth)<.1f,"Marker size changes with map zoom.");
            var pan=view.Pan;
            e=new PointerEventData(EventSystem.current){position=ScreenPoint(vr,pivot),button=PointerEventData.InputButton.Left,eligibleForClick=true};
            ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.beginDragHandler);
            e.position=ScreenPoint(vr,pivot+new Vector2(70,-40));ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.endDragHandler);
            Check(Vector2.Distance(view.Pan-pan,new Vector2(70,-40))<.1f,"Drag delta incorrect.");
            Check(!e.eligibleForClick,"Dragging can trigger a destination click.");
            Hit(B("MapRecenter"));Check(Mathf.Abs(view.Zoom-1.35f)<.001f,"Recenter zoom failed.");
            Hit(B("MapPlace_L1"));Fits(map.transform);await Shot("02-city-map-detail.png");
            view.ZoomAt(50,pivot);Check(view.Zoom==2.5f&&!B("MapZoomIn").interactable,"Maximum zoom is unbounded.");
            view.PanBy(new Vector2(-100000,100000));
            Check(Mathf.Abs(view.Pan.x+1830)<.1f&&Mathf.Abs(view.Pan.y-1188)<.1f,"Map permits empty margins.");
            view.ZoomAt(.01f,pivot);Check(view.Zoom==1&&view.Pan==Vector2.zero&&!B("MapZoomOut").interactable,"Minimum zoom/clamp incorrect.");
            await EscapeKey();Check(!hud.IsPaused&&!map.IsOpen,"Escape did not restore gameplay.");
            int travels=0;string blocked=null;
            hud.ConfigureMap(FirstRegionMap.Create(),id=>blocked,id=>{Check(!hud.IsPaused,"Travel happened while paused.");travels++;});
            map.SetCurrentLocation("camp");hud.OpenMap();map.Select("L1");await Task.Delay(300);
            Hit(B("MapTravel"));Check(map.IsConfirming&&travels==0,"Travel skipped confirmation.");
            Fits(map.transform);await Shot("03-route-contract-fixture.png");
            hud.HandleEscape();Check(!map.IsConfirming&&map.IsOpen,"Confirmation Escape closed map.");
            Hit(B("MapTravel"));blocked="잠시 후 다시 출발해요.";Hit(B("MapTravel"));Check(travels==0,"Changed availability ignored.");
            blocked=null;map.Select("L1");Hit(B("MapTravel"));Hit(B("MapTravel"));B("MapTravel").onClick.Invoke();
            await Task.Delay(350);Check(travels==1&&!hud.IsPaused,"Travel did not dispatch exactly once.");
            Check(SceneManager.sceneCount==scenes,"Map added a scene.");
            Check(JsonUtility.ToJson(SaveSystem.Current)==state,"Map browsing changed journey state.");
        }
        finally
        {
            // Restore the actual gameplay callbacks and discovery state, even if a UI assertion fails.
            if(map.IsOpen){hud.HandleEscape();await Task.Delay(350);if(map.IsOpen){hud.HandleEscape();await Task.Delay(350);}}
            var director=UnityEngine.Object.FindAnyObjectByType<Live49.Chapter00.C0OpeningDirector>();
            director.GetType().GetMethod("RefreshJourneyHud",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(director,null);
            hud.OpenMap();map.Select("L1");await Shot("01-city-map.png");
        }
        string result="PASS: actual HUD, undiscovered names hidden, native pointer selection, scroll anchoring, drag, fixed marker size, recenter, zoom boundaries, no empty margins, text fits, Escape unpauses, confirmation cancellation and availability recheck, single callback after unpause, scene count and journey state unchanged; real gameplay callbacks restored. Screenshot 03 uses temporary travel callback only.";
        File.WriteAllText(Path.Combine(Folder,"report.txt"),result);return result;
    }
}
