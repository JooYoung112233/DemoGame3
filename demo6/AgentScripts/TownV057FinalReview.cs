using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using Demo6.Game;using Demo6.Core.Town;using Newtonsoft.Json;using Object=UnityEngine.Object;
public static class TownV057FinalReview
{
 const string Out="검증/town-npc-v057/final";
 static float[] V(Vector2 v)=>new[]{v.x,v.y};
 static void Guard(){if(!Application.isPlaying||GameSession.FromTitle||!TownRoot.Instance||!Demo6.Game.PlayerController.Instance)throw new Exception("Unsaved live original Town required");Directory.CreateDirectory(Out);}
 static void Capture(string name){TownRoot.Instance.CameraRig.Snap();var c=Camera.main;var old=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24);var t=new Texture2D(1920,1080,TextureFormat.RGB24,false);try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1920,1080),0,0);t.Apply();File.WriteAllBytes(Out+"/"+name+".png",t.EncodeToPNG());}finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(t);}}
 static void Write(string name,object data)=>File.WriteAllText(Out+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));
 static object Layers()=>Object.FindObjectsByType<SpriteRenderer>().Where(s=>s.name.Contains("floor-wood")||s.name.Contains("Approved idle")||s.name.Contains("shadow")||s.name=="wall-straight"||s.name.Contains("roof-slate")).Select(s=>new{name=s.name,position=V(s.transform.position),order=s.sortingOrder,layer=s.sortingLayerName,material=s.sharedMaterial?s.sharedMaterial.name:null,group=s.GetComponentInParent<UnityEngine.Rendering.SortingGroup>()?.name}).ToArray();
 public static async Task<object> Movement()
 {
  Guard();TalkDirector.Skip(TownScript.Opening,0,ProfileCarry.Ensure(),TownRoot.QuestCtx);
  var p=Demo6.Game.PlayerController.Instance;var initial=p.Position;var originalKb=Keyboard.current;var kb=InputSystem.AddDevice<Keyboard>("TownV057ReviewKeyboard");bool bg=Application.runInBackground;var rows=new List<object>();
  try{Application.runInBackground=true;
   var cases=new[]{new{n="smith-in",a=new Vector2(31.5f,39),b=new Vector2(31.5f,43),block=false},new{n="smith-out",a=new Vector2(31.5f,43),b=new Vector2(31.5f,39),block=false},new{n="shop-in",a=new Vector2(32.5f,36),b=new Vector2(32.5f,32.2f),block=false},new{n="shop-out",a=new Vector2(32.5f,32.2f),b=new Vector2(32.5f,36),block=false},new{n="house-in",a=new Vector2(49.5f,34),b=new Vector2(49.5f,31.2f),block=false},new{n="house-out",a=new Vector2(49.5f,31.2f),b=new Vector2(49.5f,34),block=false},new{n="anvil-approach",a=new Vector2(35.65f,40.7f),b=new Vector2(35.65f,42.3f),block=false},new{n="smith-wall-stop",a=new Vector2(30,41.4f),b=new Vector2(30,38.9f),block=true}};
   foreach(var q in cases){p.Teleport(q.a);await Task.Delay(120);var key=q.b.y>q.a.y?Key.W:Key.S;var dir=(q.b-q.a).normalized;var samples=new List<object>();float started=Time.realtimeSinceStartup;InputSystem.QueueStateEvent(kb,new KeyboardState(key));bool mid=false;
    while(Time.realtimeSinceStartup-started<2f){await Task.Delay(35);if(DungeonUi.ModalOpen)throw new Exception("Unexpected modal "+DungeonUi.Modal);samples.Add(new{t=Time.realtimeSinceStartup-started,p=V(p.Position),pose=p.Pose.ToString()});if(!mid&&Time.realtimeSinceStartup-started>.4f){Capture(q.n+"-moving");mid=true;}if(Vector2.Dot(q.b-p.Position,dir)<.1f)break;}
    InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(180);Capture(q.n+"-end");float distance=Vector2.Distance(p.Position,q.b);bool pass=q.block?p.Position.y>40.7f:distance<.45f;rows.Add(new{name=q.n,start=V(q.a),target=V(q.b),end=V(p.Position),passed=pass,expectedWallStop=q.block,samples});
   }
  }finally{InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.RemoveDevice(kb);originalKb?.MakeCurrent();p.Teleport(initial);TownRoot.Instance.CameraRig.Snap();Application.runInBackground=bg;}
  var result=new{utc=DateTime.UtcNow,method="Temporary synthetic keyboard; actual PlayerController movement and Physics2D collisions. Teleport used only to seed each independent route. Not physical user input.",rows,saveSession=GameSession.FromTitle,layers=Layers(),nativeKeyboard=Keyboard.current?.native,temporaryDevices=InputSystem.devices.Where(d=>d.name.StartsWith("TownV057Review")).Select(d=>d.name).ToArray()};Write("movement",result);return result;
 }
 public static async Task<object> Overlap()
 {
  Guard();var p=Demo6.Game.PlayerController.Instance;var initial=p.Position;var originalMouse=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>("TownV057ReviewMouse");var rows=new List<object>();var input=p.GetComponent<PlayerInputReader>();var map=(InputActionMap)typeof(PlayerInputReader).GetField("_map",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input);var devices=map.devices;map.devices=new InputDevice[]{mouse};
  try{
   foreach(var npc in Object.FindObjectsByType<TownNpcIdleV057>())foreach(float dy in new[]{.65f,-.65f}){
    p.Teleport((Vector2)npc.transform.position+new Vector2(0,dy));TownRoot.Instance.CameraRig.Snap();await Task.Delay(120);
    foreach(int direction in new[]{0,1,2,3,4,5,6,7}){float a=direction*Mathf.PI/4;var screen=Camera.main.WorldToScreenPoint(p.transform.position+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*3);InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});await Task.Delay(80);string name=npc.Role+(dy>0?"-behind-":"-front-")+direction;Capture(name);rows.Add(new{name,player=V(p.Position),facing=V(p.FacingDirection),npcOrder=npc.GetComponent<SpriteRenderer>().sortingOrder,playerOrders=p.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled&&s.sprite).Select(s=>new{s.name,s.sortingOrder}).ToArray()});}
   }
   foreach(var q in new[]{new{n="smith-back-roof",p=new Vector2(31.5f,46.2f)},new{n="smith-front-wall",p=new Vector2(30,39.55f)},new{n="smith-behind-wall",p=new Vector2(30,40.82f)},new{n="shop-roof",p=new Vector2(34,29.85f)},new{n="wood-shadow",p=new Vector2(31.5f,43)}}){p.Teleport(q.p);await Task.Delay(150);Capture(q.n);}
  }finally{map.devices=devices;InputSystem.RemoveDevice(mouse);originalMouse?.MakeCurrent();p.Teleport(initial);TownRoot.Instance.CameraRig.Snap();}
  var result=new{utc=DateTime.UtcNow,method="Actual original camera/lighting, seeded overlap poses and temporary pointer for eight facing directions; unchanged gameplay scale",resolution=new[]{Screen.width,Screen.height},ortho=Camera.main.orthographicSize,rows,layers=Layers(),nativeMouse=Mouse.current?.native};Write("overlap",result);return new{captures=rows.Count+5,nativeMouse=Mouse.current?.native};
 }
 public static async Task<object> Shadow()
 {
  Guard();var p=Demo6.Game.PlayerController.Instance;var initial=p.Position;SpriteRenderer[] shadows=null;bool[] enabled=null;
  try{p.Teleport(new Vector2(31.5f,43));await Task.Delay(180);shadows=p.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled&&s.sortingOrder==-980).ToArray();enabled=shadows.Select(s=>s.enabled).ToArray();Capture("shadow-on");foreach(var s in shadows)s.enabled=false;Capture("shadow-off");var a=new Texture2D(2,2);var b=new Texture2D(2,2);a.LoadImage(File.ReadAllBytes(Out+"/shadow-on.png"));b.LoadImage(File.ReadAllBytes(Out+"/shadow-off.png"));var x=a.GetPixels32();var y=b.GetPixels32();int count=0;for(int i=0;i<x.Length;i++)if(!x[i].Equals(y[i]))count++;Object.DestroyImmediate(a);Object.DestroyImmediate(b);var result=new{shadowRenderers=shadows.Select(s=>s.name).ToArray(),changedPixels=count,passed=shadows.Length>0&&count>0,floorOrder=-985,shadowOrder=-980,method="Same-frame original camera render with player ground shadow enabled/disabled; renderer states restored"};Write("shadow",result);return result;}
  finally{if(shadows!=null)for(int i=0;i<shadows.Length;i++)shadows[i].enabled=enabled[i];p.Teleport(initial);TownRoot.Instance.CameraRig.Snap();}
 }
 public static async Task<object> Interaction()
 {
  Guard();var p=Demo6.Game.PlayerController.Instance;var initial=p.Position;var native=Keyboard.current;var kb=InputSystem.AddDevice<Keyboard>("TownV057ReviewKeyboard");var rows=new List<object>();
  try{foreach(var q in new[]{new{n="blacksmith",p=new Vector2(37,39)},new{n="anvil",p=new Vector2(35.65f,41.8f)}}){p.Teleport(q.p);await Task.Delay(250);var it=Demo6.Game.InteractionSystem.Instance.Current;string target=it?it.name:null;InputSystem.QueueStateEvent(kb,new KeyboardState(Key.F));await Task.Delay(150);InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(150);rows.Add(new{name=q.n,target,modal=DungeonUi.Modal,talkNpc=TalkWindow.AnyOpen?TalkWindow.Instance.NpcId:null,opened=DungeonUi.ModalOpen});ScreenCapture.CaptureScreenshot(Path.GetFullPath(Out+"/interaction-"+q.n+".png"));await Task.Delay(120);if(TalkWindow.AnyOpen)TalkWindow.Instance.SkipAll();else{InputSystem.QueueStateEvent(kb,new KeyboardState(Key.Escape));await Task.Delay(100);InputSystem.QueueStateEvent(kb,new KeyboardState());await Task.Delay(100);}}}
  finally{InputSystem.RemoveDevice(kb);native?.MakeCurrent();p.Teleport(initial);TownRoot.Instance.CameraRig.Snap();}Write("interaction",rows);return rows;
 }
 public static async Task<object> Idle()
 {
  Guard();var p=Demo6.Game.PlayerController.Instance;var initial=p.Position;var rows=new List<object>();try{p.Teleport(new Vector2(40.5f,37));for(int i=0;i<21;i++){await Task.Delay(90);rows.Add(new{t=Time.unscaledTime,npcs=Object.FindObjectsByType<TownNpcIdleV057>().Select(n=>new{n.Role,n.CurrentFrame,p=V(n.transform.position),scale=V(n.transform.localScale)}).ToArray()});if(i<10)Capture("idle-"+i.ToString("00"));}}finally{p.Teleport(initial);TownRoot.Instance.CameraRig.Snap();}Write("idle",rows);return new{samples=rows.Count};
 }
}
