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

public static class BeetleOriginalReview
{
    const string Evidence="E:/personalProject/Demo3/demo6/검증/돌갑충-큰색면-v028";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const float Dt=.025f;
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    static void Set(object o,Type t,string n,object v)=>t.GetField(n,F).SetValue(o,v);
    static string State(BoarBrain b)=>typeof(BoarBrain).GetField("_state",F).GetValue(b).ToString();
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Write(string name,object data)=>File.WriteAllText(Evidence+"/"+name,JsonConvert.SerializeObject(data,Formatting.Indented));
    static async Task Step(){int frame=Time.frameCount;EditorApplication.Step();var until=DateTime.UtcNow.AddSeconds(5);while(Time.frameCount==frame){if(DateTime.UtcNow>until)throw new Exception("Step timeout");await Task.Delay(5);}}
    public static async Task<object> Run()
    {
        Require(EditorApplication.isPlaying&&CombatTestRoot.Instance&&!DungeonRoot.Instance,"Original CombatTest required");
        var devices=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray();
        var pause=EditorApplication.isPaused;var capture=Time.captureDeltaTime;var fixedDt=Time.fixedDeltaTime;var scale=Time.timeScale;var audio=AudioListener.volume;var invincible=Tuning.Invincible;
        var initial=new HashSet<GameObject>(Object.FindObjectsByType<GameObject>());
        var behaviours=Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game").ToDictionary(b=>b,b=>b.enabled);
        var bodies=Object.FindObjectsByType<Rigidbody2D>().ToDictionary(b=>b,b=>b.simulated);
        var lights=Object.FindObjectsByType<Light2D>().ToDictionary(b=>b,b=>b.enabled);
        var player=CombatTestRoot.Instance.Player;var playerPos=player.Position;
        Camera camera=null;RenderTexture rt=null;Texture2D shot=null;BoarBrain boar=null;
        var results=new List<object>();
        try
        {
            EditorApplication.isPaused=true;Time.captureDeltaTime=Dt;Time.fixedDeltaTime=Dt;Time.timeScale=1;AudioListener.volume=0;Tuning.Invincible=true;
            foreach(var b in behaviours.Keys)b.enabled=false;foreach(var b in bodies.Keys)b.simulated=false;foreach(var b in lights.Keys)b.enabled=false;
            camera=new GameObject("Beetle review camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.20f,.22f);camera.cullingMask=1<<31;
            rt=new RenderTexture(640,640,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;shot=new Texture2D(640,640,TextureFormat.RGB24,false);
            var light=new GameObject("Beetle review light").AddComponent<Light2D>();light.gameObject.layer=31;light.lightType=Light2D.LightType.Global;light.intensity=1;light.color=Color.white;
            var art=AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset").topDown;
            foreach(string clip in new[]{"walk","charge","headbutt","rear-attack","broken","death"})
            {
                StrongAttackSchedule.ResetStatics();AttackTokens.ResetStatics();
                boar=(BoarBrain)EnemySpawner.Create(MonsterKind.Boar,2,new Vector2(100,0));boar.NoReward=true;
                Require(boar.IsV3,"Expected current V3 rules");
                Set(boar,typeof(Enemy),"Facing",Vector2.right);Set(boar,typeof(BoarBrain),"_cooldown",clip=="headbutt"?0f:1000f);
                foreach(var b in boar.GetComponents<MonoBehaviour>())b.enabled=false;
                var body=boar.GetComponent<Rigidbody2D>();body.simulated=false;
                var flash=boar.GetComponent<SpriteFlash>();var sr=flash.target;
                var rig=TopDownEnemyRig.Create(boar);rig.Apply();
                player.Teleport(new Vector2(clip=="headbutt"?101.5f:clip=="rear-attack"?98.5f:104,0));
                if(clip=="charge"||clip=="broken"||clip=="death")Call(boar,"BeginChargeTelegraph",Vector2.right,.7f);
                if(clip=="rear-attack")Call(boar,"BeginKick");
                string dir=Evidence+"/frames/"+clip;Directory.CreateDirectory(dir);
                var trace=new List<object>();int saved=0;bool broke=false,killed=false;bool returned=false;float travel=0;float startX=boar.Position.x;var states=new HashSet<string>();
                for(int frame=0;frame<160;frame++)
                {
                    float elapsed=frame*Dt;string state=State(boar);
                    if(clip=="broken"&&!broke&&state=="Charging"){Call(boar,"EndCharge",true);broke=true;}
                    if(clip=="death"&&!killed&&state=="Charging"){boar.Health.ApplyDamage(boar.Health.Max+1,false);killed=true;}
                    if(!boar.Broken&&!killed)Call(boar,"Think",Dt);
                    if(!killed)body.position+=(Vector2)typeof(Enemy).GetField("DesiredVelocity",F).GetValue(boar)*Dt;
                    rig.Drive(Dt,art);
                    foreach(var t in boar.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                    var legs=boar.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Leg").ToArray();
                    Require(legs.Length==4&&legs.All(r=>!r.enabled||!r.gameObject.activeInHierarchy),"Duplicate feet visible");
                    Require(sr.sprite==art.boar.body,"Beetle sprite not used");
                    Require(Mathf.Abs(boar.GetComponent<CircleCollider2D>().radius-.55f)<.0001f,"Collision radius changed");
                    camera.transform.position=boar.transform.position+new Vector3(0,0,-10);
                    bool take=clip=="walk"?frame>=0:clip=="charge"?State(boar)=="Charging"||saved>0:clip=="broken"?broke:clip=="death"?killed:elapsed>=.275f;
                    if(take&&saved<10){Capture(camera,rt,shot,dir+"/"+saved.ToString("D2")+".png");saved++;}
                    states.Add(State(boar));travel=Mathf.Max(travel,boar.Position.x-startX);
                    trace.Add(new{frame,t=elapsed,unityFrame=Time.frameCount,state=State(boar),pose=boar.CurrentPose.ToString(),boar.Broken,boar.Dead,x=boar.Position.x,saved,scale=sr.transform.localScale.ToString("F5"),offset=sr.transform.localPosition.ToString("F5"),rotation=sr.transform.localEulerAngles.z,visibleExtraFeet=legs.Count(r=>r.enabled&&r.gameObject.activeInHierarchy)});
                    returned=elapsed>.5f&&State(boar)=="Walk";
                    if(saved==10&&((clip=="walk"&&elapsed>.5f)||(clip=="charge"&&returned)||(clip=="headbutt"&&returned)||(clip=="rear-attack"&&returned)||broke||killed))break;
                    await Step();
                }
                Require(saved==10,"Expected 10 preview frames");
                if(clip=="charge")Require(states.Contains("Charging")&&returned&&travel>=6.9f&&travel<7.2f,"Charge timing/length mismatch");
                if(clip=="headbutt")Require(states.Contains("HeadbuttWindup")&&states.Contains("HeadbuttRecover")&&returned,"Headbutt path failed");
                if(clip=="rear-attack")Require(states.Contains("KickWindup")&&states.Contains("KickRecover")&&returned,"Rear attack path failed");
                if(clip=="broken")Require(boar.Broken,"Wall break path failed");
                if(clip=="death")Require(boar.Dead,"Death path failed");
                if(clip=="walk")
                {
                    flash.StopAll();flash.Blink(.4f);Call(flash,"Apply");rig.Drive(0,art);Require(Mathf.Abs(sr.color.a-.35f)<.001f,"Blink alpha changed");flash.StopAll();rig.Drive(0,art);
                    Capture(camera,rt,shot,Evidence+"/light-full.png");double full=shot.GetPixels32().Average(c=>(c.r+c.g+c.b)/3.0);light.intensity=.2f;
                    Capture(camera,rt,shot,Evidence+"/light-dim.png");double dim=shot.GetPixels32().Average(c=>(c.r+c.g+c.b)/3.0);light.intensity=1;
                    Require(full>dim+1,"Beetle light response failed");Write("light-response.json",new{full,dim,blinkAlpha=.35f});
                }
                File.WriteAllText(dir+"/trace.json",JsonConvert.SerializeObject(trace));
                results.Add(new{clip,frames=trace.Count,previewFrames=saved,states=states.ToArray(),returned,travel,visibleExtraFeet=0,colliderRadius=boar.GetComponent<CircleCollider2D>().radius,hpMax=boar.Health.Max});
                Call(boar,"OnInterrupted");Object.Destroy(boar.gameObject);await Step();boar=null;
            }
            var after=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray();Require(JsonConvert.SerializeObject(devices)==JsonConvert.SerializeObject(after),"Input state changed");
            var report=new{success=true,method="Existing BoarBrain Think and TopDownEnemyRig Drive, explicit 40 Hz editor steps; no input injection",clips=results,inputBefore=devices,inputAfter=after};Write("review-result.json",report);return report;
        }
        catch(Exception ex){File.WriteAllText(Evidence+"/review-error.txt",ex.ToString());throw;}
        finally
        {
            if(boar)Call(boar,"OnInterrupted");foreach(var go in Object.FindObjectsByType<GameObject>())if(!initial.Contains(go))Object.Destroy(go);
            if(camera)camera.targetTexture=null;RenderTexture.active=null;if(rt){rt.Release();Object.Destroy(rt);}if(shot)Object.Destroy(shot);
            player.Teleport(playerPos);foreach(var b in behaviours)if(b.Key)b.Key.enabled=b.Value;foreach(var b in bodies)if(b.Key)b.Key.simulated=b.Value;foreach(var b in lights)if(b.Key)b.Key.enabled=b.Value;
            Time.captureDeltaTime=capture;Time.fixedDeltaTime=fixedDt;Time.timeScale=scale;AudioListener.volume=audio;Tuning.Invincible=invincible;EditorApplication.isPaused=pause;
            Write("review-cleanup.json",new{complete=true,virtualDevices=InputSystem.devices.Count(d=>!d.native),Time.captureDeltaTime,Time.fixedDeltaTime,EditorApplication.isPaused});
        }
    }
    static void Capture(Camera camera,RenderTexture rt,Texture2D shot,string path)
    {camera.Render();var active=RenderTexture.active;try{RenderTexture.active=rt;shot.ReadPixels(new Rect(0,0,640,640),0,0);shot.Apply();File.WriteAllBytes(path,shot.EncodeToPNG());}finally{RenderTexture.active=active;}}
}
