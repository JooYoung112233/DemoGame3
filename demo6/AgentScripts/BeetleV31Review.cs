using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo6.Core.Combat;
using Demo6.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Newtonsoft.Json;
using Object=UnityEngine.Object;

public static class BeetleV31Review
{
    const string Ev="E:/personalProject/Demo3/demo6/검증/돌갑충-예고-v031";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const float Dt=.025f;
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    static void Set(object o,Type t,string n,object v)=>t.GetField(n,F).SetValue(o,v);
    static string State(BoarBrain b)=>typeof(BoarBrain).GetField("_state",F).GetValue(b).ToString();
    static void Require(bool b,string message){if(!b)throw new Exception(message);}
    static void Write(string n,object o)=>File.WriteAllText(Ev+"/"+n,JsonConvert.SerializeObject(o,Formatting.Indented));
    static async Task Step(){int f=Time.frameCount;EditorApplication.Step();var end=DateTime.UtcNow.AddSeconds(5);while(f==Time.frameCount){if(DateTime.UtcNow>end)throw new Exception("Step timeout");await Task.Delay(5);}}
    static void Shot(Camera cam,RenderTexture rt,Texture2D tex,string path)
    {cam.Render();var previous=RenderTexture.active;try{RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{RenderTexture.active=previous;}}
    static void OwnTelegraphs(Transform root)
    {foreach(var t in Object.FindObjectsByType<Telegraph>())if(t.Origin.x>90&&t.Origin.x<120){t.transform.SetParent(root,true);foreach(var sr in t.GetComponentsInChildren<SpriteRenderer>())sr.gameObject.layer=31;}}
    public static async Task<object> Run()
    {
        Require(EditorApplication.isPlaying&&CombatTestRoot.Instance&&!DungeonRoot.Instance,"Original CombatTest Play required");
        var input=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray();
        Require(Keyboard.current!=null&&Keyboard.current.native&&Mouse.current!=null&&Mouse.current.native,"Native hardware required");
        var paused=EditorApplication.isPaused;var cap=Time.captureDeltaTime;var fixedDt=Time.fixedDeltaTime;var scale=Time.timeScale;var volume=AudioListener.volume;var random=UnityEngine.Random.state;var sim=Physics2D.simulationMode;bool above=Telegraph.AboveDark;
        var behaviours=Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game").ToDictionary(b=>b,b=>b.enabled);
        var bodies=Object.FindObjectsByType<Rigidbody2D>().ToDictionary(b=>b,b=>b.simulated);
        var lights=Object.FindObjectsByType<Light2D>().ToDictionary(l=>l,l=>l.enabled);
        var player=CombatTestRoot.Instance.Player;var playerAt=player.Position;var invincible=Tuning.Invincible;
        float gamePpu=Camera.main.pixelHeight/(Camera.main.orthographicSize*2);
        GameObject root=null;RenderTexture rt=null;Texture2D tex=null;Texture2D grid=null;Sprite gridSprite=null;BoarBrain boar=null;Camera cam=null;var results=new List<object>();
        try
        {
            EditorApplication.isPaused=true;Time.captureDeltaTime=Dt;Time.fixedDeltaTime=Dt;Time.timeScale=1;AudioListener.volume=0;Tuning.Invincible=true;Telegraph.AboveDark=false;Physics2D.simulationMode=SimulationMode2D.Script;
            foreach(var b in behaviours.Keys)b.enabled=false;foreach(var b in bodies.Keys)b.simulated=false;foreach(var l in lights.Keys)l.enabled=false;
            root=new GameObject("v031 review owned");
            cam=new GameObject("Review camera").AddComponent<Camera>();cam.transform.SetParent(root.transform);cam.enabled=false;cam.orthographic=true;cam.orthographicSize=2.2f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.115f,.11f);cam.cullingMask=1<<31;
            rt=new RenderTexture(640,640,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;tex=new Texture2D(640,640,TextureFormat.RGB24,false);
            var light=new GameObject("Review neutral light").AddComponent<Light2D>();light.transform.SetParent(root.transform);light.gameObject.layer=31;light.lightType=Light2D.LightType.Global;light.intensity=1;
            grid=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=(x<2||y<2)?new Color(.20f,.19f,.17f):new Color(.155f,.15f,.14f);grid.SetPixels(pixels);grid.Apply();grid.wrapMode=TextureWrapMode.Repeat;
            gridSprite=Sprite.Create(grid,new Rect(0,0,64,64),Vector2.one*.5f,64,0,SpriteMeshType.FullRect);
            var ground=new GameObject("Review ground").AddComponent<SpriteRenderer>();ground.transform.SetParent(root.transform);ground.gameObject.layer=31;ground.sprite=gridSprite;ground.drawMode=SpriteDrawMode.Tiled;ground.size=new Vector2(50,20);ground.transform.position=new Vector3(104,0,0);ground.sortingOrder=-1000;RenderMaterials.MakeUnlit(ground);
            var art=AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset").topDown;
            foreach(string clip in new[]{"charge-toes","wall-contact","double-charge","walk","interrupted","death"})
            {
                StrongAttackSchedule.ResetStatics();AttackTokens.ResetStatics();
                boar=(BoarBrain)EnemySpawner.Create(MonsterKind.Boar,clip=="double-charge"?8:2,new Vector2(100,0));boar.NoReward=true;boar.transform.SetParent(root.transform,true);
                foreach(var b in boar.GetComponents<MonoBehaviour>())b.enabled=false;
                var rb=boar.GetComponent<Rigidbody2D>();rb.interpolation=RigidbodyInterpolation2D.None;rb.simulated=true;
                Set(boar,typeof(Enemy),"Facing",Vector2.right);Set(boar,typeof(BoarBrain),"_cooldown",1000f);
                var rig=TopDownEnemyRig.Create(boar);rig.Apply();rig.Drive(0,art);var fx=boar.GetComponent<BeetlePatternVfx>();Require(fx,"Toe/VFX component missing");fx.enabled=false;
                // OnDisable restores the full source; the same production Pose call below reactivates it.
                var sr=boar.GetComponent<SpriteFlash>().target;player.Teleport(new Vector2(104,2));
                GameObject wall=null;
                if(clip=="wall-contact")
                {
                    wall=new GameObject("Review actual wall collider");wall.transform.SetParent(root.transform);wall.layer=Layers.Wall;wall.transform.position=new Vector3(103.2f,0,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.6f,4f);
                    var w=new GameObject("Wall stone").AddComponent<SpriteRenderer>();w.transform.SetParent(wall.transform,false);w.gameObject.layer=31;w.sprite=ShapeSprites.Square;w.color=new Color(.28f,.29f,.30f);w.transform.localScale=new Vector3(.6f,4,1);w.sortingOrder=900;RenderMaterials.MakeUnlit(w);
                }
                if(clip!="walk")Call(boar,"BeginChargeTelegraph",Vector2.right,.7f);
                string dir=Ev+"/frames/"+clip;Directory.CreateDirectory(dir);var trace=new List<object>();int saved=0,charges=0;bool wasCharging=false,interrupted=false,killed=false;float maxToe=0;int frames=0;float maxTravel=0;bool contact=false;
                for(int frame=0;frame<190;frame++)
                {
                    if(!boar)break;string state=State(boar);
                    if(state=="Charging"&&!wasCharging)charges++;wasCharging=state=="Charging";
                    if(clip=="interrupted"&&state=="Charging"&&!interrupted){Call(boar,"ForceBreak");interrupted=true;}
                    if(clip=="death"&&state=="Charging"&&!killed){boar.Health.ApplyDamage(boar.Health.Max+1,false);killed=true;}
                    if(!boar.Dead&&!boar.Broken)Call(boar,"Think",Dt);
                    rb.linearVelocity=boar.Dead||boar.Broken?Vector2.zero:(Vector2)typeof(Enemy).GetField("DesiredVelocity",F).GetValue(boar);
                    Physics2D.SyncTransforms();Physics2D.Simulate(Dt);
                    Call(boar.GetComponent<YSort>(),"LateUpdate");rig.Drive(Dt,art);fx.Advance(Dt);
                    foreach(var renderer in boar.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=31;
                    OwnTelegraphs(root.transform);
                    Require(Mathf.Abs(boar.GetComponent<CircleCollider2D>().radius-.55f*(boar.SizeScale))<.0001f,"Collider changed");
                    var legacy=boar.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Leg");Require(legacy.All(r=>!r.enabled||!r.gameObject.activeInHierarchy),"Duplicate legacy feet");
                    bool take=clip=="wall-contact"?fx.WallImpactCount>0:clip=="walk"?frame>=8:clip=="interrupted"?interrupted:clip=="death"?killed:State(boar)=="Charging"||saved>0;
                    cam.transform.position=boar.transform.position+new Vector3(.65f,0,-10);
                    if(take&&saved<10){Shot(cam,rt,tex,dir+"/"+saved.ToString("D2")+".png");saved++;}
                    maxToe=Mathf.Max(maxToe,Mathf.Abs(fx.ToeAngle));maxTravel=Mathf.Max(maxTravel,boar.Position.x-100);contact|=fx.WallImpactCount>0;
                    trace.Add(new{frame,time=frame*Dt,unityFrame=Time.frameCount,state=State(boar),boar.Broken,boar.Dead,x=boar.Position.x,fx.DustCount,fx.WallImpactCount,fx.ActiveCount,fx.ToeAngle,saved});frames++;
                    if(clip=="walk"&&saved==10)break;
                    if(clip=="interrupted"&&frame>50)break;
                    if(clip=="wall-contact"&&contact&&fx.ActiveCount==0&&saved==10)break;
                    if((clip=="charge-toes"||clip=="double-charge")&&State(boar)=="Walk"&&saved==10&&fx.ActiveCount==0)break;
                    await Step();
                }
                Require(saved==10,"Ten preview frames required: "+clip);
                if(clip=="wall-contact")Require(contact&&fx.WallImpactCount==1&&boar.Broken&&Mathf.Abs(fx.LastContact.x-102.9f)<.04f,"Actual wall contact must emit once at collider surface");
                if(clip=="charge-toes")Require(fx.DustCount>10&&maxToe>3&&maxTravel>=6.9f&&maxTravel<7.3f,"Charge dust/pose/travel failed");
                if(clip=="double-charge")Require(charges==2&&fx.WallImpactCount==0&&fx.DustCount>20,"Repeated charge cleanup failed");
                if(clip=="interrupted")Require(fx.ActiveCount==0&&Mathf.Abs(fx.ToeAngle)<.01f,"Interrupted effects/toes retained");
                if(boar&&boar.Dead)Require(sr.sprite==art.boar.body,"Death must restore full approved source");
                results.Add(new{clip,frames,previewFrames=saved,charges,maxToe,maxTravel,wallImpactCount=fx?fx.WallImpactCount:0,dustCount=fx?fx.DustCount:0,activeAtEnd=fx?fx.ActiveCount:0});Write("frames/"+clip+"/trace.json",trace);
                if(boar){Call(boar,"OnInterrupted");Object.Destroy(boar.gameObject);}if(wall)Object.Destroy(wall);OwnTelegraphs(root.transform);foreach(var t in root.GetComponentsInChildren<Telegraph>())t.Cancel();await Step();boar=null;
            }
            cam.orthographicSize=640/(2*gamePpu);cam.transform.position=new Vector3(103.5f,0,-10);
            var telegraphs=new List<object>();
            foreach(string shape in new[]{"circle","half","rect"})
            {
                var at=new Vector2(shape=="rect"?99.7f:103.4f,0);
                Telegraph Make()=>shape=="circle"?Telegraph.Circle(at,2.4f,1f):shape=="half"?Telegraph.HalfDisc(at,new Vector2(.9f,.45f),3,1):Telegraph.Rect(at,new Vector2(1,.12f),7.55f,1.2f,1);
                var t=Make();OwnTelegraphs(root.transform);t.Drive(.62f);
                bool[] contained=new[]{at,at+new Vector2(1,0),at+new Vector2(10,0),at+new Vector2(0,10)}.Select(p=>t.Contains(p,0)).ToArray();
                var outline=t.transform.Find("Outline").GetComponent<SpriteRenderer>();var fill=t.transform.Find("Fill").GetComponent<SpriteRenderer>();var edge=t.transform.Find("Edge").GetComponent<SpriteRenderer>();var rim=t.transform.Find("Inner rim").GetComponent<SpriteRenderer>();
                Require(outline.sprite.name==shape+"-outline"&&rim&&rim.sprite.name==shape+"-rim","Final native masks missing");
                if(shape=="rect"){Require(outline.drawMode==SpriteDrawMode.Sliced,"Rect corner quality requires sliced border");Require(Mathf.Abs(outline.size.x-7.55f)<.0001f&&Mathf.Abs(outline.size.y-1.2f)<.0001f,"Rect display bounds changed");}
                Shot(cam,rt,tex,Ev+"/"+shape+"-after-game-scale.png");
                var originalOutline=outline.sprite;var originalFill=fill.sprite;var oldHalf=(Sprite)typeof(Telegraph).GetMethod("HalfDiscSprite",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                outline.sprite=fill.sprite=shape=="circle"?ShapeSprites.Circle:shape=="half"?oldHalf:ShapeSprites.Square;outline.color=Palette.TelegraphOutline;fill.color=Palette.TelegraphFill;edge.enabled=false;rim.enabled=false;
                if(shape=="rect"){outline.drawMode=fill.drawMode=SpriteDrawMode.Simple;outline.transform.localScale=new Vector3(7.55f,1.2f,1);fill.transform.localScale=new Vector3(7.55f*.62f,1.2f,1);}
                Shot(cam,rt,tex,Ev+"/"+shape+"-before-game-scale.png");
                t.Cancel();await Step();t=Make();OwnTelegraphs(root.transform);
                string dir=Ev+"/frames/telegraph-"+shape;Directory.CreateDirectory(dir);
                for(int i=0;i<10;i++){t.Drive(.775f+i*Dt);Shot(cam,rt,tex,dir+"/"+i.ToString("D2")+".png");await Step();}
                Require(t.Done,"Fill must reach boundary at duration");var pos=t.Origin;var direction=t.Direction;t.Lock();t.SetRect(at+Vector2.one*10,Vector2.left);Require(t.Origin==pos&&t.Direction==direction,"Lock semantics changed");
                t.Resolve();Require(t.Done,"Resolve timing changed");t.Cancel();await Step();
                telegraphs.Add(new{shape,previewFrames=10,gamePixelsPerUnit=gamePpu,contains=contained,lockPassed=true});
            }
            var report=new{success=true,method="Original BoarBrain + real Physics2D collision callbacks; explicit 40Hz editor steps; no input injection",clips=results,telegraphs,inputBefore=input,inputAfter=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray()};Write("review-result.json",report);return report;
        }
        catch(Exception ex){File.WriteAllText(Ev+"/review-error.txt",ex.ToString());throw;}
        finally
        {
            if(boar)Call(boar,"OnInterrupted");if(root){OwnTelegraphs(root.transform);Object.Destroy(root);}
            if(cam)cam.targetTexture=null;RenderTexture.active=null;if(rt){rt.Release();Object.Destroy(rt);}if(tex)Object.Destroy(tex);if(gridSprite)Object.Destroy(gridSprite);if(grid)Object.Destroy(grid);
            player.Teleport(playerAt);foreach(var p in behaviours)if(p.Key)p.Key.enabled=p.Value;foreach(var p in bodies)if(p.Key)p.Key.simulated=p.Value;foreach(var p in lights)if(p.Key)p.Key.enabled=p.Value;
            Physics2D.simulationMode=sim;Telegraph.AboveDark=above;Time.captureDeltaTime=cap;Time.fixedDeltaTime=fixedDt;Time.timeScale=scale;AudioListener.volume=volume;Tuning.Invincible=invincible;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;
            Write("cleanup.json",new{complete=true,Physics2D.simulationMode,Time.captureDeltaTime,Time.fixedDeltaTime,EditorApplication.isPaused,virtualDevices=InputSystem.devices.Count(d=>!d.native)});
        }
    }
}
