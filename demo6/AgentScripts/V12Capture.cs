using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using UnityEngine.Rendering.Universal;using Demo6.Game;
public static class V12Capture {
 const string Root="E:/personalProject/Demo3/demo6/검증/승인형상-대표샘플-v12";
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


 const string Art="E:/personalProject/Demo3/demo6/아트/승인형상-대표샘플-v12/PNG/";
 static Texture2D Load(string id){var t=new Texture2D(2,2,TextureFormat.RGBA32,true);t.LoadImage(File.ReadAllBytes(Art+id+".png"));t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Clamp;return t;}
 public static async Task<object> Capture(){
  Guard();if(System.Diagnostics.Process.GetCurrentProcess().Id!=int.Parse(File.ReadAllText(Root+"/review-pid.txt")))throw new Exception("Owned sample session only");
  var p=PlayerController.Instance;var origin=p.Position;bool paused=EditorApplication.isPaused;var rows=new List<object>();
  var groundTex=Load("connected-bedrock");var heroTex=Load("hero-approved-shape");Sprite ground=null,hero=null;GameObject study=null;
  try{using(var input=new InputScope()){
   Application.runInBackground=true;input.Aim=Vector2.up;
   foreach(int height in new[]{720,1080})using(var size=new SizeScope(height==720?1280:1920,height)){
    int width=height==720?1280:1920;EditorApplication.isPaused=false;p.Teleport(new Vector2(-5,20));Camera.main.GetComponent<DungeonCamera>().Snap();await Task.Delay(1600);
    EditorApplication.isPaused=true;int frame=Time.frameCount;string lights=LightState(),walls=WallState();
    var floor=UnityEngine.Object.FindObjectsByType<SpriteRenderer>().Single(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite).EndsWith("TopDownPilotV9/entry_floor.png")&&r.bounds.Contains(new Vector3(-5,20,r.bounds.center.z)));
    var oldFloor=floor.sprite;var oldBounds=floor.bounds;
    var renderers=p.GetComponentsInChildren<Renderer>().Where(r=>r.name!="Shadow").ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();
    var body=p.GetComponent<SpriteRenderer>();if(!body)body=p.GetComponentsInChildren<SpriteRenderer>().First(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite).Contains("body_leather"));
    string folder=Root+"/sample/"+height;Directory.CreateDirectory(folder);
    Shot(folder+"/before-world.png",width,height);ScreenCapture.CaptureScreenshot(folder+"/before-game.png");await Task.Delay(500);
    try{
     if(!ground)ground=Sprite.Create(groundTex,new Rect((groundTex.width-groundTex.height*1.75f)*.5f,0,groundTex.height*1.75f,groundTex.height),new Vector2(.5f,.5f),groundTex.height/16f,0,SpriteMeshType.FullRect);
     floor.sprite=ground;
     float ppu=8f*1080f/(2f*Camera.main.orthographicSize);
     if(!hero)hero=Sprite.Create(heroTex,new Rect(0,0,heroTex.width,heroTex.height),new Vector2(472f/1280f,772f/1536f),ppu,0,SpriteMeshType.FullRect);
     study=new GameObject("V12 static design sample - never saved");study.transform.position=p.transform.position;var sr=study.AddComponent<SpriteRenderer>();sr.sprite=hero;sr.sharedMaterial=body.sharedMaterial;sr.color=body.color;sr.sortingLayerID=body.sortingLayerID;sr.sortingOrder=body.sortingOrder+5;
     for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
     Shot(folder+"/sample-world.png",width,height);ScreenCapture.CaptureScreenshot(folder+"/sample-game.png");await Task.Delay(600);
     rows.Add(new{height,frameStart=frame,frameEnd=Time.frameCount,pose=p.Pose.ToString(),position=p.Position.ToString(),lightsIdentical=lights==LightState(),collidersIdentical=walls==WallState(),floorBoundsBefore=oldBounds.ToString(),floorBoundsAfter=floor.bounds.ToString(),floorBoundsIdentical=oldBounds==floor.bounds,heroPpu=ppu,cameraSize=Camera.main.orthographicSize,staticArtProxy=true,originalGameFilesModified=false});
    }finally{
     floor.sprite=oldFloor;for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];if(study)UnityEngine.Object.DestroyImmediate(study);study=null;
    }
    EditorApplication.isPaused=false;
   }
  }}finally{
   p.Teleport(origin);Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=paused;
   if(study)UnityEngine.Object.DestroyImmediate(study);if(ground)UnityEngine.Object.DestroyImmediate(ground);if(hero)UnityEngine.Object.DestroyImmediate(hero);UnityEngine.Object.DestroyImmediate(groundTex);UnityEngine.Object.DestroyImmediate(heroTex);
  }
  File.WriteAllText(Root+"/sample/capture-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows;
 }
}
