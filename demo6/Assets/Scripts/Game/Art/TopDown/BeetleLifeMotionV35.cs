using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    // Existing approved layers, cosmetic motion only. Charge stays on the v031 path.
    public sealed class BeetleLifeMotionV35 : MonoBehaviour
    {
        static readonly Vector2[] Pivots={new Vector2(403,459),new Vector2(851,459),new Vector2(332,644),new Vector2(922,644),new Vector2(344,813),new Vector2(910,813)};
        readonly List<Sprite> owned=new List<Sprite>();
        readonly SpriteRenderer[] legs=new SpriteRenderer[6];
        SpriteRenderer body,shell,head,underlap;Sprite original;BoarBrain boar;SpriteFlash flash;
        MaterialPropertyBlock block;
        float weight,headTurn,headPush,shellPush,brace,headScale=1;
        public float HeadDegrees=>headTurn;
        public float Brace=>brace;
        public static BeetleLifeMotionV35 TryCreate(BoarBrain b,SpriteRenderer renderer,Sprite full)
        {
            var names=new[]{"underlap","shell","head","leg1","leg2","leg3","leg4","leg5","leg6"};
            var textures=new List<Texture2D>();foreach(var n in names){var t=Resources.Load<Texture2D>("BeetleMotionV35/"+n);if(!t)return null;textures.Add(t);}
            var r=b.gameObject.AddComponent<BeetleLifeMotionV35>();r.boar=b;r.body=renderer;r.original=full;r.flash=b.GetComponent<SpriteFlash>();
            r.underlap=r.Make("underlap",textures[0],new Vector2(627,627));r.shell=r.Make("shell",textures[1],new Vector2(627,627));r.head=r.Make("head",textures[2],new Vector2(627,450));
            for(int i=0;i<6;i++)r.legs[i]=r.Make("leg"+(i+1),textures[i+3],Pivots[i]);r.Hide();return r;
        }
        SpriteRenderer Make(string name,Texture2D tex,Vector2 p)
        {
            var g=new GameObject("Beetle v035 "+name);g.transform.SetParent(body.transform,false);g.layer=body.gameObject.layer;
            var s=g.AddComponent<SpriteRenderer>();s.sprite=Sprite.Create(tex,new Rect(0,0,1254,1254),new Vector2((1254-p.y)/1254,(1254-p.x)/1254),660,0,SpriteMeshType.FullRect);
            s.transform.localPosition=Home(p);owned.Add(s.sprite);s.sharedMaterial=body.sharedMaterial;flash?.AddTarget(s);return s;
        }
        static Vector2 Home(Vector2 p)=>new Vector2((627-p.y)/660,(627-p.x)/660);
        static float Smooth(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
        public void Pose(float phase,float amplitude,float dt)
        {
            if(!boar||boar.Dead||boar.Broken||!body){Hide();return;}
            bool windup=boar.CurrentPose==EnemyPose.Windup,attack=boar.CurrentPose==EnemyPose.Attack;
            float p=windup?Mathf.Clamp01(boar.PoseTime/Mathf.Max(.05f,boar.PoseDuration)):0;
            float hit=attack?Mathf.Clamp01(1-boar.PoseTime/.22f):0;
            float sign=boar.IsRearAttackArt?-1:1;
            if(dt>0)
            {
                weight=Mathf.MoveTowards(weight,windup||attack?0:amplitude,dt*10);
                brace=Mathf.Lerp(brace,windup?Smooth(p):hit,1-Mathf.Exp(-28*dt));
                float target=Mathf.Clamp(Mathf.DeltaAngle(body.transform.eulerAngles.z,Mathf.Atan2(boar.FacingDirection.y,boar.FacingDirection.x)*Mathf.Rad2Deg),-5,5)*.8f;
                headTurn=Mathf.LerpAngle(headTurn,target,1-Mathf.Exp(-18*dt));
                headPush=Mathf.Lerp(headPush,sign*(-.024f*Smooth(p)+.042f*hit),1-Mathf.Exp(-26*dt));
                shellPush=Mathf.Lerp(shellPush,sign*(-.008f*Smooth(p)+.020f*hit),1-Mathf.Exp(-14*dt));
                headScale=Mathf.Lerp(headScale,windup?1-.035f*Smooth(p):1,1-Mathf.Exp(-22*dt));
            }
            if(block==null)block=new MaterialPropertyBlock();body.sprite=null;body.GetPropertyBlock(block);
            Sync(underlap,-2);Sync(shell,0);Sync(head,1);
            shell.transform.localPosition=new Vector3(shellPush,0,0);shell.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(phase-.25f)*.65f*weight);
            head.transform.localPosition=(Vector3)Home(new Vector2(627,450))+new Vector3(headPush,0,0);head.transform.localRotation=Quaternion.Euler(0,0,headTurn);head.transform.localScale=new Vector3(headScale,1,1);
            for(int i=0;i<6;i++)
            {
                Sync(legs[i],-1);bool group=i==0||i==3||i==4;float u=Mathf.Repeat(phase/(Mathf.PI*2)+(group?0:.5f),1);
                // Slow support sweep and short smooth recovery, alternating tripods.
                float step=u<.64f?Mathf.Lerp(-6.5f,6.5f,u/.64f):Mathf.Lerp(6.5f,-6.5f,Smooth((u-.64f)/.36f));
                float side=i%2==0?1:-1;bool support=boar.IsRearAttackArt?i>=4:i<2;
                float angle=side*(step*weight+(support?-6.0f:2.0f)*brace);
                legs[i].transform.localRotation=Quaternion.Euler(0,0,angle);
            }
        }
        void Sync(SpriteRenderer s,int order){s.enabled=body.enabled;s.color=body.color;s.sharedMaterial=body.sharedMaterial;s.sortingLayerID=body.sortingLayerID;s.sortingOrder=body.sortingOrder+order;s.gameObject.layer=body.gameObject.layer;s.forceRenderingOff=body.forceRenderingOff;s.SetPropertyBlock(block);}
        public void Hide()
        {
            if(body&&!body.sprite)body.sprite=original;
            if(shell)shell.enabled=false;if(head)head.enabled=false;if(underlap)underlap.enabled=false;foreach(var s in legs)if(s)s.enabled=false;
            weight=brace=headTurn=headPush=shellPush=0;headScale=1;
        }
        void OnDisable(){Hide();}
        void OnDestroy(){Hide();foreach(var s in legs)if(s){flash?.RemoveTarget(s);Destroy(s.gameObject);}foreach(var s in new[]{shell,head,underlap})if(s){flash?.RemoveTarget(s);Destroy(s.gameObject);}foreach(var s in owned)if(s)Destroy(s);}
    }
}
