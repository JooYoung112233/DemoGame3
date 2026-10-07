const fs=require('fs');let s=fs.readFileSync('AgentScripts/OgreArmReview.cs','utf8')
 .replaceAll('OgreArmReview','OgreV30Review').replaceAll('검증/오우거-팔교정-v029','검증/오우거-패턴-v030')
 .replace('OgreBrain o = null; OgreArtRig rig = null;','OgreBrain o = null; OgreArtRig rig = null; OgrePatternVfx fx = null;')
 .replace('"C-roar","D-sweep"','"C-roar","C-rockfall","D-sweep"')
 .replace('"transition-broken","death"','"transition-broken","D-repeat","death"')
 .replace('else if(clip=="C-roar")Call','else if(clip.StartsWith("C-"))Call')
 .replace('clip=="C-roar";','clip.StartsWith("C-");')
 .replace('rig.enabled=false;','rig.enabled=false;fx=o.GetComponent<OgrePatternVfx>();Require(fx,"VFX missing");fx.enabled=false;')
 .replace('bool interrupted=false,wall=false,killed=false;','bool interrupted=false,wall=false,killed=false;int cycle=0;float maxTorso=0,maxHeadLag=0,maxFootSlip=0;Vector3 planted=Vector3.zero;')
 .replace('rig.Sample(Dt);\n                    Call(o.GetComponent<YSort>(),"LateUpdate");',`rig.Sample(Dt);
                    Call(o.GetComponent<YSort>(),"LateUpdate");fx.Advance(Dt);
                    foreach(var tele in new[]{Get(o,"_telegraph") as Telegraph,Get(o,"_telegraph2") as Telegraph})if(tele)foreach(var t in tele.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                    foreach(Telegraph tele in (IEnumerable)Get(o,"_rocks"))if(tele)foreach(var t in tele.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                    maxTorso=Mathf.Max(maxTorso,Mathf.Abs(rig.TorsoYawDegrees));maxHeadLag=Mathf.Max(maxHeadLag,Mathf.Abs(Mathf.DeltaAngle(rig.TorsoYawDegrees,rig.HeadYawDegrees)));
                    var foot=rig.transform.Find("lower_body axis").localPosition;if(frameIndex==0)planted=foot;maxFootSlip=Mathf.Max(maxFootSlip,Vector3.Distance(foot,planted));
                    if(o.Broken||killed)Require(fx.ActiveCount==0,"Residual effect after break/death");`)
 .replace('bool capture = clip=="A-slam"',`if(clip=="C-rockfall") {var spots=(List<Vector2>)Get(o,"_rockSpots");if(spots.Count>0)cam.transform.position=(Vector3)spots[0]+new Vector3(0,.3f,-10);}
                    floor.transform.position=new Vector3(cam.transform.position.x,cam.transform.position.y,0);
                    bool capture = clip=="C-rockfall" ? Get(o,"_step").ToString()=="Rocks"&&(float)Get(o,"_timer")>=BossRules.RockWindup-.12f || saved>0 : clip=="A-slam"`)
 .replace('head=rig.transform.Find','fxActive=fx.ActiveCount,fx.SlamCount,fx.SweepCount,fx.RockCount,fx.DustCount,torso=rig.TorsoYawDegrees,headLag=rig.HeadYawDegrees,foot=rig.transform.Find("lower_body axis").localPosition.ToString("F5"), head=rig.transform.Find')
 .replace('corpseOrderMax==-971','corpseOrderMax==-970')
 .replace('if((rest>=.4f&&saved==10)||(killed&&rig.FallTime>=1.4f))break;',`if(clip=="D-repeat"&&rest>=.4f&&cycle<2){Require(fx.ActiveCount==0,"Effects not expired between repeated attacks");cycle++;rest=0;Call(o,"BeginSweep",Vector2.down*2);}
                    else if((rest>=.4f&&saved==10)||(killed&&rig.FallTime>=1.4f))break;`)
 .replace('results.Add(new{clip,frames=frameIndex',`if(clip=="A-slam")Require(fx.SlamCount==1,"Slam cue count");
                if(clip=="D-sweep"||clip=="D-phase2")Require(fx.SweepCount==2,"Sweep cue count");
                if(clip=="D-repeat")Require(fx.SweepCount==6&&fx.ActiveCount==0,"Repeated sweep count/expiry");
                if(clip.StartsWith("C-"))Require(fx.RockCount==7,"Rock cue count");
                if(clip.StartsWith("B-"))Require(fx.DustCount>0,"Charge dust absent");
                if(!killed)Require(maxFootSlip<.0001f,"Planted foot anchor shifted");
                results.Add(new{clip,fx.SlamCount,fx.SweepCount,fx.RoarCount,fx.RockCount,fx.DustCount,fxActiveAtEnd=fx.ActiveCount,maxTorso,maxHeadLag,maxFootSlip,frames=frameIndex`);
fs.writeFileSync('AgentScripts/OgreV30Review.cs',s);
