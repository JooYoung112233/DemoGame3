const fs=require('fs'),crypto=require('crypto');
const ev='검증/오우거-패턴-v030';
let rig=fs.readFileSync('Assets/Scripts/Game/Art/TopDown/OgreArtRig.cs','utf8');
const edit=(s,a,b)=>{if(!s.includes(a))throw Error('Anchor missing '+a.slice(0,50));return s.replace(a,b)};
rig=edit(rig,'{ "body_underlap", "free_L_original"','{ "lower_body", "torso_underlap", "free_L_original"');rig=edit(rig,'"body_clean", "grip_bridge"','"torso_clean", "grip_bridge"');
rig=edit(rig,'float sweepWeight,impactAge=99','float torsoYaw,headYaw;\n        float sweepWeight,impactAge=99');
rig=edit(rig,'public bool Falling => falling;','public bool Falling => falling;\n        public Vector2 ClubTipWorld => parts["club_endface"].root.position;\n        public int ClubSortOrder => parts["club_original"].sr.sortingOrder;\n        public float TorsoYawDegrees => torsoYaw;\n        public float HeadYawDegrees => headYaw;');
rig=edit(rig,'brain.GetComponent<YSort>()?.Refresh();rig.Sample(0);return rig;','brain.GetComponent<YSort>()?.Refresh();rig.Sample(0);OgrePatternVfx.Attach(brain,rig);return rig;');
rig=edit(rig,'foreach(var kv in parts)\n            {',`// Cosmetic weight transfer about the planted lower body; every arm piece receives
            // the same rigid transform, retaining the approved joint lengths and overlap.
            float recoil=impactAge<.28f?Mathf.Sin(impactAge/.28f*Mathf.PI):0;
            float targetYaw=slam?-3.5f*lift+2f*recoil:sweep?Mathf.Clamp(-(a+35)/120f*6f,-6f,6f)*sweepWeight:-lean*2f;
            if(broken)targetYaw=0;
            torsoYaw=Mathf.LerpAngle(torsoYaw,targetYaw,1-Mathf.Exp(-30*dt));
            headYaw=Mathf.LerpAngle(headYaw,torsoYaw,1-Mathf.Exp(-13*dt));
            Vector2 sway=slam?new Vector2(-8*lift,-8*lift+8*recoil):new Vector2(torsoYaw*.8f,lean*5);
            if(broken)sway=Vector2.zero;
            foreach(var kv in parts)
            {`);
rig=edit(rig,'if(n.StartsWith("upper_R"))Bone',`if(n=="lower_body"){p.pivot=new Vector2(326,670);p.target=p.pivot;p.sy=1-.018f*recoil;}
                else if(n.StartsWith("upper_R"))Bone`);
rig=edit(rig,'Apply(p);float shade=broken?.62f:1;',`Apply(p);
                if(n!="lower_body")
                {
                    bool isHead=n.StartsWith("head")||n.StartsWith("eye");
                    float yaw=isHead?Mathf.LerpAngle(torsoYaw,headYaw,.8f):torsoYaw;
                    var turn=Quaternion.Euler(0,0,yaw);var pivot=World(new Vector2(326,630));
                    p.root.localPosition=pivot+turn*(p.root.localPosition-pivot)+new Vector3(sway.x/PixelsPerUnit,-sway.y/PixelsPerUnit,0);
                    p.root.localRotation=turn*p.root.localRotation;
                }
                float shade=broken?.62f:1;`);
fs.writeFileSync(ev+'/candidate/OgreArtRig.cs',rig);
let brain=fs.readFileSync('Assets/Scripts/Game/Enemies/OgreBrain.cs','utf8');const hash=crypto.createHash('sha256').update(brain).digest('hex');
// Each added line is a null-safe art-only cue. Keep every existing gameplay line intact.
brain=edit(brain,'                Resolve(ref _telegraph, ref _reserved1, hit, BossPattern.Slam, true);','                GetComponent<OgrePatternVfx>()?.Slam(_slamCenter); // v030 cosmetic cue\n                Resolve(ref _telegraph, ref _reserved1, hit, BossPattern.Slam, true);');
brain=edit(brain,'            OgreSounds.Play(OgreSound.Charge);','            OgreSounds.Play(OgreSound.Charge);\n            GetComponent<OgrePatternVfx>()?.StartRun(); // v030 cosmetic cue');
brain=edit(brain,'            if (_step != Step.Run) return;','            if (_step != Step.Run) return;\n            GetComponent<OgrePatternVfx>()?.EndRun(wall); // v030 cosmetic cue');
brain=edit(brain,'                OgreSounds.Play(OgreSound.Sweep);','                OgreSounds.Play(OgreSound.Sweep);\n                GetComponent<OgrePatternVfx>()?.Sweep(); // v030 cosmetic cue');
brain=edit(brain,'                OgreSounds.Play(OgreSound.Sweep, 1f, 0.9f);','                OgreSounds.Play(OgreSound.Sweep, 1f, 0.9f);\n                GetComponent<OgrePatternVfx>()?.Sweep(); // v030 cosmetic cue');
brain=edit(brain,'            OgreSounds.Play(OgreSound.Roar, 1f, 0.9f);','            OgreSounds.Play(OgreSound.Roar, 1f, 0.9f);\n            GetComponent<OgrePatternVfx>()?.Roar(); // v030 cosmetic cue');
brain=edit(brain,'            OgreSounds.Play(OgreSound.Roar, 1f, 0.7f);','            OgreSounds.Play(OgreSound.Roar, 1f, 0.7f);\n            GetComponent<OgrePatternVfx>()?.Roar(); // v030 cosmetic cue');
brain=edit(brain,'                var t = Telegraph.Circle(_rockSpots[i], r, BossRules.RockWindup);','                var t = Telegraph.Circle(_rockSpots[i], r, BossRules.RockWindup);\n                GetComponent<OgrePatternVfx>()?.RockWarning(_rockSpots[i], BossRules.RockWindup); // v030 cosmetic cue');
brain=edit(brain,'                t.Resolve();','                GetComponent<OgrePatternVfx>()?.RockImpact(t.Origin); // v030 cosmetic cue\n                t.Resolve();');
brain=edit(brain,'        void CancelAll()\n        {','        void CancelAll()\n        {\n            GetComponent<OgrePatternVfx>()?.Clear(); // v030 cosmetic cue');
fs.writeFileSync(ev+'/candidate/OgreBrain.cs',brain);
fs.writeFileSync(ev+'/candidate-base.json',JSON.stringify({brainSha256:hash,rigSha256:crypto.createHash('sha256').update(fs.readFileSync('Assets/Scripts/Game/Art/TopDown/OgreArtRig.cs')).digest('hex')},null,2));
const stripped=brain.split('\n').filter(l=>!l.includes('// v030 cosmetic cue')).join('\n');
if(stripped!==fs.readFileSync('Assets/Scripts/Game/Enemies/OgreBrain.cs','utf8'))throw Error('Gameplay changed beyond cue lines');
console.log('Prepared rig + 10 cosmetic cue lines; gameplay text otherwise identical.');
