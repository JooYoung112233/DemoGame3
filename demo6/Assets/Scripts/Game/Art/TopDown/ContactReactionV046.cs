using UnityEngine;

namespace Demo6.Game
{
    // Approved ten-frame contact art. Presentation only; its PRNG never consumes gameplay randomness.
    public sealed class ContactReactionV046 : MonoBehaviour
    {
        const int Capacity=32;
        const float PixelsPerUnit=512f;
        sealed class Entry { public SpriteRenderer renderer; public Sprite[] frames; public float age,step; public int index; }
        static ContactReactionV046 _instance;
        static readonly Sprite[][] Clips=new Sprite[3][];
        static uint _random=0x749ead13;
        Entry[] _pool; int _next;
        public static int SpawnCount {get;private set;}
        public static int ActiveCount {get {int n=0;if(_instance)foreach(var e in _instance._pool)if(e.renderer.enabled)n++;return n;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){_instance=null;_random=0x749ead13;SpawnCount=0;for(int i=0;i<3;i++)Clips[i]=null;}
        static float RandomSigned(){_random=1664525*_random+1013904223;return ((_random>>8)/16777215f)*2-1;}
        static Sprite[] Load(int kind)
        {
            if(Clips[kind]!=null)return Clips[kind];
            var a=new Sprite[10];string name=kind==0?"hit":kind==1?"block":"parry";
            for(int i=0;i<10;i++){
                var tex=Resources.Load<Texture2D>("ContactReactionV046/"+name+"/frame_"+(i+1).ToString("00"));
                if(!tex)return null;
                a[i]=Sprite.Create(tex,new Rect(0,0,512,512),new Vector2(.5f,.5f),PixelsPerUnit,0,SpriteMeshType.FullRect);
            }
            return Clips[kind]=a;
        }
        static bool Spawn(int kind,Vector2 at,Vector2 dir,float scale)
        {
            if(!Tuning.HitSparks)return true;
            var frames=Load(kind);if(frames==null)return false;
            if(!_instance)new GameObject("ApprovedContactReactionsV046").AddComponent<ContactReactionV046>();
            var e=_instance._pool[_instance._next++%Capacity];e.frames=frames;e.age=0;e.step=kind==2?.04f:.03f;e.index=0;
            e.renderer.sprite=frames[0];e.renderer.enabled=true;e.renderer.color=Color.white;
            e.renderer.transform.position=at;
            e.renderer.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg+RandomSigned()*7f);
            e.renderer.transform.localScale=Vector3.one*scale;SpawnCount++;return true;
        }
        public static bool Hit(Enemy enemy,Vector2 direction)
        {
            if(!enemy)return false;Vector2 dir=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.right;
            var collider=enemy.GetComponent<Collider2D>();
            Vector2 at=collider&&collider.enabled?collider.ClosestPoint(enemy.Position-dir*(enemy.Radius+1f)):enemy.Position-dir*enemy.Radius*.7f;
            var tangent=new Vector2(-dir.y,dir.x);
            at+=tangent*(RandomSigned()*.035f)+dir*(.02f+RandomSigned()*.01f);
            return Spawn(0,at,dir,1f);
        }
        public static bool Shield(Vector2 playerAt,Vector2 direction,bool parry)
        {
            var dir=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.right;
            var surface=TopDownView.PlayerRig?.FirstAttackArt?.ShieldSurface;
            if(!surface||!surface.enabled){
                var p=PlayerController.Instance;if(p)foreach(var sr in p.GetComponentsInChildren<SpriteRenderer>())if(sr.name=="Shield"&&sr.enabled){surface=sr;break;}
            }
            if(!surface||!surface.sprite)return false;
            var b=surface.sprite.bounds;
            // The central ellipse lies within the convex authored shield surface, including edge-on holds.
            float x=RandomSigned()*.22f,y=RandomSigned()*.22f;
            Vector2 at=surface.transform.TransformPoint(b.center+new Vector3(b.extents.x*x,b.extents.y*y,0));
            return Spawn(parry?2:1,at,dir,parry?1.1f:.85f);
        }
        void Awake()
        {
            _instance=this;_pool=new Entry[Capacity];
            for(int i=0;i<Capacity;i++){
                var go=new GameObject("Contact_"+i);go.transform.SetParent(transform,false);var sr=go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial=RenderMaterials.Unlit;sr.sortingOrder=3202;sr.enabled=false;_pool[i]=new Entry{renderer=sr};
            }
        }
        void Update()
        {
            foreach(var e in _pool){if(!e.renderer.enabled)continue;e.age+=Time.deltaTime;int frame=Mathf.FloorToInt(e.age/e.step);
                if(frame>=10){e.renderer.enabled=false;continue;}if(frame!=e.index){e.index=frame;e.renderer.sprite=e.frames[frame];}}
        }
        void OnDisable(){if(_pool!=null)foreach(var e in _pool)e.renderer.enabled=false;}
        void OnDestroy(){if(_instance==this)_instance=null;}
    }
}
