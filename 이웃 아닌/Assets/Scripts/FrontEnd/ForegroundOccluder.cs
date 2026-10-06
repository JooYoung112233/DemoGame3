using UnityEngine;
namespace Demo5.FrontEnd {
 // Uses a source-texture region; preserves original pixels and full-texture UVs for the room shader.
 [ExecuteAlways,RequireComponent(typeof(SpriteRenderer))]
 public sealed class ForegroundOccluder:MonoBehaviour {
  public Sprite Source; public Rect SourcePixels; public Vector3 Origin;
  Sprite generated; SpriteRenderer target;
  void OnEnable(){Rebuild();}
  public void Rebuild(){if(!Source)return;target=GetComponent<SpriteRenderer>();Release();
   var crop=new Rect(Source.rect.x+SourcePixels.x,Source.rect.y+Source.rect.height-SourcePixels.yMax,SourcePixels.width,SourcePixels.height);
   generated=Sprite.Create(Source.texture,crop,new Vector2(.5f,.5f),Source.pixelsPerUnit,0,SpriteMeshType.FullRect);generated.name=name+" source region";generated.hideFlags=HideFlags.DontSave;
   Vector3 offset=new Vector3((SourcePixels.center.x-Source.pivot.x)/Source.pixelsPerUnit,(Source.rect.height-SourcePixels.center.y-Source.pivot.y)/Source.pixelsPerUnit,0);
   transform.localPosition=Origin+Vector3.Scale(offset,transform.localScale);target.sprite=generated;
  }
  void OnDisable(){if(target){target.sprite=null;transform.localPosition=Origin;}Release();}
  void Release(){if(!generated)return;if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);generated=null;}
 }
}
