using System.Collections.Generic;
using UnityEngine;
namespace Demo6.Game
{
    // World-space ribbon fed only by rendered blade socket transforms.
    public sealed class BladeTrailV042:MonoBehaviour
    {
        struct Sample {public Vector3 root,tip;public float time;}
        readonly List<Sample> _samples=new List<Sample>(64);
        readonly List<Vector3> _vertices=new List<Vector3>(128);
        readonly List<Color> _colors=new List<Color>(128);
        readonly List<Vector2> _uv=new List<Vector2>(128);
        readonly List<int> _indices=new List<int>(384);
        Mesh _mesh;MeshRenderer _renderer;Material _material;
        Vector3 _previousRoot,_previousTip;float _previousTime,_nextSample;
        bool _ready;public int Count=>_samples.Count;public bool Emitting {get;private set;}
        public int TeleportResets {get;private set;}
        const float Interval=1f/120f;
        void Awake(){
            _mesh=new Mesh{name="BladeTrailV042"};_mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _renderer=gameObject.AddComponent<MeshRenderer>();
            _material=new Material(Shader.Find("Demo6/BladeTrailV042"));_renderer.sharedMaterial=_material;_renderer.enabled=false;
        }
        public void Clear(){_samples.Clear();_ready=false;Emitting=false;if(_mesh)_mesh.Clear();if(_renderer)_renderer.enabled=false;}
        public void Drive(Vector3 root,Vector3 tip,float now,bool emit,float life,int order,int layer)
        {
            Emitting=emit;
            if(_ready&&(now<_previousTime||(root-_previousRoot).sqrMagnitude>2.25f||(tip-_previousTip).sqrMagnitude>16f)){TeleportResets++;Clear();}
            Emitting=emit;
            if(!_ready){_previousTime=now;_nextSample=now;_previousRoot=root;_previousTip=tip;_ready=true;}
            if(emit){
                int guard=0;
                while(_nextSample<=now+.00001f&&guard++<64){
                    float u=Mathf.InverseLerp(_previousTime,now,_nextSample);
                    _samples.Add(new Sample{root=Vector3.Lerp(_previousRoot,root,u),tip=Vector3.Lerp(_previousTip,tip,u),time=_nextSample});
                    _nextSample+=Interval;
                }
            }else _nextSample=now+Interval;
            _previousRoot=root;_previousTip=tip;_previousTime=now;
            while(_samples.Count>0&&now-_samples[0].time>life)_samples.RemoveAt(0);
            if(_samples.Count>60)_samples.RemoveRange(0,_samples.Count-60);
            _renderer.sortingOrder=order;_renderer.sortingLayerID=layer;
            _vertices.Clear();_colors.Clear();_uv.Clear();_indices.Clear();
            int visibleCount=_samples.Count+(emit&&_samples.Count>0?1:0);
            for(int i=0;i<visibleCount;i++){
                var s=i<_samples.Count?_samples[i]:new Sample{root=root,tip=tip,time=now};float age=Mathf.Clamp01((now-s.time)/life);
                // Converting world points through the current parent avoids trails being dragged during movement/turns.
                // V043 broader translucent arc, behind the character's parts. Only the
                // effect extends 4% beyond the blade; no sprite or socket is stretched.
                _vertices.Add(transform.InverseTransformPoint(Vector3.Lerp(s.root,s.tip,.16f)));_vertices.Add(transform.InverseTransformPoint(Vector3.LerpUnclamped(s.root,s.tip,1.04f)));
                _uv.Add(new Vector2(age,0));_uv.Add(new Vector2(age,1));
                var c=new Color(.97f,.985f,1f,.80f);_colors.Add(c);_colors.Add(c);
                if(i>0){int k=i*2;_indices.Add(k-2);_indices.Add(k-1);_indices.Add(k);_indices.Add(k);_indices.Add(k-1);_indices.Add(k+1);}
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetColors(_colors);_mesh.SetUVs(0,_uv);_mesh.SetTriangles(_indices,0);_mesh.RecalculateBounds();
            _renderer.enabled=_samples.Count>1;
        }
        void OnDisable(){Clear();}
        void OnDestroy(){if(_mesh)Destroy(_mesh);if(_material)Destroy(_material);}
    }
}
