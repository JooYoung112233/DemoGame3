using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    // Presentation only: original six toe tips, distance-based dust and contact-point stone chips.
    [DefaultExecutionOrder(320)]
    public sealed class BeetlePatternVfx : MonoBehaviour
    {
        sealed class Puff { public SpriteRenderer sr; public float age,life,size; public Vector2 at,velocity; public bool chip,impact; }
        static readonly Vector2[] NativePivots={new Vector2(295,381),new Vector2(959,381),new Vector2(221,682),new Vector2(1033,682),new Vector2(238,957),new Vector2(1016,957)};
        const int Capacity=20;
        readonly List<Sprite> owned=new List<Sprite>();
        readonly SpriteRenderer[] toes=new SpriteRenderer[6];
        readonly Puff[] pool=new Puff[Capacity];
        BeetleLifeMotionV35 life;
        Sprite bodyArt,fullArt; Sprite[] dust; Sprite chip; SpriteRenderer body; SpriteFlash flash; BoarBrain boar;
        GameObject poolRoot; bool running,shown; int next,step; float distance,weight; Vector2 previous;
        public int DustCount {get;private set;}
        public int WallImpactCount {get;private set;}
        public Vector2 LastContact {get;private set;}
        public float ToeAngle {get;private set;}
        public int ActiveCount {get{int n=0;foreach(var p in pool)if(p!=null&&p.sr&&p.sr.enabled)n++;return n;}}
        public static BeetlePatternVfx Attach(BoarBrain enemy,SpriteRenderer renderer,Sprite approved)
        {
            var fx=enemy.GetComponent<BeetlePatternVfx>();if(fx)return fx;
            if(!Resources.Load<Texture2D>("BeetleV31/body"))return null;
            fx=enemy.gameObject.AddComponent<BeetlePatternVfx>();fx.boar=enemy;fx.body=renderer;fx.fullArt=approved;fx.flash=enemy.GetComponent<SpriteFlash>();fx.Build();return fx;
        }
        Sprite Part(string name,Vector2 pivot,float ppu)
        {
            var tex=Resources.Load<Texture2D>(name);if(!tex)return null;
            var s=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),pivot,ppu,0,SpriteMeshType.FullRect);s.name=name;owned.Add(s);return s;
        }
        void Build()
        {
            bodyArt=Part("BeetleV31/body",Vector2.one*.5f,660);
            for(int i=0;i<6;i++)
            {
                var p=NativePivots[i];var go=new GameObject("Beetle toe "+(i+1));go.layer=body.gameObject.layer;go.transform.SetParent(body.transform,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=Part("BeetleV31/toe"+(i+1),new Vector2((1254-p.y)/1254,(1254-p.x)/1254),660);
                go.transform.localPosition=new Vector3((627-p.y)/660,(627-p.x)/660,0);sr.sharedMaterial=body.sharedMaterial;sr.enabled=false;toes[i]=sr;if(flash)flash.AddTarget(sr);
            }
            var tex=Resources.Load<Texture2D>("OgreVfxV30/dust");dust=new Sprite[tex.width/512];
            for(int i=0;i<dust.Length;i++){dust[i]=Sprite.Create(tex,new Rect(i*512,0,512,512),Vector2.one*.5f,512,0,SpriteMeshType.FullRect);owned.Add(dust[i]);}
            var rock=Resources.Load<Texture2D>("OgreVfxV30/rock");chip=Sprite.Create(rock,new Rect(0,0,512,512),Vector2.one*.5f,512,0,SpriteMeshType.FullRect);owned.Add(chip);
            poolRoot=new GameObject("Beetle effects (owned)");
            for(int i=0;i<Capacity;i++){var go=new GameObject("Beetle effect "+i);go.transform.SetParent(poolRoot.transform,false);var sr=go.AddComponent<SpriteRenderer>();sr.sharedMaterial=ArtRuntime.FlashMaterial;sr.enabled=false;pool[i]=new Puff{sr=sr};}
            previous=boar.Position;
            life=BeetleLifeMotionV35.TryCreate(boar,body,fullArt);
        }
        public void Pose(float phase,float amplitude,float dt)
        {
            if(!boar||boar.Dead||!body){Hide();return;}
            shown=true;
            if(life&&!running&&!boar.IsChargeArt&&!boar.Broken&&!boar.IsStunned&&boar.Aware)
            {
                life.Pose(phase,amplitude,dt);foreach(var toe in toes)if(toe)toe.enabled=false;return;
            }
            if(life)life.Hide();
            body.sprite=bodyArt;
            float target=boar.Broken?0:amplitude;weight=Mathf.MoveTowards(weight,target,Mathf.Max(0,dt)*12);
            // A small alternating tip flex; neither the shell nor the leg lengths are rescaled.
            ToeAngle=Mathf.Sin(phase*2)*weight*(running?8:5);
            for(int i=0;i<6;i++)
            {
                var sr=toes[i];sr.enabled=body.enabled;sr.gameObject.layer=body.gameObject.layer;sr.sharedMaterial=body.sharedMaterial;sr.sortingOrder=body.sortingOrder-1;sr.color=body.color;
                float sign=(i==0||i==3||i==4)?1:-1;
                sr.transform.localRotation=Quaternion.Euler(0,0,ToeAngle*sign);
            }
        }
        public void Hide()
        {
            if(life)life.Hide();
            shown=false;weight=0;ToeAngle=0;if(body&&body.sprite==bodyArt)body.sprite=fullArt;
            foreach(var sr in toes)if(sr)sr.enabled=false;
            Clear();
        }
        public void StartCharge(){running=true;distance=0;previous=boar.Position;step=0;}
        public void StopCharge(bool clearDust=false)
        {
            running=false;distance=0;
            if(clearDust)foreach(var p in pool)if(p!=null&&!p.impact){p.life=0;p.sr.enabled=false;}
        }
        public void WallImpact(Vector2 point,Vector2 normal)
        {
            if(!running||!shown||!boar||boar.Dead)return;
            running=false;distance=0;WallImpactCount++;LastContact=point;
            Vector2 outward=normal.sqrMagnitude>.1f?normal.normalized:-boar.FacingDirection;
            Vector2 tangent=new Vector2(-outward.y,outward.x);
            Emit(point+outward*.10f+tangent*.37f,.85f,.34f,false,true,outward*.25f+tangent*.5f);
            Emit(point+outward*.10f-tangent*.37f,.85f,.34f,false,true,outward*.25f-tangent*.5f);
            for(int i=0;i<4;i++)Emit(point+outward*.07f,.13f+(i%2)*.04f,.28f+i*.035f,true,true,outward*(1.1f+i*.18f)+tangent*((i-1.5f)*1.15f));
        }
        void Emit(Vector2 at,float size,float life,bool isChip,bool impact,Vector2 velocity)
        {
            var p=pool[next];next=(next+1)%Capacity;p.at=at;p.size=size;p.life=life;p.age=0;p.chip=isChip;p.impact=impact;p.velocity=velocity;p.sr.gameObject.layer=body?body.gameObject.layer:gameObject.layer;Draw(p);
        }
        public void Advance(float dt)
        {
            if(!boar||boar.Dead){Clear();return;}
            if(boar.Broken)StopCharge(true);
            if(dt<=0)return;
            foreach(var p in pool){if(p==null||p.life<=0)continue;p.age+=dt;if(p.age>=p.life){p.life=0;p.sr.enabled=false;}else Draw(p);}
            if(running&&shown)
            {
                distance+=Vector2.Distance(previous,boar.Position);
                for(int i=0;distance>=.38f&&i<3;i++,distance-=.38f)
                {
                    // Emit from alternating original middle/rear toe contacts, never from the shell centre.
                    var sr=toes[2+(step++%4)];Vector2 at=sr.transform.TransformPoint(new Vector3(-.06f,step%2==0?.025f:-.025f,0));
                    Emit(at,.54f,.24f,false,false,-boar.FacingDirection*.13f);DustCount++;
                }
            }
            previous=boar.Position;
        }
        void Draw(Puff p)
        {
            float t=Mathf.Clamp01(p.age/p.life);p.sr.enabled=true;p.sr.sprite=p.chip?chip:dust[Mathf.Min(dust.Length-1,Mathf.FloorToInt(t*dust.Length))];
            p.sr.color=new Color(1,1,1,(p.chip?.95f:.8f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t))));
            p.sr.transform.position=p.at+p.velocity*p.age+(p.chip?Vector2.up*(.17f*Mathf.Sin(t*Mathf.PI)):Vector2.zero);
            p.sr.transform.localScale=Vector3.one*p.size;p.sr.transform.rotation=Quaternion.Euler(0,0,p.chip?p.age*320:0);
            p.sr.sortingOrder=p.chip?(body?body.sortingOrder+1:1100):-55;
        }
        void LateUpdate()=>Advance(Time.deltaTime);
        public void Clear(){running=false;distance=0;foreach(var p in pool)if(p!=null){p.life=0;if(p.sr)p.sr.enabled=false;}}
        void OnDisable(){Hide();}
        void OnDestroy(){if(life){life.Hide();Destroy(life);}if(body&&body.sprite==bodyArt)body.sprite=fullArt;foreach(var sr in toes)if(sr){if(flash)flash.RemoveTarget(sr);Destroy(sr.gameObject);}if(poolRoot)Destroy(poolRoot);foreach(var s in owned)if(s)Destroy(s);}
    }
}
