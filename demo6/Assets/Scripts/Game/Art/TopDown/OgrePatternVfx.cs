using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    // Cosmetic only. Brain cues mark actual resolution; no combat queries or shared RNG.
    [DefaultExecutionOrder(300)]
    public sealed class OgrePatternVfx : MonoBehaviour
    {
        enum Kind { Slam, Dust, Sweep, Roar, Rock, HeavySlam }
        sealed class Puff { public SpriteRenderer sr; public Kind kind; public float age,life,delay,size,angle; public Vector2 at; }
        struct Cue { public Kind kind; public Vector2 at; public float size,life,angle,delay; }
        const int Capacity=24;
        readonly Dictionary<Kind,Sprite[]> clips=new Dictionary<Kind,Sprite[]>();
        readonly List<Sprite> owned=new List<Sprite>();
        readonly List<Cue> queued=new List<Cue>();
        readonly Puff[] pool=new Puff[Capacity];
        GameObject poolRoot; OgreBrain ogre; OgreArtRig rig; int next,side;
        bool running,haveTip; float distance,previousAngle; Vector2 previousPosition,previousTip;
        public int ActiveCount { get { int n=0;foreach(var p in pool)if(p!=null&&p.sr&&p.sr.enabled)n++;return n; } }
        public int SlamCount{get;private set;} public int SweepCount{get;private set;} public int RoarCount{get;private set;} public int RockCount{get;private set;} public int DustCount{get;private set;}
        public float LastImpactTime{get;private set;}
        public static void Attach(OgreBrain o,OgreArtRig r)
        {
            if(o.GetComponent<OgrePatternVfx>())return;
            var fx=o.gameObject.AddComponent<OgrePatternVfx>();fx.ogre=o;fx.rig=r;fx.Build();
        }
        void Build()
        {
            foreach(var pair in new[]{new KeyValuePair<Kind,string>(Kind.HeavySlam,"slam-heavy"),new KeyValuePair<Kind,string>(Kind.Slam,"slam"),new KeyValuePair<Kind,string>(Kind.Dust,"dust"),new KeyValuePair<Kind,string>(Kind.Sweep,"sweep"),new KeyValuePair<Kind,string>(Kind.Roar,"roar"),new KeyValuePair<Kind,string>(Kind.Rock,"rock")})
            {
                var tex=Resources.Load<Texture2D>((pair.Key==Kind.HeavySlam?"OgreSlamHeavyV1/":"OgreVfxV30/")+pair.Value);if(!tex)continue;
                int count=tex.width/512;var frames=new Sprite[count];
                for(int i=0;i<count;i++){frames[i]=Sprite.Create(tex,new Rect(i*512,0,512,512),Vector2.one*.5f,512,0,SpriteMeshType.FullRect);owned.Add(frames[i]);}
                clips.Add(pair.Key,frames);
            }
            poolRoot=new GameObject("Ogre pattern effects (owned)");
            for(int i=0;i<Capacity;i++){var go=new GameObject("Ogre effect "+i);go.transform.SetParent(poolRoot.transform,false);var sr=go.AddComponent<SpriteRenderer>();sr.enabled=false;pool[i]=new Puff{sr=sr};}
            previousPosition=ogre.Position;previousAngle=ogre.ClubAngle;
        }
        void Queue(Kind k,Vector2 at,float size,float life,float angle=0,float delay=0)=>queued.Add(new Cue{kind=k,at=at,size=size,life=life,angle=angle,delay=delay});
        public void Slam(Vector2 at){SlamCount++;LastImpactTime=Time.time;Queue(Kind.HeavySlam,at,4.1f,.32f);}
        public void Sweep(){SweepCount++;LastImpactTime=Time.time;}
        public void Roar(){RoarCount++;Queue(Kind.Roar,ogre.Position,4.8f,.38f);}
        public void RockWarning(Vector2 at,float delay)=>Queue(Kind.Rock,at,1.1f,.13f,0,Mathf.Max(0,delay-.13f));
        public void RockImpact(Vector2 at)
        {
            RockCount++;LastImpactTime=Time.time;
            foreach(var p in pool)if(p!=null&&p.kind==Kind.Rock&&(p.at-at).sqrMagnitude<.01f){p.life=0;p.sr.enabled=false;}
            Queue(Kind.Slam,at,2.1f,.28f);
        }
        public void StartRun(){running=true;distance=.55f;previousPosition=ogre.Position;}
        public void EndRun(bool wall){running=false;if(wall)Queue(Kind.Slam,ogre.Position+ogre.FacingDirection*1.15f,2.25f,.28f);}
        public void Clear()
        {
            running=false;haveTip=false;queued.Clear();distance=0;
            foreach(var p in pool)if(p!=null){p.life=0;if(p.sr)p.sr.enabled=false;}
        }
        void LateUpdate()=>Advance(Time.deltaTime);
        public void Advance(float dt)
        {
            if(!ogre||ogre.Dead||ogre.Broken){Clear();return;}
            // An impact cue still displays its first frame when that hit starts hit-stop.
            if(dt<=0){foreach(var c in queued)Emit(c);queued.Clear();return;}
            // Expire existing instances before adding this frame's impact, preserving its first frame.
            foreach(var p in pool)
            {
                if(p==null||p.life<=0)continue;p.age+=dt;
                if(p.age>=p.delay+p.life){p.life=0;p.sr.enabled=false;continue;}
                Draw(p);
            }
            if(running)
            {
                distance+=Vector2.Distance(previousPosition,ogre.Position);
                for(int i=0;distance>=.55f&&i<2;i++,distance-=.55f)
                {
                    var f=ogre.FacingDirection;var lateral=new Vector2(-f.y,f.x)*(side++%2==0?.95f:-.95f);
                    Queue(Kind.Dust,(rig?rig.FootWorld:ogre.Position)-f*.15f+lateral,1.2f,.3f,Mathf.Atan2(f.y,f.x)*Mathf.Rad2Deg);DustCount++;
                }
            }
            previousPosition=ogre.Position;
            if(rig)
            {
                Vector2 tip=rig.ClubTipWorld;float delta=Mathf.DeltaAngle(previousAngle,ogre.ClubAngle);
                // Windup speed is 630deg/s, actual sweep is 2400deg/s. Only its short strike leaves a trail.
                if(haveTip&&ogre.CurrentPattern==Demo6.Core.Combat.BossPattern.Sweep&&Mathf.Abs(delta)/dt>1100&&Vector2.Distance(tip,previousTip)<4)
                {
                    var movement=tip-previousTip;float size=Mathf.Clamp(movement.magnitude*1.35f,.8f,2f);
                    var normal=new Vector2(movement.y,-movement.x).normalized;
                    var at=(previousTip+tip)*.5f-normal*size*.20f;
                    Queue(Kind.Sweep,at,size,.10f,Mathf.Atan2(movement.y,movement.x)*Mathf.Rad2Deg-90);
                }
                previousTip=tip;previousAngle=ogre.ClubAngle;haveTip=true;
            }
            foreach(var c in queued)Emit(c);queued.Clear();
        }
        void Emit(Cue c)
        {
            if(c.kind==Kind.Sweep)foreach(var old in pool)if(old!=null&&old.kind==Kind.Sweep){old.life=0;old.sr.enabled=false;}
            if(!clips.ContainsKey(c.kind))return;var p=pool[next];next=(next+1)%Capacity;
            p.kind=c.kind;p.age=0;p.life=c.life;p.delay=c.delay;p.at=c.at;p.size=c.size;p.angle=c.angle;
            p.sr.gameObject.layer=rig?rig.gameObject.layer:gameObject.layer;
            if(c.kind==Kind.Sweep||c.kind==Kind.Roar)RenderMaterials.MakeUnlit(p.sr);else p.sr.sharedMaterial=ArtRuntime.FlashMaterial;
            Draw(p);
        }
        void Draw(Puff p)
        {
            p.sr.enabled=p.age>=p.delay;if(!p.sr.enabled)return;
            float t=Mathf.Clamp01((p.age-p.delay)/p.life);var frames=clips[p.kind];p.sr.sprite=frames[Mathf.Min(frames.Length-1,Mathf.FloorToInt(t*frames.Length))];
            float alpha=p.kind==Kind.Sweep?.62f:p.kind==Kind.Roar?.38f:.78f;
            alpha*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,1,t));
            p.sr.color=new Color(1,1,1,alpha);
            p.sr.transform.position=p.at+(p.kind==Kind.Rock?Vector2.up*(1.6f*(1-t)*(1-t)):Vector2.zero);
            p.sr.transform.rotation=Quaternion.Euler(0,0,p.angle);
            p.sr.transform.localScale=Vector3.one*p.size;
            p.sr.sortingOrder=p.kind==Kind.Rock?1040-Mathf.RoundToInt(p.at.y*20):p.kind==Kind.Sweep&&rig?rig.ClubSortOrder-1:-55;
        }
        void OnDisable()=>Clear();
        void OnDestroy(){if(poolRoot)Destroy(poolRoot);foreach(var s in owned)if(s)Destroy(s);}
    }
}
