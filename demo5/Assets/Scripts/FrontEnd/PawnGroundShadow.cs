using UnityEngine;
namespace Demo5.FrontEnd
{
    // Project the existing silhouette onto the floor. Does not alter the source artwork.
    public sealed class PawnGroundShadow:MonoBehaviour
    {
        public SpriteRenderer Body,Base;
        public Material ShadowMaterial;
        public Vector2 LightPosition=new Vector2(1.15f,3.1f);
        Mesh mesh;MeshRenderer shadow;MaterialPropertyBlock properties;
        readonly Vector3[] vertices=new Vector3[4];
        readonly Vector2[] uv=new Vector2[4];
        void Awake()
        {
            var child=new GameObject("CastShadow",typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(transform,false);
            mesh=new Mesh{name="Pawn floor projection"};child.GetComponent<MeshFilter>().sharedMesh=mesh;
            shadow=child.GetComponent<MeshRenderer>();shadow.sharedMaterial=ShadowMaterial;properties=new MaterialPropertyBlock();
        }
        void LateUpdate()
        {
            if(!Body||!Base||!Body.sprite||!ShadowMaterial)return;
            var feet=Base.bounds.center;float width=Body.bounds.size.x,height=Body.bounds.size.y;
            Vector2 away=((Vector2)feet-LightPosition).normalized;
            Vector2 cast=away*Mathf.Clamp(height*.55f,.5f,1.5f);
            Vector3 left=feet+new Vector3(-width*.5f,0,0),right=feet+new Vector3(width*.5f,0,0);
            vertices[0]=transform.InverseTransformPoint(left);vertices[1]=transform.InverseTransformPoint(right);
            vertices[2]=transform.InverseTransformPoint(right+(Vector3)cast);vertices[3]=transform.InverseTransformPoint(left+(Vector3)cast);
            var sprite=Body.sprite;var rect=sprite.textureRect;float x0=rect.xMin/sprite.texture.width,x1=rect.xMax/sprite.texture.width,y0=rect.yMin/sprite.texture.height,y1=rect.yMax/sprite.texture.height;
            if(Body.flipX){float swap=x0;x0=x1;x1=swap;}
            uv[0]=new Vector2(x0,y0);uv[1]=new Vector2(x1,y0);uv[2]=new Vector2(x1,y1);uv[3]=new Vector2(x0,y1);
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
            shadow.sortingLayerID=Base.sortingLayerID;shadow.sortingOrder=Base.sortingOrder-6;
            properties.SetTexture("_MainTex",sprite.texture);shadow.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
