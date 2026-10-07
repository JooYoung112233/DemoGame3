using System;
using System.Collections.Generic;
using System.Collections;
using Demo6.Core.Combat;
using Demo6.Core.Combat.Stance;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    // Runtime visual adapter for approved V044 art plus V045 authored motions. Reads gameplay clocks.
    // Source pixels and joint coordinates come from r6 / greatsword r7; no damage rules here.
    public sealed class FirstAttackArtV042
    {
        [Serializable] public sealed class Library { public Clip[] clips; }
        [Serializable] public sealed class Clip
        {
            public string id, weapon; public float duration, @base, tip; public float[] hits, windows;
            public int width, height, ppu, rootX, rootY; public string[] layers; public Frame[] frames; public bool hideFeet, down;
        }
        [Serializable] public sealed class Frame
        {
            public float t, twist, lean, capeTurn, headTurn, spin, releaseBlend, shoulderLeftAngle, shoulderRightAngle; public float[] bodyScale, headScale, shoulderScale, capeScale; public float footFold; public float[] right, left, bodyOrigin, capeOrigin, headOrigin, capL, capR; public bool weaponReleased; public CharacterMotionV045.Sockets sockets; public Part[] parts;
        }
        [Serializable] public sealed class Part
        {
            public int layer; public string name, resource; public float x,y,opacity;
            [NonSerialized] public Sprite sprite;
        }
        static Library _library;
        static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeSprites(){_library=null;Sprites.Clear();}
        readonly PlayerController _player;
        readonly Transform _rig;
        readonly SpriteRenderer _body, _shadow;
        readonly SpriteRenderer[] _originals;
        readonly SpriteRenderer[] _parts=new SpriteRenderer[32];
        readonly Dictionary<string,Clip> _motionClips=new Dictionary<string,Clip>();
        CharacterMotionV045 _motion;
        readonly EquipmentVisualV049 _equipment;
        readonly HashSet<string> _warming=new HashSet<string>();
        public bool ExtendedMotion {get;private set;}
        public bool OwnsExtendedMotions => _motion!=null;
        string _waveClip; float _waveClock=-1; int _waveSerial=1000000;
        readonly Dictionary<SpriteRenderer,bool> _hidden=new Dictionary<SpriteRenderer,bool>();
        readonly MaterialPropertyBlock _flash=new MaterialPropertyBlock();
        readonly Transform _baseR,_tipR,_baseL,_tipL;
        readonly SlashWaveV044 _wave;
        readonly SpriteRenderer[] _glowParts=new SpriteRenderer[3];
        float _glowValue;
        bool _applied;
        string _lastWeapon;
        float _lastAttackTime=-1;
        Frame _currentFrame;
        Clip _currentClip;
        public float[] AuthoredRight => _currentFrame?.right;
        public float AuthoredHitTime => _currentClip?.hits!=null&&_currentClip.hits.Length>0?_currentClip.hits[0]:-1;
        public bool AuthoredSlam => ExtendedMotion&&(ClipId=="great-combo-3"||ClipId=="great-execution");
        public bool Active {get;private set;}
        public string ClipId {get;private set;}
        public int FrameIndex {get;private set;}
        public float VisualTime {get;private set;}
        public Vector3 BladeBase=>_baseR.position;
        public Vector3 BladeTip=>_tipR.position;
        public int TrailSamples=>_wave.Count;
        public int ActiveSlashWaves=>_wave.ActiveWaves;
        public SlashWaveV044 SlashWave=>_wave;
        public bool TrailEmitting=>_wave.Emitting;
        public int ExpectedPartCount=>_currentFrame==null?0:_currentFrame.parts.Length-_equipment.Suppressed;
        public int VisiblePartCount {get{int n=0;foreach(var p in _parts)if(p&&p.enabled&&p.sprite&&!p.forceRenderingOff&&p.color.a>.001f)n++;return n;}}
        public float AccentTurn {get;private set;}
        public Vector2 AccentOffset {get;private set;}
        // V043: rigid upper-body accent. One transform is shared by torso, shoulders,
        // both arms, hands and weapons; source pixels, limb lengths and grips stay intact.
        static readonly float[] SwordTimes={0,.10f,.18f,.2088f,.30f,.40f,.58f};
        static readonly float[] SwordTurns={0,-14,-22,8,22,15,0};
        static readonly float[] SwordLean={0,-.025f,-.055f,.065f,.10f,.06f,0};
        static readonly float[] GreatTimes={0,.12f,.26f,.352f,.50f,.69f,.88f};
        static readonly float[] GreatTurns={0,-18,-28,4,25,14,0};
        static readonly float[] GreatLean={0,-.03f,-.08f,.06f,.14f,.08f,0};
        static readonly float[] TwinTimes={0,.07f,.115f,.156f,.205f,.256f,.33f,.42f,.52f};
        static readonly float[] TwinTurns={0,-10,-18,12,19,-14,-21,-10,0};
        static readonly float[] TwinLean={0,-.02f,-.04f,.05f,.06f,.025f,.08f,.04f,0};
        static float AccentCurve(float t,float[] times,float[] values){
            for(int i=1;i<times.Length;i++)if(t<=times[i]){
                float u=Mathf.InverseLerp(times[i-1],times[i],t);
                return Mathf.Lerp(values[i-1],values[i],u*u*(3-2*u));
            }return values[values.Length-1];
        }
        public static void AttackAccent(string weapon,float t,out float turn,out Vector2 offset){
            bool great=weapon=="wpn_greatsword",twin=weapon=="wpn_twinblades";
            var times=great?GreatTimes:twin?TwinTimes:SwordTimes;
            turn=AccentCurve(t,times,great?GreatTurns:twin?TwinTurns:SwordTurns);
            offset=new Vector2(AccentCurve(t,times,great?GreatLean:twin?TwinLean:SwordLean),0);
        }
        public Transform RightBladeBase=>_baseR;
        public Transform RightBladeTip=>_tipR;
        public SpriteRenderer ShadowPart(string name)
        {
            if(_currentFrame==null)return null;
            string suffix=name=="Body"?"approved-torso":name=="CuteHead"?"approved-head":name=="CuteShoulderR"?"approved-shoulder-R":name=="CuteShoulderL"?"approved-shoulder-L":null;
            if(suffix==null)return null;
            foreach(var p in _currentFrame.parts)if(p.name.EndsWith(suffix))return _parts[p.layer];
            return null;
        }
        public SpriteRenderer ShieldSurface
        {
            get { if(!Active||_currentFrame==null)return null;
                foreach(var part in _currentFrame.parts)if(part.name.Contains("shield-edge"))return _parts[part.layer];
                return null; }
        }
        public static bool Supported(string weapon)=>weapon=="wpn_longsword"||weapon=="wpn_greatsword"||weapon=="wpn_twinblades";
        public static bool SupportsPose(PlayerController player)=>player && Supported(player.Weapon?.id) &&
            !player.ExecutionPoseActive && (player.Pose==PlayerPose.Idle||player.Pose==PlayerPose.Move||
            (player.Pose==PlayerPose.Attack&&player.ComboIndex==0));

        public FirstAttackArtV042(PlayerController player,Transform rig,SpriteRenderer body,SpriteRenderer shadow)
        {
            _player=player;_rig=rig;_body=body;_shadow=shadow;
            _originals=player.GetComponentsInChildren<SpriteRenderer>(true);
            _equipment=new EquipmentVisualV049(player,rig);
            if(_library==null){var json=Resources.Load<TextAsset>("FirstAttackV042/clips");if(json)_library=JsonUtility.FromJson<Library>(json.text);}
            var motions=Resources.Load<TextAsset>("CharacterMotionV045/clips");
            if(motions){_motion=new CharacterMotionV045(motions.text);var extra=JsonUtility.FromJson<Library>(motions.text);foreach(var c in extra.clips)_motionClips.Add(c.id,c);}
            // Warm only visible cels for the equipped weapon, without a synchronous library-wide load.
            for(int i=0;i<_parts.Length;i++){
                var go=new GameObject("FirstAttackV042_Layer_"+i);go.layer=player.gameObject.layer;go.transform.SetParent(rig,false);
                _parts[i]=go.AddComponent<SpriteRenderer>();_parts[i].enabled=false;
            }
            _baseR=Anchor("BladeBaseR");_tipR=Anchor("BladeTipR");_baseL=Anchor("BladeBaseL");_tipL=Anchor("BladeTipL");
            var wave=new GameObject("FirstAttackV044_SlashWave");wave.layer=player.gameObject.layer;wave.transform.SetParent(rig,false);_wave=wave.AddComponent<SlashWaveV044>();
            for(int i=0;i<3;i++){var g=new GameObject("FirstAttackV045_Glow_"+i);g.layer=player.gameObject.layer;g.transform.SetParent(rig,false);var sr=g.AddComponent<SpriteRenderer>();sr.sprite=ShapeSprites.Square;sr.sharedMaterial=RenderMaterials.Unlit;sr.enabled=false;_glowParts[i]=sr;}
        }
        static Sprite LoadSprite(string resource)
        {
            if(Sprites.TryGetValue(resource,out var sprite)&&sprite)return sprite;
            var texture=Resources.Load<Texture2D>(resource);if(!texture)return null;
            sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),160,0,SpriteMeshType.FullRect);
            sprite.name=resource;Sprites.Add(resource,sprite);return sprite;
        }
        Transform Anchor(string name){var t=new GameObject("FirstAttackV042_"+name).transform;t.SetParent(_rig,false);return t;}

        public void Restore()
        {
            foreach(var h in _hidden)if(h.Key)h.Key.enabled=h.Value;
            _hidden.Clear();foreach(var sr in _parts)if(sr)sr.enabled=false;
            _equipment.Begin();
            foreach(var glow in _glowParts)if(glow)glow.enabled=false;
            _applied=false;Active=false;
        }
        public void Stop(){Restore();_wave.Clear();_lastAttackTime=-1;_motion?.Reset();_waveClip=null;_waveClock=-1;}
        public static float MapTime(float t,SwingPlan plan,Clip clip)
        {
            float from=0,to=0;
            for(int i=0;i<clip.hits.Length;i++){
                float nextFrom=plan.HitTime(i),nextTo=clip.hits[i];
                if(t<=nextFrom)return Mathf.Lerp(to,nextTo,Mathf.InverseLerp(from,nextFrom,t));
                from=nextFrom;to=nextTo;
            }
            return Mathf.Lerp(to,clip.duration,Mathf.InverseLerp(from,plan.Duration,t));
        }
        public bool Drive(float dt,float angle,float crouch,TopDownCape cape,CuteWalkV22 gait)
        {
            if(!_player||!Supported(_player.Weapon?.id)||_library==null){_wave.Clear();_motion?.Reset();return false;}
            bool attack=_player.Pose==PlayerPose.Attack;
            CharacterMotionV045.Sample sample=default;
            bool extended=_motion!=null&&_motion.TrySample(_player,dt,out sample);
            Clip clip=null; Frame f=null; int frame=0; float t=0;
            if(extended){clip=_motionClips[sample.clip.id];frame=sample.index;t=sample.time;f=clip.frames[frame];}
            else {
                bool locomotion=_player.Pose==PlayerPose.Idle||_player.Pose==PlayerPose.Move;
                if(!locomotion&&(!attack||_player.ComboIndex!=0)){_wave.Clear();return false;}
                if(_player.Weapon.id=="wpn_longsword")_motionClips.TryGetValue("sword-first-slash",out clip);
                else if(_player.Weapon.id=="wpn_twinblades")_motionClips.TryGetValue("twin-first-right-return",out clip);
                if(clip==null)foreach(var c in _library.clips)if(c.weapon==_player.Weapon.id){clip=c;break;}
                if(clip==null)return false;
                float hostTime=attack?_player.PoseTime:0;
                if(attack&&dt<=0)for(int i=0;i<_player.CurrentSwingPlan.Hits;i++){float h=_player.CurrentSwingPlan.HitTime(i);if(hostTime>=h&&hostTime-h<.04f)hostTime=h;}
                t=attack?MapTime(hostTime,_player.CurrentSwingPlan,clip):0;
                while(frame+1<clip.frames.Length&&clip.frames[frame+1].t<=t+.00001f)frame++;
                f=clip.frames[frame];
            }
            ExtendedMotion=extended;
            WeaponStanceArms.Ensure(_player);
            bool gaitActive=!extended&&!attack;
            if(_lastWeapon!=clip.weapon){_wave.Clear();_lastWeapon=clip.weapon;if(_warming.Add(clip.weapon))_wave.StartCoroutine(WarmWeapon(clip.weapon));}
            _currentFrame=f;_currentClip=clip;
            ClipId=clip.id;FrameIndex=frame;VisualTime=t;Active=true;
            float accent=0;Vector2 accentOffset=Vector2.zero;
            if(attack&&!extended)AttackAccent(clip.weapon,t,out accent,out accentOffset);
            AccentTurn=accent;AccentOffset=accentOffset;
            float scale=Mathf.Lerp(1f,CrouchRules.BodyScale,crouch),light=Mathf.Lerp(1f,CrouchRules.BodyBrightness,crouch);
            int order=_body.sortingOrder+40;
            _body.sortingOrder=order;
            var bodyAt=f.bodyOrigin!=null?new Vector2(f.bodyOrigin[0],f.bodyOrigin[1]):new Vector2(f.lean,0);
            _body.transform.localPosition=TopDownCanvas.Rotate((TopDownCanvas.Rotate(bodyAt,accent)+accentOffset)*scale,angle);
            _body.transform.localRotation=Quaternion.Euler(0,0,angle+f.twist+accent);
            _body.transform.localScale=new Vector3(scale,scale,1);
            _rig.localPosition=Vector3.zero;_rig.localRotation=Quaternion.Euler(0,0,angle);_rig.localScale=new Vector3(scale,scale,1);
            _shadow.sortingOrder=TopDownPlayerRig.ShadowOrder;
            _shadow.transform.localScale=new Vector3(.98f/scale,.92f/scale,1);
            // Existing dynamic cloth, approved source and distance-based planted gait remain in use.
            var art=CuteArtProfileV15.Current;
            Vector2 capeAt=f.capeOrigin!=null?new Vector2(f.capeOrigin[0],f.capeOrigin[1]):new Vector2(f.lean,0);
            Vector2 offset=TopDownCanvas.Rotate(capeAt,accent)+accentOffset+(gaitActive?gait.BodyOffset:Vector2.zero);
            cape.Place(art?art.cape:null,_body,_player.transform.position,angle+f.capeTurn+accent+(gaitActive?gait.BodyTurn:0),
                offset,angle,dt,true,0,gaitActive,
                f.capeScale!=null&&f.capeScale.Length>=2?(Vector2?)new Vector2(f.capeScale[0],f.capeScale[1]):null,f.footFold);
            foreach(var sr in _originals){
                if(sr==_shadow||sr.name.StartsWith("FirstAttackV042_")||!sr.enabled)continue;
                _hidden[sr]=sr.enabled;sr.enabled=false;
            }
            _body.GetPropertyBlock(_flash);
            foreach(var part in f.parts){
                var sr=_parts[part.layer];
                if(EquipmentVisualV049.Suppresses(part.name)){sr.enabled=false;_equipment.CountSuppressed();continue;}
                bool rigid=part.name.Contains("rigid-");
                if(!part.sprite)part.sprite=rigid?ShapeSprites.Square:LoadSprite(part.resource);
                if(!part.sprite)continue;
                sr.sprite=part.sprite;sr.flipX=false;sr.flipY=false;sr.sharedMaterial=_body.sharedMaterial;sr.enabled=true;
                float partLight=light*(part.name.Contains("shield-edge")&&_player.GuardMeterFraction<.35f?.8f:1);
                sr.color=new Color(partLight,partLight,partLight,_body.color.a*part.opacity);
                sr.sortingLayerID=_body.sortingLayerID;sr.sortingOrder=order+part.layer;
                sr.forceRenderingOff=_body.forceRenderingOff;
                _body.GetPropertyBlock(_flash);
                // Calibrated equipment supplies its own face and physical edge.
                sr.SetPropertyBlock(_flash);
                Vector2 at=new Vector2(part.x,part.y);float turn=0;
                if(part.name.Contains("shield-edge")&&_player.GuardMeterFraction<.35f)
                    at+=new Vector2(Mathf.Sin(Time.time*61),Mathf.Sin(Time.time*47+1.7f))*.02f;
                bool foot=part.name.Contains("foot-");
                if(attack&&!extended&&!foot){turn=accent;at=TopDownCanvas.Rotate(at,accent)+accentOffset;}
                Vector2 extraOffset=attack&&!extended?accentOffset:Vector2.zero;
                if(gaitActive&&!foot){GaitTransform(part.name,f,clip.weapon,gait,out turn,out extraOffset);at=TopDownCanvas.Rotate(at,turn)+extraOffset;}
                sr.transform.localPosition=at;sr.transform.localRotation=Quaternion.Euler(0,0,turn);sr.transform.localScale=Vector3.one;
                if(gaitActive&&foot&&art&&art.toe){
                    sr.sprite=art.toe;sr.transform.localPosition=part.name.EndsWith("-L")?gait.Left:gait.Right;
                    sr.transform.localRotation=Quaternion.Euler(0,0,gait.BodyTurn);sr.transform.localScale=new Vector3(1.05f,1.05f,1);
                }
                
                _equipment.ApplyArmor(sr,part.name,f,turn,extraOffset);
                _equipment.ApplyWeapon(sr,part.name,part.name.EndsWith("-L")?f.left:f.right,clip.weapon,turn,extraOffset,clip.id,t,clip.duration,clip.hits,attack||extended);
            }
            PlaceBlade(_baseR,_tipR,f.right,clip.@base,clip.tip);
            PlaceBlade(_baseL,_tipL,f.left,clip.@base,clip.tip);
            if(attack&&!extended){
                foreach(var anchor in new[]{_baseR,_tipR,_baseL,_tipL})
                    anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,accent)+accentOffset;
            }
            else if(gaitActive){
                GaitTransform("rigid-blade-R",f,clip.weapon,gait,out var rt,out var ro);
                GaitTransform("rigid-blade-L",f,clip.weapon,gait,out var lt,out var lo);
                foreach(var anchor in new[]{_baseR,_tipR})anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,rt)+ro;
                foreach(var anchor in new[]{_baseL,_tipL})anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,lt)+lo;
            }
            DriveGlow(dt,order,clip);
            if(attack){
                var plan=_player.CurrentSwingPlan;var times=new float[plan.Hits];for(int i=0;i<times.Length;i++)times[i]=plan.HitTime(i);
                string waveClip=clip.id;
                if(_player.ExecutionPoseActive){string prefix=clip.weapon=="wpn_longsword"?"sword":clip.weapon=="wpn_greatsword"?"great":"twin";waveClip=_player.ComboIndex==0?prefix+"-first-preserved":prefix+"-combo-"+(_player.ComboIndex+1);}
                _wave.DriveMany(clip.weapon,_player.ActionId,_player.PoseTime,_player.transform.position,angle,times,waveClip,order-1,_body.sortingLayerID,_body.forceRenderingOff);
            } else if(_player.InWeaponAct&&(_player.ActPhase==WeaponActPhase.Release||_player.ActPhase==WeaponActPhase.Flurry)){
                float clock=_player.ActPhaseTime;if(_waveClip!=clip.id||clock<_waveClock-.002f){_waveSerial++;_wave.Clear();}_waveClip=clip.id;_waveClock=clock;
                _wave.DriveMany(clip.weapon,_waveSerial,clock,_player.transform.position,angle,clip.hits,clip.id,order-1,_body.sortingLayerID,_body.forceRenderingOff);
            } else {_wave.Clear();_waveClip=null;_waveClock=-1;}

            _applied=true;return true;
        }


        IEnumerator WarmWeapon(string weapon)
        {
            var resources=new HashSet<string>();
            var clips=new List<Clip>(_motionClips.Values);if(_library?.clips!=null)clips.AddRange(_library.clips);
            foreach(var c in clips)if(c.weapon==weapon)
                foreach(var f in c.frames)foreach(var p in f.parts)
                    if(!p.name.Contains("rigid-"))resources.Add(p.resource);
            foreach(var resource in resources){
                if(Sprites.ContainsKey(resource))continue;
                var request=Resources.LoadAsync<Texture2D>(resource);yield return request;
                if(request.asset is Texture2D texture&&!Sprites.ContainsKey(resource)){
                    var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),160,0,SpriteMeshType.FullRect);
                    sprite.name=resource;Sprites[resource]=sprite;
                }
            }
        }
        static void GaitTransform(string part,Frame f,string weapon,CuteWalkV22 gait,out float turn,out Vector2 offset)
        {
            turn=gait.BodyTurn;offset=gait.BodyOffset;
            float local=0;Vector2 pivot=Vector2.zero;
            if(part.EndsWith("approved-head")){
                local=gait.HeadTurn;pivot=f.headOrigin!=null?new Vector2(f.headOrigin[0],f.headOrigin[1]):Vector2.zero;
                offset+=new Vector2(0,gait.HeadShift);
            } else if(weapon!="wpn_greatsword"&&(part.EndsWith("-L")||part.EndsWith("-R")||part.Contains("rigid-sword")||part.Contains("shield-edge"))){
                bool left=part.EndsWith("-L")||part.Contains("shield-edge");
                var socket=left?f.sockets?.shoulderL:f.sockets?.shoulderR;
                // Keep each arm, hand and held item on one shoulder transform.
                // Greatsword stays on the shared torso transform so its two grips cannot drift.
                if(socket!=null){pivot=new Vector2(socket[0],socket[1]);local=(left?-1:1)*gait.ShoulderTurn;}
            }
            offset+=TopDownCanvas.Rotate(pivot,turn)-TopDownCanvas.Rotate(pivot,turn+local);turn+=local;
        }

        void DriveGlow(float dt,int order,Clip clip)
        {
            float target=0;bool owns=false;
            if(_player.Pose==PlayerPose.WaveCast){target=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.18f,_player.PoseTime));owns=true;}
            else if(clip.weapon=="wpn_greatsword"&&_player.InWeaponAct){
                var act=new ActStance{Phase=_player.ActPhase,ActTime=_player.ActTime,PhaseTime=_player.ActPhaseTime,ChargeLevel=_player.ChargeLevel,ReleaseLevel=_player.ReleaseLevel,HeldTime=_player.ChargeHeldTime,FlinchKind=_player.ActKind};
                target=GreatswordPoses.Act(act).Glow;owns=true;
            }
            _glowValue=owns?target:Mathf.MoveTowards(_glowValue,0,Mathf.Max(0,dt)/.15f);
            float counter=TopDownView.PlayerRig!=null?TopDownView.PlayerRig.CounterGlow:0;
            GlowLine(_glowParts[0],_baseR,_tipR,.026f,new Color(.95f,.93f,.85f,_glowValue),order+34);
            var c=new Color(.9f,.95f,1f,counter*(.23f+.07f*Mathf.Sin(Time.time*9)));
            GlowLine(_glowParts[1],_baseR,_tipR,.05f,c,order+35);
            if(clip.weapon=="wpn_twinblades")GlowLine(_glowParts[2],_baseL,_tipL,.05f,c,order+35);
        }
        void GlowLine(SpriteRenderer sr,Transform start,Transform end,float width,Color color,int order)
        {
            sr.enabled=color.a>.01f;if(!sr.enabled)return;
            var a=start.localPosition;var b=end.localPosition;var delta=b-a;
            sr.transform.localPosition=(a+b)*.5f;sr.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            var size=sr.sprite.bounds.size;sr.transform.localScale=new Vector3(Mathf.Max(.02f,delta.magnitude)/size.x,width/size.y,1);
            color.a*=_body.color.a;sr.color=color;sr.sortingOrder=order;sr.sortingLayerID=_body.sortingLayerID;sr.forceRenderingOff=_body.forceRenderingOff;
        }

        static void ApplyBladeRelief(Part part,Sprite sprite,Frame frame,Clip clip,MaterialPropertyBlock block){
            Vector2 size=sprite.bounds.size;Vector2 at=new Vector2(part.x,part.y);
            Vector4 Line(float[] pose){var uv=(new Vector2(pose[0],pose[1])-at)/size+Vector2.one*.5f;float a=pose[3]*Mathf.Deg2Rad;return new Vector4(uv.x,uv.y,Mathf.Cos(a),Mathf.Sin(a));}
            block.SetFloat("_BladeRelief",.85f);block.SetVector("_BladeSize",new Vector4(size.x,size.y,clip.@base,clip.tip));
            block.SetVector("_BladeLineR",Line(frame.right));block.SetVector("_BladeLineL",Line(frame.left));
            block.SetVector("_BladeTilt",new Vector4(frame.right[4],frame.left[4],clip.weapon=="wpn_greatsword"?.66f:clip.weapon=="wpn_twinblades"?.19f:.28f,clip.weapon=="wpn_twinblades"?1:0));
        }
        static void PlaceBlade(Transform start,Transform tip,float[] pose,float bladeBase,float bladeTip)
        {
            float yaw=pose[3]*Mathf.Deg2Rad,tilt=pose[4]*Mathf.Deg2Rad;
            var axis=new Vector3(Mathf.Cos(tilt)*Mathf.Cos(yaw),Mathf.Cos(tilt)*Mathf.Sin(yaw),0);
            var grip=new Vector3(pose[0],pose[1],0);
            start.localPosition=grip+axis*bladeBase;tip.localPosition=grip+axis*bladeTip;
        }
    }
}
