using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Demo6.Core.Loot;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Demo6.Game;
public static class UiReviewV2 {
 const string Root="아트/UI-정리-v2/검수";
 static string Json(object o)=>Newtonsoft.Json.JsonConvert.SerializeObject(o,Newtonsoft.Json.Formatting.Indented);
 public static object State()=>Json(new{playing=EditorApplication.isPlaying,dirty=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>new{SceneManager.GetSceneAt(i).path,SceneManager.GetSceneAt(i).isDirty}),width=Screen.width,height=Screen.height});
 public static object Resolution(int width,int height){
  var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");
  var view=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().FirstOrDefault();if(!view)throw new Exception("No existing GameView");
  var property=type.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
  string backup=Root+"/game-view-original.json";if(!File.Exists(backup))File.WriteAllText(backup,Json(new{index=(int)property.GetValue(view)}));
  var sizes=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizes);var instance=singleton.GetProperty("instance").GetValue(null);
  var group=sizes.GetMethod("GetGroup").Invoke(instance,new object[]{GameViewSizeGroupType.Standalone});
  var sizeType=assembly.GetType("UnityEditor.GameViewSize");var enumType=assembly.GetType("UnityEditor.GameViewSizeType");
  var size=Activator.CreateInstance(sizeType,new object[]{Enum.ToObject(enumType,1),width,height,"UI Review "+width+"x"+height});
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
  int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);property.SetValue(view,count-1);view.Repaint();
  return Json(new{width,height,index=count-1});
 }
 public static async Task<string> Ready(){Application.runInBackground=true;InputSystem.EnableDevice(Keyboard.current);InputSystem.EnableDevice(Mouse.current);await Task.Delay(800);return Json(new{width=Screen.width,height=Screen.height,modal=DungeonUi.Modal});}

 public static object SeedBag(){
  var bag=(List<WeaponItem>)typeof(Inventory).GetField("_bag",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Inventory.Instance);
  if(bag.Count!=0)throw new Exception("Do not replace existing inventory");
  for(int i=0;i<15;i++)bag.Add(new WeaponItem(new[]{WeaponItem.LongswordId,WeaponItem.GreatswordId,WeaponItem.TwinbladesId}[i%3],(Grade)(i%5),1+i%10,1000));
  return Json(new{count=bag.Count,note="Disposable Play-session fixtures using existing item definitions"});
 }
 public static async Task<string> KeyPress(string key){
  var k=(Key)Enum.Parse(typeof(Key),key,true);var kb=Keyboard.current;
  InputSystem.QueueStateEvent(kb,new KeyboardState(k));await Task.Delay(120);
  InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(120);
  return Json(new{key,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked,paused=TimeScaleService.Paused,developer=ExplorationLog.Instance.PanelVisible});
 }
 public static async Task<string> Shot(string name){
  string file=Path.GetFullPath(Root+"/"+name+".png");
  ScreenCapture.CaptureScreenshot(file);await Task.Delay(450);
  return Json(new{file,exists=File.Exists(file),width=Screen.width,height=Screen.height,modal=DungeonUi.Modal});
 }
 public static object ViewInfo(){
  var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  return Json(new{position=view.position.ToString(),coordinates=new[]{"targetInParent","targetInView","viewInWindow","gameMouseOffset","gameMouseScale","zoomAreaScale"}.Select(n=>new{name=n,value=t.GetProperty(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(view)?.ToString()}).ToArray(),members=t.GetMembers(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name.ToLower().Contains("screen")||m.Name.ToLower().Contains("mouse")||m.Name.ToLower().Contains("target")||m.Name.ToLower().Contains("view")||m.Name.ToLower().Contains("zoom")).Select(m=>m.Name).Distinct().ToArray()});
 }

 public static async Task<string> Click(float x,float y){
  var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  var target=(Rect)t.GetProperty("targetInParent",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(view);
  var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var offset=(Vector2)t.GetProperty("gameMouseOffset",flags).GetValue(view);var scale=(float)t.GetProperty("gameMouseScale",flags).GetValue(view);
  var point=new Vector2(x/scale-offset.x,y/scale-offset.y);
  view.Focus();
  var before=Inventory.Instance.Equipped.DisplayName;
  view.SendEvent(new Event{type=EventType.MouseMove,mousePosition=point});
  bool acceptedDown=view.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=point});await Task.Delay(80);
  bool acceptedUp=view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=point});await Task.Delay(120);
  return Json(new{point=point.ToString(),acceptedDown,acceptedUp,before,after=Inventory.Instance.Equipped.DisplayName,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked});
 }

 public static object EventMethods()=>Json(typeof(UnityEditorInternal.InternalEditorUtility).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Where(m=>m.Name.Contains("Event")).Select(m=>m.ToString()).ToArray());
 public static async Task<string> ClickDirect(float x,float y){
  string before=Inventory.Instance.Equipped.DisplayName;
  var method=typeof(UnityEditorInternal.InternalEditorUtility).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).First(m=>m.Name=="SendEventToGameView");
  foreach(var type in new[]{EventType.MouseMove,EventType.MouseDown,EventType.MouseUp}){method.Invoke(null,new object[]{new Event{type=type,button=0,mousePosition=new Vector2(x,y)}});await Task.Delay(80);}
  return Json(new{before,after=Inventory.Instance.Equipped.DisplayName,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked});
 }
 public static object Probe(){var go=new GameObject("UI review input probe");go.AddComponent<UiInputProbe>();return "attached";}
 public static object OpenNote(){
  DungeonUi.Close(DungeonUi.Modal);
  var method=typeof(StoryItem).GetMethod("NoteText",BindingFlags.Instance|BindingFlags.NonPublic);
  var notes=UnityEngine.Object.FindObjectsByType<StoryItem>();
  var item=notes.OrderByDescending(n=>((string)method.Invoke(n,null)).Length).First();
  string text=(string)method.Invoke(item,null);DungeonUi.TryOpen(StoryItem.ModalName);
  typeof(StoryItem).GetField("_open",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(item,true);
  return Json(new{characters=text.Length,height=DungeonUi.Label.CalcHeight(new GUIContent(text),512),available=180});
 }
 public static async Task<string> EdgeCard(bool right){
  DungeonUi.Close(DungeonUi.Modal);TimeScaleService.Paused=false;
  var p=PlayerController.Instance;var pos=p.Position;
  LootDrop.Spawn(new WeaponItem(WeaponItem.GreatswordId,Grade.Legendary,10,1000),pos,pos,0);
  await Task.Delay(700);TimeScaleService.Paused=true;
  var cam=Camera.main;var follow=cam.GetComponent<DungeonCamera>();if(follow)follow.enabled=false;
  var shake=cam.GetComponent<ScreenShake>();if(shake)shake.enabled=false;
  float halfH=cam.orthographicSize,halfW=halfH*cam.aspect;
  cam.transform.position=new Vector3(pos.x+(right?-halfW+.15f:halfW-.15f),pos.y+(right?halfH-.5f:-halfH+.5f),cam.transform.position.z);
  await Task.Delay(100);return Json(new{right,card=Inventory.Instance.NearestDrop(Inventory.EquipRange)!=null});
 }
 public static object ResumeCamera(){var cam=Camera.main;var f=cam.GetComponent<DungeonCamera>();if(f){f.enabled=true;f.Snap();}var shake=cam.GetComponent<ScreenShake>();if(shake)shake.enabled=true;TimeScaleService.Paused=false;return "restored";}
 public static object Restore(){DungeonUi.Close(DungeonUi.Modal);Application.runInBackground=false;return "review flags restored";}
 public static object RestoreView(){
  var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  int index=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Root+"/game-view-original.json"))["index"].ToObject<int>();
  t.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);view.Repaint();return index;
 }

 public static async Task<string> MouseClick(float x,float y){
  InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(x,Screen.height-y),buttons=1});await Task.Delay(120);
  InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(x,Screen.height-y)});await Task.Delay(120);
  var selected=(WeaponItem)typeof(Inventory).GetField("_selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Inventory.Instance);
  return Json(new{selected=selected?.DisplayName,equipped=Inventory.Instance.Equipped.DisplayName,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked});
 }

 public static object SelectFixture(){var inv=Inventory.Instance;var item=inv.Bag[4];typeof(Inventory).GetField("_selected",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(inv,item);return item.DisplayName;}
 public static object EquipSelected(){var inv=Inventory.Instance;var item=(WeaponItem)typeof(Inventory).GetField("_selected",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(inv);var before=inv.Equipped;int count=inv.BagCount;inv.Equip(item);return Json(new{before=before.DisplayName,after=inv.Equipped.DisplayName,countBefore=count,countAfter=inv.BagCount,oldInBag=inv.Bag.Contains(before),blocked=PlayerInputReader.Blocked});}
 public static async Task<string> AttackGate(float x,float y){var input=PlayerController.Instance.GetComponent<PlayerInputReader>();InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(x,Screen.height-y),buttons=1});await Task.Delay(60);bool held=input.AttackHeld;bool developer=ExplorationLog.Instance.PointerOverPanel;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(x,Screen.height-y)});await Task.Delay(60);return Json(new{modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked,attackHeld=held,developer});}

 public static async Task<string> Suite720(){
  var results=new List<object>();
  results.Add(await KeyPress("I"));results.Add(await Shot("720-grid-reopened"));results.Add(await KeyPress("Escape"));
  results.Add(await KeyPress("K"));results.Add(await Shot("720-skills"));results.Add(await KeyPress("Escape"));
  results.Add(await KeyPress("M"));results.Add(await Shot("720-map"));results.Add(await KeyPress("Escape"));
  results.Add(OpenNote());results.Add(await Shot("720-long-note"));results.Add(await KeyPress("Escape"));
  results.Add(await KeyPress("F1"));results.Add(await Shot("720-developer"));results.Add(await AttackGate(1220,160));results.Add(await KeyPress("F1"));
  results.Add(await AttackGate(640,360));results.Add(await Shot("720-hud-final"));
  results.Add(await EdgeCard(true));results.Add(await Shot("720-card-right-edge"));results.Add(ResumeCamera());
  string json=Json(results);File.WriteAllText(Root+"/suite-720.json",json);return json;
 }
 public static async Task<string> Suite1080(){
  var results=new List<object>();results.Add(Resolution(1920,1080));await Task.Delay(500);
  results.Add(await KeyPress("I"));results.Add(await Shot("1080-grid-final"));results.Add(await KeyPress("Escape"));
  results.Add(await Shot("1080-hud-final"));results.Add(await EdgeCard(false));results.Add(await Shot("1080-card-left-edge"));results.Add(ResumeCamera());
  string json=Json(results);File.WriteAllText(Root+"/suite-1080.json",json);return json;
 }

 public static async Task<string> Final720(){
  await Ready();SeedBag();await KeyPress("I");SelectFixture();await Shot("720-grid-final");
  var state=EquipSelected();await KeyPress("Escape");await KeyPress("M");await Shot("720-map-final");await KeyPress("Escape");await KeyPress("K");await Shot("720-skills-final");await KeyPress("Escape");
  File.WriteAllText(Root+"/equipment-callback.json",(string)state);return (string)state;
 }

 public static object QueueMethods()=>Json(typeof(Event).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Where(m=>m.Name.Contains("Queue")||m.Name.Contains("Pop")).Select(m=>m.ToString()).ToArray());

 public static async Task<string> QueueClick(float x,float y){
  var queue=typeof(Event).GetMethod("QueueEvent",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static);
  foreach(var type in new[]{EventType.MouseMove,EventType.MouseDown,EventType.MouseUp}){queue.Invoke(null,new object[]{new Event{type=type,button=0,mousePosition=new Vector2(x,y)}});await Task.Delay(100);}
  var inv=Inventory.Instance;var selected=(WeaponItem)typeof(Inventory).GetField("_selected",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(inv);
  string result=Json(new{x,y,selected=selected?.DisplayName,equipped=inv.Equipped.DisplayName,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked});File.AppendAllText(Root+"/imgui-clicks.jsonl",result+"\n");return result;
 }

 public static async Task<string> VerifyVisuals(){
  var original=InputSystem.settings;var originalFlags=original.hideFlags;original.hideFlags=HideFlags.DontSave;
  var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.DontSave;InputSystem.settings=clone;clone.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;clone.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  try{await Ready();Resolution(1920,1080);await Task.Delay(400);if(Inventory.Instance.BagCount==0)SeedBag();DungeonUi.Close(DungeonUi.Modal);var results=new List<object>();
   results.Add(await KeyPress("I"));if(DungeonUi.Modal!="bag")throw new Exception("Bag did not open from keyboard");
   SelectFixture();results.Add(EquipSelected());results.Add(await Shot("1080-grid-final"));
   string oldName=Demo6.Core.Combat.WeaponPresets.Greatsword.displayName;
   try{Demo6.Core.Combat.WeaponPresets.Greatsword.displayName="오래된 검은 갱도의 수호자가 남긴 양손 대검";
    results.Add(await Shot("1080-long-item-name"));Resolution(1280,720);await Task.Delay(300);results.Add(await Shot("720-long-item-name"));
   }finally{Demo6.Core.Combat.WeaponPresets.Greatsword.displayName=oldName;}
   File.WriteAllText(Root+"/font-runtime.json",Json(new{title=DungeonUi.Title.font.fontNames,body=((Font)typeof(DungeonUi).GetField("_bodyFont",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).fontNames,titleSize=DungeonUi.Title.fontSize,bodySize=DungeonUi.Label.fontSize}));
   results.Add(await KeyPress("Escape"));
   results.Add(Resolution(1280,720));await Task.Delay(500);results.Add(await Suite720());results.Add(await Suite1080());
   File.WriteAllText(Root+"/visual-review.json",Json(results));return Json(results);
  }finally{InputSystem.settings=original;original.hideFlags=originalFlags;UnityEngine.Object.DestroyImmediate(clone);}
 }

 public static async Task<string> FinishTypography(){
  var original=InputSystem.settings;var originalFlags=original.hideFlags;original.hideFlags=HideFlags.DontSave;
  var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.DontSave;InputSystem.settings=clone;clone.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;clone.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var results=new List<object>();
  try{
   await Ready();DungeonUi.Close(DungeonUi.Modal);Resolution(1920,1080);await Task.Delay(300);await KeyPress("I");
   var inv=Inventory.Instance;var item=inv.Bag.Where(i=>i.WeaponId==WeaponItem.GreatswordId).OrderByDescending(i=>(int)i.Grade).First();
   typeof(Inventory).GetField("_selected",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(inv,item);results.Add(EquipSelected());
   await Shot("1080-grid-final");Resolution(1280,720);await Task.Delay(300);await Shot("720-grid-final");
   string oldName=Demo6.Core.Combat.WeaponPresets.Greatsword.displayName;
   try{Demo6.Core.Combat.WeaponPresets.Greatsword.displayName="오래된 검은 갱도의 수호자가 남긴 양손 대검";await Shot("720-long-item-name");Resolution(1920,1080);await Task.Delay(300);await Shot("1080-long-item-name");}
   finally{Demo6.Core.Combat.WeaponPresets.Greatsword.displayName=oldName;}
   results.Add(await KeyPress("Escape"));results.Add(await KeyPress("I"));results.Add(await KeyPress("Escape"));
   Resolution(1280,720);await Task.Delay(300);
   var stake=UnityEngine.Object.FindObjectsByType<Stake>().First();DungeonUi.TryOpen("stake");typeof(Stake).GetField("_menuOpen",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(stake,true);
   await Shot("720-stake-final");results.Add(await KeyPress("Escape"));
   var lunch=UnityEngine.Object.FindObjectsByType<LunchboxEvent>().FirstOrDefault();
   if(lunch){DungeonUi.TryOpen("lunchbox");var modal=(string)typeof(LunchboxEvent).GetField("ModalName",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetRawConstantValue();DungeonUi.Close(DungeonUi.Modal);DungeonUi.TryOpen(modal);typeof(LunchboxEvent).GetField("_open",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(lunch,true);await Shot("720-event-final");results.Add(await KeyPress("Escape"));}
   File.WriteAllText(Root+"/typography-final.json",Json(results));return Json(results);
  }finally{InputSystem.settings=original;original.hideFlags=originalFlags;UnityEngine.Object.DestroyImmediate(clone);}
 }

 public static async Task<string> EventFooterShot(){
  Application.runInBackground=true;Resolution(1280,720);await Task.Delay(500);
  var lunch=UnityEngine.Object.FindObjectsByType<LunchboxEvent>().First();DungeonUi.TryOpen(LunchboxEvent.ModalName);typeof(LunchboxEvent).GetField("_open",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(lunch,true);
  await Task.Delay(150);return await Shot("720-event-final");
 }

 public static async Task<string> ClickSuite(){await Ready();SeedBag();await KeyPress("I");var results=new List<string>{await QueueClick(564,516),await QueueClick(1300,822),await QueueClick(1470,948)};return Json(results);}

}

public class UiInputProbe:MonoBehaviour { void OnGUI(){if(Event.current.isMouse){File.AppendAllText("아트/UI-정리-v2/검수/input-events.txt",Event.current.type+" "+Event.current.mousePosition+" matrix="+GUI.matrix.m00+"\n");}}}
