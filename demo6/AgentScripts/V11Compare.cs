using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using UnityEngine.Rendering.Universal;using Demo6.Game;
public static class V11Compare {
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

 const string Art="E:/personalProject/Demo3/demo6/아트/화풍조화-교정-v11/runtime-PNG/";
 static readonly string[] Changed={"body_leather","helm_leather","short_cape","entry_floor","floor_variant_1","floor_variant_2","wall_h29","wall_v17","wall_v6a","wall_v6b"};
 static Texture2D Load(string id){var t=new Texture2D(2,2,TextureFormat.RGBA32,true);t.LoadImage(File.ReadAllBytes(Art+id+".png"));t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Clamp;t.name="V11 preview "+id;return t;}
 static Sprite From(Sprite old,Texture2D texture){var s=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(old.pivot.x/old.rect.width,old.pivot.y/old.rect.height),old.pixelsPerUnit,0,SpriteMeshType.FullRect);s.name="V11 preview";return s;}
 static Sprite Rock(SpriteRenderer renderer,Texture2D field){var rect=renderer.bounds;int w=renderer.sprite.texture.width,h=renderer.sprite.texture.height;var src=field.GetPixels32();var pixels=new Color32[w*h];for(int y=0;y<h;y++){int sy=Mathf.FloorToInt(Mathf.Repeat(rect.min.y+(y+.5f)/80f,12f)/12f*field.height);for(int x=0;x<w;x++){int sx=Mathf.FloorToInt(Mathf.Repeat(rect.min.x+(x+.5f)/80f,12f)/12f*field.width);var c=src[sy*field.width+sx];c.a=255;pixels[y*w+x]=c;}}var tex=new Texture2D(w,h,TextureFormat.RGBA32,true);tex.SetPixels32(pixels);tex.Apply();tex.filterMode=FilterMode.Trilinear;tex.wrapMode=TextureWrapMode.Clamp;return From(renderer.sprite,tex);}
 public static async Task<object> First(){return await Capture(false);}
 public static async Task<object> All(){return await Capture(true);}
 static async Task<object> Capture(bool all){
  Guard();var rows=new List<object>();var errors=new List<string>();var p=PlayerController.Instance;var origin=p.Position;bool paused=EditorApplication.isPaused,inv=Tuning.Invincible;Tuning.Invincible=true;
  var textures=Changed.ToDictionary(id=>id,id=>Load(id));var rocks=Load("rock_field");var cache=new Dictionary<string,Sprite>();var rockCache=new Dictionary<SpriteRenderer,Sprite>();
  Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=log;
  try{using(var input=new InputScope()){Application.runInBackground=true;
   foreach(int height in all?new[]{720,1080}:new[]{1080})using(var size=new SizeScope(height==720?1280:1920,height)){
    int width=height==720?1280:1920;EditorApplication.isPaused=false;await Task.Delay(1000);
    var spots=all?new[]{new{name="entry",at=new Vector2(-5,20)},new{name="c1",at=DungeonRoot.Instance.World.Cells.First(c=>c.Id=="c1").Center}}:new[]{new{name="entry",at=new Vector2(-5,20)}};
    foreach(var spot in spots)foreach(string action in all?new[]{"idle","walk","attack"}:new[]{"idle"}){
     input.Keys=Array.Empty<Key>();input.Buttons=0;p.Teleport(spot.at);input.Aim=Vector2.up;Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=false;await Task.Delay(750);
     if(action=="walk")input.Keys=new[]{Key.W};if(action=="attack")input.Buttons=1;
     await Task.Delay(action=="idle"?100:135);EditorApplication.isPaused=true;
     string lights=LightState(),walls=WallState();int frame=Time.frameCount;var before=new List<(SpriteRenderer sr,Sprite sprite)>();
     var cape=p.GetComponentInChildren<TopDownCape>().GetComponent<MeshRenderer>();var oldBlock=new MaterialPropertyBlock();cape.GetPropertyBlock(oldBlock);var newBlock=new MaterialPropertyBlock();cape.GetPropertyBlock(newBlock);
     string folder=Root+"/paired/"+height;Directory.CreateDirectory(folder);string prefix=folder+"/"+spot.name+"-"+action;
     Shot(prefix+"-before.png",width,height);
     try{
      foreach(var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>()){
       if(!sr.sprite||!sr.enabled)continue;var file=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sr.sprite));Sprite next=null;
       if(textures.TryGetValue(file,out var texture)){if(!cache.TryGetValue(file,out next)){next=From(sr.sprite,texture);cache[file]=next;}}
       else if(sr.sprite.name=="Dungeon wall"&&GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main),sr.bounds)){
        if(!rockCache.TryGetValue(sr,out next)){next=Rock(sr,rocks);rockCache[sr]=next;}
       }
       if(next){before.Add((sr,sr.sprite));sr.sprite=next;}
      }
      newBlock.SetTexture("_MainTex",textures["short_cape"]);cape.SetPropertyBlock(newBlock);
      Shot(prefix+"-after.png",width,height);
      rows.Add(new{spot=spot.name,action,height,pose=p.Pose.ToString(),position=p.Position.ToString(),facing=p.FacingDirection.ToString(),frameStart=frame,frameEnd=Time.frameCount,lightsIdentical=lights==LightState(),collidersIdentical=walls==WallState(),swaps=before.Count,cameraSize=Camera.main.orthographicSize});
     }finally{foreach(var x in before)x.sr.sprite=x.sprite;cape.SetPropertyBlock(oldBlock);}
     EditorApplication.isPaused=false;input.Keys=Array.Empty<Key>();input.Buttons=0;await Task.Delay(400);
    }
   }
  }}finally{
   p.Teleport(origin);Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=paused;Tuning.Invincible=inv;Application.logMessageReceived-=log;
   foreach(var s in cache.Values)UnityEngine.Object.DestroyImmediate(s);
   foreach(var s in rockCache.Values){UnityEngine.Object.DestroyImmediate(s.texture);UnityEngine.Object.DestroyImmediate(s);}
   foreach(var t in textures.Values)UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rocks);
  }
  var result=new{rows,errors};File.WriteAllText(Root+(all?"/paired/checks.json":"/paired/first-checks.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
