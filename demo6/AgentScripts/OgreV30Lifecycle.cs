using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Threading.Tasks;using UnityEditor;using UnityEngine;using UnityEngine.InputSystem;using Demo6.Game;using Demo6.Core.Combat;using Object=UnityEngine.Object;
public static class OgreV30Lifecycle {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 static void Require(bool b,string m){if(!b)throw new Exception(m);}
 public static async Task<object> Run(){
  Require(EditorApplication.isPlaying&&CombatTestRoot.Instance,"Original CombatTest Play required");
  var before=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray();
  var scripts=Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game").ToDictionary(b=>b,b=>b.enabled);
  bool pause=EditorApplication.isPaused;float volume=AudioListener.volume,shake=Tuning.ShakeScale;var random=UnityEngine.Random.state;
  OgreBrain o=null;GameObject pool=null;List<Sprite> sprites=null;OgreArtRig rig=null;bool auto=false,disabled=false,paused=false;
  try{
   foreach(var b in scripts.Keys)b.enabled=false;AudioListener.volume=0;Tuning.ShakeScale=0;EditorApplication.isPaused=false;
   o=(OgreBrain)EnemySpawner.Create(MonsterKind.Ogre,2,new Vector2(100,0));o.NoReward=true;
   typeof(Enemy).GetProperty("Aware",F).SetValue(o,true);typeof(Enemy).GetField("Facing",F).SetValue(o,Vector2.down);o.GetComponent<Rigidbody2D>().simulated=false;
   var fx=o.GetComponent<OgrePatternVfx>();rig=o.GetComponentInChildren<OgreArtRig>();
   pool=(GameObject)typeof(OgrePatternVfx).GetField("poolRoot",F).GetValue(fx);sprites=new List<Sprite>((List<Sprite>)typeof(OgrePatternVfx).GetField("owned",F).GetValue(fx));
   typeof(OgreBrain).GetMethod("BeginSlam",F).Invoke(o,new object[]{Vector2.down*2});
   var until=DateTime.UtcNow.AddSeconds(4);while(fx.SlamCount==0&&DateTime.UtcNow<until)await Task.Delay(20);
   auto=fx.SlamCount==1&&rig.enabled&&fx.enabled;Require(auto,"Normal Update/LateUpdate failed to emit slam: state="+typeof(OgreBrain).GetField("_state",F).GetValue(o)+", timer="+typeof(OgreBrain).GetField("_timer",F).GetValue(o)+", windup="+typeof(OgreBrain).GetField("_windup",F).GetValue(o)+", timescale="+Time.timeScale+", playerDown="+CombatTestRoot.Instance.Player.IsDown+", frame="+Time.frameCount);
   EditorApplication.isPaused=true;
   fx.Clear();fx.Slam(o.Position);fx.Advance(0);Require(fx.ActiveCount>0,"Hit-stop hid the impact's first frame");
   var renderers=pool.GetComponentsInChildren<SpriteRenderer>();var frames=renderers.Select(r=>r.sprite).ToArray();fx.Advance(0);
   paused=renderers.Select(r=>r.sprite).SequenceEqual(frames);Require(paused,"Paused VFX advanced");
   fx.enabled=false;disabled=fx.ActiveCount==0;Require(disabled,"Disabled component left effects alive");
   var texture=Resources.Load<Texture2D>("OgreVfxV30/slam");
   Object.Destroy(o.gameObject);o=null;EditorApplication.isPaused=false;await Task.Delay(100);
   Require(!pool&&sprites.All(s=>!s)&&texture,"Owned effects leaked or shared texture destroyed");
   var after=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray();
   File.WriteAllText("E:/personalProject/Demo3/demo6/검증/오우거-패턴-v030/input-during-async.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{before,after,Application.isFocused,Application.runInBackground,background=InputSystem.settings.backgroundBehavior.ToString(),keyboard=Keyboard.current?.deviceId,mouse=Mouse.current?.deviceId},Newtonsoft.Json.Formatting.Indented));
   Require(Keyboard.current!=null&&Keyboard.current.native&&Mouse.current!=null&&Mouse.current.native&&!InputSystem.devices.Any(d=>!d.native),"Unexpected virtual/current device during probe");
   var c=Camera.main;var result=new{success=true,normalUpdate=auto,pauseFreeze=paused,disableClears=disabled,ownedSpritesDestroyed=sprites.Count,poolDestroyed=!pool,sharedAtlasPreserved=(bool)texture,camera=new{c.pixelWidth,c.pixelHeight,c.orthographicSize,pixelsPerUnit=c.pixelHeight/(2*c.orthographicSize)},inputBefore=before,inputAfter=after};
   File.WriteAllText("E:/personalProject/Demo3/demo6/검증/오우거-패턴-v030/lifecycle.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
  }finally{if(o){typeof(OgreBrain).GetMethod("CancelAll",F).Invoke(o,null);Object.Destroy(o.gameObject);}if(rig)Object.Destroy(rig.gameObject);foreach(var b in scripts)if(b.Key)b.Key.enabled=b.Value;AudioListener.volume=volume;Tuning.ShakeScale=shake;UnityEngine.Random.state=random;EditorApplication.isPaused=pause;}
 }
 public static object ClearReviewTelegraph(){var owned=Object.FindObjectsByType<Telegraph>().Where(t=>Vector2.Distance(t.Origin,new Vector2(100,-1.8f))<.01f).ToArray();foreach(var t in owned)t.Cancel();return new{cleared=owned.Length,position="(100,-1.8) outside original room; owned by lifecycle probe"};}
}
