using System;
using System.Collections;
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
using Object = UnityEngine.Object;

// Temporary reviewer outside Assets. No virtual input or saved scenes/assets.
public static class OgreV30Review
{
    const string Root = "E:/personalProject/Demo3/demo6";
    const string Evidence = Root + "/검증/오우거-패턴-v030";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float Dt = .025f;
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, Type t, string n, object value) => t.GetField(n, Flags).SetValue(o, value);
    static void Property(object o, Type t, string n, object value) => t.GetProperty(n, Flags).SetValue(o, value);
    static void Write(string name, object value) => File.WriteAllText(Evidence + "/" + name, JsonConvert.SerializeObject(value, Formatting.Indented));
    static void Require(bool ok, string error) { if (!ok) throw new Exception(error); }
    static async Task Step()
    {
        int frame = Time.frameCount;
        EditorApplication.Step();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Time.frameCount == frame)
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Editor step stalled");
            await Task.Delay(5);
        }
    }
    public static async Task<object> Run()
    {
        Require(Application.dataPath.Replace("\\", "/") == Root + "/Assets", "Original project only");
        Require(EditorApplication.isPlaying && CombatTestRoot.Instance && !DungeonRoot.Instance, "CombatTest Play required");
        Require(Keyboard.current != null && Keyboard.current.native && Keyboard.current.enabled && Mouse.current != null && Mouse.current.native && Mouse.current.enabled, "Native input required");
        var inputBefore = InputSystem.devices.Select(d => new { d.deviceId, d.name, d.native, d.enabled }).ToArray();
        var paused = EditorApplication.isPaused;
        var delta = Time.captureDeltaTime;
        var fixedDelta = Time.fixedDeltaTime;
        var scale = Time.timeScale;
        var volume = AudioListener.volume;
        var random = UnityEngine.Random.state;
        var behaviours = Object.FindObjectsByType<MonoBehaviour>().Where(b => b.GetType().Namespace == "Demo6.Game").ToDictionary(b => b, b => b.enabled);
        var bodies = Object.FindObjectsByType<Rigidbody2D>().ToDictionary(b => b, b => b.simulated);
        var lights = Object.FindObjectsByType<Light2D>().ToDictionary(l => l, l => l.enabled);
        var initialObjects = new HashSet<GameObject>(Object.FindObjectsByType<GameObject>());
        Camera cam = null; RenderTexture rt = null; Texture2D shot = null; Texture2D grid = null; Sprite gridSprite = null;
        OgreBrain o = null; OgreArtRig rig = null; OgrePatternVfx fx = null;
        var results = new List<object>();bool vfxLightChecked=false;
        try
        {
            EditorApplication.isPaused = true;
            foreach (var b in behaviours.Keys) b.enabled = false;
            foreach (var b in bodies.Keys) b.simulated = false;
            foreach (var l in lights.Keys) l.enabled = false;
            Time.captureDeltaTime = Dt; Time.fixedDeltaTime = Dt; Time.timeScale = 1; AudioListener.volume = 0;
            UnityEngine.Random.InitState(2704);
            cam = new GameObject("Ogre review camera (temporary)").AddComponent<Camera>();
            cam.enabled = false; cam.orthographic = true; cam.orthographicSize = 4.15f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.19f,.20f,.22f); cam.cullingMask = 1 << 31;
            rt = new RenderTexture(640,640,24,RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            shot = new Texture2D(640,640,TextureFormat.RGB24,false);
            var light = new GameObject("Neutral review light (temporary)").AddComponent<Light2D>();
            light.gameObject.layer=31;
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            grid = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var pixels = new Color32[256*256];
            for (int y=0;y<256;y++) for (int x=0;x<256;x++) pixels[y*256+x]=(x%16==0||y%16==0)?new Color32(54,57,62,255):new Color32(48,51,56,255);
            grid.SetPixels32(pixels);grid.Apply();
            var floor = new GameObject("Review grid (temporary)").AddComponent<SpriteRenderer>(); floor.gameObject.layer=31;
            gridSprite=Sprite.Create(grid,new Rect(0,0,256,256),Vector2.one*.5f,16);floor.sprite=gridSprite;floor.sortingOrder=-1000;RenderMaterials.MakeUnlit(floor);
            floor.transform.position = new Vector3(100,-6,0); floor.transform.localScale = Vector3.one*4;
            var player = CombatTestRoot.Instance.Player;
            foreach (string clip in new[]{"A-slam","B-charge-2","B-charge-3","B-wall","C-roar","C-rockfall","D-sweep","D-phase2","phase2","broken-recover","transition-broken","D-repeat","death"})
            {
                StrongAttackSchedule.ResetStatics(); Time.timeScale=1;
                o=(OgreBrain)EnemySpawner.Create(MonsterKind.Ogre,2,new Vector2(100,0));o.NoReward=true;
                Property(o,typeof(Enemy),"Aware",true);Set(o,typeof(Enemy),"Facing",Vector2.down);
                foreach(var b in o.GetComponents<MonoBehaviour>()) b.enabled=false;
                o.GetComponent<Rigidbody2D>().simulated=false;
                rig=o.GetComponentInChildren<OgreArtRig>();Require(rig,"Art rig missing");rig.enabled=false;fx=o.GetComponent<OgrePatternVfx>();Require(fx,"VFX missing");fx.enabled=false;
                var look=o.GetComponentInChildren<OgreLook>();look.enabled=false;
                foreach(var t in rig.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                bool second=clip=="B-charge-3"||clip=="D-phase2"||clip.StartsWith("C-");
                Property(o,typeof(OgreBrain),"Phase",second?2:1);
                if(clip=="A-slam")Call(o,"BeginSlam",Vector2.down*2);
                else if(clip.StartsWith("B-")||clip=="death")Call(o,"BeginCharge",Vector2.down*8);
                else if(clip.StartsWith("D-")||clip=="broken-recover")Call(o,"BeginSweep",Vector2.down*2);
                else if(clip.StartsWith("C-"))Call(o,"BeginRoar");
                else Call(o,"BeginTransition");
                string dir=Evidence+"/frames/"+clip;Directory.CreateDirectory(dir);
                float elapsed=0,rest=0,brokenAge=0;int saved=0,frameIndex=0;bool interrupted=false,wall=false,killed=false;int cycle=0;float maxTorso=0,maxHeadLag=0,maxFootSlip=0;Vector3 planted=Vector3.zero;
                int maxRocks=0,maxRats=0,maxChargeIndex=0;bool phaseSeen=false,blinkOk=false;int corpseOrderMin=0,corpseOrderMax=0;
                var trace=new List<object>();
                while(elapsed<14)
                {
                    string state=Get(o,"_state").ToString(),step=Get(o,"_step").ToString();
                    if(clip.Contains("broken")&&!interrupted&&elapsed>=.325f)
                    {
                        Set(o,typeof(OgreBrain),"_fightActive",true);Call(o,"OnBroken");Set(o,typeof(Enemy),"_breakUntil",Time.time+BossRules.BreakSeconds);interrupted=true;
                    }
                    if(interrupted&&o.Broken)brokenAge+=Dt;
                    else if(interrupted&&state=="Broken")Call(o,"StandUp");
                    state=Get(o,"_state").ToString();
                    if(clip=="death"&&!killed&&step=="Run")
                    {
                        Set(o,typeof(OgreBrain),"_fightActive",false);o.GetComponent<SpriteFlash>().Flash(Color.white,.2f);o.Health.ApplyDamage(o.Health.Max+1,false);killed=true;
                    }
                    if(!o.Broken&&!killed)
                    {
                        switch(state)
                        {
                            case "Slam":Call(o,"TickSlam",Dt,player,false);break;
                            case "Charge":
                                Call(o,"TickCharge",Dt,player,true,Vector2.down*8);
                                if(clip=="B-wall"&&!wall&&Get(o,"_step").ToString()=="Run"&&(float)Get(o,"_timer")>.25f){Call(o,"EndRun",true);wall=true;}
                                o.GetComponent<Rigidbody2D>().position+=(Vector2)typeof(Enemy).GetField("DesiredVelocity",Flags).GetValue(o)*Dt;
                                break;
                            case "Sweep":Call(o,"TickSweep",Dt,player,false,Vector2.down*2);break;
                            case "Roar":Call(o,"TickRoar",Dt,player,false);break;
                            case "Transition":Call(o,"TickTransition",Dt);break;
                            case "Walk":Call(o,"TickWalk",Dt,false,Vector2.zero,0f);rest+=Dt;break;
                        }
                    }
                    // The brain signals and actual shared SpriteFlash shader are exercised at 40 Hz.
                    rig.Sample(Dt);
                    Call(o.GetComponent<YSort>(),"LateUpdate");fx.Advance(Dt);
                    foreach(var tele in new[]{Get(o,"_telegraph") as Telegraph,Get(o,"_telegraph2") as Telegraph})if(tele)foreach(var t in tele.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                    foreach(Telegraph tele in (IEnumerable)Get(o,"_rocks"))if(tele)foreach(var t in tele.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                    maxTorso=Mathf.Max(maxTorso,Mathf.Abs(rig.TorsoYawDegrees));maxHeadLag=Mathf.Max(maxHeadLag,Mathf.Abs(Mathf.DeltaAngle(rig.TorsoYawDegrees,rig.HeadYawDegrees)));
                    var foot=rig.transform.Find("lower_body axis").localPosition;if(frameIndex==0)planted=foot;maxFootSlip=Mathf.Max(maxFootSlip,Vector3.Distance(foot,planted));
                    if(o.Broken||killed)Require(fx.ActiveCount==0,"Residual effect after break/death");
                    Require(!rig.GetComponentsInChildren<SpriteRenderer>().Any(s=>float.IsNaN(s.transform.position.x)||float.IsInfinity(s.transform.lossyScale.x)),"Non-finite transform");
                    cam.transform.position=(killed?new Vector3(100,0,0):o.transform.position)+new Vector3(0,-.9f,-10);
                    if(clip=="C-rockfall") {var spots=(List<Vector2>)Get(o,"_rockSpots");if(spots.Count>0)cam.transform.position=(Vector3)spots[0]+new Vector3(0,.3f,-10);}
                    floor.transform.position=new Vector3(cam.transform.position.x,cam.transform.position.y,0);
                    bool capture = clip=="C-rockfall" ? Get(o,"_step").ToString()=="Rocks"&&(float)Get(o,"_timer")>=BossRules.RockWindup-.12f || saved>0 : clip=="A-slam" ? elapsed>=(float)Get(o,"_windup")-.15f :
                        clip.StartsWith("B-") ? Get(o,"_step").ToString()=="Run"||saved>0 :
                        clip=="death" ? rig.FallTime>=.30f :
                        clip=="broken-recover" ? elapsed>=3.20f :
                        clip=="transition-broken" ? elapsed>=.20f :
                        clip=="phase2" ? elapsed>=.25f :
                        clip.StartsWith("D-") ? elapsed>=(float)Get(o,"_windup")-.15f : elapsed>=.025f;
                    if(frameIndex%8==0)Capture(cam,rt,shot,dir+"/key-"+frameIndex.ToString("D3")+".png");
                    if(clip=="A-slam"&&fx.SlamCount==1&&!vfxLightChecked)
                    {
                        vfxLightChecked=true;
                        var hidden=Object.FindObjectsByType<SpriteRenderer>().Where(r=>!r.transform.parent||r.transform.parent.name!="Ogre pattern effects (owned)").ToDictionary(r=>r,r=>r.enabled);
                        foreach(var r in hidden.Keys)r.enabled=false;
                        Capture(cam,rt,shot,Evidence+"/dust-light-full.png");double vf=shot.GetPixels32().Average(p=>(p.r+p.g+p.b)/3.0);
                        light.intensity=.2f;Capture(cam,rt,shot,Evidence+"/dust-light-dim.png");double vd=shot.GetPixels32().Average(p=>(p.r+p.g+p.b)/3.0);light.intensity=1;
                        foreach(var r in hidden)if(r.Key)r.Key.enabled=r.Value;
                        Require(vf>vd+.1,"Dust did not respond to 2D lighting");Write("vfx-light-response.json",new{full=vf,dim=vd,lit=true});
                    }
                    if(capture&&saved<10)
                    {
                        Capture(cam,rt,shot,dir+"/"+saved.ToString("D2")+".png");saved++;
                    }
                    int rocks=((ICollection)Get(o,"_rocks")).Count,rats=((ICollection)Get(o,"_rats")).Count;
                    maxRocks=Math.Max(maxRocks,rocks);maxRats=Math.Max(maxRats,rats);maxChargeIndex=Math.Max(maxChargeIndex,(int)Get(o,"_chargeIndex"));phaseSeen|=o.Phase==2;
                    var ring=rig.transform.Find("Existing phase 2 indicator").GetComponent<SpriteRenderer>();
                    Require(ring.enabled==(!rig.Falling&&o.Phase>=2),"Phase rim mismatch");
                    trace.Add(new{frameIndex,unityFrame=Time.frameCount,t=elapsed,dt=Dt,state=Get(o,"_state").ToString(),step=Get(o,"_step").ToString(),o.Phase,o.ClubAngle,o.ClubReach,o.ArmLift,o.Lean,o.Broken,rig.Falling,rig.FallTime,rocks,rats,x=o.Position.x,y=o.Position.y,saved,
                        elbow=rig.ElbowPixels.ToString("F3"), wrist=rig.WristPixels.ToString("F3"), grip=rig.GripPixels.ToString("F3"), upperScale=rig.transform.Find("upper_R_rounded axis").localScale.x, foreScale=rig.transform.Find("fore_R_rounded axis").localScale.x, fxActive=fx.ActiveCount,fx.SlamCount,fx.SweepCount,fx.RockCount,fx.DustCount,torso=rig.TorsoYawDegrees,headLag=rig.HeadYawDegrees,foot=rig.transform.Find("lower_body axis").localPosition.ToString("F5"), head=rig.transform.Find("head_original axis").localPosition.ToString("F5"),left=rig.transform.Find("free_L_original axis").localEulerAngles.z});
                    if(clip=="A-slam"&&rest>=.025f&&!blinkOk)
                    {
                        var flash=o.GetComponent<SpriteFlash>();flash.StopAll();flash.Blink(.4f);Call(flash,"Apply");rig.Sample(0);
                        blinkOk=OgreArtRig.Names.All(n=>Mathf.Abs(rig.transform.Find(n+" axis").GetComponentInChildren<SpriteRenderer>().color.a-.35f)<.001f);
                        Require(blinkOk,"Blink alpha overwritten");flash.StopAll();rig.Sample(0);
                        Capture(cam,rt,shot,Evidence+"/light-full.png");double full=shot.GetPixels32().Average(c=>(c.r+c.g+c.b)/3.0);light.intensity=.2f;Capture(cam,rt,shot,Evidence+"/light-dim.png");double dim=shot.GetPixels32().Average(c=>(c.r+c.g+c.b)/3.0);light.intensity=1;Write("light-response.json",new{full,dim});Require(full>dim+1,"Lit art did not respond to light");
                    }
                    if(killed)
                    {
                        var renderers=OgreArtRig.Names.Select(n=>rig.transform.Find(n+" axis").GetComponentInChildren<SpriteRenderer>()).ToArray();
                        corpseOrderMin=renderers.Min(s=>s.sortingOrder);corpseOrderMax=renderers.Max(s=>s.sortingOrder);
                        Require(corpseOrderMin==-985&&corpseOrderMax==-970,"Corpse sorting drift");
                        var block=new MaterialPropertyBlock();foreach(var s in renderers){s.GetPropertyBlock(block);Require(block.GetFloat("_FlashAmount")==0,"Corpse retains death flash");}
                    }
                    frameIndex++;elapsed+=Dt;
                    if(clip=="D-repeat"&&rest>=.4f&&cycle<2){Require(fx.ActiveCount==0,"Effects not expired between repeated attacks");cycle++;rest=0;Call(o,"BeginSweep",Vector2.down*2);}
                    else if((rest>=.4f&&saved==10)||(killed&&rig.FallTime>=1.4f))break;
                    await Step();
                }
                Require(saved==10,"Preview must have 10 frames: "+clip);
                Require(killed||rest>=.4f,"Pattern did not return to rest: "+clip);
                if(clip=="B-charge-2")Require(maxChargeIndex==1,"Phase 1 charge count changed");
                if(clip=="B-charge-3")Require(maxChargeIndex==2,"Phase 2 charge count changed");
                if(clip=="C-roar")Require(maxRocks==7&&maxRats==4,"Roar count changed");
                File.WriteAllText(dir+"/trace.json",JsonConvert.SerializeObject(trace));
                if(clip=="A-slam")Require(fx.SlamCount==1,"Slam cue count");
                if(clip=="D-sweep"||clip=="D-phase2")Require(fx.SweepCount==2,"Sweep cue count");
                if(clip=="D-repeat")Require(fx.SweepCount==6&&fx.ActiveCount==0,"Repeated sweep count/expiry");
                if(clip.StartsWith("C-"))Require(fx.RockCount==7,"Rock cue count");
                if(clip.StartsWith("B-"))Require(fx.DustCount>0,"Charge dust absent");
                if(!killed)Require(maxFootSlip<.0001f,"Planted foot anchor shifted");
                results.Add(new{clip,fx.SlamCount,fx.SweepCount,fx.RoarCount,fx.RockCount,fx.DustCount,fxActiveAtEnd=fx.ActiveCount,maxTorso,maxHeadLag,maxFootSlip,frames=frameIndex,previewFrames=saved,returnedToRest=rest>=.4f,maxRocks,maxRats,maxChargeIndex,phaseSeen,blinkOk,corpseOrderMin,corpseOrderMax});
                Write("progress.json",results);
                Set(o,typeof(OgreBrain),"_fightActive",false);Call(o,"CancelAll");Call(o,"RemoveRats");
                Object.Destroy(o.gameObject);if(rig)Object.Destroy(rig.gameObject);await Step();o=null;rig=null;
            }
            var inputAfter=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray();
            Require(JsonConvert.SerializeObject(inputBefore)==JsonConvert.SerializeObject(inputAfter),"Input device state changed");
            var report=new{success=true,project=Root,method="Existing OgreBrain methods, explicit 40 Hz Editor steps; no input injection",clips=results,inputBefore,inputAfter,previewReplayMs=25,sourceFrameSeconds=Dt};
            Write("review-result.json",report);return report;
        }
        catch(Exception ex){File.WriteAllText(Evidence+"/review-error.txt",ex.ToString());throw;}
        finally
        {
            if(o){Set(o,typeof(OgreBrain),"_fightActive",false);Call(o,"CancelAll");Call(o,"RemoveRats");}
            foreach(var go in Object.FindObjectsByType<GameObject>())if(!initialObjects.Contains(go))Object.Destroy(go);
            if(cam)cam.targetTexture=null;RenderTexture.active=null;
            if(rt){rt.Release();Object.Destroy(rt);}if(shot)Object.Destroy(shot);if(gridSprite)Object.Destroy(gridSprite);if(grid)Object.Destroy(grid);
            foreach(var b in behaviours)if(b.Key)b.Key.enabled=b.Value;
            foreach(var b in bodies)if(b.Key)b.Key.simulated=b.Value;
            foreach(var l in lights)if(l.Key)l.Key.enabled=l.Value;
            Time.captureDeltaTime=delta;Time.fixedDeltaTime=fixedDelta;Time.timeScale=scale;AudioListener.volume=volume;UnityEngine.Random.state=random;
            EditorApplication.isPaused=paused;
            Write("review-cleanup.json",new{complete=true,EditorApplication.isPaused,virtualDevices=InputSystem.devices.Count(d=>!d.native),Time.captureDeltaTime,Time.fixedDeltaTime});
        }
    }
    static void Capture(Camera cam,RenderTexture rt,Texture2D shot,string path)
    {
        cam.Render();var active=RenderTexture.active;
        try{RenderTexture.active=rt;shot.ReadPixels(new Rect(0,0,640,640),0,0);shot.Apply();File.WriteAllBytes(path,shot.EncodeToPNG());}
        finally{RenderTexture.active=active;}
    }
}
