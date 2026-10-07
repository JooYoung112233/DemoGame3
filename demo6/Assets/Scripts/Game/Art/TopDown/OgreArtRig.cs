using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    // Art-only adapter. Reads existing brain signals; never advances AI or deals damage.
    public sealed class OgreArtRig : MonoBehaviour
    {
        public const float PixelsPerUnit = 160f;
        public static readonly string[] Names = { "ankle_L", "ankle_R", "foot_L", "foot_R", "lower_body", "torso_underlap", "free_L_original", "elbow_R_blend", "fore_R_rounded", "upper_R_rounded", "torso_clean", "grip_bridge", "club_original", "club_endface", "wrist_R_blend", "fist_R_underlap", "fist_R_original", "head_original", "eye_L_occluded", "eye_R_occluded" };
        static readonly Vector2 B=new Vector2(326,488), H=new Vector2(326,552), S=new Vector2(133,499), E=new Vector2(80,553), W=new Vector2(84,604), G=new Vector2(90,645), SL=new Vector2(550,573), Axis=new Vector2(65,293);
        sealed class Part { public Transform root; public SpriteRenderer sr; public Vector2 pivot,target; public float axis,angle,sx=1,sy=1; }
        readonly Dictionary<string,Part> parts=new Dictionary<string,Part>();
        readonly List<Sprite> ownedSprites=new List<Sprite>();
        Texture2D ringTexture; OgreLook owner;
        OgreBrain ogre; SpriteFlash flash; SpriteRenderer oldBody; SpriteRenderer ring;
        float torsoYaw,headYaw;
        readonly OgreWeightWalkV35 gait=new OgreWeightWalkV35();
        Vector2 bodyFollow,headFollow;
        public OgreWeightWalkV35 Gait => gait;
        float sweepWeight,impactAge=99,fallTime; bool falling,wasSlamLift,dustDone; Vector3 fallFrom; Vector2 fallDir; SpriteRenderer dust;
        public bool Falling => falling;
        public Vector2 ClubTipWorld => parts["club_endface"].root.position;
        public Vector2 FootWorld => parts["lower_body"].root.position;
        public int ClubSortOrder => parts["club_original"].sr.sortingOrder;
        public float TorsoYawDegrees => torsoYaw;
        public float HeadYawDegrees => headYaw;
        public float FallTime => fallTime;
        public Vector2 GripPixels {get;private set;}
        public Vector2 WristPixels {get;private set;}
        public Vector2 ElbowPixels {get;private set;}
        public static OgreArtRig TryCreate(OgreLook owner,OgreBrain brain,SpriteRenderer body,SpriteFlash hit)
        {
            var textures=new List<Texture2D>();
            foreach(var n in Names){var t=Resources.Load<Texture2D>(n=="lower_body"?"OgreMotionV35/waist":n.StartsWith("foot_")||n.StartsWith("ankle_")?"OgreMotionV35/"+n:"OgreApproved/"+n+"_styled");if(!t)return null;textures.Add(t);}
            var shader=Shader.Find("Demo6/SpriteFlash");if(!shader)return null;
            var mat=ArtRuntime.FlashMaterial;if(!mat)return null;
            var rig=owner.gameObject.AddComponent<OgreArtRig>();rig.owner=owner;rig.ogre=brain;rig.flash=hit;rig.oldBody=body;
            for(int i=0;i<Names.Length;i++)
            {
                var root=new GameObject(Names[i]+" axis").transform;root.SetParent(rig.transform,false);root.gameObject.layer=owner.gameObject.layer;
                var sr=new GameObject(Names[i]).AddComponent<SpriteRenderer>();sr.transform.SetParent(root,false);sr.gameObject.layer=owner.gameObject.layer;
                sr.sprite=Sprite.Create(textures[i],new Rect(0,0,textures[i].width,textures[i].height),Vector2.zero,PixelsPerUnit,0,SpriteMeshType.FullRect);
                rig.ownedSprites.Add(sr.sprite);
                sr.sharedMaterial=mat;sr.sortingOrder=40+i;sr.color=Color.white;hit?.AddTarget(sr);
                rig.parts.Add(Names[i],new Part{root=root,sr=sr});
            }
            // Existing phase indicator stays a separate unlit runtime effect, never painted into art.
            var ringGo=new GameObject("Existing phase 2 indicator");ringGo.transform.SetParent(rig.transform,false);
            rig.ring=ringGo.AddComponent<SpriteRenderer>();rig.ring.sprite=rig.MakeRing();rig.ring.sortingOrder=43;rig.ring.color=new Color(.86f,.85f,.81f,.9f);RenderMaterials.MakeUnlit(rig.ring);rig.ring.enabled=false;
            if(body)body.enabled=false;
            if(hit)hit.useShader=true;
            brain.GetComponent<YSort>()?.Refresh();rig.Sample(0);OgrePatternVfx.Attach(brain,rig);return rig;
        }
        Sprite MakeRing(){int n=256;var t=ringTexture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Ogre phase rim",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var a=new Color32[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float r=new Vector2(x-127.5f,y-127.5f).magnitude;a[y*n+x]=r>119&&r<125?new Color32(255,255,255,255):new Color32(0,0,0,0);}t.SetPixels32(a);t.Apply();var sprite=Sprite.Create(t,new Rect(0,0,n,n),Vector2.one*.5f,256f/2.52f);ownedSprites.Add(sprite);return sprite;}
        static float Ang(Vector2 v)=>Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg;
        static Vector2 Rot(Vector2 v,float a){float r=a*Mathf.Deg2Rad;return new Vector2(v.x*Mathf.Cos(r)-v.y*Mathf.Sin(r),v.x*Mathf.Sin(r)+v.y*Mathf.Cos(r));}
        static Vector2 Project(Vector2 v,float angle,float scale){var q=Rot(v,-Ang(Axis));q.x*=scale;return Rot(q,angle);}
        static Vector2 Quad(Vector2 a,Vector2 b,Vector2 c,float k)=>a*(1-k)*(1-k)+b*2*k*(1-k)+c*k*k;
        static Vector3 World(Vector2 p)=>new Vector3((p.x-B.x)/PixelsPerUnit,(B.y-p.y)/PixelsPerUnit,0);
        void LateUpdate(){Sample(Time.deltaTime);}
        public void Sample(float dt)
        {
            if(falling){TickFall(dt);return;}if(!ogre)return;if(ogre.Dead){owner.Fall();return;}
            if(oldBody)oldBody.enabled=false;
            bool broken=ogre.Broken,slam=ogre.CurrentPattern==Demo6.Core.Combat.BossPattern.Slam,sweep=ogre.CurrentPattern==Demo6.Core.Combat.BossPattern.Sweep;
            float lift=ogre.ArmLift,lean=ogre.Lean,a=ogre.ClubAngle;
            bool walkPose=ogre.Aware&&!broken&&!ogre.InTransition&&ogre.CurrentPose==EnemyPose.Locomotion&&(!ogre.CurrentPattern.HasValue||slam);
            // Visual cadence follows the interpolated render transform, not 50 Hz physics steps.
            Vector2 renderPosition=ogre.transform.position;
            if(broken||!ogre.Aware)gait.Reset(renderPosition);
            gait.Drive(renderPosition,Ang(ogre.FacingDirection)+90,dt,walkPose,ogre.IsMoving);
            if(slam && lift>.8f)wasSlamLift=true;
            if(slam && wasSlamLift && lift<=.001f){impactAge=0;wasSlamLift=false;}
            impactAge+=dt;
            if(!slam){wasSlamLift=false;impactAge=99;}
            float sw=sweep && !(ogre.CurrentPose==EnemyPose.Idle && Mathf.Abs(a+35)<5) ? 1:0;
            sweepWeight=Mathf.MoveTowards(sweepWeight,sw,dt*7);
            if(broken){sweepWeight=0;impactAge=99;wasSlamLift=false;}
            float shoulderLift=slam&&!broken?Mathf.SmoothStep(0,1,Mathf.Clamp01(lift)):0;
            Vector2 body=new Vector2(0,lean*7),head=new Vector2(0,lean*10),elbow=E,axis=Axis;float leftAngle=-lift*15,leftScale=1;
            // A uses the approved projected arc, driven by the actual brain angle/lift.
            if(slam)
            {
                bool raising=a<=-35;
                float k=raising?Mathf.Clamp01((-35-a)/135):Mathf.Clamp01((a+170)/199.93f);
                if(raising && lift>.99f)k=1;
                // During the strike the brain decreases ArmLift while angle crosses -35.
                bool striking=lift>0 && a>-170 && lift > Mathf.Clamp01((-35-a)/135)+.03f;
                if(striking || a>-35){k=1-lift;elbow=Quad(new Vector2(83,443),new Vector2(45,521),new Vector2(110,555),k);axis=Vector2.Lerp(new Vector2(-20,-156),new Vector2(150,270),k);}
                else{elbow=S+Rot(E-S,Mathf.Lerp(0,93,lift));axis=Vector2.Lerp(Axis,new Vector2(-20,-156),lift);}
                if(lift==0 && a>-35){float reset=Mathf.Clamp01((29.93f-a)/64.93f);elbow=Vector2.Lerp(new Vector2(110,555),E,reset);axis=Vector2.Lerp(new Vector2(150,270),Axis,reset);}
                float settle=impactAge<.24f?Mathf.Sin(impactAge/.24f*Mathf.PI)*3f:0;
                body.y=-5*lift+(a>-35?7:0)+settle;head.y=-9*lift+(a>-35?9:0)+settle*1.4f;
                leftAngle=-10*lift+(a>-35?4:0)+settle*.6f;
            }
            else
            {
                float delta=(a+35)*.42f;axis=Rot(Axis,delta)*Mathf.Lerp(.72f,1,Mathf.InverseLerp(.4f,1,ogre.ClubReach));
                elbow=E+new Vector2(-lean*5,lean*5-lift*8);head.y-=lift*5;
            }
            if(!ogre.Aware && ogre.IsEating)head.y+=Mathf.Sin(Time.time*Mathf.PI*2f*2.6f)*5f;
            if(broken){body.y=5;head.y=13;leftAngle=3;axis=Rot(Axis,-12);elbow=E+new Vector2(-5,14);}
            // A restrained follow-through only changes body presentation; the weapon angle
            // and every original strike/resolve signal remain driven by the brain.
            bool recovering=ogre.CurrentPattern.HasValue&&ogre.CurrentPose==EnemyPose.Idle;
            if(broken){bodyFollow=body;headFollow=head;}
            else if(dt>0){bodyFollow=Vector2.Lerp(bodyFollow,body,1-Mathf.Exp(-18*dt));headFollow=Vector2.Lerp(headFollow,head,1-Mathf.Exp(-11*dt));}
            if(recovering){body=Vector2.Lerp(body,bodyFollow,.22f);head=Vector2.Lerp(head,headFollow,.32f);}
            elbow=S+Vector2.ClampMagnitude(elbow-S,(E-S).magnitude);
            float fp=.35f+.65f*Mathf.Min(1,axis.magnitude/300);float angle=Ang(axis);
            Vector2 wrist=elbow+Project(W-E,angle,fp),grip=wrist+Project(G-W,angle,fp);
            if(sweepWeight>0)
            {
                // Articulated sweep: keep native segment lengths and move the grip with the arm.
                // A fixed low grip stretched the upper arm and reversed the forearm at either end.
                float theta=Mathf.Clamp(a,-120,120)*.68f;
                Vector2 sweepAxis=Rot(new Vector2(0,293),-theta)*ogre.ClubReach;
                // The upper arm opens from the shoulder across both sweeps, rather than staying fixed.
                Vector2 sweepElbow=S+Rot(E-S,-10-Mathf.Clamp(a/120f,-1,1)*26f);
                float foreAngle=98+Mathf.DeltaAngle(90,Ang(sweepAxis))*.5f;
                // Interpolate joint angles and projected lengths, not endpoints that shorten the arm mid-blend.
                Vector2 fore=wrist-elbow;
                float upperLength=Mathf.Lerp((elbow-S).magnitude,(E-S).magnitude,sweepWeight);
                float upperAngle=Mathf.LerpAngle(Ang(elbow-S),Ang(sweepElbow-S),sweepWeight);
                elbow=S+Rot(new Vector2(upperLength,0),upperAngle);
                float foreLength=Mathf.Lerp(fore.magnitude,(W-E).magnitude,sweepWeight);
                wrist=elbow+Rot(new Vector2(foreLength,0),Mathf.LerpAngle(Ang(fore),foreAngle,sweepWeight));
                axis=Vector2.Lerp(axis,sweepAxis,sweepWeight);fp=Mathf.Lerp(fp,1,sweepWeight);angle=Ang(axis);
                leftAngle-=Mathf.Clamp(a/120f,-1,1)*8f*sweepWeight;
            }
            grip=wrist+Project(G-W,angle,fp);
            // Rigid shoulder articulation: same transform for upper arm, forearm,
            // wrist, fist and club. Never lengthen a limb or separate the grip.
            // Image-space negative shoulder rotation follows positive world torso yaw; the heavy club lags a little.
            float shoulder=-gait.ShoulderTurn*.5f+gait.ClubFollow*.35f-12f*shoulderLift;
            elbow=S+Rot(elbow-S,shoulder);wrist=S+Rot(wrist-S,shoulder);grip=S+Rot(grip-S,shoulder);
            axis=Rot(axis,shoulder);angle+=shoulder;leftAngle-=gait.ShoulderTurn;
            // Raising contracts the right shoulder toward the chest in plan view; it does not lift world Y.
            Vector2 shoulderInset=(B-S).normalized*(12f*shoulderLift);
            Vector2 shoulderAt=S+body+shoulderInset;
            elbow+=body+shoulderInset;wrist+=body+shoulderInset;grip+=body+shoulderInset;
            ElbowPixels=elbow;WristPixels=wrist;GripPixels=grip;
            // Cosmetic weight transfer about the planted lower body; every arm piece receives
            // the same rigid transform, retaining the approved joint lengths and overlap.
            float recoil=impactAge<.28f?Mathf.Sin(impactAge/.28f*Mathf.PI):0;
            bool roaring=ogre.CurrentPattern==Demo6.Core.Combat.BossPattern.Roar||ogre.InTransition;
            // Apply mirrors image-space arm angles; positive world yaw follows the sweep's -theta image arc.
            float targetYaw=slam?-10f*shoulderLift+4f*recoil:sweep?Mathf.Clamp(a/120f,-1,1)*18f*sweepWeight:roaring?Mathf.Sin(lift*Mathf.PI)*1.3f:-lean*2f;
            targetYaw+=gait.TorsoTurn;
            if(broken)targetYaw=0;
            torsoYaw=Mathf.LerpAngle(torsoYaw,targetYaw,1-Mathf.Exp(-(recovering?12f:30f)*dt));
            headYaw=Mathf.LerpAngle(headYaw,torsoYaw-gait.TorsoTurn+gait.HeadTurn,1-Mathf.Exp(-13*dt));
            Vector2 sway=slam?new Vector2(-8*lift,-8*lift+8*recoil):roaring?new Vector2(0,-9*lift):new Vector2(torsoYaw*.8f,lean*5);
            sway.x+=gait.WeightShift*PixelsPerUnit;
            if(broken)sway=Vector2.zero;
            foreach(var kv in parts)
            {
                string n=kv.Key;var p=kv.Value;p.pivot=B;p.target=B+body;p.axis=0;p.angle=0;p.sx=p.sy=1;
                if(n.StartsWith("ankle_")){int side=n=="ankle_L"?0:1;var at=side==0?gait.Left:gait.Right;var hip=new Vector2(side==0?229:423,615);Bone(p,hip,new Vector2(hip.x,668),hip,new Vector2(B.x+at.x*PixelsPerUnit,B.y-at.y*PixelsPerUnit));}
                else if(n.StartsWith("foot_")){int side=n=="foot_L"?0:1;p.pivot=new Vector2(side==0?229:423,668);var at=side==0?gait.Left:gait.Right;p.target=new Vector2(B.x+at.x*PixelsPerUnit,B.y-at.y*PixelsPerUnit);}
                else if(n=="lower_body"){p.pivot=new Vector2(326,670);p.target=p.pivot;p.sy=1-.018f*recoil+(roaring&&!broken?.009f*lift:0);}
                else if(n.StartsWith("upper_R"))Bone(p,S,E,shoulderAt,elbow);
                else if(n.StartsWith("fore_R"))Bone(p,E,W,elbow,wrist);
                else if(n=="free_L_original"){p.pivot=SL;p.target=SL+body;p.angle=leftAngle;p.sx=leftScale;}
                else if(n.StartsWith("fist_R")||n=="grip_bridge"){p.pivot=W;p.target=wrist;p.axis=Ang(Axis);p.angle=angle;p.sx=fp;}
                else if(n=="elbow_R_blend"){p.pivot=E;p.target=elbow;p.angle=(Ang(elbow-shoulderAt)-Ang(E-S)+angle-Ang(Axis))*.5f;}
                else if(n=="wrist_R_blend"){p.pivot=W;p.target=wrist;p.angle=angle-Ang(Axis);}
                else if(n=="club_endface"){p.pivot=new Vector2(155,938);p.target=grip+axis;p.axis=Ang(Axis);p.angle=angle;p.sx=1+.6f*(1-Mathf.Min(1,axis.magnitude/300));}
                else if(n=="club_original"){p.pivot=G;p.target=grip;p.axis=Ang(Axis);p.angle=angle;p.sx=Mathf.Max(.14f,axis.magnitude/Axis.magnitude);}
                else if(n.StartsWith("head")||n.StartsWith("eye")){p.pivot=H;p.target=H+head;}
                Apply(p);
                if(n!="lower_body"&&!n.StartsWith("foot_")&&!n.StartsWith("ankle_"))
                {
                    bool isHead=n.StartsWith("head")||n.StartsWith("eye");
                    // Keep a small head lag, bounded so the neck stays inside the shoulder overlap.
                    float yaw=isHead?torsoYaw+Mathf.Clamp(Mathf.DeltaAngle(torsoYaw,headYaw),-6f,6f):torsoYaw;
                    var turn=Quaternion.Euler(0,0,yaw);var pivot=World(new Vector2(326,630));
                    p.root.localPosition=pivot+turn*(p.root.localPosition-pivot)+new Vector3(sway.x/PixelsPerUnit,-sway.y/PixelsPerUnit,0);
                    p.root.localRotation=turn*p.root.localRotation;
                }
                float shade=broken?.62f:1;float alpha=oldBody?oldBody.color.a:p.sr.color.a;
                p.sr.color=new Color(shade,shade,shade,alpha);
            }
            transform.localRotation=Quaternion.Euler(0,0,Ang(ogre.FacingDirection)+90);
            transform.localPosition=ogre.VisualJitter;transform.localScale=Vector3.one*(broken?.92f:1);
            ring.enabled=ogre.Phase>=2;
        }
        static void Bone(Part p,Vector2 a,Vector2 b,Vector2 ta,Vector2 tb){p.pivot=a;p.target=ta;p.axis=Ang(b-a);p.angle=Ang(tb-ta);p.sx=(tb-ta).magnitude/(b-a).magnitude;}
        static void Apply(Part p)
        {
            p.root.localPosition=World(p.target);p.root.localRotation=Quaternion.Euler(0,0,-p.angle);p.root.localScale=new Vector3(p.sx,p.sy,1);
            p.sr.transform.localRotation=Quaternion.Euler(0,0,p.axis);
            p.sr.transform.localPosition=Quaternion.Euler(0,0,p.axis)*new Vector3(-p.pivot.x/PixelsPerUnit,(p.pivot.y-1254)/PixelsPerUnit,0);
        }
        public void BeginFall()
        {
            if(falling)return;falling=true;fallTime=0;fallFrom=transform.position;fallDir=ogre?ogre.FacingDirection:Vector2.down;
            transform.SetParent(null,true);transform.localScale=Vector3.one;ring.enabled=false;
            ogre?.GetComponent<YSort>()?.Refresh();
            var block=new MaterialPropertyBlock();
            for(int i=0;i<Names.Length;i++){var p=parts[Names[i]];flash?.RemoveTarget(p.sr);p.sr.sortingOrder=-989+i;
                p.sr.GetPropertyBlock(block);block.SetFloat("_FlashAmount",0);p.sr.SetPropertyBlock(block);block.Clear();}
            // Release the club as in OgreLook.Fall; retain separate articulated body pieces.
            foreach(string n in new[]{"club_original","club_endface","grip_bridge"})parts[n].root.localPosition+=new Vector3(-.25f,-.3f,0);
            flash=null;
        }
        public void TickFall(float dt)
        {
            fallTime+=dt;float k=Mathf.Clamp01(fallTime/.7f),e=k*k;
            transform.position=fallFrom+(Vector3)(fallDir*.7f*e);transform.localScale=new Vector3(1-.06f*e,1+.25f*e,1);
            if(k>=1&&!dustDone)
            {
                dustDone=true;ScreenShake.Add(.1f,.2f);OgreSounds.Play(OgreSound.Slam,.7f,.7f);
                var go=new GameObject("OgreFallDust");go.transform.position=transform.position;go.layer=gameObject.layer;dust=go.AddComponent<SpriteRenderer>();dust.sprite=ShapeSprites.Ring;dust.sortingOrder=-955;
            }
            if(dust){float d=Mathf.Clamp01((fallTime-.7f)/.6f);dust.transform.localScale=Vector3.one*Mathf.Lerp(2.6f,6.4f,d);dust.color=new Color(.55f,.5f,.43f,.5f*(1-d));if(d>=1)Destroy(dust.gameObject);}
            float alpha=1-Mathf.Clamp01((fallTime-.7f-20)/1.5f);
            foreach(var p in parts.Values){float shade=Mathf.Lerp(1,.5f,e);p.sr.color=new Color(shade,shade,shade,alpha);}
            if(fallTime>=22.2f)Destroy(gameObject);
        }
        void OnDestroy()
        {
            if(dust)Destroy(dust.gameObject);
            foreach(var p in parts.Values)if(p.sr)flash?.RemoveTarget(p.sr);
            foreach(var sprite in ownedSprites)if(sprite)Destroy(sprite);
            if(ringTexture)Destroy(ringTexture);
        }
    }
}
