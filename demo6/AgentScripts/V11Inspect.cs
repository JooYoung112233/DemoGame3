using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using UnityEngine.Rendering.Universal;using Demo6.Game;
public static class V11Inspect {
 const string Root="E:/personalProject/Demo3/demo6/검증/화풍조화-교정-v11";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Guard(){if(!Application.dataPath.Contains("topdown-v11-harmony-review")||!EditorApplication.isPlaying||!DungeonRoot.Instance||TopDownView.PlayerRig==null||!TopDownView.PlayerRig.Applied)throw new Exception("Owned isolated Dungeon Play required");}
 sealed class InputScope:IDisposable {
  public Vector2 Aim=Vector2.up;public Key[] Keys=Array.Empty<Key>();public ushort Buttons;
  readonly InputActionMap actionMap; readonly UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices; readonly Keyboard keyboard;readonly Mouse mouse;readonly List<InputDevice> enabled=new List<InputDevice>();readonly InputSettings settings;readonly InputSettings.BackgroundBehavior bg;readonly InputSettings.EditorInputBehaviorInPlayMode editor;
  public InputScope(){settings=InputSystem.settings;bg=settings.backgroundBehavior;editor=settings.editorInputBehaviorInPlayMode;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;foreach(var d in InputSystem.devices.ToArray())if((d is Keyboard||d is Mouse)&&d.enabled){enabled.Add(d);InputSystem.DisableDevice(d);}keyboard=InputSystem.AddDevice<Keyboard>("V10ReviewKeyboard");mouse=InputSystem.AddDevice<Mouse>("V10ReviewMouse");actionMap=(InputActionMap)typeof(PlayerInputReader).GetField("_map",F).GetValue(PlayerController.Instance.GetComponent<PlayerInputReader>());previousDevices=actionMap.devices;actionMap.devices=new InputDevice[]{keyboard,mouse};InputSystem.onBeforeUpdate+=Feed;}
  void Feed(){if(!PlayerController.Instance||!Camera.main)return;Vector2 cursor=Camera.main.WorldToScreenPoint(PlayerController.Instance.transform.position+(Vector3)(Aim*4));InputSystem.QueueStateEvent(keyboard,new KeyboardState(Keys));InputSystem.QueueStateEvent(mouse,new MouseState{position=cursor,buttons=Buttons});}
  public void Dispose(){InputSystem.onBeforeUpdate-=Feed;actionMap.devices=previousDevices;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);foreach(var d in enabled)if(d.added)InputSystem.EnableDevice(d);settings.backgroundBehavior=bg;settings.editorInputBehaviorInPlayMode=editor;}
 }
 sealed class SizeScope:IDisposable {
  readonly Type gt;readonly EditorWindow view;readonly PropertyInfo index;readonly object group,entry;readonly int previous;
  public SizeScope(int w,int h){var ass=typeof(Editor).Assembly;gt=ass.GetType("UnityEditor.GameView");view=Resources.FindObjectsOfTypeAll(gt).Cast<EditorWindow>().FirstOrDefault()??EditorWindow.GetWindow(gt);index=gt.GetProperty("selectedSizeIndex",F);previous=(int)index.GetValue(view);var sizes=typeof(ScriptableSingleton<>).MakeGenericType(ass.GetType("UnityEditor.GameViewSizes")).GetProperty("instance").GetValue(null);group=sizes.GetType().GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});entry=Activator.CreateInstance(ass.GetType("UnityEditor.GameViewSize"),new object[]{Enum.ToObject(ass.GetType("UnityEditor.GameViewSizeType"),1),w,h,"V9 temporary paired check"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{entry});index.SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();}
  public void Dispose(){index.SetValue(view,previous);foreach(var f in group.GetType().GetFields(F))if(f.GetValue(group)is IList list&&!list.IsReadOnly)for(int i=list.Count-1;i>=0;i--)if(ReferenceEquals(list[i],entry))list.RemoveAt(i);view.Repaint();}
 }
 static void Shot(string file,int width,int height){var c=Camera.main;var old=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);Texture2D tex=null;try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());}finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);}}
 static string LightState()=>string.Join("|",UnityEngine.Object.FindObjectsByType<Light2D>().OrderBy(l=>l.GetEntityId().ToString()).Select(l=>l.GetEntityId()+":"+l.transform.position+":"+l.intensity+":"+l.color));
 static string WallState()=>string.Join("|",UnityEngine.Object.FindObjectsByType<Collider2D>().Where(c=>c.gameObject.layer==Layers.Wall).OrderBy(c=>c.GetEntityId().ToString()).Select(c=>c.GetEntityId()+":"+c.transform.position+":"+c.bounds));


 public static async Task<object> Capture(){
  Guard();var p=PlayerController.Instance;var old=p.Position;bool paused=EditorApplication.isPaused;var rows=new List<object>();
  try{using(var input=new InputScope())using(var size=new SizeScope(1920,1080)){
   Application.runInBackground=true;EditorApplication.isPaused=false;input.Aim=Vector2.up;await Task.Delay(1200);
   foreach(var spot in new[]{new{name="entry",at=new Vector2(-6,16)},new{name="wall",at=new Vector2(-5,21)}}){
    p.Teleport(spot.at);Camera.main.GetComponent<DungeonCamera>().Snap();await Task.Delay(900);
    string dir=Root+"/diagnosis";Directory.CreateDirectory(dir);Shot(dir+"/current-"+spot.name+"-world.png",1920,1080);
    ScreenCapture.CaptureScreenshot(dir+"/current-"+spot.name+"-game.png");await Task.Delay(500);
    rows.Add(new{spot=spot.name,pose=p.Pose.ToString(),position=p.Position.ToString(),camera=Camera.main.orthographicSize});
   }
  }}finally{p.Teleport(old);Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=paused;}
  File.WriteAllText(Root+"/diagnosis/capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows;
 }
}
