using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object=UnityEngine.Object;

// One-zone read-only art preview. No AssetDatabase import or runtime asset-file writes.
public static class MapV34Preview
{
    const string Root="E:/personalProject/Demo3/demo6/";
    const string Ev=Root+"검증/맵-v034-기존조명/";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Need(bool b,string message){if(!b)throw new Exception(message);}
    static void Write(string n,object v)=>File.WriteAllText(Ev+n,JsonConvert.SerializeObject(v,Formatting.Indented));
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    static async Task Load(string name){var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(10);await Task.Delay(220);}
    static string Geometry(Collider2D[] cols)=>JsonConvert.SerializeObject(cols.Select(c=>new{c.name,type=c.GetType().Name,c.enabled,layer=c.gameObject.layer,offset=c.offset.ToString("F6"),position=c.transform.position.ToString("F6"),scale=c.transform.lossyScale.ToString("F6"),bounds=c.bounds.ToString("F6")}));
    static string LightState(Light2D[] lights)=>JsonConvert.SerializeObject(lights.Select(l=>new{l.name,l.enabled,l.intensity,color=l.color.ToString(),l.lightType,l.pointLightInnerRadius,l.pointLightOuterRadius,l.shadowsEnabled,l.shadowIntensity,position=l.transform.position.ToString("F6")}));
    static object RendererState(SpriteRenderer r)=>new{r.name,r.enabled,r.drawMode,r.tileMode,size=r.size.ToString("F6"),matrix=r.transform.localToWorldMatrix.ToString("F6"),material=r.sharedMaterial.name,shader=r.sharedMaterial.shader.name,color=r.color.ToString(),r.sortingLayerID,r.sortingOrder,layer=r.gameObject.layer};
    static string PostState(Camera cam)
    {
        var data=cam.GetUniversalAdditionalCameraData();
        return JsonConvert.SerializeObject(new{data.renderPostProcessing,mask=data.volumeLayerMask.value,data.renderType,data.antialiasing,data.dithering,cam.allowHDR,volumes=Object.FindObjectsByType<Volume>().Select(v=>new{v.name,v.enabled,v.isGlobal,v.weight,v.priority,profile=v.sharedProfile?EditorJsonUtility.ToJson(v.sharedProfile):null,components=v.sharedProfile?v.sharedProfile.components.Select(c=>EditorJsonUtility.ToJson(c)).ToArray():Array.Empty<string>()}).ToArray()});
    }
    static void Capture(Camera cam,string file,int width,int height)
    {
        var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;float oldAspect=cam.aspect;var rt=RenderTexture.GetTemporary(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.aspect=(float)width/height;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Ev+file,image.EncodeToPNG());}
        finally{cam.targetTexture=oldTarget;cam.aspect=oldAspect;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    public static async Task<object> Run()
    {
        Need(Application.dataPath.Replace("\\","/")==Root+"Assets","Original project only");
        var scene=SceneManager.GetActiveScene();Need(EditorApplication.isPlaying&&CombatTestRoot.Instance&&scene.name=="CombatTest"&&!scene.isDirty,"Preserve unexpected scene or unsaved state");
        Need(!EditorApplication.isPaused&&!PlayerInputReader.Blocked,"Preserve current pause/modal");
        var original=CombatTestRoot.Instance;int preset=(int)original.CurrentPreset,floor=original.Floor;string weapon=original.TestWeaponId;Vector2 playerAt=original.Player.Position;var hp=original.Player.Health.Current;var potions=original.Player.Potions;
        var tuning=typeof(Tuning).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>!f.IsLiteral&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string))).ToDictionary(f=>f,f=>f.GetValue(null));
        var carry=ProfileCarry.Data;var trip=ProfileCarry.Trip;var paused=EditorApplication.isPaused;var speed=TimeScaleService.GameSpeed;var timePaused=TimeScaleService.Paused;var volume=AudioListener.volume;var random=UnityEngine.Random.state;
        Write("runtime-before.json",new{scene=scene.path,preset,floor,weapon,playerAt=new[]{playerAt.x,playerAt.y},hp,potions,tuning=tuning.ToDictionary(p=>p.Key.Name,p=>p.Value),carry,trip,speed,timePaused,paused,volume,input=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray()});
        var bindings=JObject.Parse(File.ReadAllText(Root+"아트/맵-화풍정리/v034-dark-zone/preview-bindings.json"));
        var ownedArt=new List<UnityEngine.Object>();var actors=new List<Enemy>();var warnings=new List<Telegraph>();GameObject review=null,damage=null;SpriteRenderer floorRenderer=null,wallRenderer=null;Sprite oldFloor=null,oldWall=null;Camera cam=null;Vector3 cameraAt=default;Quaternion cameraRotation=default;float cameraSize=0,cameraAspect=0;Dictionary<Renderer,bool> veil=null;bool refsRestored=false;
        try
        {
            ProfileCarry.Install(CarryData.NewProfile(340034));ProfileCarry.SetTrip(new TripPlan{Floor=1,ForcedSeed=0,Arrival=ArrivalKind.FirstStart});AudioListener.volume=0;Tuning.Invincible=true;
            await Load("DungeonTest");var dungeon=DungeonRoot.Instance;Need(dungeon&&dungeon.Playing,"Original DungeonTest failed to start");await Task.Delay(160);
            EditorApplication.isPaused=true;foreach(var b in Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game"))b.enabled=false;foreach(var b in Object.FindObjectsByType<Rigidbody2D>())b.simulated=false;
            var cell=dungeon.World.Find("E");Need(cell!=null&&cell.Bounds.size==new Vector2(28,16),"Expected entrance E dimensions");Vector2 center=cell.Center;
            var sprites=Object.FindObjectsByType<SpriteRenderer>();
            floorRenderer=sprites.Single(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite)=="Assets/Resources/CuteV15/floor.png"&&Vector2.Distance(r.transform.position,center)<.01f);
            wallRenderer=sprites.Single(r=>r.sprite&&r.transform.parent&&r.transform.parent.name=="Wall Down"&&Vector2.Distance(r.transform.parent.position,center+Vector2.down*8)<.01f&&r.name=="Visual");
            oldFloor=floorRenderer.sprite;oldWall=wallRenderer.sprite;
            Need(AssetDatabase.GetAssetPath(oldWall)=="Assets/Resources/TopDownPilotV9/wall_h29.png","Expected original lower wall sprite");
            Need(floorRenderer.drawMode==SpriteDrawMode.Tiled&&floorRenderer.size==new Vector2(28,16)&&Mathf.Abs(oldFloor.pixelsPerUnit-118)<.001f,"Floor tiling mismatch");
            Need(oldWall.rect.width==3712&&oldWall.rect.height==180&&Mathf.Abs(oldWall.pixelsPerUnit-128)<.001f,"Wall dimensions/PPU mismatch");
            var otherSprites=sprites.Where(r=>r!=floorRenderer&&r!=wallRenderer).ToDictionary(r=>r,r=>r.sprite);
            var keepFloor=JsonConvert.SerializeObject(RendererState(floorRenderer));var keepWall=JsonConvert.SerializeObject(RendererState(wallRenderer));
            var collisions=Object.FindObjectsByType<Collider2D>();string geometry=Geometry(collisions);var lights=Object.FindObjectsByType<Light2D>();string lightState=LightState(lights);string glyphs=dungeon.Glyphs;ulong seed=dungeon.Seed;
            cam=Camera.main;Need(cam,"Actual game camera missing");cameraAt=cam.transform.position;cameraRotation=cam.transform.rotation;cameraSize=cam.orthographicSize;cameraAspect=cam.aspect;int width=cam.pixelWidth,height=cam.pixelHeight;
            string post=PostState(cam);Need(cam.GetUniversalAdditionalCameraData().renderPostProcessing,"Original dungeon postprocessing must stay enabled");Write("camera-light-before.json",new{camera=cam.name,width,height,cameraSize,position=cameraAt.ToString("F6"),post=JToken.Parse(post),lights=JToken.Parse(lightState),vision=new{dungeon.Vision.VisionOn,dungeon.Vision.ConeOn,dungeon.Vision.LookDeg,dungeon.Vision.CurrentViewRadius,dungeon.Vision.CurrentNearRadius}});
            Sprite ReadCandidate(string name,Sprite template)
            {
                var entry=bindings[name];var source=template.texture;var tex=new Texture2D(2,2,TextureFormat.RGBA32,source.mipmapCount>1,false){filterMode=source.filterMode,wrapMode=source.wrapMode,anisoLevel=source.anisoLevel,name="v034 preview "+name};
                Need(tex.LoadImage(File.ReadAllBytes((string)entry["file"])),"Candidate PNG failed: "+name);
                var s=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2((float)entry["pivot"][0],(float)entry["pivot"][1]),(float)entry["ppu"],0,SpriteMeshType.FullRect);s.name="v034 preview "+name;ownedArt.Add(s);ownedArt.Add(tex);return s;
            }
            var newFloor=ReadCandidate("floor",oldFloor);var newWall=ReadCandidate("wall",oldWall);var cracks=ReadCandidate("damage",oldFloor);
            Need(newFloor.bounds.size==oldFloor.bounds.size&&newWall.bounds.size==oldWall.bounds.size,"Candidate changes displayed bounds");
            review=new GameObject("v034 temporary visual review");damage=new GameObject("v034 two local fractures");damage.layer=floorRenderer.gameObject.layer;damage.transform.SetParent(floorRenderer.transform.parent,false);damage.transform.position=center+new Vector2(-8,-4);damage.transform.rotation=Quaternion.identity;
            var damageRenderer=damage.AddComponent<SpriteRenderer>();damageRenderer.sprite=cracks;damageRenderer.sharedMaterial=floorRenderer.sharedMaterial;damageRenderer.color=floorRenderer.color;damageRenderer.sortingLayerID=floorRenderer.sortingLayerID;damageRenderer.sortingOrder=floorRenderer.sortingOrder+1;damage.SetActive(false);
            Need((Vector2)damage.transform.position==center+new Vector2(-8,-4)&&Vector2.Distance(damageRenderer.bounds.size,new Vector2(8,4))<.001f,"Local damage world placement/scale mismatch");
            void SetCandidate(bool on){floorRenderer.sprite=on?newFloor:oldFloor;wallRenderer.sprite=on?newWall:oldWall;damage.SetActive(on);}
            var captureRandom=UnityEngine.Random.state;
            Capture(cam,"game-v032.png",width,height);SetCandidate(true);UnityEngine.Random.state=captureRandom;Capture(cam,"game-v034.png",width,height);
            Need(PostState(cam)==post&&LightState(lights)==lightState,"Lighting/exposure/postprocessing changed");
            // Additional contrast review: explicitly hide only visibility overlays; actual camera/Volume/lights stay on.
            SetCandidate(false);veil=Object.FindObjectsByType<Renderer>().Where(r=>r.name=="Vision veil"||r.name=="Vision memory fog").ToDictionary(r=>r,r=>r.enabled);foreach(var p in veil)p.Key.enabled=false;
            cam.transform.position=new Vector3(center.x,center.y,cameraAt.z);cam.orthographicSize=8;
            var art=dungeon.ProjectArt.topDown;Vector2 anchor=dungeon.Player.Position;var kinds=new[]{MonsterKind.Rat,MonsterKind.Boar,MonsterKind.Ogre};var places=new[]{anchor+new Vector2(0,2.6f),anchor+new Vector2(2.4f,1.2f),anchor+new Vector2(6,3)};
            for(int i=0;i<kinds.Length;i++)
            {
                var e=EnemySpawner.Create(kinds[i],2,places[i]);actors.Add(e);e.NoReward=true;e.transform.SetParent(review.transform,true);typeof(Enemy).GetField("Facing",F).SetValue(e,Vector2.down);
                foreach(var b in e.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;foreach(var c in e.GetComponentsInChildren<Collider2D>())c.enabled=false;e.GetComponent<Rigidbody2D>().simulated=false;
                if(e is OgreBrain){var rig=e.GetComponentInChildren<OgreArtRig>();Need(rig,"Approved Ogre rig missing");rig.Sample(0);}
                else{var rig=TopDownEnemyRig.Create(e);rig.Apply();rig.Drive(0,art);var fx=e.GetComponent<BeetlePatternVfx>();if(fx)fx.enabled=false;rig.Drive(0,art);}
                Call(e.GetComponent<YSort>(),"LateUpdate");
            }
            warnings.Add(Telegraph.Circle(anchor+new Vector2(0,1.8f),1.2f,1));warnings.Add(Telegraph.HalfDisc(anchor+new Vector2(6,1.2f),Vector2.down,3,1));warnings.Add(Telegraph.Rect(anchor+new Vector2(-4,-2.7f),Vector2.right,7.55f,1.2f,1));foreach(var t in warnings){t.transform.SetParent(review.transform,true);t.Drive(.7f);}
            captureRandom=UnityEngine.Random.state;Capture(cam,"contrast-v032-veil-hidden.png",1920,1080);SetCandidate(true);UnityEngine.Random.state=captureRandom;Capture(cam,"contrast-v034-veil-hidden.png",1920,1080);
            Need(PostState(cam)==post&&LightState(lights)==lightState,"Review changed original light/postprocessing");Need(Geometry(collisions)==geometry&&dungeon.Glyphs==glyphs&&dungeon.Seed==seed,"Gameplay geometry or map changed");
            Need(JsonConvert.SerializeObject(RendererState(floorRenderer))==keepFloor&&JsonConvert.SerializeObject(RendererState(wallRenderer))==keepWall,"Renderer settings changed beyond sprite references");Need(otherSprites.All(p=>p.Key&&p.Key.sprite==p.Value),"Another existing renderer sprite changed");
            SetCandidate(false);Object.DestroyImmediate(damage);damage=null;
            foreach(var p in veil)if(p.Key)p.Key.enabled=p.Value;cam.transform.position=cameraAt;cam.transform.rotation=cameraRotation;cam.orthographicSize=cameraSize;cam.aspect=cameraAspect;
            refsRestored=floorRenderer.sprite==oldFloor&&wallRenderer.sprite==oldWall;Need(refsRestored,"Preview references failed to restore");
            Write("preview-result.json",new{success=true,sharedAssetsWritten=false,baseline="Installed v032",candidate="v034 only E floor and E lower wall; one 8x4 transparent damage sprite",cell=cell.Id,cellCenter=center.ToString("F3"),floorRenderer=RendererState(floorRenderer),wallRenderer=RendererState(wallRenderer),width,height,actualCamera=cam.name,actualOriginalPostProcessing=true,exposure=DungeonPostFx.Exposure,contrast=DungeonPostFx.Contrast,saturation=DungeonPostFx.Saturation,vignette= dungeon.Atmosphere.PostFx.On?DungeonPostFx.VignetteIntensity:0,lightsUnchanged=true,postProcessingUnchanged=true,normalVisionPreservedForGamePair=true,artReview="Only Vision veil and Vision memory fog hidden; actual original camera/Volume/lights retained",otherSpriteReferencesUnchanged=true,collisionCount=collisions.Length,collisionsUnchanged=true,mapGenerationUnchanged=true,previewReferencesRestored=refsRestored,temporaryDamageRemoved=true});
            return new{success=true,files=new[]{"game-v032.png","game-v034.png","contrast-v032-veil-hidden.png","contrast-v034-veil-hidden.png"},sharedAssetsWritten=false,refsRestored,normalVisionPreserved=true,actualOriginalPostProcessing=true};
        }
        catch(Exception ex){Write("error.json",new{error=ex.ToString()});throw;}
        finally
        {
            if(floorRenderer&&oldFloor)floorRenderer.sprite=oldFloor;if(wallRenderer&&oldWall)wallRenderer.sprite=oldWall;if(damage)Object.DestroyImmediate(damage);
            if(veil!=null)foreach(var p in veil)if(p.Key)p.Key.enabled=p.Value;
            if(cam){cam.transform.position=cameraAt;cam.transform.rotation=cameraRotation;cam.orthographicSize=cameraSize;cam.aspect=cameraAspect;}
            foreach(var e in actors)if(e){if(e is OgreBrain)Call(e,"CancelAll");else if(e is BoarBrain)Call(e,"OnInterrupted");Object.Destroy(e.gameObject);}foreach(var t in warnings)if(t)t.Cancel();if(review)Object.Destroy(review);foreach(var a in ownedArt)if(a)Object.Destroy(a);
            EditorApplication.isPaused=false;ProfileCarry.Install(carry);ProfileCarry.SetTrip(trip);await Load("CombatTest");foreach(var p in tuning)p.Key.SetValue(null,p.Value);
            var restored=CombatTestRoot.Instance;if(restored){restored.SetFloor(floor);restored.SetTestWeapon(weapon);restored.ApplyPreset((CombatTestRoot.Preset)preset);restored.Player.Teleport(playerAt);restored.Player.RestoreVitals(hp,potions);}
            TimeScaleService.GameSpeed=speed;TimeScaleService.Paused=timePaused;AudioListener.volume=volume;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;
            if(Keyboard.current!=null&&Keyboard.current.native&&!Keyboard.current.enabled)InputSystem.EnableDevice(Keyboard.current);if(Mouse.current!=null&&Mouse.current.native&&!Mouse.current.enabled)InputSystem.EnableDevice(Mouse.current);
            var game=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView");if(game)game.Focus();
            Write("runtime-restored.json",new{EditorApplication.isPlaying,EditorApplication.isPaused,scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,preset=restored?restored.CurrentPreset.ToString():null,floor=restored?restored.Floor:0,weapon=restored?restored.TestWeaponId:null,profileReferenceRestored=ReferenceEquals(ProfileCarry.Data,carry)&&ReferenceEquals(ProfileCarry.Trip,trip),playerPosition=restored?restored.Player.Position.ToString("F5"):null,Time.captureDeltaTime,Time.fixedDeltaTime,TimeScaleService.GameSpeed,TimeScaleService.Paused,devices=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray()});
        }
    }
}
