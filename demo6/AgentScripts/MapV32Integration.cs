using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object=UnityEngine.Object;

public static class MapV32Integration
{
    const string Root="E:/personalProject/Demo3/demo6/";
    const string Ev=Root+"검증/맵-화풍정리-v032/";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string[] Paths={"Assets/Resources/CuteV15/floor.png","Assets/Resources/TopDownPilotV9/blocked_arch.png"};
    static void Need(bool yes,string why){if(!yes)throw new Exception(why);}
    static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void Write(string path,object data)=>File.WriteAllText(Ev+path,JsonConvert.SerializeObject(data,Formatting.Indented));
    static object Call(object o,string name,params object[] a)=>o.GetType().GetMethod(name,F).Invoke(o,a);
    static async Task Load(string name){var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(10);await Task.Delay(180);}
    static double Capture(Camera cam,string file,int w=1920,int h=1080)
    {
        var old=cam.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(w,h,24);var image=new Texture2D(w,h,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.aspect=(float)w/h;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();File.WriteAllBytes(Ev+file,image.EncodeToPNG());return image.GetPixels32().Average(c=>(c.r+c.g+c.b)/3.0);}
        finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static string Geometry(Collider2D[] col)=>JsonConvert.SerializeObject(col.Select(c=>new{c.name,type=c.GetType().Name,c.enabled,layer=c.gameObject.layer,offset=c.offset.ToString("F6"),position=c.transform.position.ToString("F6"),scale=c.transform.lossyScale.ToString("F6"),bounds=c.bounds.ToString("F6")}));
    static string Lights(Light2D[] ls)=>JsonConvert.SerializeObject(ls.Select(l=>new{l.name,l.enabled,l.intensity,color=l.color.ToString(),l.lightType,l.pointLightInnerRadius,l.pointLightOuterRadius,l.shadowsEnabled,l.shadowIntensity,pos=l.transform.position.ToString("F6")}));
    public static async Task<object> Run()
    {
        Need(Application.dataPath.Replace("\\","/")==Root+"Assets","Original project only");
        var scene=SceneManager.GetActiveScene();Need(EditorApplication.isPlaying&&CombatTestRoot.Instance&&scene.name=="CombatTest"&&!scene.isDirty,"Preserve unexpected or dirty scene");
        Need(!EditorApplication.isPaused&&!PlayerInputReader.Blocked,"Preserve current pause/modal");
        var original=CombatTestRoot.Instance;int preset=(int)original.CurrentPreset,floor=original.Floor;string weapon=original.TestWeaponId;Vector2 playerAt=original.Player.Position;var hp=original.Player.Health.Current;var potions=original.Player.Potions;
        var tuning=typeof(Tuning).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>!f.IsLiteral&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string))).ToDictionary(f=>f,f=>f.GetValue(null));
        var carry=ProfileCarry.Data;var trip=ProfileCarry.Trip;var paused=EditorApplication.isPaused;var gameSpeed=TimeScaleService.GameSpeed;var timePaused=TimeScaleService.Paused;var audio=AudioListener.volume;var random=UnityEngine.Random.state;
        var input=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray();
        var plan=JArray.Parse(File.ReadAllText(Root+"아트/맵-화풍정리/apply-map.json"));
        foreach(var p in plan){Need(Hash(Root+(string)p["source"])==(string)p["sourceSha256"],"Source changed");Need(Hash(Root+(string)p["target"])==(string)p["expectedTargetSha256"],"Target changed; preserve concurrent work");}
        var meta=Paths.ToDictionary(p=>p,p=>Hash(Root+p+".meta"));
        Write("runtime-before.json",new{scene=scene.path,preset,floor,weapon,playerAt=new[]{playerAt.x,playerAt.y},hp,potions,tuning=tuning.ToDictionary(p=>p.Key.Name,p=>p.Value),carry,trip,gameSpeed,timePaused,paused,audio,input});
        bool applied=false,success=false;GameObject review=null;var actors=new List<Enemy>();var telegraphs=new List<Telegraph>();
        try
        {
            ProfileCarry.Install(CarryData.NewProfile(320032));ProfileCarry.SetTrip(new TripPlan{Floor=1,ForcedSeed=0,Arrival=ArrivalKind.FirstStart});
            AudioListener.volume=0;Tuning.Invincible=true;await Load("DungeonTest");
            var dungeon=DungeonRoot.Instance;Need(dungeon&&dungeon.Floor==1&&dungeon.Playing,"Original dungeon failed to start");
            await Task.Delay(150);
            EditorApplication.isPaused=true;
            foreach(var b in Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game"))b.enabled=false;
            foreach(var b in Object.FindObjectsByType<Rigidbody2D>())b.simulated=false;
            var allSprites=Object.FindObjectsByType<SpriteRenderer>();
            var floors=allSprites.Where(s=>s.sprite&&AssetDatabase.GetAssetPath(s.sprite)==Paths[0]).ToArray();
            var arches=allSprites.Where(s=>s.sprite&&AssetDatabase.GetAssetPath(s.sprite)==Paths[1]).ToArray();
            Need(floors.Length>0&&arches.Length>0,"Actual floor/arch art not found in original dungeon");
            Need(floors.All(s=>s.drawMode==SpriteDrawMode.Tiled&&s.tileMode==SpriteTileMode.Continuous&&Mathf.Abs(s.sprite.pixelsPerUnit-118)<.001f),"Existing four-unit floor tiling is not active");
            var worldColliders=Object.FindObjectsByType<Collider2D>();string geometry=Geometry(worldColliders);string glyphs=dungeon.Glyphs;ulong seed=dungeon.Seed;
            var lights=Object.FindObjectsByType<Light2D>();string lightState=Lights(lights);
            review=new GameObject("Map v032 temporary review");
            var cameraGo=new GameObject("Map comparison camera");cameraGo.transform.SetParent(review.transform);var cam=cameraGo.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.enabled=false;cam.orthographicSize=DungeonCamera.Size;cam.transform.position=new Vector3(0,16,-10);cam.transform.rotation=Camera.main.transform.rotation;
            // Keep one unstaged, actual game camera view as evidence; comparison actors below are visual-only.
            var gamePosition=Camera.main.transform.position;cam.transform.position=gamePosition;Capture(cam,"map-before-game.png",Camera.main.pixelWidth,Camera.main.pixelHeight);
            cam.transform.position=new Vector3(0,16,-10);
            var art=dungeon.ProjectArt.topDown;
            var kinds=new[]{MonsterKind.Rat,MonsterKind.Boar,MonsterKind.Ogre};var places=new[]{new Vector2(-3,18),new Vector2(0,18),new Vector2(6,18)};
            for(int i=0;i<kinds.Length;i++)
            {
                var e=EnemySpawner.Create(kinds[i],2,places[i]);actors.Add(e);e.NoReward=true;e.transform.SetParent(review.transform,true);
                typeof(Enemy).GetField("Facing",F).SetValue(e,Vector2.down);
                foreach(var b in e.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var c in e.GetComponentsInChildren<Collider2D>())c.enabled=false;e.GetComponent<Rigidbody2D>().simulated=false;
                if(e is OgreBrain){var rig=e.GetComponentInChildren<OgreArtRig>();Need(rig,"Approved Ogre rig missing");rig.Sample(0);}
                else{var rig=TopDownEnemyRig.Create(e);rig.Apply();rig.Drive(0,art);var fx=e.GetComponent<BeetlePatternVfx>();if(fx)fx.enabled=false;rig.Drive(0,art);}
                Call(e.GetComponent<YSort>(),"LateUpdate");
            }
            telegraphs.Add(Telegraph.Circle(new Vector2(-3,16.5f),1.2f,1));telegraphs.Add(Telegraph.HalfDisc(new Vector2(6,16.5f),Vector2.down,3,1));telegraphs.Add(Telegraph.Rect(new Vector2(-9,11.3f),Vector2.right,7.55f,1.2f,1));
            foreach(var t in telegraphs){t.transform.SetParent(review.transform,true);t.Drive(.7f);}
            Capture(cam,"map-before-room.png");
            var arch=arches.OrderBy(s=>Vector2.Distance(s.transform.position,dungeon.Player.Position)).First();
            cam.transform.position=arch.transform.position+new Vector3(0,0,-10);cam.orthographicSize=3;Capture(cam,"arch-before-detail.png",960,640);
            applied=true;foreach(var p in plan){File.Copy(Root+(string)p["source"],Root+(string)p["target"],true);AssetDatabase.ImportAsset((string)p["target"],ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);}
            foreach(var p in plan)Need(Hash(Root+(string)p["target"])==(string)p["sourceSha256"],"Applied PNG mismatch");
            foreach(string p in Paths)Need(Hash(Root+p+".meta")==meta[p],"Importer/GUID changed");
            var floorSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Paths[0]);var archSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Paths[1]);
            Need(floorSprite.texture.width==472&&floorSprite.texture.height==472&&Mathf.Abs(floorSprite.bounds.size.x-4)<.0001f&&Mathf.Abs(floorSprite.pixelsPerUnit-118)<.0001f,"Floor period/scale changed");
            Need(archSprite.texture.width==640&&archSprite.texture.height==320&&Mathf.Abs(archSprite.pixelsPerUnit-128)<.0001f,"Arch import changed");
            Need(floors.All(s=>s.sprite==floorSprite)&&arches.All(s=>s.sprite==archSprite),"Live scene references did not refresh");
            Capture(cam,"arch-after-detail.png",960,640);
            cam.transform.position=new Vector3(0,16,-10);cam.orthographicSize=DungeonCamera.Size;Capture(cam,"map-after-room.png");
            // Existing original light sources, values and positions are used throughout both captures.
            Need(Lights(lights)==lightState,"Original lighting changed");Need(Geometry(worldColliders)==geometry,"Original collision geometry changed");Need(dungeon.Glyphs==glyphs&&dungeon.Seed==seed,"Map generation changed");
            foreach(var e in actors)if(e)e.gameObject.SetActive(false);foreach(var t in telegraphs)if(t)t.gameObject.SetActive(false);
            cam.transform.position=gamePosition;Capture(cam,"map-after-game.png",Camera.main.pixelWidth,Camera.main.pixelHeight);
            var lightCheck=LightCheck(cam,dungeon.Player.Position,floors[0],arch);
            var result=new{success=true,scene="Original DungeonTest Play",floor=dungeon.Floor,seed,glyphs,changedPNGs=Paths,metasUnchanged=true,liveFloorRenderers=floors.Length,liveArchRenderers=arches.Length,floorPixels=new[]{472,472},floorPpu=118,floorPeriod=4,archPixels=new[]{640,320},archPpu=128,colliderCount=worldColliders.Length,collisionGeometryUnchanged=true,lightingCount=lights.Length,lightingUnchanged=true,mapGenerationUnchanged=true,lightCheck,comparison="1920x1080 actual Unity render, camera size 8; existing lights frozen identically; three temporary approved actor models plus v031 warnings, no collision or AI; unstaged actual game views also saved"};Write("integration-result.json",result);success=true;return result;
        }
        catch(Exception ex)
        {
            Write("error.json",new{error=ex.ToString(),applied});
            if(applied&&!success)foreach(var p in plan)
            {
                string target=(string)p["target"];if(Hash(Root+target)!=(string)p["sourceSha256"])continue;
                File.Copy(Ev+"backup/"+Path.GetFileName(target),Root+target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            }
            throw;
        }
        finally
        {
            EditorApplication.isPaused=false;
            foreach(var e in actors)if(e){if(e is OgreBrain)Call(e,"CancelAll");else if(e is BoarBrain)Call(e,"OnInterrupted");Object.Destroy(e.gameObject);}
            if(review)Object.Destroy(review);foreach(var t in telegraphs)if(t)t.Cancel();
            ProfileCarry.Install(carry);ProfileCarry.SetTrip(trip);
            await Load("CombatTest");
            foreach(var p in tuning)p.Key.SetValue(null,p.Value);
            var restored=CombatTestRoot.Instance;
            if(restored){restored.SetFloor(floor);restored.SetTestWeapon(weapon);restored.ApplyPreset((CombatTestRoot.Preset)preset);restored.Player.Teleport(playerAt);restored.Player.RestoreVitals(hp,potions);}
            TimeScaleService.GameSpeed=gameSpeed;TimeScaleService.Paused=timePaused;AudioListener.volume=audio;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;
            if(Keyboard.current!=null&&Keyboard.current.native&&!Keyboard.current.enabled)InputSystem.EnableDevice(Keyboard.current);if(Mouse.current!=null&&Mouse.current.native&&!Mouse.current.enabled)InputSystem.EnableDevice(Mouse.current);
            var game=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView");if(game)game.Focus();
            Write("runtime-restored.json",new{EditorApplication.isPlaying,EditorApplication.isPaused,scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,preset=restored?restored.CurrentPreset.ToString():null,floor=restored?restored.Floor:0,weapon=restored?restored.TestWeaponId:null,profileReferenceRestored=ReferenceEquals(ProfileCarry.Data,carry)&&ReferenceEquals(ProfileCarry.Trip,trip),playerPosition=restored?restored.Player.Position.ToString("F5"):null,Time.captureDeltaTime,Time.fixedDeltaTime,TimeScaleService.GameSpeed,TimeScaleService.Paused,devices=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray()});
        }
    }
    static object LightCheck(Camera cam,Vector2 at,SpriteRenderer floor,SpriteRenderer arch)
    {
        var existing=Object.FindObjectsByType<Renderer>().ToDictionary(r=>r,r=>r.enabled);var samples=new List<GameObject>();
        try
        {
            foreach(var r in existing.Keys)r.enabled=false;
            var values=new List<object>();
            foreach(var source in new[]{floor,arch})
            {
                var go=new GameObject("Map installed texture light sample");samples.Add(go);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=source.sprite;sr.sharedMaterial=source.sharedMaterial;sr.color=source.color;sr.sortingLayerID=source.sortingLayerID;sr.sortingOrder=100;go.transform.position=at;
                cam.transform.position=(Vector3)at+Vector3.back*10;cam.orthographicSize=3;string name=source==floor?"floor":"arch";double lit=Capture(cam,name+"-existing-light.png",512,512);
                go.transform.position+=(Vector3)Vector2.right*100;cam.transform.position+=Vector3.right*100;double dark=Capture(cam,name+"-ambient-only.png",512,512);
                Need(lit>dark+1,"Existing lamp response missing: "+name);values.Add(new{name,lit,dark,material=sr.sharedMaterial.shader.name});go.SetActive(false);
            }
            return values;
        }
        finally{foreach(var g in samples)if(g)Object.DestroyImmediate(g);foreach(var p in existing)if(p.Key)p.Key.enabled=p.Value;}
    }
}
