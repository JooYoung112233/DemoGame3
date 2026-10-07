using UnityEngine;

namespace Demo6.Game
{
    /// <summary>고정된 어깨와 지연되는 끝단. 충돌·스탯이 없는 시각 파츠, 프레임 독립 감쇠.</summary>
    public sealed class TopDownCape : MonoBehaviour
    {
        const int Columns=17, Rows=5;
        Mesh _mesh;
        MeshRenderer _renderer;
        Sprite _source;
        Vector3[] _rest, _vertices;
        Color[] _colors;
        MaterialPropertyBlock _block;
        Vector2 _lastPosition, _drag, _dragVelocity;
        float _tailAngle;
        bool _initialized;
        bool _cute;
        float _flutter, _flutterVelocity, _flutterPhase, _lastAngle;
        float _attackLag, _attackLagVelocity;
        public float TailLag { get; private set; }
        public Vector2 TailDrag => _drag;
        public float VisibleHemDisplacement { get; private set; }
        public float FlutterStrength => _flutter;
        void OnEnable()=>_initialized=false;
        void OnDestroy()=>ReleaseMesh();
        void ReleaseMesh(){if(!_mesh)return;if(Application.isPlaying)Destroy(_mesh);else DestroyImmediate(_mesh);_mesh=null;}
        void SetSource(Sprite sprite, bool cute)
        {
            _cute=cute;_initialized=false;
            _source=sprite;
            if(!_renderer){_renderer=gameObject.AddComponent<MeshRenderer>();gameObject.AddComponent<MeshFilter>();_block=new MaterialPropertyBlock();}
            _renderer.enabled=sprite;
            if(!sprite)return;
            ReleaseMesh();
            _mesh=new Mesh{name="Short cape deformable cloth"};_mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=_mesh;
            _renderer.sharedMaterial=ArtRuntime.FlashMaterial ? ArtRuntime.FlashMaterial : RenderMaterials.Unlit;
            _block.SetTexture("_MainTex",sprite.texture);_renderer.SetPropertyBlock(_block);
            int columns=cute?33:Columns,rows=cute?13:Rows;
            _rest=new Vector3[columns*rows];_vertices=new Vector3[_rest.Length];_colors=new Color[_rest.Length];var uv=new Vector2[_rest.Length];var tri=new int[(columns-1)*(rows-1)*6];int t=0;
            var b=sprite.bounds;var tex=sprite.textureRect;
            for(int x=0;x<columns;x++)for(int y=0;y<rows;y++){
                int i=x*rows+y;float u=x/(float)(columns-1),v=y/(float)(rows-1);
                _rest[i]=new Vector3(Mathf.Lerp(b.min.x,b.max.x,u),Mathf.Lerp(b.min.y,b.max.y,v),0);
                uv[i]=new Vector2((tex.x+tex.width*u)/sprite.texture.width,(tex.y+tex.height*v)/sprite.texture.height);
                if(x==columns-1||y==rows-1)continue;
                tri[t++]=i;tri[t++]=i+1;tri[t++]=i+rows;tri[t++]=i+1;tri[t++]=i+rows+1;tri[t++]=i+rows;
            }
            _mesh.vertices=_rest;_mesh.uv=uv;_mesh.triangles=tri;
        }
        public void Place(Sprite source,SpriteRenderer body,Vector2 position,float angle,Vector2 offset,float rigAngle,float dt,bool cute=false,float attackImpulse=0f,bool walkingPose=false,Vector2? poseScale=null,float poseTuck=0f)
        {
            if(_source!=source || !_renderer || _cute!=cute)SetSource(source,cute);
            if(!source)return;
            bool jump=!_initialized || (position-_lastPosition).sqrMagnitude>4;
            if(jump){_tailAngle=angle;_lastAngle=angle;_drag=Vector2.zero;_dragVelocity=Vector2.zero;_flutter=0;_flutterVelocity=0;_flutterPhase=0;_attackLag=0;_attackLagVelocity=0;_initialized=true;}
            if(dt>0){
                dt=Mathf.Min(dt,.1f);
                var velocity=jump?Vector2.zero:(position-_lastPosition)/dt;
                Vector2 local=TopDownCanvas.Rotate(-velocity*(cute?(walkingPose?.032f:.048f):.023f),-angle);
                local=Vector2.ClampMagnitude(local,cute?(walkingPose?.16f:.24f):.14f);
                _drag=Vector2.SmoothDamp(_drag,local,ref _dragVelocity,cute?.12f:.16f,3f,dt);
                _tailAngle=Mathf.LerpAngle(_tailAngle,angle,1-Mathf.Exp(-(cute?(walkingPose?9f:6f):8f)*dt));
                if(cute){
                    float turnSpeed=Mathf.Abs(Mathf.DeltaAngle(_lastAngle,angle))/dt;
                    float energy=Mathf.Clamp01(velocity.magnitude/3.5f+turnSpeed/700f+Mathf.Abs(attackImpulse)*.7f);
                    _attackLag=Mathf.SmoothDamp(_attackLag,-attackImpulse*14f,ref _attackLagVelocity,.13f,220f,dt);
                    _flutter=Mathf.SmoothDamp(_flutter,energy,ref _flutterVelocity,energy>_flutter?.09f:.32f,6f,dt);
                    _flutterPhase+=dt*(8f+Mathf.Min(velocity.magnitude,5f)*1.8f);
                }
            }
            _lastPosition=position;_lastAngle=angle;
            TailLag=Mathf.Clamp(Mathf.DeltaAngle(angle,_tailAngle)+(cute?_attackLag:0f),cute?-42f:-32f,cute?42f:32f);
            transform.localPosition=TopDownCanvas.Rotate(offset,0);
            transform.localRotation=Quaternion.Euler(0,0,angle-rigAngle);
            transform.localScale=cute?new Vector3(body.transform.localScale.x/Mathf.Max(.001f,transform.parent.localScale.x),body.transform.localScale.y/Mathf.Max(.001f,transform.parent.localScale.y),1):Vector3.one;
            if(poseScale.HasValue)transform.localScale=Vector3.Scale(transform.localScale,new Vector3(poseScale.Value.x,poseScale.Value.y,1));
            float tuck=Mathf.Clamp01(poseTuck);
            VisibleHemDisplacement=0;
            for(int i=0;i<_rest.Length;i++){
                Vector2 v=_rest[i];float weight=Mathf.Clamp01((-v.x-(cute?.10f:.18f))/(cute?.54f:.75f));
                weight=cute?Mathf.SmoothStep(0f,1f,weight):weight*weight;
                var anchor=new Vector2(cute?-.10f:-.18f,0);v=anchor+TopDownCanvas.Rotate(v-anchor,TailLag*weight)+_drag*weight;
                if(cute){
                    // Bend the cloth centreline sideways while retaining its width
                    // direction. Rotating each row independently can fold opaque
                    // triangles when fast aiming and an attack impulse combine.
                    v=_rest[i];
                    v.x+=_drag.x*weight;
                    v.y+=(v.x-anchor.x)*Mathf.Sin(TailLag*Mathf.Deg2Rad)*weight+_drag.y*weight;
                    float wave=Mathf.Sin(_flutterPhase-weight*5f+_rest[i].y*2.8f);
                    v.y+=wave*(walkingPose?.045f:.09f)*_flutter*weight;
                    // With the existing 0.24 drag clamp, 0.02 keeps columns ordered.
                    v.x+=Mathf.Sin(_flutterPhase*.77f-weight*4f)*(walkingPose?.01f:.02f)*_flutter*weight;
                    // Measure only the visible cape region, not the transparent canvas.
                    if(_rest[i].x<-.35f&&_rest[i].x>-.69f&&Mathf.Abs(_rest[i].y)<.48f)
                        VisibleHemDisplacement=Mathf.Max(VisibleHemDisplacement,Vector2.Distance(v,_rest[i]));
                }
                if(tuck>0)v=Vector2.Lerp(v,_rest[i],tuck*.72f);
                // 끝단은 언제나 등 뒤. 어깨 부근은 원본에 고정하여 몸을 뚫고 나오지 않는다.
                if(weight>0)v.x=Mathf.Min(v.x,cute?-.10f:-.18f);
                _vertices[i]=v;_colors[i]=body.color;
            }
            _mesh.vertices=_vertices;_mesh.colors=_colors;_mesh.RecalculateBounds();
            // Mesh colours already carry body tint/alpha; bypass SpriteRenderer-only instance data.
            _renderer.sharedMaterial=body.sharedMaterial;
            body.GetPropertyBlock(_block);
            _block.SetTexture("_MainTex",source.texture);
            _block.SetFloat("_UseMeshRenderer",1f);
            _renderer.SetPropertyBlock(_block);
            _renderer.sortingLayerID=body.sortingLayerID;_renderer.sortingOrder=body.sortingOrder-2;_renderer.forceRenderingOff=body.forceRenderingOff;
        }
    }
}
