using System.Collections.Generic;
using UnityEngine;
namespace Demo6.Game
{
    // Independent authored crescents. Only attack identity, heading and existing hit clocks are read.
    // There is no blade-socket sampling, gameplay collision, damage or range mutation.
    public sealed class SlashWaveV044 : MonoBehaviour
    {
        struct Pulse { public bool started; public Vector3 origin; public float angle; }
        readonly Pulse[] _pulses=new Pulse[16];
        readonly List<Vector3> _vertices=new List<Vector3>(256);
        readonly List<Vector2> _uv=new List<Vector2>(256);
        readonly List<Color> _colors=new List<Color>(256);
        readonly List<int> _triangles=new List<int>(768);
        Mesh _mesh;MeshRenderer _renderer;Material _material;
        int _action=-1,_blockedAction=-1;string _weapon;Vector3 _previousOwner;
        public int Count=>_renderer&&_renderer.enabled?_vertices.Count:0;
        public int ActiveWaves {get;private set;}
        public bool Emitting=>ActiveWaves>0;
        public int TeleportResets {get;private set;}
        public int SortingOrder=>_renderer?_renderer.sortingOrder:0;
        public string MaterialShader=>_material&&_material.shader?_material.shader.name:null;
        void Awake(){
            _mesh=new Mesh{name="Independent slash crescents V044"};_mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _renderer=gameObject.AddComponent<MeshRenderer>();
            _material=new Material(Shader.Find("Demo6/SlashWaveV044")){hideFlags=HideFlags.DontSave};
            _renderer.sharedMaterial=_material;_renderer.enabled=false;
            _renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_renderer.receiveShadows=false;
        }
        public void Clear(){
            _action=-1;_blockedAction=-1;_weapon=null;ActiveWaves=0;
            for(int i=0;i<_pulses.Length;i++)_pulses[i]=default;
            _vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
            if(_mesh)_mesh.Clear();if(_renderer)_renderer.enabled=false;
        }
        public void Drive(string weapon,int action,float clock,Vector3 origin,float heading,int hits,float hit0,float hit1,int order,int layer,bool hidden)
        {
            if(_blockedAction==action)return;
            if(_action==action&&(origin-_previousOwner).sqrMagnitude>9f){TeleportResets++;Clear();_blockedAction=action;return;}
            if(_action!=action||_weapon!=weapon){Clear();_action=action;_weapon=weapon;}
            _previousOwner=origin;
            bool great=weapon=="wpn_greatsword",twin=weapon=="wpn_twinblades";
            float lead=great?.065f:twin?.035f:.048f;
            float life=great?.265f:twin?.19f:.225f;
            float radius=great?2.65f:twin?1.98f:2.22f;
            float width=great?.49f:twin?.27f:.36f;
            float span=great?155f:twin?112f:139f;
            _vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();ActiveWaves=0;
            for(int i=0;i<(twin?Mathf.Min(hits,2):1);i++){
                float age=clock-(i==0?hit0:hit1)+lead;
                if(age<0||age>life)continue;
                var pulse=_pulses[i];
                if(!pulse.started){pulse.started=true;pulse.origin=origin;pulse.angle=heading;_pulses[i]=pulse;}
                float progress=Mathf.Clamp01(age/life);
                float spread=1-Mathf.Pow(1-Mathf.Clamp01(age/(lead+.012f)),3);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.26f,1f,progress));
                float opacity=Mathf.SmoothStep(0,1,age/.018f)*fade*(great?.82f:twin?.80f:.80f);
                if(opacity<.004f)continue;
                float r=radius*Mathf.Lerp(.80f,1.055f,1-Mathf.Pow(1-progress,2));
                float sweep=Mathf.Lerp(-span*.42f,span*.55f,spread);
                float drawnSpan=span*Mathf.Lerp(.14f,1f,spread);
                float direction=i==1?-1:1;
                // Two distinct crescent branches cross/answer one another at the twin hit clocks.
                float offset=twin?(i==0?-16f:22f):great?-6f:0;
                float head=offset+direction*sweep;
                float tail=head-direction*drawnSpan;
                var forward=Rotate(Vector2.right,pulse.angle);
                var side=Rotate(Vector2.up,pulse.angle);
                Vector3 center=pulse.origin+(Vector3)(forward*.10f+side*(twin?(i==0?-.09f:.09f):0));
                AddCrescent(center,pulse.angle,tail,head,r,width*(great?1.05f:1f),opacity);
                    if(age<.085f)AddCrescent(center,pulse.angle,Mathf.Lerp(tail,head,.30f),head,r+.075f,great?.035f:twin?.016f:.022f,opacity*.52f*(1-age/.085f));
                ActiveWaves++;
            }
            _renderer.sortingOrder=order;_renderer.sortingLayerID=layer;_renderer.forceRenderingOff=hidden;
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);_mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
            _renderer.enabled=ActiveWaves>0&&!hidden;
        }
        // V045: the same independent white crescent for later combo and weapon-action hits.
        // Only presentation changes: these pulses never call damage or change hit schedules.
        public void DriveMany(string weapon,int action,float clock,Vector3 origin,float heading,float[] hitTimes,string clipId,int order,int layer,bool hidden)
        {
            if(hitTimes==null||hitTimes.Length==0){Clear();return;}
            if(clipId.Contains("-first-")){
                Drive(weapon,action,clock,origin,heading,hitTimes.Length,hitTimes[0],hitTimes.Length>1?hitTimes[1]:0,order,layer,hidden);return;
            }
            if(_blockedAction==action)return;
            if(_action==action&&(origin-_previousOwner).sqrMagnitude>9f){TeleportResets++;Clear();_blockedAction=action;return;}
            if(_action!=action||_weapon!=weapon){Clear();_action=action;_weapon=weapon;}
            _previousOwner=origin;
            bool great=weapon=="wpn_greatsword",twin=weapon=="wpn_twinblades",flurry=clipId=="twin-flurry";
            bool crossed=clipId=="twin-combo-3"||clipId=="twin-combo-4";
            float lead=great?.065f:twin?.035f:.048f,life=great?.265f:twin?.19f:.225f;
            float radius=great?2.65f:twin?1.98f:2.22f,width=great?.49f:twin?.27f:.36f,span=great?155f:twin?112f:139f;
            _vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();ActiveWaves=0;int slot=0;
            for(int hit=0;hit<hitTimes.Length;hit++){
                int branches=twin&&(crossed||(flurry&&hit==hitTimes.Length-1))?2:1;
                for(int branch=0;branch<branches&&slot<_pulses.Length;branch++,slot++){
                    float direction=branches==2?(branch==0?1:-1):twin?((hit%2==0?1:-1)*(clipId=="twin-combo-2"?-1:1)):clipId=="sword-combo-2"?-1:1;
                    float age=clock-hitTimes[hit]+lead;if(age<0||age>life)continue;
                    var pulse=_pulses[slot];if(!pulse.started){pulse.started=true;pulse.origin=origin;pulse.angle=heading;_pulses[slot]=pulse;}
                    float progress=Mathf.Clamp01(age/life),spread=1-Mathf.Pow(1-Mathf.Clamp01(age/(lead+.012f)),3);
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.26f,1f,progress));
                    float opacity=Mathf.SmoothStep(0,1,age/.018f)*fade*(great?.82f:.80f);if(opacity<.004f)continue;
                    float r=radius*Mathf.Lerp(.80f,1.055f,1-Mathf.Pow(1-progress,2));
                    float sweep=Mathf.Lerp(-span*.42f,span*.55f,spread),drawnSpan=span*Mathf.Lerp(.14f,1f,spread);
                    float offset=twin?(direction>0?-16f:22f):great?-6f:0,head=offset+direction*sweep,tail=head-direction*drawnSpan;
                    var forward=Rotate(Vector2.right,pulse.angle);var side=Rotate(Vector2.up,pulse.angle);
                    Vector3 center=pulse.origin+(Vector3)(forward*.10f+side*(twin?(direction>0?-.09f:.09f):0));
                    AddCrescent(center,pulse.angle,tail,head,r,width*(great?1.05f:1f),opacity);
                    if(age<.085f)AddCrescent(center,pulse.angle,Mathf.Lerp(tail,head,.30f),head,r+.075f,great?.035f:twin?.016f:.022f,opacity*.52f*(1-age/.085f));ActiveWaves++;
                }
            }
            _renderer.sortingOrder=order;_renderer.sortingLayerID=layer;_renderer.forceRenderingOff=hidden;
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);_mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
            _renderer.enabled=ActiveWaves>0&&!hidden;
        }
        void AddCrescent(Vector3 center,float heading,float tail,float head,float radius,float width,float alpha){
            const int steps=40;int begin=_vertices.Count;
            for(int i=0;i<=steps;i++){
                float u=i/(float)steps;
                float angle=(heading+Mathf.Lerp(tail,head,u))*Mathf.Deg2Rad;
                var axis=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                // A narrow annular crescent leaves an open center; no filled white sector.
                float belly=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*u)),.62f);
                float thickness=width*belly*(.64f+.36f*u);
                float outer=radius+.025f*Mathf.Sin(Mathf.PI*u);
                _vertices.Add(transform.InverseTransformPoint(center+axis*(outer-thickness)));
                _vertices.Add(transform.InverseTransformPoint(center+axis*outer));
                _uv.Add(new Vector2(u,0));_uv.Add(new Vector2(u,1));
                var color=new Color(.965f,.980f,1f,alpha);_colors.Add(color);_colors.Add(color);
                if(i>0){int k=begin+i*2;_triangles.Add(k-2);_triangles.Add(k-1);_triangles.Add(k);_triangles.Add(k);_triangles.Add(k-1);_triangles.Add(k+1);}
            }
        }
        static Vector2 Rotate(Vector2 v,float angle){float a=angle*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);return new Vector2(v.x*c-v.y*s,v.x*s+v.y*c);}
        void OnDisable(){Clear();}
        void OnDestroy(){if(_mesh)Destroy(_mesh);if(_material)Destroy(_material);}
    }
}
