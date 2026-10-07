using System;
using System.Collections.Generic;
using UnityEngine;
namespace Demo6.Game
{
    // Extruded silhouette sides keep the original blade, guard and hilt readable edge-on.
    // Profiles are sampled from approved PNG alpha; this component has no combat state.
    public sealed class WeaponEdgeV052 : MonoBehaviour
    {
        [Serializable] public sealed class Sample { public float x,low,high,r,g,b; }
        [Serializable] public sealed class Profile { public string key; public Sample[] samples; }
        [Serializable] public sealed class Library { public Profile[] profiles; }
        static Library _library;
        Mesh _mesh; MeshRenderer _renderer;
        readonly List<Vector3> _vertices=new List<Vector3>();
        readonly List<Color> _colors=new List<Color>();
        readonly List<Vector2> _uv=new List<Vector2>();
        readonly List<int> _indices=new List<int>();
        MaterialPropertyBlock _block;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetProfiles(){_library=null;}
        void Awake(){_block=new MaterialPropertyBlock();_mesh=new Mesh{name="Approved weapon extruded sides V052"};_mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;_renderer=gameObject.AddComponent<MeshRenderer>();_renderer.enabled=false;_renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_renderer.receiveShadows=false;}
        public void Hide(){if(_renderer)_renderer.enabled=false;}
        public void Draw(SpriteRenderer face,string key,Vector2 grip,Quaternion rotation,float depth,bool left)
        {
            if(_library==null){var json=Resources.Load<TextAsset>("EquipmentV052/weapon-edge-profiles");if(json)_library=JsonUtility.FromJson<Library>(json.text);}
            Profile profile=null;if(_library!=null)foreach(var p in _library.profiles)if(p.key==key){profile=p;break;}
            if(profile?.samples==null){Hide();return;}
            _vertices.Clear();_colors.Clear();_uv.Clear();_indices.Clear();
            var samples=profile.samples;
            Vector3 Project(float x,float y,float z){var point=rotation*new Vector3(x,left?-y:y,z);return new Vector3(grip.x+point.x,grip.y+point.y,0);}
            void Quad(Sample a,Sample b,float ya,float yb){
                int n=_vertices.Count;
                _vertices.Add(Project(a.x,ya,-depth*.5f));_vertices.Add(Project(a.x,ya,depth*.5f));
                _vertices.Add(Project(b.x,yb,-depth*.5f));_vertices.Add(Project(b.x,yb,depth*.5f));
                for(int i=0;i<4;i++){var s=i<2?a:b;_colors.Add(new Color(s.r*.60f*face.color.r,s.g*.60f*face.color.g,s.b*.60f*face.color.b,face.color.a));_uv.Add(Vector2.one*.5f);}
                _indices.Add(n);_indices.Add(n+1);_indices.Add(n+2);_indices.Add(n+2);_indices.Add(n+1);_indices.Add(n+3);
            }
            for(int i=1;i<samples.Length;i++){Quad(samples[i-1],samples[i],samples[i-1].low,samples[i].low);Quad(samples[i-1],samples[i],samples[i-1].high,samples[i].high);}
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetColors(_colors);_mesh.SetUVs(0,_uv);_mesh.SetTriangles(_indices,0);_mesh.RecalculateBounds();
            _renderer.sharedMaterial=face.sharedMaterial;_renderer.sortingLayerID=face.sortingLayerID;_renderer.sortingOrder=face.sortingOrder-1;_renderer.forceRenderingOff=face.forceRenderingOff;
            face.GetPropertyBlock(_block);_block.SetFloat("_UseMeshRenderer",1);_block.SetFloat("_BladeRelief",0);_block.SetTexture("_MainTex",Texture2D.whiteTexture);_renderer.SetPropertyBlock(_block);_renderer.enabled=face.enabled;
        }
        void OnDestroy(){if(_mesh)Destroy(_mesh);}
    }
}
